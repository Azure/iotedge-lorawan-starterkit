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
            Console.WriteLine();
            Console.WriteLine($"Querying device {devEui} in IoT Hub...");

            Twin twin;

            try
            {
                twin = await configurationHelper.RegistryManager.GetTwinAsync(devEui);
            }
            catch (Exception ex)
            {
                StatusConsole.WriteLine(MessageType.Error, ex.Message);
                return null;
            }

            return twin;
        }

        public bool VerifyDeviceTwin(string devEui, string netId, Twin twin, ConfigurationHelper configurationHelper)
        {
            bool isOtaa = false;
            bool isAbp = false;
            bool isValid = true;
            Console.WriteLine();

            devEui = ValidationHelper.CleanString(devEui);

            string appEui = twin.Properties.Desired.Contains(TwinProperty.AppEUI) ? ValidationHelper.GetTwinPropertyValue(twin.Properties.Desired[TwinProperty.AppEUI].Value) : null;
            string appKey = twin.Properties.Desired.Contains(TwinProperty.AppKey) ? ValidationHelper.GetTwinPropertyValue(twin.Properties.Desired[TwinProperty.AppKey].Value) : null;

            string nwkSKey = twin.Properties.Desired.Contains(TwinProperty.NwkSKey) ? ValidationHelper.GetTwinPropertyValue(twin.Properties.Desired[TwinProperty.NwkSKey].Value) : null;
            string appSKey = twin.Properties.Desired.Contains(TwinProperty.AppSKey) ? ValidationHelper.GetTwinPropertyValue(twin.Properties.Desired[TwinProperty.AppSKey].Value) : null;
            string devAddr = twin.Properties.Desired.Contains(TwinProperty.DevAddr) ? ValidationHelper.GetTwinPropertyValue(twin.Properties.Desired[TwinProperty.DevAddr].Value) : null;
            string abpRelaxMode = twin.Properties.Desired.Contains(TwinProperty.ABPRelaxMode) ? ValidationHelper.GetTwinPropertyValue(twin.Properties.Desired[TwinProperty.ABPRelaxMode].Value) : null;

            netId = ValidationHelper.CleanNetId(netId);

            string gatewayID = twin.Properties.Desired.Contains(TwinProperty.GatewayID) ? ValidationHelper.GetTwinPropertyValue(twin.Properties.Desired[TwinProperty.GatewayID].Value) : null;
            string sensorDecoder = twin.Properties.Desired.Contains(TwinProperty.SensorDecoder) ? ValidationHelper.GetTwinPropertyValue(twin.Properties.Desired[TwinProperty.SensorDecoder].Value) : null;
            string classType = twin.Properties.Desired.Contains(TwinProperty.ClassType) ? ValidationHelper.GetTwinPropertyValue(twin.Properties.Desired[TwinProperty.ClassType].Value) : null;
            string downlinkEnabled = twin.Properties.Desired.Contains(TwinProperty.DownlinkEnabled) ? ValidationHelper.GetTwinPropertyValue(twin.Properties.Desired[TwinProperty.DownlinkEnabled].Value) : null;
            string preferredWindow = twin.Properties.Desired.Contains(TwinProperty.PreferredWindow) ? ValidationHelper.GetTwinPropertyValue(twin.Properties.Desired[TwinProperty.PreferredWindow].Value) : null;
            string deduplication = twin.Properties.Desired.Contains(TwinProperty.Deduplication) ? ValidationHelper.GetTwinPropertyValue(twin.Properties.Desired[TwinProperty.Deduplication].Value) : null;
            string rx2DataRate = twin.Properties.Desired.Contains(TwinProperty.RX2DataRate) ? ValidationHelper.GetTwinPropertyValue(twin.Properties.Desired[TwinProperty.RX2DataRate].Value) : null;
            string rx1DrOffset = twin.Properties.Desired.Contains(TwinProperty.RX1DROffset) ? ValidationHelper.GetTwinPropertyValue(twin.Properties.Desired[TwinProperty.RX1DROffset].Value) : null;
            string supports32BitFCnt = twin.Properties.Desired.Contains(TwinProperty.Supports32BitFCnt) ? ValidationHelper.GetTwinPropertyValue(twin.Properties.Desired[TwinProperty.Supports32BitFCnt].Value) : null;
            string fCntUpStart = twin.Properties.Desired.Contains(TwinProperty.FCntUpStart) ? ValidationHelper.GetTwinPropertyValue(twin.Properties.Desired[TwinProperty.FCntUpStart].Value) : null;
            string fCntDownStart = twin.Properties.Desired.Contains(TwinProperty.FCntDownStart) ? ValidationHelper.GetTwinPropertyValue(twin.Properties.Desired[TwinProperty.FCntDownStart].Value) : null;
            string fCntResetCounter = twin.Properties.Desired.Contains(TwinProperty.FCntResetCounter) ? ValidationHelper.GetTwinPropertyValue(twin.Properties.Desired[TwinProperty.FCntResetCounter].Value) : null;

            string fCntUpStartReported = twin.Properties.Reported.Contains(TwinProperty.FCntUpStart) ? ValidationHelper.GetTwinPropertyValue(twin.Properties.Reported[TwinProperty.FCntUpStart].Value) : null;
            string fCntDownStartReported = twin.Properties.Reported.Contains(TwinProperty.FCntDownStart) ? ValidationHelper.GetTwinPropertyValue(twin.Properties.Reported[TwinProperty.FCntDownStart].Value) : null;
            string fCntResetCounterReported = twin.Properties.Reported.Contains(TwinProperty.FCntResetCounter) ? ValidationHelper.GetTwinPropertyValue(twin.Properties.Reported[TwinProperty.FCntResetCounter].Value) : null;

            if (!string.IsNullOrEmpty(appEui) || !string.IsNullOrEmpty(appKey))
                isOtaa = true;

            if (!string.IsNullOrEmpty(nwkSKey) || !string.IsNullOrEmpty(appSKey) || !string.IsNullOrEmpty(devAddr))
                isAbp = true;

            // ABP device
            if (isAbp && !isOtaa)
            {
                Console.WriteLine("ABP device configuration detected.");

                isValid = this.ValidateDevice(
                    new Program.AddOptions()
                    {
                        Type = "ABP",
                        DevEui = devEui,
                        AppEui = appEui,
                        AppKey = appKey,
                        NetId = netId,
                        ABPRelaxMode = abpRelaxMode,
                        AppSKey = appSKey,
                        NwkSKey = nwkSKey,
                        DevAddr = devAddr,
                        GatewayId = gatewayID,
                        SensorDecoder = sensorDecoder,
                        ClassType = classType,
                        DownlinkEnabled = downlinkEnabled,
                        PreferredWindow = preferredWindow,
                        Deduplication = deduplication,
                        Rx2DataRate = rx2DataRate,
                        Rx1DrOffset = rx1DrOffset,
                        Supports32BitFCnt = supports32BitFCnt,
                        FCntUpStart = fCntUpStart,
                        FCntDownStart = fCntDownStart,
                        FCntResetCounter = fCntResetCounter
                    },
                    fCntUpStartReported,
                    fCntDownStartReported,
                    fCntResetCounterReported,
                    configurationHelper);
            }

            // OTAA device
            else if (isOtaa && !isAbp)
            {
                Console.WriteLine("OTAA device configuration detected.");
                isValid = this.ValidateDevice(
                    new Program.AddOptions()
                    {
                        Type = "OTAA",
                        DevEui = devEui,
                        AppEui = appEui,
                        AppKey = appKey,
                        NetId = netId,
                        ABPRelaxMode = abpRelaxMode,
                        AppSKey = appSKey,
                        NwkSKey = nwkSKey,
                        DevAddr = devAddr,
                        GatewayId = gatewayID,
                        SensorDecoder = sensorDecoder,
                        ClassType = classType,
                        DownlinkEnabled = downlinkEnabled,
                        PreferredWindow = preferredWindow,
                        Deduplication = deduplication,
                        Rx2DataRate = rx2DataRate,
                        Rx1DrOffset = rx1DrOffset,
                        Supports32BitFCnt = supports32BitFCnt,
                        FCntUpStart = fCntUpStart,
                        FCntDownStart = fCntDownStart,
                        FCntResetCounter = fCntResetCounter
                    },
                    fCntUpStartReported,
                    fCntDownStartReported,
                    fCntResetCounterReported,
                    configurationHelper);
            }

            // Unknown device type
            else
            {
                StatusConsole.WriteLine(MessageType.Error, "Can't determine if ABP or OTAA device.");
                Console.WriteLine("ABP devices should contain NwkSKey, AppSKey and DevAddr, not AppEUI and AppKey.");
                Console.WriteLine("OTAA devices should contain AppEUI and AppKey, not NwkSKey, AppSKey and DevAddr.");
                isValid = false;
            }

            if (isValid)
            {
                Console.WriteLine();
                Console.Write($"The configuration for device {devEui} ");
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("is valid.");
                Console.ResetColor();
            }
            else
            {
                Console.WriteLine();
                Console.Write($"Error: The configuration for device {devEui} ");
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("is NOT valid.");
                Console.ResetColor();
            }

            return isValid;
        }

        public object CleanOptions(object optsObject, bool isNewDevice)
        {
            dynamic opts;

            if (isNewDevice)
                opts = optsObject as Program.AddOptions;
            else
                opts = optsObject as Program.UpdateOptions;

            if (!string.IsNullOrEmpty(opts.DevEui))
                opts.DevEui = ValidationHelper.CleanString(opts.DevEui);

            // ABP device specific properties
            if (!string.IsNullOrEmpty(opts.NwkSKey))
                opts.NwkSKey = ValidationHelper.CleanString(opts.NwkSKey);

            if (!string.IsNullOrEmpty(opts.AppSKey))
                opts.AppSKey = ValidationHelper.CleanString(opts.AppSKey);

            if (!string.IsNullOrEmpty(opts.DevAddr))
                opts.DevAddr = ValidationHelper.CleanString(opts.DevAddr);

            if (!string.IsNullOrEmpty(opts.NetId))
                opts.NetId = ValidationHelper.CleanNetId(opts.NetId);

            if (!string.IsNullOrEmpty(opts.ABPRelaxMode))
                opts.ABPRelaxMode = ValidationHelper.CleanString(opts.ABPRelaxMode);

            // OTAA device specific properties
            if (!string.IsNullOrEmpty(opts.AppEui))
                opts.AppEui = ValidationHelper.CleanString(opts.AppEui);

            if (!string.IsNullOrEmpty(opts.AppKey))
                opts.AppKey = ValidationHelper.CleanString(opts.AppKey);

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

            if (!string.IsNullOrEmpty(opts.FCntUpStart))
                opts.FCntUpStart = ValidationHelper.CleanString(opts.FCntUpStart);

            if (!string.IsNullOrEmpty(opts.FCntDownStart))
                opts.FCntDownStart = ValidationHelper.CleanString(opts.FCntDownStart);

            if (!string.IsNullOrEmpty(opts.FCntResetCounter))
                opts.FCntResetCounter = ValidationHelper.CleanString(opts.FCntResetCounter);

            return opts as object;
        }

        public Program.AddOptions CompleteMissingAddOptions(Program.AddOptions opts, ConfigurationHelper configurationHelper)
        {
            Console.WriteLine();
            Console.WriteLine($"Completing missing options for device...");

            if (string.IsNullOrEmpty(opts.DevEui))
            {
                opts.DevEui = Keygen.Generate(8);
                StatusConsole.WriteLine(MessageType.Info, $"Generating missing DevEUI: {opts.DevEui}");
            }

            // ABP device specific properties
            if (string.Equals(opts.Type, "ABP", StringComparison.InvariantCultureIgnoreCase))
            {
                if (string.IsNullOrEmpty(opts.NwkSKey))
                {
                    opts.NwkSKey = Keygen.Generate(16);
                    StatusConsole.WriteLine(MessageType.Info, $"Generating missing NwkSKey: {opts.NwkSKey}");
                }

                if (string.IsNullOrEmpty(opts.AppSKey))
                {
                    opts.AppSKey = Keygen.Generate(16);
                    StatusConsole.WriteLine(MessageType.Info, $"Generating missing AppSKey: {opts.AppSKey}");
                }

                if (string.IsNullOrEmpty(opts.DevAddr))
                {
                    opts.DevAddr = Keygen.Generate(4);
                    StatusConsole.WriteLine(MessageType.Info, $"Generating missing DevAddr: {opts.DevAddr}");
                }

                if (ValidationHelper.ValidateHexStringTwinProperty(opts.DevAddr, 4, out string _))
                {
                    var newDevAddr = NetIdHelper.SetNwkIdPart(opts.DevAddr, opts.NetId, configurationHelper);
                    if (!string.Equals(newDevAddr, opts.DevAddr, StringComparison.InvariantCultureIgnoreCase))
                    {
                        opts.DevAddr = newDevAddr;
                        StatusConsole.WriteLine(MessageType.Info, $"Adapting DevAddr: {opts.DevAddr} based on NetId {(string.IsNullOrEmpty(opts.NetId) ? configurationHelper.NetId : opts.NetId)}");
                    }
                }
            }

            // OTAA device specific properties
            else
            {
                if (string.IsNullOrEmpty(opts.AppEui))
                {
                    opts.AppEui = Keygen.Generate(16);
                    StatusConsole.WriteLine(MessageType.Info, $"Generating missing AppEUI: {opts.AppEui}");
                }

                if (string.IsNullOrEmpty(opts.AppKey))
                {
                    opts.AppKey = Keygen.Generate(16);
                    StatusConsole.WriteLine(MessageType.Info, $"Generating missing AppKey: {opts.AppKey}");
                }
            }

            // Shared, non optional device properties
            if (opts.GatewayId == null)
            {
                opts.GatewayId = string.Empty;
                StatusConsole.WriteLine(MessageType.Info, $"GatewayId is missing. Adding empty property.");
            }

            if (opts.SensorDecoder == null)
            {
                opts.SensorDecoder = string.Empty;
                StatusConsole.WriteLine(MessageType.Info, $"SensorDecoder is missing. Adding empty property.");
            }

            return opts;
        }

        public Program.UpdateOptions CompleteMissingUpdateOptions(Program.UpdateOptions opts, ConfigurationHelper configurationHelper)
        {
            Console.WriteLine();
            Console.WriteLine($"Completing missing options for device...");

            // ABP device specific properties
            if (!string.IsNullOrEmpty(opts.DevAddr) && ValidationHelper.ValidateHexStringTwinProperty(opts.DevAddr, 4, out string _))
            {
                var newDevAddr = NetIdHelper.SetNwkIdPart(opts.DevAddr, opts.NetId, configurationHelper);
                if (!string.Equals(newDevAddr, opts.DevAddr, StringComparison.InvariantCultureIgnoreCase))
                {
                    opts.DevAddr = newDevAddr;
                    StatusConsole.WriteLine(MessageType.Info, $"Adapting DevAddr: {opts.DevAddr} based on NetId {(string.IsNullOrEmpty(opts.NetId) ? configurationHelper.NetId : opts.NetId)}.");
                }
            }

            // Shared, non optional device properties
            if (string.Equals("null", opts.GatewayId, StringComparison.InvariantCultureIgnoreCase))
            {
                opts.GatewayId = string.Empty;
                StatusConsole.WriteLine(MessageType.Info, $"GatewayId is set to \"null\". Adding empty property.");
            }

            if (string.Equals("null", opts.SensorDecoder, StringComparison.InvariantCultureIgnoreCase))
            {
                opts.SensorDecoder = string.Empty;
                StatusConsole.WriteLine(MessageType.Info, $"SensorDecoder is set to \"null\". Adding empty property.");
            }

            return opts;
        }

        public bool ValidateDevice(Program.AddOptions opts, string fCntUpStartReported, string fCntDownStartReported, string fCntResetCounterReported, ConfigurationHelper configurationHelper)
        {
            string validationError = string.Empty;
            bool isValid = true;

            Console.WriteLine();
            Console.WriteLine($"Verifying device {opts.DevEui} twin data...");

            // ******************************************************************************************
            // DevEui
            // ******************************************************************************************
            if (string.IsNullOrEmpty(opts.DevEui))
            {
                StatusConsole.WriteLine(MessageType.Error, "DevEui is missing.");
                isValid = false;
            }
            else if (!ValidationHelper.ValidateHexStringTwinProperty(opts.DevEui, 8, out validationError))
            {
                StatusConsole.WriteLine(MessageType.Error, $"DevEui {opts.DevEui} is invalid: {validationError}.");
                isValid = false;
            }
            else
            {
                StatusConsole.WriteLine(MessageType.Info, $"DevEui {opts.DevEui} is valid.");
            }

            // ******************************************************************************************
            // ABP device specific properties
            // ******************************************************************************************
            if (string.Equals(opts.Type, "ABP", StringComparison.InvariantCultureIgnoreCase))
            {
                // NwkSKey
                if (string.IsNullOrEmpty(opts.NwkSKey))
                {
                    StatusConsole.WriteLine(MessageType.Error, "NwkSKey is missing.");
                    isValid = false;
                }
                else if (!ValidationHelper.ValidateHexStringTwinProperty(opts.NwkSKey, 16, out validationError))
                {
                    StatusConsole.WriteLine(MessageType.Error, $"NwkSKey {opts.NwkSKey} is invalid: {validationError}.");
                    isValid = false;
                }
                else
                {
                    StatusConsole.WriteLine(MessageType.Info, $"NwkSKey {opts.NwkSKey} is valid.");
                }

                // AppSKey
                if (string.IsNullOrEmpty(opts.AppSKey))
                {
                    StatusConsole.WriteLine(MessageType.Error, "AppSKey is missing.");
                    isValid = false;
                }
                else if (!ValidationHelper.ValidateHexStringTwinProperty(opts.AppSKey, 16, out validationError))
                {
                    StatusConsole.WriteLine(MessageType.Error, $"AppSKey {opts.AppSKey} is invalid: {validationError}.");
                    isValid = false;
                }
                else
                {
                    StatusConsole.WriteLine(MessageType.Info, $"AppSKey {opts.AppSKey} is valid.");
                }

                // NetId
                if (!string.IsNullOrEmpty(opts.NetId))
                {
                    if (!ValidationHelper.ValidateHexStringTwinProperty(opts.NetId, 3, out validationError))
                    {
                        StatusConsole.WriteLine(MessageType.Error, $"NetId {opts.NetId} is invalid: {validationError}.");
                        isValid = false;
                    }
                    else
                    {
                        StatusConsole.WriteLine(MessageType.Info, $"NetId {opts.NetId} is valid.");
                    }
                }

                // DevAddr
                if (string.IsNullOrEmpty(opts.DevAddr))
                {
                    StatusConsole.WriteLine(MessageType.Error, "DevAddr is missing.");
                    isValid = false;
                }
                else if (!ValidationHelper.ValidateHexStringTwinProperty(opts.DevAddr, 4, out validationError))
                {
                    StatusConsole.WriteLine(MessageType.Error, $"DevAddr {opts.DevAddr} is invalid: {validationError}.");
                    isValid = false;
                }
                else
                {
                    var devAddrCorrect = NetIdHelper.SetNwkIdPart(opts.DevAddr, opts.NetId, configurationHelper);

                    if (string.Equals(devAddrCorrect, opts.DevAddr))
                    {
                        StatusConsole.WriteLine(MessageType.Info, $"DevAddr {opts.DevAddr} is valid based on NetId {(string.IsNullOrEmpty(opts.NetId) ? configurationHelper.NetId : opts.NetId)}.");
                    }
                    else
                    {
                        StatusConsole.WriteLine(MessageType.Error, $"DevAddr {opts.DevAddr} is invalid based on NetId {(string.IsNullOrEmpty(opts.NetId) ? configurationHelper.NetId : opts.NetId)}");
                        StatusConsole.WriteLine(MessageType.Warning, $"To stop seeing this error, provid the --netid parameter or set the NetId in the settings file.");
                        isValid = false;
                    }
                }

                // AbpRelaxMode
                if (!string.IsNullOrEmpty(opts.ABPRelaxMode))
                {
                    if (!ValidationHelper.ValidateBoolTwinProperty(opts.ABPRelaxMode, out validationError))
                    {
                        StatusConsole.WriteLine(MessageType.Error, $"ABPRelaxMode {opts.ABPRelaxMode} is invalid: {validationError}.");
                        isValid = false;
                    }
                    else
                    {
                        StatusConsole.WriteLine(MessageType.Info, $"ABPRelaxMode {opts.ABPRelaxMode} is valid.");
                    }
                }

                // FCntUpStart
                if (!string.IsNullOrEmpty(opts.FCntUpStart))
                {
                    if (!ValidationHelper.ValidateUIntTwinProperty(opts.FCntUpStart, 0, uint.MaxValue, out validationError))
                    {
                        StatusConsole.WriteLine(MessageType.Error, $"FCntUpStart {opts.FCntUpStart} is invalid: {validationError}.");
                        isValid = false;
                    }
                    else
                    {
                        StatusConsole.WriteLine(MessageType.Info, $"FCntUpStart {opts.FCntUpStart} is valid.");
                    }
                }

                // FCntDownStart
                if (!string.IsNullOrEmpty(opts.FCntDownStart))
                {
                    if (!ValidationHelper.ValidateUIntTwinProperty(opts.FCntDownStart, 0, uint.MaxValue, out validationError))
                    {
                        StatusConsole.WriteLine(MessageType.Error, $"FCntDownStart {opts.FCntDownStart} is invalid: {validationError}.");
                        isValid = false;
                    }
                    else
                    {
                        StatusConsole.WriteLine(MessageType.Info, $"FCntDownStart {opts.FCntDownStart} is valid.");
                    }
                }

                // FCntResetCounter
                if (!string.IsNullOrEmpty(opts.FCntResetCounter))
                {
                    if (!ValidationHelper.ValidateUIntTwinProperty(opts.FCntResetCounter, 0, uint.MaxValue, out validationError))
                    {
                        StatusConsole.WriteLine(MessageType.Error, $"FCntResetCounter {opts.FCntResetCounter} is invalid: {validationError}.");
                        isValid = false;
                    }
                    else
                    {
                        StatusConsole.WriteLine(MessageType.Info, $"FCntResetCounter {opts.FCntResetCounter} is valid.");
                    }
                }

                // Frame Counter Settings
                if (!ValidationHelper.ValidateFcntSettings(opts, fCntUpStartReported, fCntDownStartReported, fCntResetCounterReported))
                {
                    isValid = false;
                }

                // ******************************************************************************************
                // Invalid properties for ABP devices
                // ******************************************************************************************

                // AppEui
                if (!string.IsNullOrEmpty(opts.AppEui))
                {
                    StatusConsole.WriteLine(MessageType.Error, $"AppEUI {opts.AppEui} is invalid for ABP devices.");
                    isValid = false;
                }

                // AppKey
                if (!string.IsNullOrEmpty(opts.AppKey))
                {
                    StatusConsole.WriteLine(MessageType.Error, $"AppKey {opts.AppKey} is invalid for ABP devices.");
                    isValid = false;
                }
            }

            // ******************************************************************************************
            // OTAA device specific properties
            // ******************************************************************************************
            else
            {
                // AppEui
                if (string.IsNullOrEmpty(opts.AppEui))
                {
                    Console.WriteLine("Error: AppEUI is missing.");
                    isValid = false;
                }
                else if (!ValidationHelper.ValidateHexStringTwinProperty(opts.AppEui, 16, out validationError))
                {
                    StatusConsole.WriteLine(MessageType.Error, $"AppEUI {opts.AppEui} is invalid: {validationError}.");
                    isValid = false;
                }
                else
                {
                    StatusConsole.WriteLine(MessageType.Info, $"AppEui {opts.AppEui} is valid.");
                }

                // AppKey
                if (string.IsNullOrEmpty(opts.AppKey))
                {
                    Console.WriteLine("Error: AppKey is missing.");
                    isValid = false;
                }
                else if (!ValidationHelper.ValidateHexStringTwinProperty(opts.AppKey, 16, out validationError))
                {
                    StatusConsole.WriteLine(MessageType.Error, $"AppKey {opts.AppKey} is invalid: {validationError}.");
                    isValid = false;
                }
                else
                {
                    StatusConsole.WriteLine(MessageType.Info, $"AppKey {opts.AppKey} is valid.");
                }

                // ******************************************************************************************
                // Invalid properties for OTAA devices
                // ******************************************************************************************

                // NwkSKey
                if (!string.IsNullOrEmpty(opts.NwkSKey))
                {
                    StatusConsole.WriteLine(MessageType.Error, $"NwkSKey {opts.NwkSKey} is invalid for OTAA devices.");
                    isValid = false;
                }

                // AppSKey
                if (!string.IsNullOrEmpty(opts.AppSKey))
                {
                    StatusConsole.WriteLine(MessageType.Error, $"AppSKey {opts.AppSKey} is invalid for OTAA devices.");
                    isValid = false;
                }

                // DevAddr
                if (!string.IsNullOrEmpty(opts.DevAddr))
                {
                    StatusConsole.WriteLine(MessageType.Error, $"DevAddr {opts.DevAddr} is invalid for OTAA devices.");
                    isValid = false;
                }

                // NetId
                if (!string.IsNullOrEmpty(opts.NetId))
                {
                    StatusConsole.WriteLine(MessageType.Warning, $"NetId {opts.NetId} is not used in OTAA devices.");
                }

                // ABPRelaxMode
                if (!string.IsNullOrEmpty(opts.ABPRelaxMode))
                {
                    StatusConsole.WriteLine(MessageType.Warning, $"ABPRelaxMode {opts.ABPRelaxMode} is invalid/ignored for OTAA devices.");
                }

                // FCntUpStart
                if (!string.IsNullOrEmpty(opts.FCntUpStart))
                {
                    StatusConsole.WriteLine(MessageType.Warning, $"FCntUpStart {opts.FCntUpStart} is invalid/ignored for OTAA devices.");
                }

                // FCntDownStart
                if (!string.IsNullOrEmpty(opts.FCntDownStart))
                {
                    StatusConsole.WriteLine(MessageType.Warning, $"FCntDownStart {opts.FCntDownStart} is invalid/ignored for OTAA devices.");
                }

                // FCntResetCounter
                if (!string.IsNullOrEmpty(opts.FCntResetCounter))
                {
                    StatusConsole.WriteLine(MessageType.Warning, $"FCntResetCounter {opts.FCntResetCounter} is invalid/ignored for OTAA devices.");
                }
            }

            // ******************************************************************************************
            // // Shared, non optional device properties
            // ******************************************************************************************

            // SensorDecoder
            if (!ValidationHelper.ValidateSensorDecoder(opts.SensorDecoder))
            {
                isValid = false;
            }

            // GatewayId
            if (opts.GatewayId == null)
            {
                StatusConsole.WriteLine(MessageType.Error, $"GatewayId is missing.");
                isValid = false;
            }
            else if (opts.GatewayId == string.Empty)
            {
                StatusConsole.WriteLine(MessageType.Info, $"GatewayId is empty. This is valid.");
            }
            else
            {
                StatusConsole.WriteLine(MessageType.Info, $"GatewayId {opts.GatewayId} is valid.");
            }

            // ******************************************************************************************
            // // Shared, optional device properties
            // ******************************************************************************************

            // ClassType
            if (!string.IsNullOrEmpty(opts.ClassType))
            {
                if (!(string.Equals(opts.ClassType, "A", StringComparison.InvariantCultureIgnoreCase) ||
                    string.Equals(opts.ClassType, "C", StringComparison.InvariantCultureIgnoreCase)))
                {
                    StatusConsole.WriteLine(MessageType.Error, $"ClassType {opts.ClassType} is invalid: If set, it needs to be \"A\" or \"C\".");
                    isValid = false;
                }
                else
                {
                    StatusConsole.WriteLine(MessageType.Info, $"ClassType {opts.ClassType} is valid.");
                }
            }

            // DownlinkEnabled
            if (!string.IsNullOrEmpty(opts.DownlinkEnabled))
            {
                if (!ValidationHelper.ValidateBoolTwinProperty(opts.DownlinkEnabled, out validationError))
                {
                    StatusConsole.WriteLine(MessageType.Error, $"DownlinkEnabled {opts.DownlinkEnabled} is invalid: {validationError}.");
                    isValid = false;
                }
                else
                {
                    StatusConsole.WriteLine(MessageType.Info, $"DownlinkEnabled {opts.DownlinkEnabled} is valid.");
                }
            }

            // PreferredWindow
            if (!string.IsNullOrEmpty(opts.PreferredWindow))
            {
                if (!ValidationHelper.ValidateUIntTwinProperty(opts.PreferredWindow, 1, 2, out validationError))
                {
                    StatusConsole.WriteLine(MessageType.Error, $"PreferredWindow {opts.PreferredWindow} is invalid: {validationError}");
                    isValid = false;
                }
                else
                {
                    StatusConsole.WriteLine(MessageType.Info, $"PreferredWindow {opts.PreferredWindow} is valid.");
                }
            }

            // Deduplication
            if (!string.IsNullOrEmpty(opts.Deduplication))
            {
                if (!(string.Equals(opts.Deduplication, "None", StringComparison.InvariantCultureIgnoreCase) ||
                    string.Equals(opts.Deduplication, "Drop", StringComparison.InvariantCultureIgnoreCase) ||
                    string.Equals(opts.Deduplication, "Mark", StringComparison.InvariantCultureIgnoreCase)))
                {
                    StatusConsole.WriteLine(MessageType.Error, $"Deduplication {opts.Deduplication} is invalid: If set, it needs to be \"None\", \"Drop\" or \"Mark\".");
                    isValid = false;
                }
                else
                {
                    StatusConsole.WriteLine(MessageType.Info, $"Deduplication {opts.Deduplication} is valid.");
                }
            }

            // Rx2DataRate
            if (!string.IsNullOrEmpty(opts.Rx2DataRate))
            {
                if (!ValidationHelper.ValidateDataRateTwinProperty(opts.Rx2DataRate, out validationError))
                {
                    StatusConsole.WriteLine(MessageType.Error, $"Rx2DataRate {opts.Rx2DataRate} is invalid: {validationError}.");
                    isValid = false;
                }
                else
                {
                    StatusConsole.WriteLine(MessageType.Info, $"Rx2DataRate {opts.Rx2DataRate} is valid.");
                }
            }

            // Rx1DrOffset
            if (!string.IsNullOrEmpty(opts.Rx1DrOffset))
            {
                if (!ValidationHelper.ValidateUIntTwinProperty(opts.Rx1DrOffset, 0, 15, out validationError))
                {
                    StatusConsole.WriteLine(MessageType.Error, $"Rx1DrOffset {opts.Rx1DrOffset} is invalid: {validationError}.");
                    isValid = false;
                }
                else
                {
                    StatusConsole.WriteLine(MessageType.Info, $"Rx1DrOffset {opts.Rx1DrOffset} is valid.");
                }
            }

            // Supports32BitFCnt
            if (!string.IsNullOrEmpty(opts.Supports32BitFCnt))
            {
                if (!ValidationHelper.ValidateBoolTwinProperty(opts.Supports32BitFCnt, out validationError))
                {
                    StatusConsole.WriteLine(MessageType.Error, $"Supports32BitFCnt {opts.Supports32BitFCnt} is invalid: {validationError}.");
                    isValid = false;
                }
                else
                {
                    StatusConsole.WriteLine(MessageType.Info, $"Supports32BitFCnt {opts.Supports32BitFCnt} is valid.");
                }
            }

            return isValid;
        }

        public Twin CreateDeviceTwin(Program.AddOptions opts)
        {
            var twinProperties = new TwinProperties();
            Console.WriteLine();

            // ******************************************************************************************
            // DevEui NOT located in twin
            // ******************************************************************************************

            // ABP device specific properties
            if (string.Equals(opts.Type, "ABP", StringComparison.InvariantCultureIgnoreCase))
            {
                Console.WriteLine($"Creating ABP device twin: {opts.DevEui}...");

                twinProperties.Desired[TwinProperty.AppSKey] = ValidationHelper.SetStringTwinProperty(opts.AppSKey);
                twinProperties.Desired[TwinProperty.NwkSKey] = ValidationHelper.SetStringTwinProperty(opts.NwkSKey);
                twinProperties.Desired[TwinProperty.DevAddr] = ValidationHelper.SetStringTwinProperty(opts.DevAddr);

                if (!string.IsNullOrEmpty(opts.ABPRelaxMode))
                    twinProperties.Desired[TwinProperty.ABPRelaxMode] = ValidationHelper.SetBoolTwinProperty(opts.ABPRelaxMode);

                if (!string.IsNullOrEmpty(opts.FCntUpStart))
                    twinProperties.Desired[TwinProperty.FCntUpStart] = ValidationHelper.SetUIntTwinProperty(opts.FCntUpStart);

                if (!string.IsNullOrEmpty(opts.FCntDownStart))
                    twinProperties.Desired[TwinProperty.FCntDownStart] = ValidationHelper.SetUIntTwinProperty(opts.FCntDownStart);

                if (!string.IsNullOrEmpty(opts.FCntResetCounter))
                    twinProperties.Desired[TwinProperty.FCntResetCounter] = ValidationHelper.SetUIntTwinProperty(opts.FCntResetCounter);
            }

            // OTAA device specific properties
            else
            {
                Console.WriteLine($"Creating OTAA device twin: {opts.DevEui}...");

                twinProperties.Desired[TwinProperty.AppEUI] = ValidationHelper.SetStringTwinProperty(opts.AppEui);
                twinProperties.Desired[TwinProperty.AppKey] = ValidationHelper.SetStringTwinProperty(opts.AppKey);
            }

            // Shared, non optional properties
            twinProperties.Desired[TwinProperty.GatewayID] = ValidationHelper.SetStringTwinProperty(opts.GatewayId);
            twinProperties.Desired[TwinProperty.SensorDecoder] = ValidationHelper.SetStringTwinProperty(opts.SensorDecoder);

            // Shared, optional properties
            if (!string.IsNullOrEmpty(opts.ClassType))
                twinProperties.Desired[TwinProperty.ClassType] = ValidationHelper.SetStringTwinProperty(opts.ClassType);

            if (!string.IsNullOrEmpty(opts.DownlinkEnabled))
                twinProperties.Desired[TwinProperty.DownlinkEnabled] = ValidationHelper.SetBoolTwinProperty(opts.DownlinkEnabled);

            if (!string.IsNullOrEmpty(opts.PreferredWindow))
                twinProperties.Desired[TwinProperty.PreferredWindow] = ValidationHelper.SetUIntTwinProperty(opts.PreferredWindow);

            if (!string.IsNullOrEmpty(opts.Deduplication))
                twinProperties.Desired[TwinProperty.Deduplication] = ValidationHelper.SetStringTwinProperty(opts.Deduplication);

            if (!string.IsNullOrEmpty(opts.Rx2DataRate))
                twinProperties.Desired[TwinProperty.RX2DataRate] = ValidationHelper.SetStringTwinProperty(opts.Rx2DataRate);

            if (!string.IsNullOrEmpty(opts.Rx1DrOffset))
                twinProperties.Desired[TwinProperty.RX1DROffset] = ValidationHelper.SetUIntTwinProperty(opts.Rx1DrOffset);

            if (!string.IsNullOrEmpty(opts.Supports32BitFCnt))
                twinProperties.Desired[TwinProperty.Supports32BitFCnt] = ValidationHelper.SetBoolTwinProperty(opts.Supports32BitFCnt);

            var twin = new Twin();
            twin.Properties = twinProperties;

            return twin;
        }

        public Twin UpdateDeviceTwin(Twin twin, Program.UpdateOptions opts)
        {
            Console.WriteLine();
            Console.WriteLine($"Applying changes to device {opts.DevEui} twin...");

            // ******************************************************************************************
            // DevEui can NOT be updated!
            // ******************************************************************************************

            // ABP device properties
            if (!string.IsNullOrEmpty(opts.AppSKey))
                twin.Properties.Desired[TwinProperty.AppSKey] = ValidationHelper.SetStringTwinProperty(opts.AppSKey);

            if (!string.IsNullOrEmpty(opts.NwkSKey))
                twin.Properties.Desired[TwinProperty.NwkSKey] = ValidationHelper.SetStringTwinProperty(opts.NwkSKey);

            if (!string.IsNullOrEmpty(opts.DevAddr))
                twin.Properties.Desired[TwinProperty.DevAddr] = ValidationHelper.SetStringTwinProperty(opts.DevAddr);

            if (!string.IsNullOrEmpty(opts.ABPRelaxMode))
                twin.Properties.Desired[TwinProperty.ABPRelaxMode] = ValidationHelper.SetBoolTwinProperty(opts.ABPRelaxMode);

            if (!string.IsNullOrEmpty(opts.FCntUpStart))
                twin.Properties.Desired[TwinProperty.FCntUpStart] = ValidationHelper.SetUIntTwinProperty(opts.FCntUpStart);

            if (!string.IsNullOrEmpty(opts.FCntDownStart))
                twin.Properties.Desired[TwinProperty.FCntDownStart] = ValidationHelper.SetUIntTwinProperty(opts.FCntDownStart);

            if (!string.IsNullOrEmpty(opts.FCntResetCounter))
                twin.Properties.Desired[TwinProperty.FCntResetCounter] = ValidationHelper.SetUIntTwinProperty(opts.FCntResetCounter);

            // OTAA device properties
            if (!string.IsNullOrEmpty(opts.AppEui))
                twin.Properties.Desired[TwinProperty.AppEUI] = ValidationHelper.SetStringTwinProperty(opts.AppEui);

            if (!string.IsNullOrEmpty(opts.AppKey))
                twin.Properties.Desired[TwinProperty.AppKey] = ValidationHelper.SetStringTwinProperty(opts.AppKey);

            // Shared, non optional properties
            if (opts.GatewayId != null)
                twin.Properties.Desired[TwinProperty.GatewayID] = ValidationHelper.SetStringTwinProperty(opts.GatewayId);

            if (opts.SensorDecoder != null)
                twin.Properties.Desired[TwinProperty.SensorDecoder] = ValidationHelper.SetStringTwinProperty(opts.SensorDecoder);

            // Shared, optional properties
            if (!string.IsNullOrEmpty(opts.ClassType))
                twin.Properties.Desired[TwinProperty.ClassType] = ValidationHelper.SetStringTwinProperty(opts.ClassType);

            if (!string.IsNullOrEmpty(opts.DownlinkEnabled))
                twin.Properties.Desired[TwinProperty.DownlinkEnabled] = ValidationHelper.SetBoolTwinProperty(opts.DownlinkEnabled);

            if (!string.IsNullOrEmpty(opts.PreferredWindow))
                twin.Properties.Desired[TwinProperty.PreferredWindow] = ValidationHelper.SetUIntTwinProperty(opts.PreferredWindow);

            if (!string.IsNullOrEmpty(opts.Deduplication))
                twin.Properties.Desired[TwinProperty.Deduplication] = ValidationHelper.SetStringTwinProperty(opts.Deduplication);

            if (!string.IsNullOrEmpty(opts.Rx2DataRate))
                twin.Properties.Desired[TwinProperty.RX2DataRate] = ValidationHelper.SetStringTwinProperty(opts.Rx2DataRate);

            if (!string.IsNullOrEmpty(opts.Rx1DrOffset))
                twin.Properties.Desired[TwinProperty.RX1DROffset] = ValidationHelper.SetUIntTwinProperty(opts.Rx1DrOffset);

            if (!string.IsNullOrEmpty(opts.Supports32BitFCnt))
                twin.Properties.Desired[TwinProperty.Supports32BitFCnt] = ValidationHelper.SetBoolTwinProperty(opts.Supports32BitFCnt);

            return twin;
        }

        public async Task<bool> WriteDeviceTwin(Twin twin, string devEui, ConfigurationHelper configurationHelper, bool isNewDevice)
        {
            var device = new Device(devEui);
            BulkRegistryOperationResult result;

            Console.WriteLine();
            Console.WriteLine($"Writing device {devEui} twin to IoT Hub...");

            // Add new device
            if (isNewDevice)
            {
                try
                {
                    result = await configurationHelper.RegistryManager.AddDeviceWithTwinAsync(device, twin);
                }
                catch (Exception ex)
                {
                    StatusConsole.WriteLine(MessageType.Error, ex.Message);
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
                    StatusConsole.WriteLine(MessageType.Error, ex.Message);
                    return false;
                }
            }

            return true;
        }

        public async Task<bool> QueryDevices(ConfigurationHelper configurationHelper, int page, int total)
        {
            var count = 0;
            IEnumerable<string> currentPage;
            string totalString = (total == -1) ? "all" : total.ToString();

            Console.WriteLine();
            Console.WriteLine($"Listing devices...");
            Console.WriteLine($"Page: {page}, Total: {totalString}");
            Console.WriteLine();

            var query = configurationHelper.RegistryManager.CreateQuery("SELECT * FROM devices", page);

            while (query.HasMoreResults)
            {
                try
                {
                    currentPage = await query.GetNextAsJsonAsync(); // .GetNextAsTwinAsync();
                }
                catch (Exception ex)
                {
                    StatusConsole.WriteLine(MessageType.Error, ex.Message);
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

        public async Task<bool> RemoveDevice(string devEui, ConfigurationHelper configurationHelper)
        {
            Device device;

            Console.WriteLine();
            Console.WriteLine($"Finding existing device {devEui} on IoT Hub...");

            try
            {
                device = await configurationHelper.RegistryManager.GetDeviceAsync(devEui);
            }
            catch (Exception ex)
            {
                StatusConsole.WriteLine(MessageType.Error, ex.Message);
                return false;
            }

            if (device != null)
            {
                Console.WriteLine($"Removing device {devEui} from IoT Hub...");

                try
                {
                    await configurationHelper.RegistryManager.RemoveDeviceAsync(device);
                }
                catch (Exception ex)
                {
                    StatusConsole.WriteLine(MessageType.Error, ex.Message);
                    return false;
                }
            }
            else
            {
                Console.WriteLine($"Device {devEui} not found in IoT Hub. Aborting.");
                return false;
            }

            Console.WriteLine($"Success!");
            return true;
        }
    }
}
