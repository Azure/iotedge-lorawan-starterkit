// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tools.CLI
{
    using System;
    using CommandLine;
    using LoRaWan.Tools.CLI.Helpers;
    using LoRaWan.Tools.CLI.Options;
    using Microsoft.Azure.Devices.Shared;
    using Newtonsoft.Json.Linq;

    public class Program
    {
        static ConfigurationHelper configurationHelper = new ConfigurationHelper();
        static IoTDeviceHelper iotDeviceHelper = new IoTDeviceHelper();

        static int Main(string[] args)
        {
            WriteAzureLogo();
            Console.WriteLine("Azure IoT Edge LoRaWAN Starter Kit LoRa Leaf Device Provisioning Tool.");
            Console.Write("This tool complements ");
            Console.ForegroundColor = ConsoleColor.Blue;
            Console.WriteLine("http://aka.ms/lora");
            Console.ResetColor();
            Console.WriteLine();

            var success = Parser.Default.ParseArguments<ListOptions, QueryOptions, VerifyOptions, AddOptions, UpdateOptions, RemoveOptions>(args)
                .MapResult(
                    (ListOptions opts) => RunListAndReturnExitCode(opts),
                    (QueryOptions opts) => RunQueryAndReturnExitCode(opts),
                    (VerifyOptions opts) => RunVerifyAndReturnExitCode(opts),
                    (AddOptions opts) => RunAddAndReturnExitCode(opts),
                    (UpdateOptions opts) => RunUpdateAndReturnExitCode(opts),
                    (RemoveOptions opts) => RunRemoveAndReturnExitCode(opts),
                    errs => false);

            if ((bool)success)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine();
                Console.WriteLine("Successfully terminated.");
                Console.ResetColor();

                return (int)ExitCode.Success;
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine();
                Console.WriteLine("Terminated with errors.");
                Console.ResetColor();

                return (int)ExitCode.Error;
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

            return isSuccess;
        }

        private static object RunQueryAndReturnExitCode(QueryOptions opts)
        {
            if (!configurationHelper.ReadConfig())
                return false;

            var twin = iotDeviceHelper.QueryDeviceTwin(opts.DevEui, configurationHelper).Result;

            if (twin != null)
            {
                Console.WriteLine();
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"DevEUI: {opts.DevEui}");
                Console.WriteLine(TwinToString(twin));
                Console.ResetColor();
                return true;
            }
            else
            {
                StatusConsole.WriteLine(MessageType.Error, $"Could not get data for device {opts.DevEui}.");
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
                Console.WriteLine();
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"DevEUI: {opts.DevEui}");
                Console.WriteLine(TwinToString(twin));
                Console.ResetColor();

                return iotDeviceHelper.VerifyDeviceTwin(opts.DevEui, opts.NetId, twin, configurationHelper);
            }
            else
            {
                StatusConsole.WriteLine(MessageType.Error, $"Could not get data for device {opts.DevEui}.");
                return false;
            }
        }

        private static object RunAddAndReturnExitCode(AddOptions opts)
        {
            if (!configurationHelper.ReadConfig())
                return false;

            bool isSuccess = false;

            opts = iotDeviceHelper.CleanOptions(opts as object, true) as AddOptions;
            opts = iotDeviceHelper.CompleteMissingAddOptions(opts, configurationHelper);

            if (iotDeviceHelper.VerifyDevice(opts, null, null, null, configurationHelper))
            {
                Twin twin = iotDeviceHelper.CreateDeviceTwin(opts);
                isSuccess = iotDeviceHelper.WriteDeviceTwin(twin, opts.DevEui, configurationHelper, true).Result;
            }
            else
            {
                StatusConsole.WriteLine(MessageType.Error, $"Can not add {opts.Type.ToUpper()} device.");
            }

            if (isSuccess)
            {
                var twin = iotDeviceHelper.QueryDeviceTwin(opts.DevEui, configurationHelper).Result;
                Console.WriteLine();
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"DevEUI: {opts.DevEui}");
                Console.WriteLine(TwinToString(twin));
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
            opts = iotDeviceHelper.CompleteMissingUpdateOptions(opts, configurationHelper);

            var twin = iotDeviceHelper.QueryDeviceTwin(opts.DevEui, configurationHelper).Result;

            if (twin != null)
            {
                twin = iotDeviceHelper.UpdateDeviceTwin(twin, opts);

                if (iotDeviceHelper.VerifyDeviceTwin(opts.DevEui, opts.NetId, twin, configurationHelper))
                {
                    isSuccess = iotDeviceHelper.WriteDeviceTwin(twin, opts.DevEui, configurationHelper, false).Result;

                    if (isSuccess)
                    {
                        var newTwin = iotDeviceHelper.QueryDeviceTwin(opts.DevEui, configurationHelper).Result;
                        Console.WriteLine();
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.WriteLine($"DevEUI: {opts.DevEui}");
                        Console.WriteLine(TwinToString(newTwin));
                        Console.ResetColor();
                    }
                    else
                    {
                        Console.WriteLine();
                        StatusConsole.WriteLine(MessageType.Error, $"Can not update device {opts.DevEui}.");
                    }
                }
                else
                {
                    Console.WriteLine();
                    StatusConsole.WriteLine(MessageType.Error, $"Errors found in Twin data. Device {opts.DevEui} was not updated.");
                }
            }
            else
            {
                Console.WriteLine();
                StatusConsole.WriteLine(MessageType.Error, $"Could not get data for device {opts.DevEui}. Failed to update.");
                isSuccess = false;
            }

            return isSuccess;
        }

        private static string TwinToString(Twin twin)
        {
            var twinData = JObject.Parse(twin.Properties.Desired.ToJson());
            twinData.Remove("$metadata");
            twinData.Remove("$version");
            return twinData.ToString();
        }

        private static object RunRemoveAndReturnExitCode(RemoveOptions opts)
        {
            if (!configurationHelper.ReadConfig())
                return false;

            return iotDeviceHelper.RemoveDevice(opts.DevEui, configurationHelper).Result;
        }

        private static void WriteAzureLogo()
        {
            Console.WriteLine();
            Console.WriteLine(" █████╗ ███████╗██╗   ██╗██████╗ ███████╗");
            Console.WriteLine("██╔══██╗╚══███╔╝██║   ██║██╔══██╗██╔════╝");
            Console.WriteLine("███████║  ███╔╝ ██║   ██║██████╔╝█████╗  ");
            Console.WriteLine("██╔══██║ ███╔╝  ██║   ██║██╔══██╗██╔══╝  ");
            Console.WriteLine("██║  ██║███████╗╚██████╔╝██║  ██║███████╗");
            Console.WriteLine("╚═╝  ╚═╝╚══════╝ ╚═════╝ ╚═╝  ╚═╝╚══════╝");
            Console.WriteLine();
        }
    }
}
