// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

namespace LoRaWan.NetworkServer.BasicsStation
{
    using System;
    using System.Buffers.Binary;
    using System.IO;
    using System.Linq;
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
        private readonly Func<StationEui, CancellationToken, Task<(long?, Stream)>> fwUpgradeFetcher;

        public CupsResponse(CupsUpdateInfoRequest cupsUpdateInfoRequest,
                            CupsTwinInfo cupsTwinInfo,
                            Func<StationEui, ConcentratorCredentialType, CancellationToken, Task<string>> credentialFetcher,
                            Func<StationEui, CancellationToken, Task<(long?, Stream)>> fwUpgradeFetcher)
        {
            this.cupsUpdateInfoRequest = cupsUpdateInfoRequest;
            this.cupsTwinInfo = cupsTwinInfo;
            this.credentialFetcher = credentialFetcher;
            this.fwUpgradeFetcher = fwUpgradeFetcher;
        }


        internal async Task<(Memory<byte> ResponseBytes, int FwLength, Stream? FwStream)> SerializeAsync(Memory<byte> response, CancellationToken token)
        {
            // checking for disequalities in desired and reported configuration
            var currentPosition = 0;

            currentPosition = WriteUriConditionally(ConcentratorCredentialType.Cups, response.Span, currentPosition);
            currentPosition = WriteUriConditionally(ConcentratorCredentialType.Lns, response.Span, currentPosition);
            currentPosition = await WriteCredentialsConditionallyAsync(ConcentratorCredentialType.Cups, response, currentPosition, token);
            currentPosition = await WriteCredentialsConditionallyAsync(ConcentratorCredentialType.Lns, response, currentPosition, token);

            (currentPosition, var fwLength, var fwStream) = await WriteFirmwareUpgradeConditionallyAsync(response, currentPosition, token);

            return (response[..currentPosition], fwLength, fwStream);
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
                var uri = endpointType switch
                {
                    ConcentratorCredentialType.Cups => this.cupsTwinInfo.CupsUri,
                    ConcentratorCredentialType.Lns => this.cupsTwinInfo.TcUri,
                    _ => throw new SwitchExpressionException(nameof(endpointType))
                };
                var uriWithoutTrailingSlash = uri.GetComponents(UriComponents.Scheme | UriComponents.HostAndPort, UriFormat.Unescaped);
                currentPosition += WriteToSpan((byte)uriWithoutTrailingSlash.Length, response[currentPosition..]);
                currentPosition += WriteToSpan(Encoding.UTF8.GetBytes(uriWithoutTrailingSlash), response[currentPosition..]);
            }
            else
            {
                currentPosition += WriteToSpan((byte)0, response[currentPosition..]);
            }

            return currentPosition;
        }

        private async Task<(int, int, Stream?)> WriteFirmwareUpgradeConditionallyAsync(Memory<byte> response, int currentPosition, CancellationToken token)
        {
            var firmwareMismatch = !string.Equals(this.cupsUpdateInfoRequest.Package, this.cupsTwinInfo.Package, StringComparison.OrdinalIgnoreCase);

            if (firmwareMismatch)
            {
                if (!this.cupsUpdateInfoRequest.KeyChecksums.Any(c => c == this.cupsTwinInfo.FwKeyChecksum))
                {
                    throw new InvalidOperationException("Remote firmware signature generated with a key whose checksum is not available in CUPS request.");
                }

                var signature = Convert.FromBase64String(this.cupsTwinInfo.FwSignature);
                currentPosition += WriteToSpan(signature.Length + 4, response.Span[currentPosition..]);
                currentPosition += WriteToSpan(this.cupsTwinInfo.FwKeyChecksum, response.Span[currentPosition..]);
                currentPosition += WriteToSpan(signature, response.Span[currentPosition..]);
                var (contentLength, fwStream) = await this.fwUpgradeFetcher(this.cupsUpdateInfoRequest.StationEui, token);
                if (contentLength is null or <= 0 || fwStream is null)
                    throw new InvalidOperationException("Firmware could not be properly downloaded from function. Check logs.");
                if (contentLength is >= int.MaxValue)
                    throw new InvalidOperationException($"Firmware size can't be greater than {int.MaxValue}");
                currentPosition += WriteToSpan(unchecked((int)contentLength), response.Span[currentPosition..]);
                return (currentPosition, unchecked((int)contentLength), fwStream);
            }
            else
            {
                // No Firmware update
                // Signature length (4bytes)
                currentPosition += WriteToSpan(0U, response.Span[currentPosition..]);
                // CRC of the Key used for the signature (4bytes)
                currentPosition += WriteToSpan(0U, response.Span[currentPosition..]);
                // Length of the update data (4bytes)
                currentPosition += WriteToSpan(0U, response.Span[currentPosition..]);
                return (currentPosition, 0, null);
            }
        }

        private async Task<int> WriteCredentialsConditionallyAsync(ConcentratorCredentialType endpointType,
                                                                   Memory<byte> response,
                                                                   int currentPosition,
                                                                   CancellationToken token)
        {
            var checksumMismatch = endpointType switch
            {
                ConcentratorCredentialType.Cups => this.cupsUpdateInfoRequest.CupsCredentialsChecksum != this.cupsTwinInfo.CupsCredCrc,
                ConcentratorCredentialType.Lns => this.cupsUpdateInfoRequest.TcCredentialsChecksum != this.cupsTwinInfo.TcCredCrc,
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

        private static int WriteToSpan(int value, Span<byte> span)
        {
            var length = sizeof(uint);
            BinaryPrimitives.WriteInt32LittleEndian(span[..length], value);
            return length;
        }
    }
}
