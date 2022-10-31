// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tools.CLI.Options
{
    using CommandLine;

    [Verb("upgrade-firmware", HelpText = "Triggers a firmware upgrade for a station.")]
    public class UpgradeFirmwareOptions
    {
        [Option("storage-connection-string",
                Required = true,
                HelpText = "Storage account connection string: Connection string of the Storage account.")]
        public string StorageConnectionString { get; set; }

        [Option("stationeui",
                Required = true,
                HelpText = "Station EUI: A 16 bit hex string (e.g. 'AABBCCDDEEFFGGHH').")]
        public string StationEui { get; set; }

        [Option("package",
                Required = true,
                HelpText = "Package: New package version to be installed on the station (e.g. '1.0.0').")]
        public string Package { get; set; }

        [Option("firmware-location",
                Required = true,
                HelpText = "Firmware location: Local path of the firmware upgrade executable.")]
        public string FirmwareLocation { get; set; }

        [Option("digest-location",
                Required = true,
                HelpText = "Digest location: Local path of the file containing a base 64 encoded digest of the upgrade file.")]
        public string DigestLocation { get; set; }

        [Option("checksum-location",
                Required = true,
                HelpText = "Checksum location: Local path of the file containing a CRC32 checksum of the signature key used to generate the digest.")]
        public string ChecksumLocation { get; set; }
    }
}
