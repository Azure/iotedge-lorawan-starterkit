// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tools.CLI.CommandLineOptions
{
    using CommandLine;

    [Verb("rotate-certificate", HelpText = "Rotates the certificates for a station.")]
    public class RotateCertificateOptions
    {
        public RotateCertificateOptions(string stationEui, string certificateBundleLocation, string clientCertificateThumbprint)
        {
            StationEui = stationEui;
            CertificateBundleLocation = certificateBundleLocation;
            ClientCertificateThumbprint = clientCertificateThumbprint;
        }

        [Option("stationeui",
                Required = true,
                HelpText = "Station EUI: Required id '--concentrator' switch is set. A 16 bit hex string ('AABBCCDDEEFFGGHH').")]
        public string StationEui { get; }

        [Option("certificate-bundle-location",
                Required = true,
                HelpText = "Certificate bundle location: Points to the location of the (UTF-8-encoded) certificate bundle file.")]
        public string CertificateBundleLocation { get; }

        [Option("client-certificate-thumbprint",
                Required = true,
                HelpText = "Client certificate thumbprint: A client certificate thumbprint that should be accepted by the CUPS/LNS endpoints.")]
        public string ClientCertificateThumbprint { get; }
    }
}
