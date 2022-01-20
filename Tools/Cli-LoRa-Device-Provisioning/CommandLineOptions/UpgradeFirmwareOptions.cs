// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tools.CLI.CommandLineOptions
{
    using CommandLine;

    [Verb("upgrade-firmware", HelpText = "Triggers a firmware upgrade for a station.")]
    public class UpgradeFirmwareOptions
    {
        public UpgradeFirmwareOptions(string stationEui, string package, string firmwareLocation, string digest, string checksum)
        {
            StationEui = stationEui;
            Package = package;
            FirmwareLocation = firmwareLocation;
            Digest = digest;
            Checksum = checksum;
        }

        [Option("stationeui",
                Required = true,
                HelpText = "Station EUI: A 16 bit hex string ('AABBCCDDEEFFGGHH').")]
        public string StationEui { get; }

        [Option("package",
                Required = true,
                HelpText = "Package: New package version to be installed on the station ('1.0.0').")]
        public string Package { get; }

        [Option("firmware-location",
                Required = true,
                HelpText = "Firmware location: Local path of the firmware upgrade executable.")]
        public string FirmwareLocation { get; }

        [Option("digest",
                Required = true,
                HelpText = "Digest: digest of the firmware upgrade file, generated with a signature key installed on the station.")]
        public string Digest { get; }

        [Option("checksum",
                Required = true,
                HelpText = "Checksum: CRC32 checksum of the signature key used to generate the digest.")]
        public string Checksum { get; }
    }
}
