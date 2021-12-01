// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

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
                LogAndSetBadRequest(httpContext, null, "Request is not specifying a Content-Length.");
                return;
            }
            if (contentLength > MaximumAllowedContentLength)
            {
                LogAndSetBadRequest(httpContext, null, "Request body is exceeding the maximum content-length limit of {MaximumAllowedContentLength}.", MaximumAllowedContentLength);
                return;
            }

            // reading the input stream
            using var reader = new StreamReader(httpContext.Request.Body);
            var inputChars = new char[(int)contentLength];
            var totalReadBytes = 0;
            var iterationReadBytes = 0;
            do
            {
                iterationReadBytes = await reader.ReadAsync(inputChars, 0, (int)contentLength);
                totalReadBytes += iterationReadBytes;
            } while (totalReadBytes < contentLength && iterationReadBytes != 0);

            if (totalReadBytes > contentLength)
            {
                LogAndSetBadRequest(httpContext, null, "Stream includes more bytes than what expected.");
                return;
            }

            // reading the request from Basic Station
            CupsUpdateInfoRequest updateRequest;
            try
            {
                updateRequest = CupsEndpoint.UpdateRequestReader.Read(string.Concat(inputChars));
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

            currentPosition = WriteUriConditionally(updateRequest.CupsUri, remoteCupsConfig.CupsUri, response, currentPosition);
            currentPosition = WriteUriConditionally(updateRequest.TcUri, remoteCupsConfig.TcUri, response, currentPosition);
            currentPosition = await WriteCredentialsConditionallyAsync(updateRequest.CupsCredentialsChecksum, remoteCupsConfig.CupsCredentialsChecksum, ConcentratorCredentialType.Cups, response, currentPosition, token);
            currentPosition = await WriteCredentialsConditionallyAsync(updateRequest.TcCredentialsChecksum, remoteCupsConfig.TcCredentialsChecksum, ConcentratorCredentialType.Lns, response, currentPosition, token);

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
            httpContext.Response.StatusCode = (int)System.Net.HttpStatusCode.OK;
            httpContext.Response.ContentType = "application/octet-stream";
            httpContext.Response.ContentLength = currentPosition;
            _ = await httpContext.Response.BodyWriter.WriteAsync(toWrite, token);

            void LogAndSetBadRequest(HttpContext httpContext, Exception? ex, string message, params object?[] args)
            {
                this.logger.LogError(ex, message, args);
                httpContext.Response.StatusCode = (int)System.Net.HttpStatusCode.BadRequest;
            }

            static int WriteUriConditionally(Uri? requestUri, Uri configUri, IMemoryOwner<byte> response, int currentPosition)
            {
                if (requestUri != configUri)
                {
                    var uriWithoutTrailingSlash = configUri.GetComponents(UriComponents.Scheme | UriComponents.HostAndPort, UriFormat.Unescaped);

                    currentPosition += WriteToSpan((byte)uriWithoutTrailingSlash.Length, response.Memory.Span[currentPosition..]);
                    currentPosition += WriteToSpan(Encoding.UTF8.GetBytes(uriWithoutTrailingSlash), response.Memory.Span[currentPosition..]);
                }
                else
                {
                    currentPosition += WriteToSpan(0, response.Memory.Span[currentPosition..]);
                }

                return currentPosition;
            }

            async Task<int> WriteCredentialsConditionallyAsync(uint requestChecksum, uint configChecksum, ConcentratorCredentialType credentialType, IMemoryOwner<byte> response, int currentPosition, CancellationToken token)
            {
                if (requestChecksum != configChecksum)
                {
                    var credentialBase64String = await this.deviceAPIServiceBase.FetchStationCredentialsAsync(updateRequest.StationEui, credentialType, token);

                    var credentialBytes = Convert.FromBase64String(credentialBase64String);
                    currentPosition += WriteToSpan((ushort)credentialBytes.Length, response.Memory.Span[currentPosition..]);
                    currentPosition += WriteToSpan(credentialBytes, response.Memory.Span[currentPosition..]);
                }
                else
                {
                    currentPosition += WriteToSpan((ushort)0, response.Memory.Span[currentPosition..]);
                }

                return currentPosition;
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
