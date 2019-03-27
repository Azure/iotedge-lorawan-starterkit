// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tools.CLI.Options
{
    using CommandLine;

    [Verb("list", HelpText = "Lits devices in IoT Hub.")]
    public class ListOptions
    {
        [Option(
            "page",
            Required = false,
            HelpText = "Devices per page. Default is 10.")]
        public string Page { get; set; }

        [Option(
            "total",
            Required = false,
            HelpText = "Maximum number of devices to list. Default is all.")]
        public string Total { get; set; }
    }
}
