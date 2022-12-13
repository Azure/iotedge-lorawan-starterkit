// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

namespace LoRaWan.NetworkServer.BasicsStation.Processors
{
    using System;
    using System.Buffers;
    using System.Diagnostics.Metrics;
    using System.Linq;
    using System.Net.Http;
    using System.Text;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using Jacob;
    using LoRaTools.CommonAPI;
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
            HttpContent? firmware = null;

            try
            {
                string json;

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

                var firmwareLength = 0U;

                if (!string.Equals(updateRequest.Package, remoteCupsConfig.Package, StringComparison.OrdinalIgnoreCase))
                {
                    if (!updateRequest.KeyChecksums.Any(c => c == remoteCupsConfig.FwKeyChecksum))
                        throw new InvalidOperationException("Remote firmware signature generated with a key whose checksum is not available in CUPS request.");

                    (firmware, firmwareLength) =
                        await this.deviceAPIServiceBase.FetchStationFirmwareAsync(updateRequest.StationEui, token) switch
                        {
                            { Headers.ContentLength: null or <= 0 } => throw new InvalidOperationException("Firmware could not be properly downloaded from function. Check logs."),
                            { Headers.ContentLength: >= uint.MaxValue } => throw new InvalidOperationException($"Firmware size can't be greater than {int.MaxValue}"),
                            var someContent => (someContent, unchecked((uint)(someContent.Headers.ContentLength ?? 0)))
                        };
                }

                async Task<byte[]> FetchCredentialsAsync(ConcentratorCredentialType type) =>
                    Convert.FromBase64String(await this.deviceAPIServiceBase.FetchStationCredentialsAsync(updateRequest.StationEui, type, token));

                var updateResponseHeader = new CupsUpdateInfoResponseHeader
                {
                    CupsUrl = updateRequest.CupsUri != remoteCupsConfig.CupsUri ? remoteCupsConfig.CupsUri : null,
                    LnsUrl = updateRequest.TcUri != remoteCupsConfig.TcUri ? remoteCupsConfig.TcUri : null,
                    CupsCredential = updateRequest.CupsCredentialsChecksum != remoteCupsConfig.CupsCredCrc ? await FetchCredentialsAsync(ConcentratorCredentialType.Cups) : null,
                    LnsCredential = updateRequest.TcCredentialsChecksum != remoteCupsConfig.TcCredCrc ? await FetchCredentialsAsync(ConcentratorCredentialType.Lns) : null,
                    UpdateSignature = firmwareLength > 0 ? Convert.FromBase64String(remoteCupsConfig.FwSignatureInBase64) : null,
                    SignatureKeyCrc = firmwareLength > 0 ? remoteCupsConfig.FwKeyChecksum : 0,
                    UpdateDataLength = firmwareLength,
                };

                using var rentedUpdateResponseHeaderBytes = MemoryPool<byte>.Shared.Rent(2048);
                var updateResponseHeaderBytes = updateResponseHeader.Serialize(rentedUpdateResponseHeaderBytes.Memory);

                var response = httpContext.Response;
                response.StatusCode = (int)System.Net.HttpStatusCode.OK;
                response.ContentType = "application/octet-stream";
                response.ContentLength = updateResponseHeaderBytes.Length + firmwareLength;

                _ = await response.BodyWriter.WriteAsync(updateResponseHeaderBytes, token);

                if (firmware is { } someFirmware)
                {
                    await someFirmware.CopyToAsync(response.BodyWriter.AsStream(), token);
                }
            }
            catch (Exception ex) when (ExceptionFilterUtility.False(() => this.logger.LogError(ex, "An exception occurred while processing requests: {Exception}.", ex),
                                                                    () => this.unhandledExceptionCount?.Add(1)))
            {
                throw;
            }
            finally
            {
#pragma warning disable CA1508 // Avoid dead conditional code (false positive)
                firmware?.Dispose();
#pragma warning restore CA1508 // Avoid dead conditional code
            }

            void LogAndSetBadRequest(Exception? ex, string message, params object?[] args)
            {
                this.logger.LogError(ex, message, args);
                httpContext.Response.StatusCode = (int)System.Net.HttpStatusCode.BadRequest;
            }
        }
    }
}
