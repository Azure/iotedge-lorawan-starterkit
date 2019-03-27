// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tools.CLI.Options
{
    using CommandLine;

    [Verb("remove", HelpText = "Remove an existing device from IoT Hub.")]
    public class RemoveOptions
    {
        [Option(
            "deveui",
            Required = true,
            HelpText = "DevEUI / Device Id.")]
        public string DevEui { get; set; }
    }
}
