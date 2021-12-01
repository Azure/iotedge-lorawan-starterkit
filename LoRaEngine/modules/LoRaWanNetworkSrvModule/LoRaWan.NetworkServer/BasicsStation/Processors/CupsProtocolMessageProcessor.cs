// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer.BasicsStation.Processors
{
    using System;
    using System.Buffers;
    using System.Buffers.Binary;
    using System.IO;
    using System.Text;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using LoRaTools.CommonAPI;
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

            // reading the request from Basic Station
            CupsUpdateInfoRequest updateRequest;
            try
            {
                updateRequest = CupsEndpoint.UpdateRequestReader.Read(input);
            }
            catch (UriFormatException uriException)
            {
                LogAndSetBadRequest(httpContext, uriException, "Current CUPS/TC uri was not properly parsed. Please double check the input.");
                return;
            }
            catch (FormatException formatException)
            {
                LogAndSetBadRequest(httpContext, formatException, "Station EUI was not properly parsed. Please double check the input.");
                return;
            }
            catch (JsonException jsonException)
            {
                LogAndSetBadRequest(httpContext, jsonException, "One of the fields was not properly handled. Please double check the input.");
                return;
            }

            // reading the configuration stored in twin
            var remoteCupsConfig = await this.basicsStationConfigurationService.GetCupsConfigAsync(updateRequest.StationEui, token);

            // checking for disequalities in desired and reported configuration
            using var response = MemoryPool<byte>.Shared.Rent(2048);
            var currentPosition = 0;
            if (updateRequest.CupsUri != remoteCupsConfig.CupsUri)
            {
                var escapedUri = remoteCupsConfig.CupsUri.GetComponents(UriComponents.Scheme | UriComponents.HostAndPort, UriFormat.Unescaped);

                currentPosition += WriteToSpan((byte)escapedUri.Length, response.Memory.Span[currentPosition..]);
                currentPosition += WriteToSpan(Encoding.UTF8.GetBytes(escapedUri), response.Memory.Span[currentPosition..]);
            }
            else
            {
                currentPosition += WriteToSpan(0, response.Memory.Span[currentPosition..]);
            }

            if (updateRequest.TcUri != remoteCupsConfig.TcUri)
            {
                var escapedUri = remoteCupsConfig.TcUri.GetComponents(UriComponents.Scheme | UriComponents.HostAndPort, UriFormat.Unescaped);

                currentPosition += WriteToSpan((byte)escapedUri.Length, response.Memory.Span[currentPosition..]);
                currentPosition += WriteToSpan(Encoding.UTF8.GetBytes(escapedUri), response.Memory.Span[currentPosition..]);
            }
            else
            {
                currentPosition += WriteToSpan(0, response.Memory.Span[currentPosition..]);
            }

            if (updateRequest.CupsCredentialsChecksum != remoteCupsConfig.CupsCredentialsChecksum)
            {
                var cupsCredentials = await this.deviceAPIServiceBase.FetchStationCredentialsAsync(updateRequest.StationEui, ConcentratorCredentialType.Cups, token);

                var cupsCredentialsBytes = Convert.FromBase64String(cupsCredentials);
                currentPosition += WriteToSpan((ushort)cupsCredentialsBytes.Length, response.Memory.Span[currentPosition..]);
                currentPosition += WriteToSpan(cupsCredentialsBytes, response.Memory.Span[currentPosition..]);
            }
            else
            {
                currentPosition += WriteToSpan((ushort)0, response.Memory.Span[currentPosition..]);
            }

            if (updateRequest.TcCredentialsChecksum != remoteCupsConfig.TcCredentialsChecksum)
            {
                var lnsCredentials = await this.deviceAPIServiceBase.FetchStationCredentialsAsync(updateRequest.StationEui, ConcentratorCredentialType.Lns, token);

                var lnsCredentialsBytes = Convert.FromBase64String(lnsCredentials);
                currentPosition += WriteToSpan((ushort)lnsCredentialsBytes.Length, response.Memory.Span[currentPosition..]);
                currentPosition += WriteToSpan(lnsCredentialsBytes, response.Memory.Span[currentPosition..]);
            }
            else
            {
                currentPosition += WriteToSpan((ushort)0, response.Memory.Span[currentPosition..]);
            }

            /*
             * Following fields are left empty as no firmware update feature is implemented yet
             */

            // Signature length (4bytes)
            currentPosition += WriteToSpan((uint)0, response.Memory.Span[currentPosition..]);
            // CRC of the Key used for the signature (4bytes)
            currentPosition += WriteToSpan((uint)0, response.Memory.Span[currentPosition..]);
            // Length of the update data (4bytes)
            currentPosition += WriteToSpan((uint)0, response.Memory.Span[currentPosition..]);

            var toWrite = response.Memory.Span[..currentPosition].ToArray();
            httpContext.Response.ContentType = "application/octet-stream";
            httpContext.Response.ContentLength = currentPosition;
            _ = await httpContext.Response.BodyWriter.WriteAsync(toWrite, token);

            void LogAndSetBadRequest(HttpContext httpContext, Exception ex, string message)
            {
                this.logger.LogError(ex, message);
                httpContext.Response.StatusCode = (int)System.Net.HttpStatusCode.BadRequest;
            }
        }

        private static int WriteToSpan(Span<byte> value, Span<byte> span)
        {
            var length = value.Length;
            value.CopyTo(span[..length]);
            return length;
        }

        private static int WriteToSpan(byte value, Span<byte> span)
        {
            span[0] = value;
            return sizeof(byte);
        }

        private static int WriteToSpan(ushort value, Span<byte> span, bool littleEndian = true)
        {
            var length = sizeof(ushort);
            if (littleEndian)
            {
                BinaryPrimitives.WriteUInt16LittleEndian(span[..length], value);
            }
            else
            {
                BinaryPrimitives.WriteUInt16BigEndian(span[..length], value);
            }
            return length;
        }

        private static int WriteToSpan(uint value, Span<byte> span, bool littleEndian = true)
        {
            var length = sizeof(uint);
            if (littleEndian)
            {
                BinaryPrimitives.WriteUInt32LittleEndian(span[..length], value);
            }
            else
            {
                BinaryPrimitives.WriteUInt32BigEndian(span[..length], value);
            }
            return length;
        }
    }
}
