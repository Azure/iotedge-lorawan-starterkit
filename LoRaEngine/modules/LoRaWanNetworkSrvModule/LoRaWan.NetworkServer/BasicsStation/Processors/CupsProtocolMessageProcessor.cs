// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

namespace LoRaWan.NetworkServer.BasicsStation.Processors
{
    using System;
    using System.Diagnostics.Metrics;
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
        private readonly Counter<int>? unhandledExceptionCount;
        internal const int MaximumAllowedContentLength = 2048;

        public CupsProtocolMessageProcessor(IBasicsStationConfigurationService basicsStationConfigurationService,
                                            LoRaDeviceAPIServiceBase deviceAPIServiceBase,
                                            ILogger<CupsProtocolMessageProcessor> logger,
                                            Meter? meter)
        {
            this.basicsStationConfigurationService = basicsStationConfigurationService;
            this.deviceAPIServiceBase = deviceAPIServiceBase;
            this.logger = logger;
            this.unhandledExceptionCount = meter?.CreateCounter<int>(MetricRegistry.UnhandledExceptions);
        }

        public async Task HandleUpdateInfoAsync(HttpContext httpContext, CancellationToken token)
        {
            string json;

            try
            {
                // checking content length
#pragma warning disable IDE0010 // Add missing cases (false positive)
                switch (httpContext.Request.ContentLength)
#pragma warning restore IDE0010 // Add missing cases
                {
                    case null:
                        LogAndSetBadRequest(null, "Request is not specifying a Content-Length.");
                        return;
                    case > MaximumAllowedContentLength:
                        LogAndSetBadRequest(null, "Request body is exceeding the maximum content-length limit of {MaximumAllowedContentLength}.", MaximumAllowedContentLength);
                        return;
                    case var contentLength:
                    {
                        var reader = httpContext.Request.BodyReader;
                        var result = await reader.ReadAtLeastAsync(checked((int)contentLength), token);

                        if (result.Buffer.Length != contentLength)
                        {
                            LogAndSetBadRequest(null, "Actual content length does not match the expected length of {ContentLength} bytes.", contentLength);
                            return;
                        }

                        json = Encoding.UTF8.GetString(result.Buffer);
                        reader.AdvanceTo(result.Buffer.End);
                        break;
                    }
                }

                // reading the request from Basic Station
                CupsUpdateInfoRequest updateRequest;
                try
                {
                    // We are assuming that input is a UTF8 Json
                    updateRequest = CupsEndpoint.UpdateRequestReader.Read(json);
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
            }
            catch (Exception ex) when (ExceptionFilterUtility.False(() => this.logger.LogError(ex, "An exception occurred while processing requests: {Exception}.", ex),
                                                                    () => this.unhandledExceptionCount?.Add(1)))
            {
                throw;
            }

            void LogAndSetBadRequest(Exception? ex, string message, params object?[] args)
            {
                this.logger.LogError(ex, message, args);
                httpContext.Response.StatusCode = (int)System.Net.HttpStatusCode.BadRequest;
            }
        }
    }
}
