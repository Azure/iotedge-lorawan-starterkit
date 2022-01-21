// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

namespace LoRaWan.NetworkServer.BasicsStation
{
    using System;
    using System.Text.Json.Serialization;
    using Newtonsoft.Json;

    internal sealed record CupsTwinInfo
    {
        // Credential management does not require anything more than the shared endpoint URIs and CRCs
        // Firmware management features could require to define fields in twin in a different way than the station/model/package ones
        public CupsTwinInfo(Uri cupsUri,
                            Uri tcUri,
                            uint cupsCredCrc,
                            uint tcCredCrc,
                            string cupsCredentialUrl,
                            string tcCredentialUrl,
                            string package,
                            uint fwKeyChecksum,
                            string fwSignatureInBase64,
                            Uri fwUrl)
        {
            CupsUri = cupsUri ?? throw new ArgumentNullException(nameof(cupsUri));
            TcUri = tcUri ?? throw new ArgumentNullException(nameof(tcUri));
            CupsCredCrc = cupsCredCrc;
            TcCredCrc = tcCredCrc;
            CupsCredentialUrl = cupsCredentialUrl;
            TcCredentialUrl = tcCredentialUrl;
            Package = package;
            FwKeyChecksum = fwKeyChecksum;
            FwSignatureInBase64 = fwSignatureInBase64;
            FwUrl = fwUrl;
        }

        [JsonPropertyName("cupsUri")]
        [JsonProperty("cupsUri")]
        public Uri CupsUri { get; }

        [JsonPropertyName("tcUri")]
        [JsonProperty("tcUri")]
        public Uri TcUri { get; }

        [JsonPropertyName("cupsCredCrc")]
        [JsonProperty("cupsCredCrc")]
        public uint CupsCredCrc { get; init; }

        [JsonPropertyName("tcCredCrc")]
        [JsonProperty("tcCredCrc")]
        public uint TcCredCrc { get; init; }

        [JsonPropertyName("cupsCredentialUrl")]
        [JsonProperty("cupsCredentialUrl")]
        public string CupsCredentialUrl { get; }

        [JsonPropertyName("tcCredentialUrl")]
        [JsonProperty("tcCredentialUrl")]
        public string TcCredentialUrl { get; }

        [JsonPropertyName("package")]
        [JsonProperty("package")]
        public string Package { get; init; }

        [JsonPropertyName("fwKeyChecksum")]
        [JsonProperty("fwKeyChecksum")]
        public uint FwKeyChecksum { get; init; }

        [JsonPropertyName("fwSignature")]
        [JsonProperty("fwSignature")]
        public string FwSignatureInBase64 { get; init; }

        [JsonPropertyName("fwUrl")]
        [JsonProperty("fwUrl")]
        public Uri FwUrl { get; init; }
    }
}
