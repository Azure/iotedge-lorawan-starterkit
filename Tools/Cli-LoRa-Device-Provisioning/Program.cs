// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tools.CLI
{
    using System;
    using System.Buffers.Binary;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;
    using Azure;
    using CommandLine;
    using LoRaWan.Tools.CLI.CommandLineOptions;
    using LoRaWan.Tools.CLI.Helpers;
    using LoRaWan.Tools.CLI.Options;
    using Microsoft.Azure.Devices.Shared;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    public static class Program
    {
        private static readonly ConfigurationHelper configurationHelper = new ConfigurationHelper();

        public static async Task<int> Main(string[] args)
        {
            try
            {
                WriteAzureLogo();
                Console.WriteLine("Azure IoT Edge LoRaWAN Starter Kit LoRa Leaf Device Provisioning Tool.");
                Console.Write("This tool complements ");
                Console.ForegroundColor = ConsoleColor.Blue;
                Console.WriteLine("http://aka.ms/lora");
                Console.ResetColor();
                Console.WriteLine();

                var success = await Parser.Default.ParseArguments<ListOptions, QueryOptions, VerifyOptions, BulkVerifyOptions, AddOptions, UpdateOptions, RemoveOptions, RotateCertificateOptions, RevokeOptions>(args)
                    .MapResult(
                        (ListOptions opts) => RunListAndReturnExitCode(opts),
                        (QueryOptions opts) => RunQueryAndReturnExitCode(opts),
                        (VerifyOptions opts) => RunVerifyAndReturnExitCode(opts),
                        (BulkVerifyOptions opts) => RunBulkVerifyAndReturnExitCode(opts),
                        (AddOptions opts) => RunAddAndReturnExitCode(opts),
                        (UpdateOptions opts) => RunUpdateAndReturnExitCode(opts),
                        (RemoveOptions opts) => RunRemoveAndReturnExitCode(opts),
                        (RotateCertificateOptions opts) => RunRotateCertificateAndReturnExitCodeAsync(opts),
                        (RevokeOptions opts) => RunRevokeAndReturnExitCodeAsync(opts),
                        errs => Task.FromResult(false));

                if (success)
                {
                    WriteToConsole("Successfully terminated.", ConsoleColor.Green);
                    return (int)ExitCode.Success;
                }
                else
                {
                    WriteToConsole("Terminated with errors.", ConsoleColor.Red);
                    return (int)ExitCode.Error;
                }
            }
#pragma warning disable CA1031 // Do not catch general exception types
            // Fallback error handling for whole CLI.
            catch (Exception ex)
#pragma warning restore CA1031 // Do not catch general exception types
            {
                WriteToConsole($"Terminated with error: {ex}.", ConsoleColor.Red);
                return (int)ExitCode.Error;
            }

            static void WriteToConsole(string message, ConsoleColor color)
            {
                Console.ForegroundColor = color;
                Console.WriteLine();
                Console.WriteLine(message);
                Console.ResetColor();
            }
        }

        private static async Task<bool> RunListAndReturnExitCode(ListOptions opts)
        {
            if (!configurationHelper.ReadConfig())
                return false;

            if (!int.TryParse(opts.Page, out var page))
                page = 10;

            if (!int.TryParse(opts.Total, out var total))
                total = -1;

            var isSuccess = await IoTDeviceHelper.QueryDevices(configurationHelper, page, total);

            return isSuccess;
        }

        private static async Task<bool> RunQueryAndReturnExitCode(QueryOptions opts)
        {
            if (!configurationHelper.ReadConfig())
                return false;

            var twin = await IoTDeviceHelper.QueryDeviceTwin(opts.DevEui, configurationHelper);

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

            var twin = await IoTDeviceHelper.QueryDeviceTwin(opts.DevEui, configurationHelper);

            if (twin != null)
            {
                StatusConsole.WriteTwin(opts.DevEui, twin);
                return IoTDeviceHelper.VerifyDeviceTwin(opts.DevEui, opts.NetId, twin, configurationHelper, true);
            }
            else
            {
                StatusConsole.WriteLogLine(MessageType.Error, $"Could not get data for device {opts.DevEui}.");
                return false;
            }
        }

        private static async Task<bool> RunBulkVerifyAndReturnExitCode(BulkVerifyOptions opts)
        {
            if (!configurationHelper.ReadConfig())
                return false;

            if (!int.TryParse(opts.Page, out var page))
                page = 0;

            var isSuccess = await IoTDeviceHelper.QueryDevicesAndVerify(configurationHelper, page);

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

            var isSuccess = false;

            opts = IoTDeviceHelper.CleanOptions(opts, true) as AddOptions;

            if (opts.Type.Equals("concentrator", StringComparison.OrdinalIgnoreCase))
            {
                var isVerified = IoTDeviceHelper.VerifyConcentrator(opts);
                if (!isVerified) return false;
                if (configurationHelper.CertificateStorageContainerClient is null)
                {
                    StatusConsole.WriteLogLine(MessageType.Error, "Storage account is not correctly configured.");
                    return false;
                }

                if (await IoTDeviceHelper.QueryDeviceTwin(opts.StationEui, configurationHelper) is not null)
                {
                    StatusConsole.WriteLogLine(MessageType.Error, "Station was already created, please use the 'update' verb to update an existing station.");
                    return false;
                }

                if (opts.NoCups)
                {
                    var twin = IoTDeviceHelper.CreateConcentratorTwin(opts, 0, null);
                    return await IoTDeviceHelper.WriteDeviceTwin(twin, opts.StationEui, configurationHelper, true);
                }
                else
                {
                    return await UploadCertificateBundleAsync(opts.CertificateBundleLocation, opts.StationEui, async (crcHash, bundleStorageUri) =>
                    {
                        var twin = IoTDeviceHelper.CreateConcentratorTwin(opts, crcHash, bundleStorageUri);
                        return await IoTDeviceHelper.WriteDeviceTwin(twin, opts.StationEui, configurationHelper, true);
                    });
                }
            }

            opts = IoTDeviceHelper.CompleteMissingAddOptions(opts, configurationHelper);

            if (IoTDeviceHelper.VerifyDevice(opts, null, null, null, configurationHelper, true))
            {
                var twin = IoTDeviceHelper.CreateDeviceTwin(opts);
                isSuccess = await IoTDeviceHelper.WriteDeviceTwin(twin, opts.DevEui, configurationHelper, true);
            }
            else
            {
                StatusConsole.WriteLogLine(MessageType.Error, $"Can not add {opts.Type.ToUpper(CultureInfo.InvariantCulture)} device.");
            }

            if (isSuccess)
            {
                var twin = await IoTDeviceHelper.QueryDeviceTwin(opts.DevEui, configurationHelper);
                StatusConsole.WriteTwin(opts.DevEui, twin);
            }

            return isSuccess;
        }

        private static async Task<bool> RunRotateCertificateAndReturnExitCodeAsync(RotateCertificateOptions opts)
        {
            if (!configurationHelper.ReadConfig())
                return false;

            if (!File.Exists(opts.CertificateBundleLocation))
            {
                StatusConsole.WriteLogLine(MessageType.Error, "Certificate bundle does not exist at defined location.");
                return false;
            }

            var twin = await IoTDeviceHelper.QueryDeviceTwin(opts.StationEui, configurationHelper);

            if (twin is null)
            {
                StatusConsole.WriteLogLine(MessageType.Error, "Device was not found in IoT Hub. Please create it first.");
                return false;
            }

            var twinJObject = JsonConvert.DeserializeObject<JObject>(twin.Properties.Desired.ToJson());
            var cupsProperties = twinJObject[TwinProperty.Cups];
            var oldCupsCredentialBundleLocation = new Uri(cupsProperties[TwinProperty.CupsCredentialUrl].ToString());
            var oldTcCredentialBundleLocation = new Uri(cupsProperties[TwinProperty.TcCredentialUrl].ToString());

            // Upload new certificate bundle
            var success = await UploadCertificateBundleAsync(opts.CertificateBundleLocation, opts.StationEui, async (crcHash, bundleStorageUri) =>
            {
                var thumbprints = (JArray)twinJObject[TwinProperty.ClientThumbprint];
                if (!thumbprints.Any(t => string.Equals(t.ToString(), opts.ClientCertificateThumbprint, StringComparison.OrdinalIgnoreCase)))
                    thumbprints.Add(JToken.Parse($"\"{opts.ClientCertificateThumbprint}\""));

                twin.Properties.Desired[TwinProperty.ClientThumbprint] = thumbprints;
                twin.Properties.Desired[TwinProperty.Cups][TwinProperty.CupsCredentialCrc] = crcHash;
                twin.Properties.Desired[TwinProperty.Cups][TwinProperty.TcCredentialCrc] = crcHash;
                twin.Properties.Desired[TwinProperty.Cups][TwinProperty.CupsCredentialUrl] = bundleStorageUri;
                twin.Properties.Desired[TwinProperty.Cups][TwinProperty.TcCredentialUrl] = bundleStorageUri;

                return await IoTDeviceHelper.WriteDeviceTwin(twin, opts.StationEui, configurationHelper, isNewDevice: false);
            });

            // Clean up old certificate bundles
            try
            {
                _ = await configurationHelper.CertificateStorageContainerClient.DeleteBlobIfExistsAsync(oldCupsCredentialBundleLocation.Segments.Last());
            }
            finally
            {
                _ = await configurationHelper.CertificateStorageContainerClient.DeleteBlobIfExistsAsync(oldTcCredentialBundleLocation.Segments.Last());
            }

            return success;
        }

        private static async Task<bool> RunRevokeAndReturnExitCodeAsync(RevokeOptions opts)
        {
            if (!configurationHelper.ReadConfig())
                return false;

            var twin = await IoTDeviceHelper.QueryDeviceTwin(opts.StationEui, configurationHelper);

            if (twin is null)
            {
                StatusConsole.WriteLogLine(MessageType.Error, "Device was not found in IoT Hub. Please create it first.");
                return false;
            }

            var twinJObject = JsonConvert.DeserializeObject<JObject>(twin.Properties.Desired.ToJson());
            var clientThumprints = twinJObject[TwinProperty.ClientThumbprint];
            var t = clientThumprints.FirstOrDefault(t => t.ToString().Equals(opts.ClientCertificateThumbprint, StringComparison.OrdinalIgnoreCase));

            if (t is null)
                StatusConsole.WriteLogLine(MessageType.Error, "Specified thumbprint not found.");

            t?.Remove();
            twin.Properties.Desired[TwinProperty.ClientThumbprint] = clientThumprints;
            return await IoTDeviceHelper.WriteDeviceTwin(twin, opts.StationEui, configurationHelper, isNewDevice: false);
        }

        private static async Task<bool> RunUpdateAndReturnExitCode(UpdateOptions opts)
        {
            if (!configurationHelper.ReadConfig())
                return false;

            var isSuccess = false;

            opts = IoTDeviceHelper.CleanOptions(opts, false) as UpdateOptions;
            opts = IoTDeviceHelper.CompleteMissingUpdateOptions(opts, configurationHelper);

            var twin = await IoTDeviceHelper.QueryDeviceTwin(opts.DevEui, configurationHelper);

            if (twin != null)
            {
                twin = IoTDeviceHelper.UpdateDeviceTwin(twin, opts);

                if (IoTDeviceHelper.VerifyDeviceTwin(opts.DevEui, opts.NetId, twin, configurationHelper, true))
                {
                    isSuccess = await IoTDeviceHelper.WriteDeviceTwin(twin, opts.DevEui, configurationHelper, false);

                    if (isSuccess)
                    {
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

        private static async Task<bool> UploadCertificateBundleAsync(string certificateBundleLocation, string stationEui, Func<uint, Uri, Task<bool>> uploadSuccessActionAsync)
        {
            var certificateBundleBlobName = $"{stationEui}-{Guid.NewGuid():N}";
            var blobClient = configurationHelper.CertificateStorageContainerClient.GetBlobClient(certificateBundleBlobName);
            var fileContent = File.ReadAllBytes(certificateBundleLocation);

            try
            {
                _ = await blobClient.UploadAsync(new BinaryData(fileContent), overwrite: false);
            }
            catch (RequestFailedException ex)
            {
                StatusConsole.WriteLogLine(MessageType.Error, $"Uploading certificate bundle failed with error: '{ex.Message}'.");
                return false;
            }

            try
            {
                using var crc = new Force.Crc32.Crc32Algorithm();
                var crcHash = BinaryPrimitives.ReadUInt32BigEndian(crc.ComputeHash(fileContent));
                if (!await uploadSuccessActionAsync(crcHash, blobClient.Uri))
                    await CleanupAsync();
                return true;
            }
            catch (Exception)
            {
                await CleanupAsync();
                throw;
            }

            Task CleanupAsync() => blobClient.DeleteIfExistsAsync();
        }

        private static async Task<bool> RunRemoveAndReturnExitCode(RemoveOptions opts)
        {
            if (!configurationHelper.ReadConfig())
                return false;

            return await IoTDeviceHelper.RemoveDevice(opts.DevEui, configurationHelper);
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
