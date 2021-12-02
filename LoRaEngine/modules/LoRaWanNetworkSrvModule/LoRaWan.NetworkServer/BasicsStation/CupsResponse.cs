// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

namespace LoRaWan.NetworkServer.BasicsStation
{
    using System;
    using System.Buffers;
    using System.Buffers.Binary;
    using System.Runtime.CompilerServices;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using LoRaTools.CommonAPI;

    internal sealed class CupsResponse
    {
        private readonly CupsUpdateInfoRequest cupsUpdateInfoRequest;
        private readonly CupsTwinInfo cupsTwinInfo;
        private readonly Func<StationEui, ConcentratorCredentialType, CancellationToken, Task<string>> credentialFetcher;

        public CupsResponse(CupsUpdateInfoRequest cupsUpdateInfoRequest,
                            CupsTwinInfo cupsTwinInfo,
                            Func<StationEui, ConcentratorCredentialType, CancellationToken, Task<string>> credentialFetcher)
        {
            this.cupsUpdateInfoRequest = cupsUpdateInfoRequest;
            this.cupsTwinInfo = cupsTwinInfo;
            this.credentialFetcher = credentialFetcher;
        }

        internal async Task<byte[]> SerializeAsync(CancellationToken token)
        {
            // checking for disequalities in desired and reported configuration
            using var response = MemoryPool<byte>.Shared.Rent(2048);
            var currentPosition = 0;

            currentPosition = WriteUriConditionally(ConcentratorCredentialType.Cups, response.Memory.Span, currentPosition);
            currentPosition = WriteUriConditionally(ConcentratorCredentialType.Lns, response.Memory.Span, currentPosition);
            currentPosition = await WriteCredentialsConditionallyAsync(ConcentratorCredentialType.Cups, response.Memory, currentPosition, token);
            currentPosition = await WriteCredentialsConditionallyAsync(ConcentratorCredentialType.Lns, response.Memory, currentPosition, token);

            /*
             * Following fields are left empty as no firmware update feature is implemented yet
             */

            // Signature length (4bytes)
            currentPosition += WriteToSpan(0U, response.Memory.Span[currentPosition..]);
            // CRC of the Key used for the signature (4bytes)
            currentPosition += WriteToSpan(0U, response.Memory.Span[currentPosition..]);
            // Length of the update data (4bytes)
            currentPosition += WriteToSpan(0U, response.Memory.Span[currentPosition..]);

            return response.Memory.Span[..currentPosition].ToArray();
        }

        private int WriteUriConditionally(ConcentratorCredentialType endpointType, Span<byte> response, int currentPosition)
        {
            var uriMismatch = endpointType switch
            {
                ConcentratorCredentialType.Cups => this.cupsUpdateInfoRequest.CupsUri != this.cupsTwinInfo.CupsUri,
                ConcentratorCredentialType.Lns => this.cupsUpdateInfoRequest.TcUri != this.cupsTwinInfo.TcUri,
                _ => throw new SwitchExpressionException(nameof(endpointType))
            };

            if (uriMismatch)
            {
                var uriWithoutTrailingSlash = endpointType switch
                {
                    ConcentratorCredentialType.Cups => GetCupsNormalizedUri(this.cupsTwinInfo.CupsUri),
                    ConcentratorCredentialType.Lns => GetCupsNormalizedUri(this.cupsTwinInfo.TcUri),
                    _ => throw new SwitchExpressionException(nameof(endpointType))
                };

                currentPosition += WriteToSpan((byte)uriWithoutTrailingSlash.Length, response[currentPosition..]);
                currentPosition += WriteToSpan(Encoding.UTF8.GetBytes(uriWithoutTrailingSlash), response[currentPosition..]);
            }
            else
            {
                currentPosition += WriteToSpan(0, response[currentPosition..]);
            }

            return currentPosition;

            static string GetCupsNormalizedUri(Uri uri) =>
                uri.GetComponents(UriComponents.Scheme | UriComponents.HostAndPort, UriFormat.Unescaped);
        }

        private async Task<int> WriteCredentialsConditionallyAsync(ConcentratorCredentialType endpointType,
                                                                   Memory<byte> response,
                                                                   int currentPosition,
                                                                   CancellationToken token)
        {
            var checksumMismatch = endpointType switch
            {
                ConcentratorCredentialType.Cups => this.cupsUpdateInfoRequest.CupsCredentialsChecksum != this.cupsTwinInfo.CupsCredentialsChecksum,
                ConcentratorCredentialType.Lns => this.cupsUpdateInfoRequest.TcCredentialsChecksum != this.cupsTwinInfo.TcCredentialsChecksum,
                _ => throw new SwitchExpressionException(nameof(endpointType))
            };

            if (checksumMismatch)
            {
                var credentialBase64String = await this.credentialFetcher(this.cupsUpdateInfoRequest.StationEui, endpointType, token);

                var credentialBytes = Convert.FromBase64String(credentialBase64String);
                currentPosition += WriteToSpan((ushort)credentialBytes.Length, response.Span[currentPosition..]);
                currentPosition += WriteToSpan(credentialBytes, response.Span[currentPosition..]);
            }
            else
            {
                currentPosition += WriteToSpan((ushort)0, response.Span[currentPosition..]);
            }

            return currentPosition;
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

        private static int WriteToSpan(ushort value, Span<byte> span)
        {
            var length = sizeof(ushort);
            BinaryPrimitives.WriteUInt16LittleEndian(span[..length], value);
            return length;
        }

        private static int WriteToSpan(uint value, Span<byte> span)
        {
            var length = sizeof(uint);
            BinaryPrimitives.WriteUInt32LittleEndian(span[..length], value);
            return length;
        }
    }
}
