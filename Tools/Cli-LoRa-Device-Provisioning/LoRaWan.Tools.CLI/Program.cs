// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tools.CLI
{
    using System;
    using System.Buffers.Binary;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Azure;
    using CommandLine;
    using LoRaWan.Tools.CLI.Helpers;
    using LoRaWan.Tools.CLI.Options;
    using Microsoft.Azure.Devices;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;
    using QueryOptions = Options.QueryOptions;

    public static class Program
    {
        private const string EDGE_GATEWAY_MANIFEST_FILE = "./gateway-deployment-template.json";
        private const string EDGE_GATEWAY_OBSERVABILITY_MANIFEST_FILE = "./gateway-observability-layer-template.json";

        public static async Task<int> Main(string[] args)
        {
            if (args is null) throw new ArgumentNullException(nameof(args));

            WriteAzureLogo();
            Console.WriteLine("Azure IoT Edge LoRaWAN Starter Kit LoRa Device Provisioning Tool.");
            Console.Write("This tool complements ");
            Console.ForegroundColor = ConsoleColor.Blue;
            Console.WriteLine("http://aka.ms/lora");
            Console.ResetColor();
            Console.WriteLine();

            try
            {
                var configurationHelper = new ConfigurationHelper();
                if (!configurationHelper.ReadConfig(args))
                {
                    WriteToConsole("Failed to parse configuration.", ConsoleColor.Red);
                    return (int)ExitCode.Error;
                }

                return await Run(args, configurationHelper);
            }
#pragma warning disable CA1031 // Do not catch general exception types
            // Fallback error handling for whole CLI.
            catch (Exception ex)
#pragma warning restore CA1031 // Do not catch general exception types
            {
                WriteToConsole($"Terminated with error: {ex}.", ConsoleColor.Red);
                return (int)ExitCode.Error;
            }
        }

        internal static async Task<int> Run(string[] args, ConfigurationHelper configurationHelper)
        {
            using var parser = new Parser(config =>
            {
                config.CaseInsensitiveEnumValues = true;
                config.HelpWriter = Console.Error;
            });

            var success = await parser.ParseArguments<ListOptions, QueryOptions, VerifyOptions, BulkVerifyOptions, AddOptions, AddGatewayOption, UpdateOptions, RemoveOptions, RotateCertificateOptions, RevokeOptions, UpgradeFirmwareOptions>(args)
                .MapResult(
                    (ListOptions opts) => RunListAndReturnExitCode(configurationHelper, opts),
                    (QueryOptions opts) => RunQueryAndReturnExitCode(configurationHelper, opts),
                    (VerifyOptions opts) => RunVerifyAndReturnExitCode(configurationHelper, opts),
                    (BulkVerifyOptions opts) => RunBulkVerifyAndReturnExitCode(configurationHelper, opts),
                    (AddOptions opts) => RunAddAndReturnExitCode(configurationHelper, opts),
                    (AddGatewayOption opts) => RunAddGatewayAndReturnExitCode(configurationHelper, opts),
                    (UpdateOptions opts) => RunUpdateAndReturnExitCode(configurationHelper, opts),
                    (RemoveOptions opts) => RunRemoveAndReturnExitCode(configurationHelper, opts),
                    (RotateCertificateOptions opts) => RunRotateCertificateAndReturnExitCodeAsync(configurationHelper, opts),
                    (RevokeOptions opts) => RunRevokeAndReturnExitCodeAsync(configurationHelper, opts),
                    (UpgradeFirmwareOptions opts) => RunUpgradeFirmwareAndReturnExitCodeAsync(configurationHelper, opts),
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

        private static void WriteToConsole(string message, ConsoleColor color)
        {
            Console.ForegroundColor = color;
            Console.WriteLine();
            Console.WriteLine(message);
            Console.ResetColor();
        }

        private static async Task<bool> RunListAndReturnExitCode(ConfigurationHelper configurationHelper, ListOptions opts)
        {
            if (!int.TryParse(opts.Page, out var page))
                page = 10;

            if (!int.TryParse(opts.Total, out var total))
                total = -1;

            var isSuccess = await IoTDeviceHelper.QueryDevices(configurationHelper, page, total);

            return isSuccess;
        }

        private static async Task<bool> RunQueryAndReturnExitCode(ConfigurationHelper configurationHelper, QueryOptions opts)
        {
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

        private static async Task<bool> RunVerifyAndReturnExitCode(ConfigurationHelper configurationHelper, VerifyOptions opts)
        {
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

        private static async Task<bool> RunBulkVerifyAndReturnExitCode(ConfigurationHelper configurationHelper, BulkVerifyOptions opts)
        {
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

        private static async Task<bool> RunAddAndReturnExitCode(ConfigurationHelper configurationHelper, AddOptions opts)
        {
            opts = IoTDeviceHelper.CleanOptions(opts, true) as AddOptions;

            if (opts.Type == DeviceType.Concentrator)
            {
                return await CreateConcentratorDevice(configurationHelper, opts);
            }

            var isSuccess = false;

            opts = IoTDeviceHelper.CompleteMissingAddOptions(opts, configurationHelper);

            if (IoTDeviceHelper.VerifyDevice(opts, null, null, null, configurationHelper, true))
            {
                var twin = IoTDeviceHelper.CreateDeviceTwin(opts);

                // Check if the device already exists.
                var existingDeviceTwins = await IoTDeviceHelper.QueryDeviceTwin(opts.DevEui, configurationHelper);
                if (existingDeviceTwins is null)
                {
                    WriteToConsole($"Device {opts.DevEui} already exists, updating the twins.", ConsoleColor.Yellow);
                    isSuccess = await IoTDeviceHelper.WriteDeviceTwin(twin, opts.DevEui, configurationHelper, true);
                }
                else
                {
                    var updateOptions = opts.ToUpdateOptions();
                    var newTwins = IoTDeviceHelper.UpdateDeviceTwin(existingDeviceTwins, updateOptions);
                    newTwins.DeviceId = opts.DevEui;
                    isSuccess = await IoTDeviceHelper.WriteDeviceTwin(newTwins, opts.DevEui, configurationHelper, false);
                }
            }
            else
            {
                StatusConsole.WriteLogLine(MessageType.Error, $"Can not add {opts.Type.ToString().ToUpper(CultureInfo.InvariantCulture)} device.");
            }

            if (isSuccess)
            {
                var twin = await IoTDeviceHelper.QueryDeviceTwin(opts.DevEui, configurationHelper);
                StatusConsole.WriteTwin(opts.DevEui, twin);
            }

            return isSuccess;
        }

        private static async Task<bool> RunAddGatewayAndReturnExitCode(ConfigurationHelper configurationHelper, AddGatewayOption opts)
        {
            if (true == opts.MonitoringEnabled)
            {
                if (string.IsNullOrEmpty(opts.IoTHubResourceId))
                {
                    StatusConsole.WriteLogLine(MessageType.Error, "Provide iot hub resource identifier when enabling gateway monitoring.");
                    return false;
                }

                if (string.IsNullOrEmpty(opts.LogAnalyticsWorkspaceId))
                {
                    StatusConsole.WriteLogLine(MessageType.Error, "Provide log analytics workspace identifier when enabling gateway monitoring.");
                    return false;
                }

                if (string.IsNullOrEmpty(opts.LogAnalyticsSharedKey))
                {
                    StatusConsole.WriteLogLine(MessageType.Error, "Provide log analytics shared key when enabling gateway monitoring.");
                    return false;
                }

                var deploymentLayerContent = await GetEdgeObservabilityDeployment(opts);
                if (!await IoTDeviceHelper.CreateObservabilityDeploymentLayer(opts, deploymentLayerContent, configurationHelper))
                {
                    StatusConsole.WriteLogLine(MessageType.Error, "Failed to deploy observability deployment layer.");
                    return false;
                }
            }

            var deviceConfigurationContent = await GetEdgeGatewayDeployment(opts);
            return await IoTDeviceHelper.CreateGatewayTwin(opts, deviceConfigurationContent, configurationHelper);
        }

        private static async Task<ConfigurationContent> GetEdgeObservabilityDeployment(AddGatewayOption opts)
        {
            var manifest = await File.ReadAllTextAsync(EDGE_GATEWAY_OBSERVABILITY_MANIFEST_FILE);
            var tokenReplacements = new Dictionary<string, string>
            {
                { "[$iot_hub_resource_id]", opts.IoTHubResourceId },
                { "[$log_analytics_workspace_id]", opts.LogAnalyticsWorkspaceId },
                { "[$log_analytics_shared_key]", opts.LogAnalyticsSharedKey },
            };

            foreach (var token in tokenReplacements)
            {
                manifest = manifest.Replace(token.Key, token.Value);
            }

            return JsonConvert.DeserializeObject<ConfigurationContent>(manifest);
        }

        private static async Task<ConfigurationContent> GetEdgeGatewayDeployment(AddGatewayOption opts)
        {
            var manifest = await File.ReadAllTextAsync(EDGE_GATEWAY_MANIFEST_FILE);
            var tokenReplacements = new Dictionary<string, string>
            {
                { "[$reset_pin]", opts.ResetPin.ToString() },
                { "[\"$spi_speed\"]", opts.SpiSpeed == AddGatewayOption.DefaultSpiSpeed ? string.Empty : ",\"SPI_SPEED\":{\"value\":\"2\"}" },
                { "[\"$spi_dev\"]", opts.SpiDev == AddGatewayOption.DefaultSpiDev ? string.Empty : $",\"SPI_DEV\":{{\"value\":\"{opts.SpiDev}\"}}" },
                { "[$TWIN_FACADE_SERVER_URL]", opts.ApiURL.ToString() },
                { "[$TWIN_FACADE_AUTH_CODE]", opts.ApiAuthCode },
                { "[$TWIN_HOST_ADDRESS]", opts.TwinHostAddress },
                { "[$TWIN_NETWORK]", opts.Network },
                { "[$az_edge_version]", opts.AzureIotEdgeVersion },
                { "[$lora_version]", opts.LoRaVersion },
            };

            foreach (var token in tokenReplacements)
            {
                manifest = manifest.Replace(token.Key, token.Value);
            }

            return JsonConvert.DeserializeObject<ConfigurationContent>(manifest);
        }

        private static async Task<bool> CreateConcentratorDevice(ConfigurationHelper configurationHelper, AddOptions opts)
        {
            var isVerified = IoTDeviceHelper.VerifyConcentrator(opts);
            if (!isVerified) return false;
            if (!opts.NoCups && configurationHelper.CertificateStorageContainerClient is null)
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
                return await UploadCertificateBundleAsync(configurationHelper, opts.CertificateBundleLocation, opts.StationEui, async (crcHash, bundleStorageUri) =>
                {
                    var twin = IoTDeviceHelper.CreateConcentratorTwin(opts, crcHash, bundleStorageUri);
                    return await IoTDeviceHelper.WriteDeviceTwin(twin, opts.StationEui, configurationHelper, true);
                });
            }
        }

        private static async Task<bool> RunRotateCertificateAndReturnExitCodeAsync(ConfigurationHelper configurationHelper, RotateCertificateOptions opts)
        {
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
            var success = await UploadCertificateBundleAsync(configurationHelper, opts.CertificateBundleLocation, opts.StationEui, async (crcHash, bundleStorageUri) =>
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

        private static async Task<bool> RunRevokeAndReturnExitCodeAsync(ConfigurationHelper configurationHelper, RevokeOptions opts)
        {
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

        private static async Task<bool> RunUpdateAndReturnExitCode(ConfigurationHelper configurationHelper, UpdateOptions opts)
        {
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

        private static async Task<bool> UploadCertificateBundleAsync(ConfigurationHelper configurationHelper, string certificateBundleLocation, string stationEui, Func<uint, Uri, Task<bool>> uploadSuccessActionAsync)
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

        private static async Task<bool> RunUpgradeFirmwareAndReturnExitCodeAsync(ConfigurationHelper configurationHelper, UpgradeFirmwareOptions opts)
        {
            if (!File.Exists(opts.FirmwareLocation))
            {
                StatusConsole.WriteLogLine(MessageType.Error, "Firmware upgrade file does not exist at the specified location.");
                return false;
            }

            if (!File.Exists(opts.DigestLocation))
            {
                StatusConsole.WriteLogLine(MessageType.Error, "Digest of the firmware upgrade does not exist at the specified location.");
                return false;
            }

            if (!File.Exists(opts.ChecksumLocation))
            {
                StatusConsole.WriteLogLine(MessageType.Error, "CRC32 checksum of the signature key does not exist at the specified location.");
                return false;
            }

            // Upload firmware file to storage account
            var success = await UploadFirmwareAsync(configurationHelper, opts.FirmwareLocation, opts.StationEui, opts.Package, async (firmwareBlobUri) =>
            {
                var twin = await IoTDeviceHelper.QueryDeviceTwin(opts.StationEui, configurationHelper);

                if (twin is null)
                {
                    StatusConsole.WriteLogLine(MessageType.Error, "Device was not found in IoT Hub. Please create it first.");
                    return false;
                }

                if (!uint.TryParse(File.ReadAllText(opts.ChecksumLocation, Encoding.UTF8), out var checksum))
                {
                    StatusConsole.WriteLogLine(MessageType.Error, $"Could not parse the key checksum from file {opts.ChecksumLocation}.");
                    return false;
                }

                // Update station device twin
                twin.Properties.Desired[TwinProperty.Cups][TwinProperty.FirmwareVersion] = opts.Package;
                twin.Properties.Desired[TwinProperty.Cups][TwinProperty.FirmwareUrl] = firmwareBlobUri;
                twin.Properties.Desired[TwinProperty.Cups][TwinProperty.FirmwareKeyChecksum] = checksum;
                twin.Properties.Desired[TwinProperty.Cups][TwinProperty.FirmwareSignature] = File.ReadAllText(opts.DigestLocation, Encoding.UTF8);

                return await IoTDeviceHelper.WriteDeviceTwin(twin, opts.StationEui, configurationHelper, isNewDevice: false);
            });

            return success;
        }

        private static async Task<bool> UploadFirmwareAsync(ConfigurationHelper configurationHelper, string firmwareLocation, string stationEui, string package, Func<Uri, Task<bool>> uploadSuccessActionAsync)
        {
            var firmwareBlobName = $"{stationEui}-{package}";
            var blobClient = configurationHelper.FirmwareStorageContainerClient.GetBlobClient(firmwareBlobName);
            var fileContent = File.ReadAllBytes(firmwareLocation);

            StatusConsole.WriteLogLine(MessageType.Info, $"Uploading firmware {firmwareBlobName} to storage account...");

            try
            {
                _ = await blobClient.UploadAsync(new BinaryData(fileContent), overwrite: false);
            }
            catch (RequestFailedException ex)
            {
                StatusConsole.WriteLogLine(MessageType.Error, $"Uploading firmware failed with error: '{ex.Message}'.");
                return false;
            }

            try
            {
                if (!await uploadSuccessActionAsync(blobClient.Uri))
                {
                    await CleanupAsync();
                    return false;
                }

                return true;
            }
            catch (Exception)
            {
                await CleanupAsync();
                throw;
            }

            Task CleanupAsync() => blobClient.DeleteIfExistsAsync();
        }

        private static async Task<bool> RunRemoveAndReturnExitCode(ConfigurationHelper configurationHelper, RemoveOptions opts)
        {
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
