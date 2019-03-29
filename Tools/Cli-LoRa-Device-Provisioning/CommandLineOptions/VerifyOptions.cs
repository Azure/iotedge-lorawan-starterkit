// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tools.CLI.Options
{
    using CommandLine;

    [Verb("verify", HelpText = "Verify a single device in IoT Hub.")]
    public class VerifyOptions
    {
        [Option(
            "deveui",
            Required = true,
            HelpText = "DevEUI / Device Id.")]
        public string DevEui { get; set; }

        [Option(
            "netid",
            Required = false,
            HelpText = "Network ID (Only for ABP devices): A 3 bit hex string. Will default to 000001 or NetId set in settings file if left blank. (optional)")]
        public string NetId { get; set; }
    }
}
