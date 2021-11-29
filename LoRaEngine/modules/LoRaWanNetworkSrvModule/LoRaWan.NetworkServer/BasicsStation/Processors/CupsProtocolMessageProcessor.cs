// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer.BasicsStation.Processors
{
    using System;
    using System.Buffers;
    using System.Buffers.Binary;
    using System.IO;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using LoRaWan.NetworkServer.BasicsStation.JsonHandlers;
    using Microsoft.AspNetCore.Http;
    using Microsoft.Extensions.Logging;

    internal class CupsProtocolMessageProcessor : ICupsProtocolMessageProcessor
    {
        private readonly IBasicsStationConfigurationService basicsStationConfigurationService;
        private readonly LoRaDeviceAPIServiceBase deviceAPIServiceBase;
        private readonly ILogger<CupsProtocolMessageProcessor> logger;

        public CupsProtocolMessageProcessor(IBasicsStationConfigurationService basicsStationConfigurationService,
                                            LoRaDeviceAPIServiceBase deviceAPIServiceBase,
                                            ILogger<CupsProtocolMessageProcessor> logger)
        {
            this.basicsStationConfigurationService = basicsStationConfigurationService;
            this.deviceAPIServiceBase = deviceAPIServiceBase;
            this.logger = logger;
        }

        public async Task HandleUpdateInfoAsync(HttpContext httpContext, CancellationToken token)
        {
            using var reader = new StreamReader(httpContext.Request.Body);
            var input = await reader.ReadToEndAsync();

            var updateRequest = CupsEndpoint.UpdateRequestReader.Read(input);
            if (updateRequest.StationEui is null)
                throw new InvalidOperationException(nameof(updateRequest.StationEui));

            var remoteCupsConfig = await this.basicsStationConfigurationService.GetCupsConfigAsync(updateRequest.StationEui.Value, token);

            // TODO: Rent
            using var response = MemoryPool<byte>.Shared.Rent(8192);
            var writtenBytes = 0;
            if (updateRequest.CupsUri != remoteCupsConfig.CupsUri)
            {
                var escapedUri = remoteCupsConfig.CupsUri.GetComponents(UriComponents.SchemeAndServer, UriFormat.Unescaped);

                GetProgressingSpan()[0] = (byte)escapedUri.Length;
                writtenBytes += 1;
                Encoding.UTF8.GetBytes(escapedUri).CopyTo(GetProgressingSpan());
                writtenBytes += escapedUri.Length;
            }
            else
            {
                GetProgressingSpan()[0] = 0;
                writtenBytes += 1;
            }

            if (updateRequest.TcUri != remoteCupsConfig.TcUri)
            {
                var escapedUri = remoteCupsConfig.TcUri.GetComponents(UriComponents.SchemeAndServer, UriFormat.Unescaped);

                GetProgressingSpan()[0] = (byte)escapedUri.Length;
                writtenBytes += 1;
                Encoding.UTF8.GetBytes(escapedUri).CopyTo(GetProgressingSpan());
                writtenBytes += escapedUri.Length;
            }
            else
            {
                GetProgressingSpan()[0] = 0;
                writtenBytes += 1;
            }

            if (updateRequest.CupsCredentialsChecksum != remoteCupsConfig.CupsCredentialsChecksum)
            {
                // TODO: Replace with enum
                var cupsCredentials = await this.deviceAPIServiceBase.FetchStationCredentialsAsync(updateRequest.StationEui.Value, "Cups");

                var cupsCredentialsBytes = Convert.FromBase64String(cupsCredentials);
                BinaryPrimitives.WriteInt16LittleEndian(GetProgressingSpan(), (short)cupsCredentialsBytes.Length);
                writtenBytes += 2;
                cupsCredentialsBytes.CopyTo(GetProgressingSpan());
                writtenBytes += cupsCredentials.Length;
            }
            else
            {
                BinaryPrimitives.WriteInt16LittleEndian(GetProgressingSpan(), 0);
                writtenBytes += 2;
            }

            if (updateRequest.TcCredentialsChecksum != remoteCupsConfig.TcCredentialsChecksum)
            {
                // TODO: Replace with enum
                var lnsCredentials = await this.deviceAPIServiceBase.FetchStationCredentialsAsync(updateRequest.StationEui.Value, "Lns");

                var lnsCredentialsBytes = Convert.FromBase64String(lnsCredentials);
                BinaryPrimitives.WriteInt16LittleEndian(GetProgressingSpan(), (short)lnsCredentialsBytes.Length);
                writtenBytes += 2;
                lnsCredentialsBytes.CopyTo(GetProgressingSpan());
                writtenBytes += lnsCredentials.Length;
            }
            else
            {
                BinaryPrimitives.WriteInt16LittleEndian(GetProgressingSpan(), 0);
                writtenBytes += 2;
            }

            /*
             * 4 bytes sigLen (sig)
             * 4 bytes keyCrc
             * sig sig
             * 4 bytes updlen (udn)
             * udn
             */

            //sig
            BinaryPrimitives.WriteUInt32LittleEndian(GetProgressingSpan(), 0);
            writtenBytes += 4;
            //keycrc
            BinaryPrimitives.WriteUInt32LittleEndian(GetProgressingSpan(), 0);
            writtenBytes += 4;
            //updlen
            BinaryPrimitives.WriteUInt32LittleEndian(GetProgressingSpan(), 0);
            writtenBytes += 4;

            var toWrite = response.Memory.Span[..writtenBytes].ToArray();
            httpContext.Response.Clear();
            httpContext.Response.ContentType = "application/octet-stream";
            httpContext.Response.ContentLength = writtenBytes;
            _ = await httpContext.Response.BodyWriter.WriteAsync(toWrite, token);

            Span<byte> GetProgressingSpan()
            {
                return response.Memory.Span[writtenBytes..];
            }
        }
    }
}
