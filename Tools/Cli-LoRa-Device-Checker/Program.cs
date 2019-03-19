// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cli_LoRa_Device_Checker
{
    using System;
    using CommandLine;
    using Microsoft.Azure.Devices.Shared;
    using Newtonsoft.Json.Linq;

    public class Program
    {
        static ConfigurationHelper configurationHelper = new ConfigurationHelper();
        static IoTDeviceHelper iotDeviceHelper = new IoTDeviceHelper();

        [Verb("list", HelpText = "Lits devices.")]
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

        [Verb("verify", HelpText = "Verify a single device.")]
        public class VerifyOptions
        {
            [Option(
                "deveui",
                Required = true,
                HelpText = "DevEUI / Device Id.")]
            public string DevEui { get; set; }
        }

        [Verb("query", HelpText = "Query a device twin.")]
        public class QueryOptions
        {
            [Option(
                "deveui",
                Required = true,
                HelpText = "DevEUI / Device Id.")]
            public string DevEui { get; set; }
        }

        [Verb("add", HelpText = "Add a new device.")]
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
                "appeui",
                Required = false,
                HelpText = "AppEUI (only for ABP devices): A 16 bit hex string. Will be randomly generated if left blank.")]
            public string AppEui { get; set; }

            [Option(
                "appkey",
                Required = false,
                HelpText = "AppKey (only for ABP devices): A 16 bit hex string. Will be randomly generated if left blank.")]
            public string AppKey { get; set; }

            [Option(
                "abprelaxmode",
                Required = false,
                HelpText = "ABPRelaxMode (ABP relaxed framecounter, only for ABP devices): True or false. (optional)")]
            public string ABPRelaxMode { get; set; }

            [Option(
                "appskey",
                Required = false,
                HelpText = "AppSKey (Only for OTAA devices): A 16 bit hex string. Will be randomly generated if left blank.")]
            public string AppSKey { get; set; }

            [Option(
                "nwkskey",
                Required = false,
                HelpText = "NwkSKey (Only for OTAA devices): A 16 bit hex string. Will be randomly generated if left blank.")]
            public string NwkSKey { get; set; }

            [Option(
                "devaddr",
                Required = false,
                HelpText = "DevAddr (Only for OTAA devices): A 4 bit hex string. Will be randomly generated if left blank.")]
            public string DevAddr { get; set; }

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
                HelpText = "Rx2DataRate (Receive window 2 data rate): Any of the allowed data rates. EU: SF12BW125, SF11BW125, SF10BW125, SF8BW125, SF7BW125, SF7BW250 or 50. US: SF10BW125, SF9BW125, SF8BW125, SF7BW125, SF8BW500, SF12BW500, SF11BW500, SF10BW500, SF9BW500, SF8BW500, SF8BW500. (optional).")]
            public string Rx2DataRate { get; set; }

            [Option(
                "rx1droffset",
                Required = false,
                HelpText = "Rx1DrOffset (Receive window 1 data rate offset): 0 through 15 (optional).")]
            public string Rx1DrOffset { get; set; }

            [Option(
                "supports32bitfcnt",
                Required = false,
                HelpText = "Supports32BitFCnt (Support for 32bit frame counter): True or false. (optional)")]
            public string Supports32BitFCnt { get; set; }
        }

        [Verb("update", HelpText = "Update an existing device.")]
        public class UpdateOptions
        {
            [Option(
                "deveui",
                Required = true,
                HelpText = "DevEUI / Device Id: A 16 bit hex string.")]
            public string DevEui { get; set; }

            [Option(
                "appeui",
                Required = false,
                HelpText = "AppEUI (only for ABP devices): A 16 bit hex string.")]
            public string AppEui { get; set; }

            [Option(
                "appkey",
                Required = false,
                HelpText = "AppKey (only for ABP devices): A 16 bit hex string.")]
            public string AppKey { get; set; }

            [Option(
                "abprelaxmode",
                Required = false,
                HelpText = "ABPRelaxMode (ABP relaxed framecounter, only for ABP devices): True or false. (optional)")]
            public string ABPRelaxMode { get; set; }

            [Option(
                "appskey",
                Required = false,
                HelpText = "AppSKey (Only for OTAA devices): A 16 bit hex string.")]
            public string AppSKey { get; set; }

            [Option(
                "nwkskey",
                Required = false,
                HelpText = "NwkSKey (Only for OTAA devices): A 16 bit hex string.")]
            public string NwkSKey { get; set; }

            [Option(
                "devaddr",
                Required = false,
                HelpText = "DevAddr (Only for OTAA devices): A 4 bit hex string.")]
            public string DevAddr { get; set; }

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
                HelpText = "Rx2DataRate (Receive window 2 data rate): Any of the allowed data rates. EU: SF12BW125, SF11BW125, SF10BW125, SF8BW125, SF7BW125, SF7BW250 or 50. US: SF10BW125, SF9BW125, SF8BW125, SF7BW125, SF8BW500, SF12BW500, SF11BW500, SF10BW500, SF9BW500, SF8BW500, SF8BW500. (optional).")]
            public string Rx2DataRate { get; set; }

            [Option(
                "rx1droffset",
                Required = false,
                HelpText = "Rx1DrOffset (Receive window 1 data rate offset): 0 through 15 (optional).")]
            public string Rx1DrOffset { get; set; }

            [Option(
                "supports32bitfcnt",
                Required = false,
                HelpText = "Supports32BitFCnt (Support for 32bit frame counter): True or false. (optional)")]
            public string Supports32BitFCnt { get; set; }
        }

        static void Main(string[] args)
        {
            Console.WriteLine("Azure IoT Edge LoRaWAN Starter Kit LoRa Leaf Device Verification Utility.");
            Console.Write("This tool complements ");
            Console.ForegroundColor = ConsoleColor.Blue;
            Console.WriteLine("http://aka.ms/lora \n");
            Console.ResetColor();

            var success = Parser.Default.ParseArguments<ListOptions, QueryOptions, VerifyOptions, AddOptions, UpdateOptions>(args)
                .MapResult(
                    (ListOptions opts) => RunListAndReturnExitCode(opts),
                    (QueryOptions opts) => RunQueryAndReturnExitCode(opts),
                    (VerifyOptions opts) => RunVerifyAndReturnExitCode(opts),
                    (AddOptions opts) => RunAddAndReturnExitCode(opts),
                    (UpdateOptions opts) => RunUpdateAndReturnExitCode(opts),
                    errs => false);

            if ((bool)success)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("\nSuccessfully terminated.");
                Console.ResetColor();
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("\nTerminated with errors.");
                Console.ResetColor();
            }
        }

        private static object RunListAndReturnExitCode(ListOptions opts)
        {
            int page;
            int total;

            if (!configurationHelper.ReadConfig())
                return false;

            if (!int.TryParse(opts.Page, out page))
                page = 10;

            if (!int.TryParse(opts.Total, out total))
                total = -1;

            var isSuccess = iotDeviceHelper.QueryDevices(configurationHelper, page, total).Result;

            Console.WriteLine();
            Console.WriteLine("Done.");
            return isSuccess;
        }

        private static object RunQueryAndReturnExitCode(QueryOptions opts)
        {
            if (!configurationHelper.ReadConfig())
                return false;

            var twin = iotDeviceHelper.QueryDeviceTwin(opts.DevEui, configurationHelper).Result;

            if (twin != null)
            {
                var twinData = JObject.Parse(twin.Properties.Desired.ToJson());
                twinData.Remove("$metadata");
                twinData.Remove("$version");
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine(twinData.ToString());
                Console.ResetColor();
                Console.WriteLine();
                return true;
            }
            else
            {
                Console.WriteLine($"Could not get data for device {opts.DevEui}.");
                return false;
            }
        }

        private static object RunVerifyAndReturnExitCode(VerifyOptions opts)
        {
            if (!configurationHelper.ReadConfig())
                return false;

            var twin = iotDeviceHelper.QueryDeviceTwin(opts.DevEui, configurationHelper).Result;

            if (twin != null)
            {
                var twinData = JObject.Parse(twin.Properties.Desired.ToJson());
                twinData.Remove("$metadata");
                twinData.Remove("$version");
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine(twinData.ToString());
                Console.ResetColor();
                Console.WriteLine();

                return iotDeviceHelper.VerifyDeviceTwin(opts.DevEui, twin);
            }
            else
            {
                Console.WriteLine($"Could not get data for device {opts.DevEui}.");
                return false;
            }
        }

        private static object RunAddAndReturnExitCode(AddOptions opts)
        {
            if (!configurationHelper.ReadConfig())
                return false;

            bool isSuccess = false;

            opts = iotDeviceHelper.CompleteMissingOptions(opts);
            opts = iotDeviceHelper.CleanOptions(opts as object, true) as AddOptions;

            if (iotDeviceHelper.ValidateDevice(opts))
            {
                Twin twin = iotDeviceHelper.CreateDeviceTwin(opts);
                isSuccess = iotDeviceHelper.WriteDeviceTwin(twin, opts.DevEui, configurationHelper, true).Result;
            }
            else
            {
                Console.WriteLine($"Can not add {opts.Type} device.");
            }

            if (isSuccess)
            {
                var twin = iotDeviceHelper.QueryDeviceTwin(opts.DevEui, configurationHelper).Result;
                var twinData = JObject.Parse(twin.Properties.Desired.ToJson());
                twinData.Remove("$metadata");
                twinData.Remove("$version");
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine(twinData.ToString());
                Console.ResetColor();
            }

            return isSuccess;
        }

        private static object RunUpdateAndReturnExitCode(UpdateOptions opts)
        {
            if (!configurationHelper.ReadConfig())
                return false;

            bool isSuccess = false;

            opts = iotDeviceHelper.CleanOptions(opts as object, false) as UpdateOptions;

            var twin = iotDeviceHelper.QueryDeviceTwin(opts.DevEui, configurationHelper).Result;

            if (twin != null)
            {
                twin = iotDeviceHelper.UpdateDeviceTwin(twin, opts);

                if (iotDeviceHelper.VerifyDeviceTwin(opts.DevEui, twin))
                {
                    isSuccess = iotDeviceHelper.WriteDeviceTwin(twin, opts.DevEui, configurationHelper, false).Result;

                    if (isSuccess)
                    {
                        Console.WriteLine($"Device {opts.DevEui} updated.");

                        var newTwin = iotDeviceHelper.QueryDeviceTwin(opts.DevEui, configurationHelper).Result;
                        var newTwinData = JObject.Parse(newTwin.Properties.Desired.ToJson());
                        newTwinData.Remove("$metadata");
                        newTwinData.Remove("$version");
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.WriteLine(newTwinData.ToString());
                        Console.ResetColor();
                    }
                    else
                    {
                        Console.WriteLine($"Can not update device {opts.DevEui}.");
                    }
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"\nErrors found in Twin data. Device {opts.DevEui} NOT updated.");
                    Console.ResetColor();
                }
            }
            else
            {
                Console.WriteLine($"Could not get data for device {opts.DevEui}. Failed to update.");
                isSuccess = false;
            }

            return isSuccess;
        }
    }
}
