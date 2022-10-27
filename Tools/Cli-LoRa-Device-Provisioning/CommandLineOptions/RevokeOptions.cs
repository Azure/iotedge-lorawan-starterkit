// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tools.CLI.Options
{
    using CommandLine;

    [Verb("revoke", HelpText = "Revokes a client certificate thumbprint for a station.")]
    public class RevokeOptions
    {
        [Option("stationeui",
                Required = true,
                HelpText = "Station EUI: Required id '--concentrator' switch is set. A 16 bit hex string ('AABBCCDDEEFFGGHH').")]
        public string StationEui { get; set; }

        [Option("client-certificate-thumbprint",
                Required = true,
                HelpText = "Client certificate thumbprint: A client certificate thumbprint that should be revoked and not accepted anymore by the CUPS/LNS endpoints.")]
        public string ClientCertificateThumbprint { get; set; }
    }
}
