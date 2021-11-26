// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tools.CLI
{
    using System;
    using System.Buffers.Binary;
    using System.IO;
    using System.Text;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;
    using Azure;
    using Azure.Storage.Blobs.Models;
    using CommandLine;
    using LoRaWan.Tools.CLI.Helpers;
    using LoRaWan.Tools.CLI.Options;
    using Microsoft.Azure.Devices.Shared;

    public class Program
    {
        static ConfigurationHelper configurationHelper = new ConfigurationHelper();
        static IoTDeviceHelper iotDeviceHelper = new IoTDeviceHelper();

        static async Task<int> Main(string[] args)
        {
            WriteAzureLogo();
            Console.WriteLine("Azure IoT Edge LoRaWAN Starter Kit LoRa Leaf Device Provisioning Tool.");
            Console.Write("This tool complements ");
            Console.ForegroundColor = ConsoleColor.Blue;
            Console.WriteLine("http://aka.ms/lora");
            Console.ResetColor();
            Console.WriteLine();

            var success = await Parser.Default.ParseArguments<ListOptions, QueryOptions, VerifyOptions, BulkVerifyOptions, AddOptions, UpdateOptions, RemoveOptions>(args)
                .MapResult(
                    (ListOptions opts) => RunListAndReturnExitCode(opts),
                    (QueryOptions opts) => RunQueryAndReturnExitCode(opts),
                    (VerifyOptions opts) => RunVerifyAndReturnExitCode(opts),
                    (BulkVerifyOptions opts) => RunBulkVerifyAndReturnExitCode(opts),
                    (AddOptions opts) => RunAddAndReturnExitCode(opts),
                    (UpdateOptions opts) => RunUpdateAndReturnExitCode(opts),
                    (RemoveOptions opts) => RunRemoveAndReturnExitCode(opts),
                    errs => Task.FromResult(false));

            if (success)
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

        private static async Task<bool> RunListAndReturnExitCode(ListOptions opts)
        {
            int page;
            int total;

            if (!configurationHelper.ReadConfig())
                return false;

            if (!int.TryParse(opts.Page, out page))
                page = 10;

            if (!int.TryParse(opts.Total, out total))
                total = -1;

            var isSuccess = await iotDeviceHelper.QueryDevices(configurationHelper, page, total);

            return isSuccess;
        }

        private static async Task<bool> RunQueryAndReturnExitCode(QueryOptions opts)
        {
            if (!configurationHelper.ReadConfig())
                return false;

            var twin = await iotDeviceHelper.QueryDeviceTwin(opts.DevEui, configurationHelper);

            if (twin != null)
            {
                StatusConsole.WriteTwin(opts.DevEui, twin);
                return true;
            }
            else
            {
                StatusConsole.WriteLogLine(MessageType.Error, $"Could not get data for device {opts.DevEui}.");
                return false;
            }
        }

        private static async Task<bool> RunVerifyAndReturnExitCode(VerifyOptions opts)
        {
            if (!configurationHelper.ReadConfig())
                return false;

            var twin = await iotDeviceHelper.QueryDeviceTwin(opts.DevEui, configurationHelper);

            if (twin != null)
            {
                StatusConsole.WriteTwin(opts.DevEui, twin);
                return iotDeviceHelper.VerifyDeviceTwin(opts.DevEui, opts.NetId, twin, configurationHelper, true);
            }
            else
            {
                StatusConsole.WriteLogLine(MessageType.Error, $"Could not get data for device {opts.DevEui}.");
                return false;
            }
        }

        private static async Task<bool> RunBulkVerifyAndReturnExitCode(BulkVerifyOptions opts)
        {
            int page;

            if (!configurationHelper.ReadConfig())
                return false;

            if (!int.TryParse(opts.Page, out page))
                page = 0;

            var isSuccess = await iotDeviceHelper.QueryDevicesAndVerify(configurationHelper, page);

            Console.WriteLine();
            if (isSuccess)
            {
                StatusConsole.WriteLogLine(MessageType.Info, "No errors were encountered.");
            }
            else
            {
                StatusConsole.WriteLogLine(MessageType.Error, "Errors detected in devices.");
            }

            return isSuccess;
        }

        private static async Task<bool> RunAddAndReturnExitCode(AddOptions opts)
        {
            if (!configurationHelper.ReadConfig())
                return false;

            bool isSuccess = false;

            opts = iotDeviceHelper.CleanOptions(opts as object, true) as AddOptions;

            if (opts.Type.ToUpperInvariant().Equals("CONCENTRATOR"))
            {
                var isVerified = iotDeviceHelper.VerifyConcentrator(opts);
                if (!isVerified) return false;
                if (configurationHelper.CertificateStorageContainerClient is null)
                {
                    StatusConsole.WriteLogLine(MessageType.Error, "Storage account is not correctly configured.");
                    return false;
                }

                // renormalize to Unix line endings
                var certificateBundleBlobName = opts.StationEui;
                var blobClient = configurationHelper.CertificateStorageContainerClient.GetBlobClient(certificateBundleBlobName);
                var certificateContent = Regex.Replace(File.ReadAllText(opts.CertificateBundleLocation), @"\r\n|\n\r|\n|\r", "\n");

                try
                {
                    _ = await blobClient.UploadAsync(new BinaryData(Encoding.UTF8.GetBytes(certificateContent)), overwrite: false);
                }
                catch (RequestFailedException ex) when (ex.ErrorCode == BlobErrorCode.BlobAlreadyExists)
                {
                    StatusConsole.WriteLogLine(MessageType.Error, $"Uploading certificate bundle failed because bundle already exists. Please use the 'update' verb to update existing concentrator configuration.");
                    return false;
                }
                catch (RequestFailedException ex)
                {
                    StatusConsole.WriteLogLine(MessageType.Error, $"Uploading certificate bundle failed with error: '{ex.Message}'.");
                    return false;
                }

                try
                {
                    var crc = new Force.Crc32.Crc32Algorithm();
                    var crcHash = BinaryPrimitives.ReadUInt32BigEndian(crc.ComputeHash(Encoding.UTF8.GetBytes(certificateContent)));
                    var twin = iotDeviceHelper.CreateConcentratorTwin(opts, crcHash, blobClient.Uri);
                    var success = await iotDeviceHelper.WriteDeviceTwin(twin, opts.StationEui, configurationHelper, true);
                    if (!success) await CleanupAsync();
                    return success;
                }
                catch (Exception)
                {
                    // If the twin was not successfully created, remove the uploaded certificate bundle.
                    await CleanupAsync();
                    throw;
                }

                Task CleanupAsync() => blobClient.DeleteIfExistsAsync();
            }

            opts = iotDeviceHelper.CompleteMissingAddOptions(opts, configurationHelper);

            if (iotDeviceHelper.VerifyDevice(opts, null, null, null, configurationHelper, true))
            {
                Twin twin = iotDeviceHelper.CreateDeviceTwin(opts);
                isSuccess = await iotDeviceHelper.WriteDeviceTwin(twin, opts.DevEui, configurationHelper, true);
            }
            else
            {
                StatusConsole.WriteLogLine(MessageType.Error, $"Can not add {opts.Type.ToUpper()} device.");
            }

            if (isSuccess)
            {
                var twin = await iotDeviceHelper.QueryDeviceTwin(opts.DevEui, configurationHelper);
                StatusConsole.WriteTwin(opts.DevEui, twin);
            }

            return isSuccess;
        }

        private static async Task<bool> RunUpdateAndReturnExitCode(UpdateOptions opts)
        {
            if (!configurationHelper.ReadConfig())
                return false;

            bool isSuccess = false;

            opts = iotDeviceHelper.CleanOptions(opts as object, false) as UpdateOptions;
            opts = iotDeviceHelper.CompleteMissingUpdateOptions(opts, configurationHelper);

            var twin = await iotDeviceHelper.QueryDeviceTwin(opts.DevEui, configurationHelper);

            if (twin != null)
            {
                twin = iotDeviceHelper.UpdateDeviceTwin(twin, opts);

                if (iotDeviceHelper.VerifyDeviceTwin(opts.DevEui, opts.NetId, twin, configurationHelper, true))
                {
                    isSuccess = await iotDeviceHelper.WriteDeviceTwin(twin, opts.DevEui, configurationHelper, false);

                    if (isSuccess)
                    {
                        var newTwin = await iotDeviceHelper.QueryDeviceTwin(opts.DevEui, configurationHelper);
                        StatusConsole.WriteTwin(opts.DevEui, twin);
                    }
                    else
                    {
                        Console.WriteLine();
                        StatusConsole.WriteLogLine(MessageType.Error, $"Can not update device {opts.DevEui}.");
                    }
                }
                else
                {
                    Console.WriteLine();
                    StatusConsole.WriteLogLine(MessageType.Error, $"Errors found in Twin data. Device {opts.DevEui} was not updated.");
                }
            }
            else
            {
                Console.WriteLine();
                StatusConsole.WriteLogLine(MessageType.Error, $"Could not get data for device {opts.DevEui}. Failed to update.");
                isSuccess = false;
            }

            return isSuccess;
        }

        private static async Task<bool> RunRemoveAndReturnExitCode(RemoveOptions opts)
        {
            if (!configurationHelper.ReadConfig())
                return false;

            return await iotDeviceHelper.RemoveDevice(opts.DevEui, configurationHelper);
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
