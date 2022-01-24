// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tools.CLI.CommandLineOptions
{
    using CommandLine;

    public class OptionsBase
    {
        [Option(
            "iothub-connection-string",
            Required = true,
            HelpText = "IoT Hub connection string: Connection string (iothubowner) of the IoT Hub (HostName=xxx.azure-devices.net;SharedAccessKeyName=iothubowner;SharedAccessKey=xxx).")]
        public string IoTHubConnectionString { get; set; }
    }
}
