// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tools.CLI.CommandLineOptions
{
    using CommandLine;

    [Verb("upgrade-firmware", HelpText = "Triggers a firmware upgrade for a station.")]
    public class UpgradeFirmwareOptions
    {
        public UpgradeFirmwareOptions(string stationEui, string package, string firmwareLocation, string digestLocation, string checksumLocation)
        {
            StationEui = stationEui;
            Package = package;
            FirmwareLocation = firmwareLocation;
            DigestLocation = digestLocation;
            ChecksumLocation = checksumLocation;
        }

        [Option("stationeui",
                Required = true,
                HelpText = "Station EUI: A 16 bit hex string (e.g. 'AABBCCDDEEFFGGHH').")]
        public string StationEui { get; }

        [Option("package",
                Required = true,
                HelpText = "Package: New package version to be installed on the station (e.g. '1.0.0').")]
        public string Package { get; }

        [Option("firmware-location",
                Required = true,
                HelpText = "Firmware location: Local path of the firmware upgrade executable.")]
        public string FirmwareLocation { get; }

        [Option("digest-location",
                Required = true,
                HelpText = "Digest location: Local path of the file containing a digest of the firmware upgrade, generated with a signature key installed on the station.")]
        public string DigestLocation { get; }

        [Option("checksum-location",
                Required = true,
                HelpText = "Checksum location: Local path of the file containing a CRC32 checksum of the signature key used to generate the digest.")]
        public string ChecksumLocation { get; }
    }
}
