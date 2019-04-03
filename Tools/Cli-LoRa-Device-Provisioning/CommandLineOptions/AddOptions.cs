// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tools.CLI.Options
{
    using CommandLine;

    [Verb("add", HelpText = "Add a new device to IoT Hub.")]
    public class AddOptions
    {
        [Option(
            "type",
            Required = true,
            HelpText = "Device type: Must be ABP or OTAA.")]
        public string Type { get; set; }

        [Option(
            "deveui",
            Required = false,
            HelpText = "DevEUI / Device Id: A 16 bit hex string. Will be randomly generated if left blank.")]
        public string DevEui { get; set; }

        [Option(
            "appskey",
            Required = false,
            HelpText = "AppSKey (Only for ABP devices): A 16 bit hex string. Will be randomly generated if left blank.")]
        public string AppSKey { get; set; }

        [Option(
            "nwkskey",
            Required = false,
            HelpText = "NwkSKey (Only for ABP devices): A 16 bit hex string. Will be randomly generated if left blank.")]
        public string NwkSKey { get; set; }

        [Option(
            "devaddr",
            Required = false,
            HelpText = "DevAddr (Only for ABP devices): A 4 bit hex string. Will be randomly generated if left blank.")]
        public string DevAddr { get; set; }

        [Option(
            "netid",
            Required = false,
            HelpText = "Network ID (Only for ABP devices): A 3 bit hex string. Will default to 000001 or NetId set in settings file if left blank. (optional)")]
        public string NetId { get; set; }

        [Option(
            "abprelaxmode",
            Required = false,
            HelpText = "ABPRelaxMode (ABP relaxed framecounter, only for ABP devices): True or false. (optional)")]
        public string ABPRelaxMode { get; set; }

        [Option(
            "appeui",
            Required = false,
            HelpText = "AppEUI (only for OTAA devices): A 16 bit hex string. Will be randomly generated if left blank.")]
        public string AppEui { get; set; }

        [Option(
            "appkey",
            Required = false,
            HelpText = "AppKey (only for OTAA devices): A 16 bit hex string. Will be randomly generated if left blank.")]
        public string AppKey { get; set; }

        [Option(
            "gatewayid",
            Required = false,
            HelpText = "GatewayID: A hostname. (optional)")]
        public string GatewayId { get; set; }

        [Option(
            "decoder",
            Required = false,
            HelpText = "SensorDecoder: The name of an integrated decoder function or the URI to a decoder in a custom decoder module in the format: http://modulename/api/decodername. (optional)")]
        public string SensorDecoder { get; set; }

        [Option(
            "classtype",
            Required = false,
            HelpText = "ClassType: \"A\" (default) or \"C\". (optional)")]
        public string ClassType { get; set; }

        [Option(
            "downlinkenabled",
            Required = false,
            HelpText = "DownlinkEnabled: True or false. (optional)")]
        public string DownlinkEnabled { get; set; }

        [Option(
            "preferredwindow",
            Required = false,
            HelpText = "PreferredWindow (Preferred receive window): 1 or 2. (optional)")]
        public string PreferredWindow { get; set; }

        [Option(
            "deduplication",
            Required = false,
            HelpText = "Deduplication: None (default), Drop or Mark. (optional)")]
        public string Deduplication { get; set; }

        [Option(
            "rx2datarate",
            Required = false,
            HelpText = "Rx2DataRate (Receive window 2 data rate, currently only supported for OTAA devices): Any of the allowed data rates. EU: SF12BW125, SF11BW125, SF10BW125, SF8BW125, SF7BW125, SF7BW250 or 50. US: SF10BW125, SF9BW125, SF8BW125, SF7BW125, SF8BW500, SF12BW500, SF11BW500, SF10BW500, SF9BW500, SF8BW500, SF8BW500. (optional).")]
        public string Rx2DataRate { get; set; }

        [Option(
            "rx1droffset",
            Required = false,
            HelpText = "Rx1DrOffset (Receive window 1 data rate offset, currently only supported for OTAA devices): 0 through 15. (optional)")]
        public string Rx1DrOffset { get; set; }

        [Option(
            "rxdelay",
            Required = false,
            HelpText = "RxDelay (Delay in seconds for sending downstream messages, currently only supported for OTAA devices): 0 through 15. (optional)")]
        public string RxDelay { get; set; }

        [Option(
            "keepalivetimeout",
            Required = false,
            HelpText = "KeepAliveTimeout (Timeout in seconds before device client connection is closed): 0 or 60 and above. (optional)")]
        public string KeepAliveTimeout { get; set; }

        [Option(
            "supports32bitfcnt",
            Required = false,
            HelpText = "Supports32BitFCnt (Support for 32bit frame counter): True or false. (optional)")]
        public string Supports32BitFCnt { get; set; }

        [Option(
            "fcntupstart",
            Required = false,
            HelpText = "FCntUpStart (Frame counter up start value): 0 through 4294967295. (optional)")]
        public string FCntUpStart { get; set; }

        [Option(
            "fcntdownstart",
            Required = false,
            HelpText = "FCntDownStart (Frame counter down start value): 0 through 4294967295. (optional)")]
        public string FCntDownStart { get; set; }

        [Option(
            "fcntresetcounter",
            Required = false,
            HelpText = "FCntResetCounter (Frame counter reset counter value): 0 through 4294967295. (optional)")]
        public string FCntResetCounter { get; set; }
    }
}
