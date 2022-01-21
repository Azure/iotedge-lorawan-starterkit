// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

namespace LoRaWan.NetworkServer.BasicsStation
{
    using System;
    using System.Runtime.Serialization;

    /// <summary>
    /// Represents the HTTP POST response (binary) entity of the CUPS Protocol.
    /// </summary>

    internal sealed class CupsUpdateInfoResponseHeader
    {
        #pragma warning disable format

        public Uri? CupsUrl                            { get; init; } // cupsUri (cupsUriLen inferred)
        public Uri? LnsUrl                             { get; init; } // tcUri (tcUriLen inferred)
        public ReadOnlyMemory<byte> CupsCredential     { get; init; } // cupsCred (cupsCredLen inferred)
        public ReadOnlyMemory<byte> LnsCredential      { get; init; } // tcCred (tcCred inferred)
        public uint SignatureKeyCrc                    { get; init; } // keyCRC
        public ReadOnlyMemory<byte> UpdateSignature    { get; init; } // sig (sigLen inferred)
        public uint UpdateDataLength                   { get; init; } // updLen

        #pragma warning restore format

        public Memory<byte> Serialize(Memory<byte> buffer) =>
            buffer[..Serialize(buffer.Span).Length];

        public Span<byte> Serialize(Span<byte> buffer)
        {
            // Source of the following table:
            // https://lora-developers.semtech.com/build/software/lora-basics/lora-basics-for-gateways/?url=cupsproto.html#http-post-response

            /*-----------------------------------------------------------*\
            | Bytes | Field       | Description                           |
            |-------|-------------|---------------------------------------|
            | 1     | cupsUriLen  | Length of CUPS URI (cun)              |
            | cun   | cupsUri     | CUPS URI (cups.uri)                   |
            | 1     | tcUriLen    | Length of LNS URI (tun)               |
            | tun   | tcUri       | LNS URI (tc.uri)                      |
            | 2     | cupsCredLen | Length of CUPS credentials (ccn)      |
            | ccn   | cupsCred    | Credentials blob                      |
            | 2     | tcCredLen   | Length of LNS credentials (tcn)       |
            | tcn   | tcCred      | Credentials blob                      |
            | 4     | sigLen      | Length of signature for update blob   |
            | 4     | keyCRC      | CRC of the key used for the signature |
            | sig   | sig         | Signature over the update blob        |
            | 4     | updLen      | Length of generic update data (udn)   |
            | udn   | updData     | Generic update data blob              |
            \*-----------------------------------------------------------*/

            var rest = buffer;
            rest = WriteUrl(rest, CupsUrl, nameof(CupsUrl));
            rest = WriteUrl(rest, LnsUrl, nameof(LnsUrl));
            rest = WriteShort(rest, CupsCredential.Span, nameof(CupsCredential));
            rest = WriteShort(rest, LnsCredential.Span, nameof(LnsCredential));
            const int signatureKeyCrcSize = 4;
            rest = UpdateSignature is { Length: var signatureLength and > 0 } signature
                   ? rest.WriteUInt32LittleEndian(unchecked((uint)signatureLength) + signatureKeyCrcSize)
                         .WriteUInt32LittleEndian(SignatureKeyCrc)
                         .Write(signature.Span)
                   : rest.WriteUInt32LittleEndian(0);
            rest = rest.WriteUInt32LittleEndian(UpdateDataLength);
            return buffer[..^rest.Length];

            static Span<byte> WriteUrl(Span<byte> buffer, Uri? url, string name) =>
#pragma warning disable IDE0072 // Add missing cases (false positive)
                url?.GetLeftPart(UriPartial.Authority) switch
#pragma warning restore IDE0072 // Add missing cases
                {
                    null => buffer.Write((byte)0),
                    { Length: > byte.MaxValue } => throw new SerializationException($"Length of {name} is too long."),
                    { } someUrl => buffer.Write((byte)someUrl.Length).WriteUtf8(someUrl),
                };

            static Span<byte> WriteShort(Span<byte> buffer, ReadOnlySpan<byte> bytes, string name) =>
                bytes.Length <= ushort.MaxValue ? buffer.WriteUInt16LittleEndian(unchecked((ushort)bytes.Length)).Write(bytes)
                                                : throw new SerializationException($"Length of {name} is too long.");
        }
    }
}
