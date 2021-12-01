// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

namespace LoRaWan.NetworkServer.BasicsStation.Processors
{
    using System;
    using System.Buffers;
    using System.Text;
    using System.Text.Json;
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
        internal const int MaximumAllowedContentLength = 2048;

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
            // checking content length
            var contentLength = httpContext.Request.ContentLength;
            if (contentLength is null)
            {
                LogAndSetBadRequest(null, "Request is not specifying a Content-Length.");
                return;
            }
            if (contentLength > MaximumAllowedContentLength)
            {
                LogAndSetBadRequest(null, "Request body is exceeding the maximum content-length limit of {MaximumAllowedContentLength}.", MaximumAllowedContentLength);
                return;
            }

            // reading the input stream
            using var inputBytes = MemoryPool<byte>.Shared.Rent();
            var totalReadBytes = 0;
            var iterationReadBytes = 0;
            do
            {
                iterationReadBytes = await httpContext.Request.Body.ReadAsync(inputBytes.Memory[totalReadBytes..], token);
                totalReadBytes += iterationReadBytes;
            } while (totalReadBytes < contentLength && iterationReadBytes != 0);

            if (totalReadBytes > contentLength)
            {
                LogAndSetBadRequest(null, "Stream includes more bytes than what expected.");
                return;
            }

            // reading the request from Basic Station
            CupsUpdateInfoRequest updateRequest;
            try
            {
                // We are assuming that input is a UTF8 Json
                updateRequest = CupsEndpoint.UpdateRequestReader.Read(Encoding.UTF8.GetString(inputBytes.Memory[..totalReadBytes].ToArray()));
            }
            catch (UriFormatException uriException)
            {
                LogAndSetBadRequest(uriException, "Current CUPS/TC uri was not properly parsed. Please double check the input.");
                return;
            }
            catch (FormatException formatException)
            {
                LogAndSetBadRequest(formatException, "Station EUI was not properly parsed. Please double check the input.");
                return;
            }
            catch (JsonException jsonException)
            {
                LogAndSetBadRequest(jsonException, "One of the fields was not properly handled. Please double check the input.");
                return;
            }

            // reading the configuration stored in twin
            var remoteCupsConfig = await this.basicsStationConfigurationService.GetCupsConfigAsync(updateRequest.StationEui, token);

            var responseBytes = await new CupsResponse(updateRequest, remoteCupsConfig, this.deviceAPIServiceBase.FetchStationCredentialsAsync).SerializeAsync(token);

            httpContext.Response.StatusCode = (int)System.Net.HttpStatusCode.OK;
            httpContext.Response.ContentType = "application/octet-stream";
            httpContext.Response.ContentLength = responseBytes.Length;
            _ = await httpContext.Response.BodyWriter.WriteAsync(responseBytes, token);

            void LogAndSetBadRequest(Exception? ex, string message, params object?[] args)
            {
                this.logger.LogError(ex, message, args);
                httpContext.Response.StatusCode = (int)System.Net.HttpStatusCode.BadRequest;
            }
        }
    }
}
