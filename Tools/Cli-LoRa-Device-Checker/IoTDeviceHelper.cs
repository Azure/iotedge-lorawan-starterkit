// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cli_LoRa_Device_Checker
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices;
    using Microsoft.Azure.Devices.Shared;
    using Newtonsoft.Json.Linq;

    public class IoTDeviceHelper
    {
        public async Task<Twin> QueryDeviceTwin(string devEui, ConfigurationHelper configurationHelper)
        {
            Console.WriteLine($"\nQuerying device {devEui} in IoT Hub...");

            Twin twin;

            try
            {
                twin = await configurationHelper.RegistryManager.GetTwinAsync(devEui);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                return null;
            }

            return twin;
        }

        public bool VerifyDeviceTwin(string devEui, Twin twin)
        {
            bool isOTAA = false;
            bool isABP = false;
            bool isValid = true;

            var appEUI = twin.Properties.Desired.Contains(TwinProperty.AppEUI) ? ValidationHelper.GetTwinPropertyValue(twin.Properties.Desired[TwinProperty.AppEUI].Value) : null;
            var appKey = twin.Properties.Desired.Contains(TwinProperty.AppKey) ? ValidationHelper.GetTwinPropertyValue(twin.Properties.Desired[TwinProperty.AppKey].Value) : null;

            var nwkSKey = twin.Properties.Desired.Contains(TwinProperty.NwkSKey) ? ValidationHelper.GetTwinPropertyValue(twin.Properties.Desired[TwinProperty.NwkSKey].Value) : null;
            var appSKey = twin.Properties.Desired.Contains(TwinProperty.AppSKey) ? ValidationHelper.GetTwinPropertyValue(twin.Properties.Desired[TwinProperty.AppSKey].Value) : null;
            var devAddr = twin.Properties.Desired.Contains(TwinProperty.DevAddr) ? ValidationHelper.GetTwinPropertyValue(twin.Properties.Desired[TwinProperty.DevAddr].Value) : null;

            var gatewayID = twin.Properties.Desired.Contains(TwinProperty.GatewayID) ? ValidationHelper.GetTwinPropertyValue(twin.Properties.Desired[TwinProperty.GatewayID].Value) : null;
            var sensorDecoder = twin.Properties.Desired.Contains(TwinProperty.SensorDecoder) ? ValidationHelper.GetTwinPropertyValue(twin.Properties.Desired[TwinProperty.SensorDecoder].Value) : null;
            var classType = twin.Properties.Desired.Contains(TwinProperty.ClassType) ? ValidationHelper.GetTwinPropertyValue(twin.Properties.Desired[TwinProperty.ClassType].Value) : null;
            var abpRelaxMode = twin.Properties.Desired.Contains(TwinProperty.ABPRelaxMode) ? ValidationHelper.GetTwinPropertyValue(twin.Properties.Desired[TwinProperty.ABPRelaxMode].Value) : null;
            var downlinkEnabled = twin.Properties.Desired.Contains(TwinProperty.DownlinkEnabled) ? ValidationHelper.GetTwinPropertyValue(twin.Properties.Desired[TwinProperty.DownlinkEnabled].Value) : null;
            var preferredWindow = twin.Properties.Desired.Contains(TwinProperty.PreferredWindow) ? ValidationHelper.GetTwinPropertyValue(twin.Properties.Desired[TwinProperty.PreferredWindow].Value) : null;
            var deduplication = twin.Properties.Desired.Contains(TwinProperty.Deduplication) ? ValidationHelper.GetTwinPropertyValue(twin.Properties.Desired[TwinProperty.Deduplication].Value) : null;
            var rx2DataRate = twin.Properties.Desired.Contains(TwinProperty.RX2DataRate) ? ValidationHelper.GetTwinPropertyValue(twin.Properties.Desired[TwinProperty.RX2DataRate].Value) : null;
            var rx1DrOffset = twin.Properties.Desired.Contains(TwinProperty.RX1DROffset) ? ValidationHelper.GetTwinPropertyValue(twin.Properties.Desired[TwinProperty.RX1DROffset].Value) : null;
            var supports32BitFCnt = twin.Properties.Desired.Contains(TwinProperty.Supports32BitFCnt) ? ValidationHelper.GetTwinPropertyValue(twin.Properties.Desired[TwinProperty.Supports32BitFCnt].Value) : null;

            if (!string.IsNullOrEmpty(appEUI) || !string.IsNullOrEmpty(appKey))
                isABP = true;

            if (!string.IsNullOrEmpty(nwkSKey) || !string.IsNullOrEmpty(appSKey) || !string.IsNullOrEmpty(devAddr))
                isOTAA = true;

            // ABP device
            if (isABP && !isOTAA)
            {
                Console.WriteLine("ABP device configuration detected.");

                isValid = this.ValidateDevice(
                    new Program.AddOptions()
                    {
                        Type = "ABP",
                        DevEui = devEui,
                        AppEui = appEUI,
                        AppKey = appKey,
                        ABPRelaxMode = abpRelaxMode,
                        GatewayId = gatewayID,
                        SensorDecoder = sensorDecoder,
                        ClassType = classType,
                        DownlinkEnabled = downlinkEnabled,
                        PreferredWindow = preferredWindow,
                        Deduplication = deduplication,
                        Rx2DataRate = rx2DataRate,
                        Rx1DrOffset = rx1DrOffset,
                        Supports32BitFCnt = supports32BitFCnt
                    });

                if (!string.IsNullOrEmpty(nwkSKey))
                {
                    Console.WriteLine("Error: ABP device should not contain NwkSKey.");
                    isValid = false;
                }

                if (!string.IsNullOrEmpty(appSKey))
                {
                    Console.WriteLine("Error: ABP device should not contain AppSKey.");
                    isValid = false;
                }

                if (!string.IsNullOrEmpty(devAddr))
                {
                    Console.WriteLine("Error: ABP device should not contain DevAddr.");
                    isValid = false;
                }
            }

            // OTAA device
            else if (isOTAA && !isABP)
            {
                Console.WriteLine("OTAA device configuration detected.");
                isValid = this.ValidateDevice(
                    new Program.AddOptions()
                    {
                        Type = "OTAA",
                        DevEui = devEui,
                        NwkSKey = nwkSKey,
                        AppSKey = appSKey,
                        DevAddr = devAddr,
                        GatewayId = gatewayID,
                        SensorDecoder = sensorDecoder,
                        ClassType = classType,
                        DownlinkEnabled = downlinkEnabled,
                        PreferredWindow = preferredWindow,
                        Deduplication = deduplication,
                        Rx2DataRate = rx2DataRate,
                        Rx1DrOffset = rx1DrOffset,
                        Supports32BitFCnt = supports32BitFCnt
                    });

                if (!string.IsNullOrEmpty(appEUI))
                {
                    Console.WriteLine("Error: OTAA device should not contain AppEUI.");
                    isValid = false;
                }

                if (!string.IsNullOrEmpty(appKey))
                {
                    Console.WriteLine("Error: OTAA device should not contain AppKey.");
                    isValid = false;
                }
            }

            // Unknown device type
            else
            {
                Console.WriteLine("Error: Can't determine if OTAA or ABP device.");
                Console.WriteLine("OTAA devices should contain NwkSKey, AppSKey and DevAddr, not AppEUI and AppKey.");
                Console.WriteLine("ABP devices should contain AppEUI and AppKey, not NwkSKey, AppSKey and DevAddr.");
                isValid = false;
            }

            if (isValid)
            {
                Console.Write($"The configuration for device {devEui} ");
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("is valid.");
                Console.ResetColor();
            }
            else
            {
                Console.Write($"Error: The configuration for device {devEui} ");
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("is NOT valid.");
                Console.ResetColor();
            }

            return isValid;
        }

        public Program.AddOptions CompleteMissingOptions(Program.AddOptions opts)
        {
            Console.WriteLine($"\nCompleting missing options for device...");

            // OTAA device specific properties
            if (string.Equals(opts.Type, "OTAA", StringComparison.InvariantCultureIgnoreCase))
            {
                if (string.IsNullOrEmpty(opts.DevEui))
                {
                    opts.DevEui = Keygen.Generate(8);
                    Console.WriteLine($"Info: Generating missing DevEUI: {opts.DevEui}");
                }

                if (string.IsNullOrEmpty(opts.NwkSKey))
                {
                    opts.NwkSKey = Keygen.Generate(16);
                    Console.WriteLine($"Info: Generating missing NwkSKey: {opts.NwkSKey}");
                }

                if (string.IsNullOrEmpty(opts.AppSKey))
                {
                    opts.AppSKey = Keygen.Generate(16);
                    Console.WriteLine($"Info: Generating missing AppSKey: {opts.AppSKey}");
                }

                if (string.IsNullOrEmpty(opts.DevAddr))
                {
                    opts.DevAddr = Keygen.Generate(4);
                    Console.WriteLine($"Info: Generating missing DevAddr: {opts.DevAddr}");
                }
            }

            // ABP device specific properties
            else
            {
                if (string.IsNullOrEmpty(opts.DevEui))
                {
                    opts.DevEui = Keygen.Generate(8);
                    Console.WriteLine($"Info: Generating missing DevEUI: {opts.DevEui}");
                }

                if (string.IsNullOrEmpty(opts.AppEui))
                {
                    opts.AppEui = Keygen.Generate(16);
                    Console.WriteLine($"Info: Generating missing AppEUI: {opts.AppEui}");
                }

                if (string.IsNullOrEmpty(opts.AppKey))
                {
                    opts.AppKey = Keygen.Generate(16);
                    Console.WriteLine($"Info: Generating missing AppKey: {opts.AppKey}");
                }
            }

            return opts;
        }

        public object CleanOptions(object optsObject, bool isNewDevice)
        {
            dynamic opts;

            if (isNewDevice)
                opts = optsObject as Program.AddOptions;
            else
                opts = optsObject as Program.UpdateOptions;

            // OTAA device specific properties
            if (!string.IsNullOrEmpty(opts.DevEui))
                opts.DevEui = ValidationHelper.CleanString(opts.DevEui);

            if (!string.IsNullOrEmpty(opts.NwkSKey))
                opts.NwkSKey = ValidationHelper.CleanString(opts.NwkSKey);

            if (!string.IsNullOrEmpty(opts.AppSKey))
                opts.AppSKey = ValidationHelper.CleanString(opts.AppSKey);

            if (!string.IsNullOrEmpty(opts.DevAddr))
                opts.DevAddr = ValidationHelper.CleanString(opts.DevAddr);

            // ABP device specific properties
            if (!string.IsNullOrEmpty(opts.DevEui))
                opts.DevEui = ValidationHelper.CleanString(opts.DevEui);

            if (!string.IsNullOrEmpty(opts.AppEui))
                opts.AppEui = ValidationHelper.CleanString(opts.AppEui);

            if (!string.IsNullOrEmpty(opts.AppKey))
                opts.AppKey = ValidationHelper.CleanString(opts.AppKey);

            if (!string.IsNullOrEmpty(opts.ABPRelaxMode))
                opts.ABPRelaxMode = ValidationHelper.CleanString(opts.ABPRelaxMode);

            // Shared device properties
            if (!string.IsNullOrEmpty(opts.GatewayId))
                opts.GatewayId = ValidationHelper.CleanString(opts.GatewayId);

            if (!string.IsNullOrEmpty(opts.SensorDecoder))
                opts.SensorDecoder = ValidationHelper.CleanString(opts.SensorDecoder);

            if (!string.IsNullOrEmpty(opts.ClassType))
                opts.ClassType = ValidationHelper.CleanString(opts.ClassType);

            if (!string.IsNullOrEmpty(opts.DownlinkEnabled))
                opts.DownlinkEnabled = ValidationHelper.CleanString(opts.DownlinkEnabled);

            if (!string.IsNullOrEmpty(opts.PreferredWindow))
                opts.PreferredWindow = ValidationHelper.CleanString(opts.PreferredWindow);

            if (!string.IsNullOrEmpty(opts.Deduplication))
                opts.Deduplication = ValidationHelper.CleanString(opts.Deduplication);

            if (!string.IsNullOrEmpty(opts.Rx2DataRate))
                opts.Rx2DataRate = ValidationHelper.CleanString(opts.Rx2DataRate);

            if (!string.IsNullOrEmpty(opts.Rx1DrOffset))
                opts.Rx1DrOffset = ValidationHelper.CleanString(opts.Rx1DrOffset);

            if (!string.IsNullOrEmpty(opts.Supports32BitFCnt))
                opts.Supports32BitFCnt = ValidationHelper.CleanString(opts.Supports32BitFCnt);

            return opts as object;
        }

        public bool ValidateDevice(Program.AddOptions opts)
        {
            string validationError = string.Empty;
            bool isValid = true;

            Console.WriteLine($"\nVerifying device {opts.DevEui} twin data...");

            if (string.IsNullOrEmpty(opts.DevEui))
            {
                Console.WriteLine("Error: DevEui is missing.");
                isValid = false;
            }
            else if (!ValidationHelper.ValidateHexStringTwinProperty(opts.DevEui, 8, out validationError))
            {
                Console.WriteLine($"Error: DevEui {opts.DevEui} is invalid: {validationError}.");
                isValid = false;
            }
            else
            {
                Console.WriteLine($"Info: DevEui {opts.DevEui} is valid.");
            }

            // OTAA device specific properties
            if (string.Equals(opts.Type, "OTAA", StringComparison.InvariantCultureIgnoreCase))
            {
                if (string.IsNullOrEmpty(opts.NwkSKey))
                {
                    Console.WriteLine("Error: NwkSKey is missing.");
                    isValid = false;
                }
                else if (!ValidationHelper.ValidateHexStringTwinProperty(opts.NwkSKey, 16, out validationError))
                {
                    Console.WriteLine($"Error: NwkSKey {opts.NwkSKey} is invalid: {validationError}.");
                    isValid = false;
                }
                else
                {
                    Console.WriteLine($"Info: NwkSKey {opts.NwkSKey} is valid.");
                }

                if (string.IsNullOrEmpty(opts.AppSKey))
                {
                    Console.WriteLine("Error: AppSKey is missing.");
                    isValid = false;
                }
                else if (!ValidationHelper.ValidateHexStringTwinProperty(opts.AppSKey, 16, out validationError))
                {
                    Console.WriteLine($"Error: AppSKey {opts.AppSKey} is invalid: {validationError}.");
                    isValid = false;
                }
                else
                {
                    Console.WriteLine($"Info: AppSKey {opts.AppSKey} is valid.");
                }

                if (string.IsNullOrEmpty(opts.DevAddr))
                {
                    Console.WriteLine("Error: DevAddr is missing.");
                    isValid = false;
                }
                else if (!ValidationHelper.ValidateHexStringTwinProperty(opts.DevAddr, 4, out validationError))
                {
                    Console.WriteLine($"Error: DevAddr {opts.DevAddr} is invalid: {validationError}.");
                    isValid = false;
                }
                else
                {
                    Console.WriteLine($"Info: DevAddr {opts.DevAddr} is valid.");
                }

                if (!ValidationHelper.ValidateSensorDecoder(opts.SensorDecoder, out validationError))
                {
                    isValid = false;
                }

                if (!string.IsNullOrEmpty(validationError))
                {
                    Console.WriteLine(validationError);
                }
                else
                {
                    Console.WriteLine($"Info: SensorDecoder {opts.SensorDecoder} is valid.");
                }
            }

            // ABP device specific properties
            else
            {
                if (string.IsNullOrEmpty(opts.AppEui))
                {
                    Console.WriteLine("Error: AppEUI is missing.");
                    isValid = false;
                }
                else if (!ValidationHelper.ValidateHexStringTwinProperty(opts.AppEui, 16, out validationError))
                {
                    Console.WriteLine($"Error: AppEUI {opts.AppEui} is invalid: {validationError}.");
                    isValid = false;
                }
                else
                {
                    Console.WriteLine($"Info: AppEui {opts.AppEui} is valid.");
                }

                if (string.IsNullOrEmpty(opts.AppKey))
                {
                    Console.WriteLine("Error: AppKey is missing.");
                    isValid = false;
                }
                else if (!ValidationHelper.ValidateHexStringTwinProperty(opts.AppKey, 16, out validationError))
                {
                    Console.WriteLine($"Error: AppKey {opts.AppKey} is invalid: {validationError}.");
                    isValid = false;
                }
                else
                {
                    Console.WriteLine($"Info: AppKey {opts.AppKey} is valid.");
                }

                if (!ValidationHelper.ValidateSensorDecoder(opts.SensorDecoder, out validationError))
                {
                    isValid = false;
                }

                if (!string.IsNullOrEmpty(validationError))
                {
                    Console.WriteLine(validationError);
                }
                else
                {
                    Console.WriteLine($"Info: SensorDecoder {opts.SensorDecoder} is valid.");
                }

                if (!string.IsNullOrEmpty(opts.ABPRelaxMode))
                {
                    if (!ValidationHelper.ValidateBoolTwinProperty(opts.ABPRelaxMode, out validationError))
                    {
                        Console.WriteLine($"Error: ABPRelaxMode {opts.ABPRelaxMode} is invalid: {validationError}.");
                        isValid = false;
                    }
                    else
                    {
                        Console.WriteLine($"Info: ABPRelaxMode {opts.ABPRelaxMode} is valid.");
                    }
                }
            }

            // Shared device properties
            if (!string.IsNullOrEmpty(opts.ClassType))
            {
                if (!(string.Equals(opts.ClassType, "A", StringComparison.InvariantCultureIgnoreCase) ||
                    string.Equals(opts.ClassType, "C", StringComparison.InvariantCultureIgnoreCase)))
                {
                    Console.WriteLine($"Error: ClassType {opts.ClassType} is invalid: If set, it needs to be \"A\" or \"C\".");
                    isValid = false;
                }
                else
                {
                    Console.WriteLine($"Info: ClassType {opts.ClassType} is valid.");
                }
            }

            if (!string.IsNullOrEmpty(opts.DownlinkEnabled))
            {
                if (!ValidationHelper.ValidateBoolTwinProperty(opts.DownlinkEnabled, out validationError))
                {
                    Console.WriteLine($"Error: DownlinkEnabled {opts.DownlinkEnabled} is invalid: {validationError}.");
                    isValid = false;
                }
                else
                {
                    Console.WriteLine($"Info: DownlinkEnabled {opts.DownlinkEnabled} is valid.");
                }
            }

            if (!string.IsNullOrEmpty(opts.PreferredWindow))
            {
                if (!ValidationHelper.ValdateIntTwinProperty(opts.PreferredWindow, 1, 2, out validationError))
                {
                    Console.WriteLine($"Error: PreferredWindow {opts.PreferredWindow} is invalid: {validationError}");
                    isValid = false;
                }
                else
                {
                    Console.WriteLine($"Info: PreferredWindow {opts.PreferredWindow} is valid.");
                }
            }

            if (!string.IsNullOrEmpty(opts.Deduplication))
            {
                if (!(string.Equals(opts.Deduplication, "None", StringComparison.InvariantCultureIgnoreCase) ||
                    string.Equals(opts.Deduplication, "Drop", StringComparison.InvariantCultureIgnoreCase) ||
                    string.Equals(opts.Deduplication, "Mark", StringComparison.InvariantCultureIgnoreCase)))
                {
                    Console.WriteLine($"Error: Deduplication {opts.Deduplication} is invalid: If set, it needs to be \"None\", \"Drop\" or \"Mark\".");
                    isValid = false;
                }
                else
                {
                    Console.WriteLine($"Info: Deduplication {opts.Deduplication} is valid.");
                }
            }

            if (!string.IsNullOrEmpty(opts.Rx2DataRate))
            {
                if (!ValidationHelper.ValdateDataRateTwinProperty(opts.Rx2DataRate, out validationError))
                {
                    Console.WriteLine($"Error: Rx2DataRate {opts.Rx2DataRate} is invalid: {validationError}.");
                    isValid = false;
                }
                else
                {
                    Console.WriteLine($"Info: Rx2DataRate {opts.Rx2DataRate} is valid.");
                }
            }

            if (!string.IsNullOrEmpty(opts.Rx1DrOffset))
            {
                if (!ValidationHelper.ValdateIntTwinProperty(opts.Rx1DrOffset, 0, 15, out validationError))
                {
                    Console.WriteLine($"Error: Rx1DrOffset {opts.Rx1DrOffset} is invalid: {validationError}.");
                    isValid = false;
                }
                else
                {
                    Console.WriteLine($"Info: Rx1DrOffset {opts.Rx1DrOffset} is valid.");
                }
            }

            if (!string.IsNullOrEmpty(opts.Supports32BitFCnt))
            {
                if (!ValidationHelper.ValidateBoolTwinProperty(opts.Supports32BitFCnt, out validationError))
                {
                    Console.WriteLine($"Error: Supports32BitFCnt {opts.Supports32BitFCnt} is invalid: {validationError}.");
                    isValid = false;
                }
                else
                {
                    Console.WriteLine($"Info: Supports32BitFCnt {opts.Supports32BitFCnt} is valid.");
                }
            }

            return isValid;
        }

        public Twin CreateDeviceTwin(Program.AddOptions opts)
        {
            var twinProperties = new TwinProperties();

            // OTAA device specific properties
            if (string.Equals(opts.Type, "OTAA", StringComparison.InvariantCultureIgnoreCase))
            {
                Console.WriteLine($"\nCreating OTAA device twin: {opts.DevEui}...");

                twinProperties.Desired[TwinProperty.AppSKey] = opts.AppSKey;
                twinProperties.Desired[TwinProperty.NwkSKey] = opts.NwkSKey;
                twinProperties.Desired[TwinProperty.DevAddr] = opts.DevAddr;
            }

            // ABP device specific properties
            else
            {
                Console.WriteLine($"\nCreating ABP device twin: {opts.DevEui}...");

                twinProperties.Desired[TwinProperty.AppEUI] = opts.AppEui;
                twinProperties.Desired[TwinProperty.AppKey] = opts.AppKey;

                if (!string.IsNullOrEmpty(opts.ABPRelaxMode))
                    twinProperties.Desired[TwinProperty.ABPRelaxMode] = ValidationHelper.SetBoolTwinProperty(opts.ABPRelaxMode);
            }

            // Shared properties
            twinProperties.Desired[TwinProperty.GatewayID] = opts.GatewayId;
            twinProperties.Desired[TwinProperty.SensorDecoder] = opts.SensorDecoder;

            // Shared optional properties
            if (!string.IsNullOrEmpty(opts.ClassType))
                twinProperties.Desired[TwinProperty.ClassType] = opts.ClassType;

            if (!string.IsNullOrEmpty(opts.DownlinkEnabled))
                twinProperties.Desired[TwinProperty.DownlinkEnabled] = ValidationHelper.SetBoolTwinProperty(opts.DownlinkEnabled);

            if (!string.IsNullOrEmpty(opts.PreferredWindow))
                twinProperties.Desired[TwinProperty.PreferredWindow] = Convert.ToInt32(opts.PreferredWindow);

            if (!string.IsNullOrEmpty(opts.Deduplication))
                twinProperties.Desired[TwinProperty.Deduplication] = opts.Deduplication;

            if (!string.IsNullOrEmpty(opts.Rx2DataRate))
                twinProperties.Desired[TwinProperty.RX2DataRate] = opts.Rx2DataRate;

            if (!string.IsNullOrEmpty(opts.Rx1DrOffset))
                twinProperties.Desired[TwinProperty.RX1DROffset] = Convert.ToInt32(opts.Rx1DrOffset);

            if (!string.IsNullOrEmpty(opts.Supports32BitFCnt))
                twinProperties.Desired[TwinProperty.Supports32BitFCnt] = ValidationHelper.SetBoolTwinProperty(opts.Supports32BitFCnt);

            var twin = new Twin();
            twin.Properties = twinProperties;

            return twin;
        }

        public Twin UpdateDeviceTwin(Twin twin, Program.UpdateOptions opts)
        {
            Console.WriteLine($"\nApplying changes to device {opts.DevEui} twin...");

            // OTAA device properties
            if (!string.IsNullOrEmpty(opts.AppSKey))
                twin.Properties.Desired[TwinProperty.AppSKey] = opts.AppSKey;

            if (!string.IsNullOrEmpty(opts.NwkSKey))
                twin.Properties.Desired[TwinProperty.NwkSKey] = opts.NwkSKey;

            if (!string.IsNullOrEmpty(opts.DevAddr))
                twin.Properties.Desired[TwinProperty.DevAddr] = opts.DevAddr;

            // ABP device properties
            if (!string.IsNullOrEmpty(opts.AppEui))
                twin.Properties.Desired[TwinProperty.AppEUI] = opts.AppEui;

            if (!string.IsNullOrEmpty(opts.AppKey))
                twin.Properties.Desired[TwinProperty.AppKey] = opts.AppKey;

            if (!string.IsNullOrEmpty(opts.ABPRelaxMode))
                twin.Properties.Desired[TwinProperty.ABPRelaxMode] = ValidationHelper.SetBoolTwinProperty(opts.ABPRelaxMode);

            // Shared properties
            if (!string.IsNullOrEmpty(opts.GatewayId))
                twin.Properties.Desired[TwinProperty.GatewayID] = opts.GatewayId;

            if (!string.IsNullOrEmpty(opts.SensorDecoder))
                twin.Properties.Desired[TwinProperty.SensorDecoder] = opts.SensorDecoder;

            // Shared optional properties
            if (!string.IsNullOrEmpty(opts.ClassType))
                twin.Properties.Desired[TwinProperty.ClassType] = opts.ClassType;

            if (!string.IsNullOrEmpty(opts.DownlinkEnabled))
                twin.Properties.Desired[TwinProperty.DownlinkEnabled] = ValidationHelper.SetBoolTwinProperty(opts.DownlinkEnabled);

            if (!string.IsNullOrEmpty(opts.PreferredWindow))
                twin.Properties.Desired[TwinProperty.PreferredWindow] = Convert.ToInt32(opts.PreferredWindow);

            if (!string.IsNullOrEmpty(opts.Deduplication))
                twin.Properties.Desired[TwinProperty.Deduplication] = opts.Deduplication;

            if (!string.IsNullOrEmpty(opts.Rx2DataRate))
                twin.Properties.Desired[TwinProperty.RX2DataRate] = opts.Rx2DataRate;

            if (!string.IsNullOrEmpty(opts.Rx1DrOffset))
                twin.Properties.Desired[TwinProperty.RX1DROffset] = Convert.ToInt32(opts.Rx1DrOffset);

            if (!string.IsNullOrEmpty(opts.Supports32BitFCnt))
                twin.Properties.Desired[TwinProperty.Supports32BitFCnt] = ValidationHelper.SetBoolTwinProperty(opts.Supports32BitFCnt);

            return twin;
        }

        public async Task<bool> WriteDeviceTwin(Twin twin, string deviceId, ConfigurationHelper configurationHelper, bool isNewDevice)
        {
            var device = new Device(deviceId);
            BulkRegistryOperationResult result;

            Console.WriteLine($"\nWriting device {deviceId} twin to IoT Hub...");

            // Add new device
            if (isNewDevice)
            {
                try
                {
                    result = await configurationHelper.RegistryManager.AddDeviceWithTwinAsync(device, twin);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error: {ex.Message}");
                    return false;
                }

                if (result.IsSuccessful)
                {
                    Console.WriteLine($"Success!");
                }
                else
                {
                    Console.WriteLine($"Error adding device:");
                    foreach (var error in result.Errors)
                    {
                        Console.WriteLine($"Device Id: {error.DeviceId}, Code: {error.ErrorCode}, Error: {error.ErrorStatus}");
                    }

                    return false;
                }
            }

            // Update existing device
            else
            {
                try
                {
                    await configurationHelper.RegistryManager.UpdateTwinAsync(twin.DeviceId, twin, twin.ETag);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error: {ex.Message}");
                    return false;
                }
            }

            return true;
        }

        public async Task<bool> QueryDevices(ConfigurationHelper configurationHelper, int page, int total)
        {
            var count = 0;
            IEnumerable<string> currentPage;

            Console.WriteLine($"Listing devices...");
            Console.WriteLine($"Page: {page}, Total: {total}\n");

            var query = configurationHelper.RegistryManager.CreateQuery("SELECT * FROM devices", page);

            while (query.HasMoreResults)
            {
                try
                {
                    currentPage = await query.GetNextAsJsonAsync(); // .GetNextAsTwinAsync();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error: {ex.Message}");
                    return false;
                }

                foreach (var jsonString in currentPage)
                {
                    JObject json = JObject.Parse(jsonString);

                    Console.WriteLine($"DevEUI: {(string)json["deviceId"]}");

                    Console.ForegroundColor = ConsoleColor.Yellow;
                    JObject desired = json.SelectToken("$.properties.desired") as JObject;
                    desired.Remove("$metadata");
                    desired.Remove("$version");
                    Console.WriteLine(desired);
                    Console.ResetColor();

                    Console.WriteLine();

                    if (count++ >= total - 1 && total >= 0)
                        return true;
                }

                Console.WriteLine("Press any key to continue.");
                Console.ReadKey();
            }

            return true;
        }
    }
}
