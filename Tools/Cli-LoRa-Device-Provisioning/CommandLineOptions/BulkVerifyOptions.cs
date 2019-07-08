// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tools.CLI.Options
{
    using CommandLine;

    [Verb("bulkverify", HelpText = "Verify all devices in IoT Hub.")]
    public class BulkVerifyOptions
    {
        [Option(
            "page",
            Required = false,
            HelpText = "Errors listed per page. Default is all.")]
        public string Page { get; set; }
    }
}
