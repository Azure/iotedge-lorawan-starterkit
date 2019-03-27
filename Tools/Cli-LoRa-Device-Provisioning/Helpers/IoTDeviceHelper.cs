// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tools.CLI.Helpers
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using LoRaWan.Tools.CLI.Options;
    using Microsoft.Azure.Devices;
    using Microsoft.Azure.Devices.Shared;
    using Newtonsoft.Json.Linq;

    public class IoTDeviceHelper
    {
        private string[] classTypes = { "A", "C" };
        private string[] deduplicationModes = { "None", "Drop", "Mark" };

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

            Console.WriteLine("done.");
            return twin;
        }

        public bool VerifyDeviceTwin(string devEui, string netId, Twin twin, ConfigurationHelper configurationHelper, bool isVerbose)
        {
            bool isOtaa = false;
            bool isAbp = false;
            bool isValid = true;
            Console.WriteLine();
            Console.WriteLine($"Analyzing device {devEui}...");

            devEui = ValidationHelper.CleanString(devEui);

            string appEui = this.ReadTwin(twin.Properties.Desired, TwinProperty.AppEUI);
            string appKey = this.ReadTwin(twin.Properties.Desired, TwinProperty.AppKey);

            string nwkSKey = this.ReadTwin(twin.Properties.Desired, TwinProperty.NwkSKey);
            string appSKey = this.ReadTwin(twin.Properties.Desired, TwinProperty.AppSKey);
            string devAddr = this.ReadTwin(twin.Properties.Desired, TwinProperty.DevAddr);
            string abpRelaxMode = this.ReadTwin(twin.Properties.Desired, TwinProperty.ABPRelaxMode);

            netId = ValidationHelper.CleanNetId(netId);

            string gatewayID = this.ReadTwin(twin.Properties.Desired, TwinProperty.GatewayID);
            string sensorDecoder = this.ReadTwin(twin.Properties.Desired, TwinProperty.SensorDecoder);
            string classType = this.ReadTwin(twin.Properties.Desired, TwinProperty.ClassType);
            string downlinkEnabled = this.ReadTwin(twin.Properties.Desired, TwinProperty.DownlinkEnabled);
            string preferredWindow = this.ReadTwin(twin.Properties.Desired, TwinProperty.PreferredWindow);
            string deduplication = this.ReadTwin(twin.Properties.Desired, TwinProperty.Deduplication);
            string rx2DataRate = this.ReadTwin(twin.Properties.Desired, TwinProperty.RX2DataRate);
            string rx1DrOffset = this.ReadTwin(twin.Properties.Desired, TwinProperty.RX1DROffset);
            string supports32BitFCnt = this.ReadTwin(twin.Properties.Desired, TwinProperty.Supports32BitFCnt);
            string fCntUpStart = this.ReadTwin(twin.Properties.Desired, TwinProperty.FCntUpStart);
            string fCntDownStart = this.ReadTwin(twin.Properties.Desired, TwinProperty.FCntDownStart);
            string fCntResetCounter = this.ReadTwin(twin.Properties.Desired, TwinProperty.FCntResetCounter);

            string fCntUpStartReported = this.ReadTwin(twin.Properties.Reported, TwinProperty.FCntUpStart);
            string fCntDownStartReported = this.ReadTwin(twin.Properties.Reported, TwinProperty.FCntDownStart);
            string fCntResetCounterReported = this.ReadTwin(twin.Properties.Reported, TwinProperty.FCntResetCounter);

            isOtaa = !string.IsNullOrEmpty(appEui) || !string.IsNullOrEmpty(appKey);
            isAbp = !string.IsNullOrEmpty(nwkSKey) || !string.IsNullOrEmpty(appSKey) || !string.IsNullOrEmpty(devAddr);

            // ABP device
            if (isAbp && !isOtaa)
            {
                StatusConsole.WriteLine(MessageType.Info, "ABP device configuration detected.");

                isValid = this.VerifyDevice(
                    new AddOptions()
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
                    configurationHelper,
                    isVerbose);
            }

            // OTAA device
            else if (isOtaa && !isAbp)
            {
                StatusConsole.WriteLine(MessageType.Info, "OTAA device configuration detected.");
                isValid = this.VerifyDevice(
                    new AddOptions()
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
                    configurationHelper,
                    isVerbose);
            }

            // Unknown device type
            else
            {
                StatusConsole.WriteLine(MessageType.Error, "Can't determine if ABP or OTAA device.");
                if (isVerbose)
                {
                    Console.WriteLine("ABP devices should contain NwkSKey, AppSKey and DevAddr, not AppEUI and AppKey.");
                    Console.WriteLine("OTAA devices should contain AppEUI and AppKey, not NwkSKey, AppSKey and DevAddr.");
                }

                isValid = false;
            }

            if (isVerbose)
            {
                Console.WriteLine();
                Console.WriteLine("Verification Result:");
            }

            if (isValid)
            {
                StatusConsole.WriteLine(MessageType.Info, $"The configuration for device {devEui} is valid.");
            }
            else
            {
                StatusConsole.WriteLine(MessageType.Error, $"The configuration for device {devEui} is not valid.");
            }

            if (isVerbose)
                Console.WriteLine("done.");

            return isValid;
        }

        public string ReadTwin(TwinCollection collection, string property)
        {
            return collection.Contains(property) ? ValidationHelper.GetTwinPropertyValue(collection[property]) : null;
        }

        public object CleanOptions(object optsObject, bool isNewDevice)
        {
            dynamic opts;

            if (isNewDevice)
                opts = optsObject as AddOptions;
            else
                opts = optsObject as UpdateOptions;

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

            return (object)opts;
        }

        public AddOptions CompleteMissingAddOptions(AddOptions opts, ConfigurationHelper configurationHelper)
        {
            Console.WriteLine();
            Console.WriteLine($"Completing missing options for device...");

            if (string.IsNullOrEmpty(opts.DevEui))
            {
                opts.DevEui = Keygen.Generate(8);
                StatusConsole.WriteLine(MessageType.Info, $"Generating missing DevEUI: {opts.DevEui}");
            }

            // ABP device specific properties
            if (string.Equals(opts.Type, "ABP", StringComparison.OrdinalIgnoreCase))
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
                    if (!string.Equals(newDevAddr, opts.DevAddr, StringComparison.OrdinalIgnoreCase))
                    {
                        opts.DevAddr = newDevAddr;
                        StatusConsole.WriteLine(MessageType.Info, $"Adapting DevAddr to: {opts.DevAddr} based on NetId {(string.IsNullOrEmpty(opts.NetId) ? configurationHelper.NetId : opts.NetId)}");
                    }
                }
            }

            // OTAA device specific properties
            else
            {
                if (string.IsNullOrEmpty(opts.AppEui))
                {
                    opts.AppEui = Keygen.Generate(8);
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

            Console.WriteLine("done.");
            return opts;
        }

        public UpdateOptions CompleteMissingUpdateOptions(UpdateOptions opts, ConfigurationHelper configurationHelper)
        {
            Console.WriteLine();
            Console.WriteLine($"Completing missing options for device...");

            // ABP device specific properties
            if (!string.IsNullOrEmpty(opts.DevAddr) && ValidationHelper.ValidateHexStringTwinProperty(opts.DevAddr, 4, out string _))
            {
                var newDevAddr = NetIdHelper.SetNwkIdPart(opts.DevAddr, opts.NetId, configurationHelper);
                if (!string.Equals(newDevAddr, opts.DevAddr, StringComparison.OrdinalIgnoreCase))
                {
                    opts.DevAddr = newDevAddr;
                    StatusConsole.WriteLine(MessageType.Warning, $"Adapting DevAddr: {opts.DevAddr} based on NetId {(string.IsNullOrEmpty(opts.NetId) ? configurationHelper.NetId : opts.NetId)}.");
                }
            }

            // Shared, non optional device properties
            if (string.Equals("null", opts.GatewayId, StringComparison.OrdinalIgnoreCase))
            {
                opts.GatewayId = string.Empty;
                StatusConsole.WriteLine(MessageType.Info, $"GatewayId is set to \"null\". Adding empty property.");
            }

            if (string.Equals("null", opts.SensorDecoder, StringComparison.OrdinalIgnoreCase))
            {
                opts.SensorDecoder = string.Empty;
                StatusConsole.WriteLine(MessageType.Info, $"SensorDecoder is set to \"null\". Adding empty property.");
            }

            Console.WriteLine("done.");
            return opts;
        }

        public bool VerifyDevice(AddOptions opts, string fCntUpStartReported, string fCntDownStartReported, string fCntResetCounterReported, ConfigurationHelper configurationHelper, bool isVerbose)
        {
            string validationError = string.Empty;
            bool isValid = true;

            if (isVerbose)
            {
                Console.WriteLine();
                Console.WriteLine($"Verifying device {opts.DevEui} twin data...");
            }

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
                if (isVerbose)
                    StatusConsole.WriteLine(MessageType.Info, $"DevEui {opts.DevEui} is valid.");
            }

            // ******************************************************************************************
            // ABP device specific properties
            // ******************************************************************************************
            if (string.Equals(opts.Type, "ABP", StringComparison.OrdinalIgnoreCase))
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
                    if (isVerbose)
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
                    if (isVerbose)
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
                        if (isVerbose)
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
                        if (isVerbose)
                            StatusConsole.WriteLine(MessageType.Info, $"DevAddr {opts.DevAddr} is valid based on NetId {(string.IsNullOrEmpty(opts.NetId) ? configurationHelper.NetId : opts.NetId)}.");
                    }
                    else
                    {
                        StatusConsole.WriteLine(MessageType.Error, $"DevAddr {opts.DevAddr} is invalid based on NetId {(string.IsNullOrEmpty(opts.NetId) ? configurationHelper.NetId : opts.NetId)}.");

                        if (isVerbose)
                        {
                            StatusConsole.WriteLine(MessageType.Warning, $"DevAddr {opts.DevAddr} belongs to NetId ending in byte {NetIdHelper.GetNwkIdPart(opts.DevAddr).ToString("X2")}.");
                            StatusConsole.WriteLine(MessageType.Info, $"To stop seeing this error, provide the --netid parameter or set the NetId in the settings file.");
                        }

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
                        if (isVerbose)
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
                        if (isVerbose)
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
                        if (isVerbose)
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
                        if (isVerbose)
                            StatusConsole.WriteLine(MessageType.Info, $"FCntResetCounter {opts.FCntResetCounter} is valid.");
                    }
                }

                // Frame Counter Settings
                if (isVerbose)
                {
                    if (!ValidationHelper.ValidateFcntSettings(opts, fCntUpStartReported, fCntDownStartReported, fCntResetCounterReported))
                    {
                        isValid = false;
                    }
                }

                // ******************************************************************************************
                // Invalid properties for ABP devices
                // ******************************************************************************************

                // AppEui
                if (!string.IsNullOrEmpty(opts.AppEui))
                {
                    StatusConsole.WriteLine(MessageType.Error, $"AppEUI is an invalid property for ABP devices.");
                    isValid = false;
                }

                // AppKey
                if (!string.IsNullOrEmpty(opts.AppKey))
                {
                    StatusConsole.WriteLine(MessageType.Error, $"AppKey is an invalid property for ABP devices.");
                    isValid = false;
                }

                // Rx2DataRate
                if (!string.IsNullOrEmpty(opts.Rx2DataRate))
                {
                    if (!ValidationHelper.ValidateDataRateTwinProperty(opts.Rx2DataRate, out validationError))
                    {
                        StatusConsole.WriteLine(MessageType.Error, $"Rx2DataRate {opts.Rx2DataRate} is invalid: {validationError}.");
                        isValid = false;
                    }

                    StatusConsole.WriteLine(MessageType.Warning, $"Rx2DataRate is currently not supported for ABP devices.");
                }

                // Rx1DrOffset
                if (!string.IsNullOrEmpty(opts.Rx1DrOffset))
                {
                    if (!ValidationHelper.ValidateUIntTwinProperty(opts.Rx1DrOffset, 0, 15, out validationError))
                    {
                        StatusConsole.WriteLine(MessageType.Error, $"Rx1DrOffset {opts.Rx1DrOffset} is invalid: {validationError}.");
                        isValid = false;
                    }

                    StatusConsole.WriteLine(MessageType.Warning, $"Rx1DrOffset is currently not supported for ABP devices.");
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
                    StatusConsole.WriteLine(MessageType.Error, "AppEUI is missing.");
                    isValid = false;
                }
                else if (!ValidationHelper.ValidateHexStringTwinProperty(opts.AppEui, 8, out validationError))
                {
                    StatusConsole.WriteLine(MessageType.Error, $"AppEUI {opts.AppEui} is invalid: {validationError}.");
                    isValid = false;
                }
                else
                {
                    if (isVerbose)
                        StatusConsole.WriteLine(MessageType.Info, $"AppEui {opts.AppEui} is valid.");
                }

                // AppKey
                if (string.IsNullOrEmpty(opts.AppKey))
                {
                    StatusConsole.WriteLine(MessageType.Error, "AppKey is missing.");
                    isValid = false;
                }
                else if (!ValidationHelper.ValidateHexStringTwinProperty(opts.AppKey, 16, out validationError))
                {
                    StatusConsole.WriteLine(MessageType.Error, $"AppKey {opts.AppKey} is invalid: {validationError}.");
                    isValid = false;
                }
                else
                {
                    if (isVerbose)
                        StatusConsole.WriteLine(MessageType.Info, $"AppKey {opts.AppKey} is valid.");
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
                        if (isVerbose)
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
                        if (isVerbose)
                            StatusConsole.WriteLine(MessageType.Info, $"Rx1DrOffset {opts.Rx1DrOffset} is valid.");
                    }
                }

                // ******************************************************************************************
                // Invalid properties for OTAA devices
                // ******************************************************************************************

                // NwkSKey
                if (!string.IsNullOrEmpty(opts.NwkSKey))
                {
                    StatusConsole.WriteLine(MessageType.Error, $"NwkSKey is invalid for OTAA devices.");
                    isValid = false;
                }

                // AppSKey
                if (!string.IsNullOrEmpty(opts.AppSKey))
                {
                    StatusConsole.WriteLine(MessageType.Error, $"AppSKey is invalid for OTAA devices.");
                    isValid = false;
                }

                // DevAddr
                if (!string.IsNullOrEmpty(opts.DevAddr))
                {
                    StatusConsole.WriteLine(MessageType.Error, $"DevAddr is invalid for OTAA devices.");
                    isValid = false;
                }

                // NetId
                if (!string.IsNullOrEmpty(opts.NetId))
                {
                    if (!ValidationHelper.ValidateHexStringTwinProperty(opts.NetId, 3, out validationError))
                    {
                        StatusConsole.WriteLine(MessageType.Error, $"NetId {opts.NetId} is invalid: {validationError}.");
                        isValid = false;
                    }

                    StatusConsole.WriteLine(MessageType.Warning, $"NetId is not used in OTAA devices.");
                }

                // ABPRelaxMode
                if (!string.IsNullOrEmpty(opts.ABPRelaxMode))
                {
                    if (!ValidationHelper.ValidateBoolTwinProperty(opts.ABPRelaxMode, out validationError))
                    {
                        StatusConsole.WriteLine(MessageType.Error, $"ABPRelaxMode {opts.ABPRelaxMode} is invalid: {validationError}.");
                        isValid = false;
                    }

                    StatusConsole.WriteLine(MessageType.Warning, $"ABPRelaxMode is invalid/ignored for OTAA devices.");
                }

                // FCntUpStart
                if (!string.IsNullOrEmpty(opts.FCntUpStart))
                {
                    if (!ValidationHelper.ValidateUIntTwinProperty(opts.FCntUpStart, 0, uint.MaxValue, out validationError))
                    {
                        StatusConsole.WriteLine(MessageType.Error, $"FCntUpStart {opts.FCntUpStart} is invalid: {validationError}.");
                        isValid = false;
                    }

                    StatusConsole.WriteLine(MessageType.Warning, $"FCntUpStart is invalid/ignored for OTAA devices.");
                }

                // FCntDownStart
                if (!string.IsNullOrEmpty(opts.FCntDownStart))
                {
                    if (!ValidationHelper.ValidateUIntTwinProperty(opts.FCntDownStart, 0, uint.MaxValue, out validationError))
                    {
                        StatusConsole.WriteLine(MessageType.Error, $"FCntDownStart {opts.FCntDownStart} is invalid: {validationError}.");
                        isValid = false;
                    }

                    StatusConsole.WriteLine(MessageType.Warning, $"FCntDownStart is invalid/ignored for OTAA devices.");
                }

                // FCntResetCounter
                if (!string.IsNullOrEmpty(opts.FCntResetCounter))
                {
                    if (!ValidationHelper.ValidateUIntTwinProperty(opts.FCntResetCounter, 0, uint.MaxValue, out validationError))
                    {
                        StatusConsole.WriteLine(MessageType.Error, $"FCntResetCounter {opts.FCntResetCounter} is invalid: {validationError}.");
                        isValid = false;
                    }

                    StatusConsole.WriteLine(MessageType.Warning, $"FCntResetCounter is invalid/ignored for OTAA devices.");
                }
            }

            // ******************************************************************************************
            // // Shared, non optional device properties
            // ******************************************************************************************

            // SensorDecoder
            if (!ValidationHelper.ValidateSensorDecoder(opts.SensorDecoder, isVerbose))
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
                if (isVerbose)
                    StatusConsole.WriteLine(MessageType.Info, $"GatewayId is empty. This is valid.");
            }
            else
            {
                if (isVerbose)
                    StatusConsole.WriteLine(MessageType.Info, $"GatewayId {opts.GatewayId} is valid.");
            }

            // ******************************************************************************************
            // // Shared, optional device properties
            // ******************************************************************************************

            // ClassType
            if (!string.IsNullOrEmpty(opts.ClassType))
            {
                if (!Array.Exists(this.classTypes, classType => string.Equals(
                    classType, opts.ClassType, StringComparison.OrdinalIgnoreCase)))
                {
                    StatusConsole.WriteLine(MessageType.Error, $"ClassType {opts.ClassType} is invalid: If set, it needs to be \"A\" or \"C\".");
                    isValid = false;
                }
                else
                {
                    if (isVerbose)
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
                    if (isVerbose)
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
                    if (isVerbose)
                        StatusConsole.WriteLine(MessageType.Info, $"PreferredWindow {opts.PreferredWindow} is valid.");
                }
            }

            // Deduplication
            if (!string.IsNullOrEmpty(opts.Deduplication))
            {
                if (!Array.Exists(this.deduplicationModes, deduplicationMode => string.Equals(
                    deduplicationMode, opts.Deduplication, StringComparison.OrdinalIgnoreCase)))
                {
                    StatusConsole.WriteLine(MessageType.Error, $"Deduplication {opts.Deduplication} is invalid: If set, it needs to be \"None\", \"Drop\" or \"Mark\".");
                    isValid = false;
                }
                else
                {
                    if (isVerbose)
                        StatusConsole.WriteLine(MessageType.Info, $"Deduplication {opts.Deduplication} is valid.");
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
                    if (isVerbose)
                        StatusConsole.WriteLine(MessageType.Info, $"Supports32BitFCnt {opts.Supports32BitFCnt} is valid.");
                }
            }

            if (isVerbose)
                Console.WriteLine("done.");

            return isValid;
        }

        public Twin CreateDeviceTwin(AddOptions opts)
        {
            var twinProperties = new TwinProperties();
            Console.WriteLine();

            // ******************************************************************************************
            // DevEui NOT located in twin
            // ******************************************************************************************

            // ABP device specific properties
            if (string.Equals(opts.Type, "ABP", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine($"Creating ABP device twin: {opts.DevEui}...");

                twinProperties.Desired[TwinProperty.AppSKey] = ValidationHelper.ConvertToStringTwinProperty(opts.AppSKey);
                twinProperties.Desired[TwinProperty.NwkSKey] = ValidationHelper.ConvertToStringTwinProperty(opts.NwkSKey);
                twinProperties.Desired[TwinProperty.DevAddr] = ValidationHelper.ConvertToStringTwinProperty(opts.DevAddr);

                if (!string.IsNullOrEmpty(opts.ABPRelaxMode))
                    twinProperties.Desired[TwinProperty.ABPRelaxMode] = ValidationHelper.ConvertToBoolTwinProperty(opts.ABPRelaxMode);

                if (!string.IsNullOrEmpty(opts.FCntUpStart))
                    twinProperties.Desired[TwinProperty.FCntUpStart] = ValidationHelper.ConvertToUIntTwinProperty(opts.FCntUpStart);

                if (!string.IsNullOrEmpty(opts.FCntDownStart))
                    twinProperties.Desired[TwinProperty.FCntDownStart] = ValidationHelper.ConvertToUIntTwinProperty(opts.FCntDownStart);

                if (!string.IsNullOrEmpty(opts.FCntResetCounter))
                    twinProperties.Desired[TwinProperty.FCntResetCounter] = ValidationHelper.ConvertToUIntTwinProperty(opts.FCntResetCounter);
            }

            // OTAA device specific properties
            else
            {
                Console.WriteLine($"Creating OTAA device twin: {opts.DevEui}...");

                twinProperties.Desired[TwinProperty.AppEUI] = ValidationHelper.ConvertToStringTwinProperty(opts.AppEui);
                twinProperties.Desired[TwinProperty.AppKey] = ValidationHelper.ConvertToStringTwinProperty(opts.AppKey);
            }

            // Shared, non optional properties
            twinProperties.Desired[TwinProperty.GatewayID] = ValidationHelper.ConvertToStringTwinProperty(opts.GatewayId);
            twinProperties.Desired[TwinProperty.SensorDecoder] = ValidationHelper.ConvertToStringTwinProperty(opts.SensorDecoder);

            // Shared, optional properties
            if (!string.IsNullOrEmpty(opts.ClassType))
                twinProperties.Desired[TwinProperty.ClassType] = ValidationHelper.ConvertToStringTwinProperty(opts.ClassType);

            if (!string.IsNullOrEmpty(opts.DownlinkEnabled))
                twinProperties.Desired[TwinProperty.DownlinkEnabled] = ValidationHelper.ConvertToBoolTwinProperty(opts.DownlinkEnabled);

            if (!string.IsNullOrEmpty(opts.PreferredWindow))
                twinProperties.Desired[TwinProperty.PreferredWindow] = ValidationHelper.ConvertToUIntTwinProperty(opts.PreferredWindow);

            if (!string.IsNullOrEmpty(opts.Deduplication))
                twinProperties.Desired[TwinProperty.Deduplication] = ValidationHelper.ConvertToStringTwinProperty(opts.Deduplication);

            if (!string.IsNullOrEmpty(opts.Rx2DataRate))
                twinProperties.Desired[TwinProperty.RX2DataRate] = ValidationHelper.ConvertToStringTwinProperty(opts.Rx2DataRate);

            if (!string.IsNullOrEmpty(opts.Rx1DrOffset))
                twinProperties.Desired[TwinProperty.RX1DROffset] = ValidationHelper.ConvertToUIntTwinProperty(opts.Rx1DrOffset);

            if (!string.IsNullOrEmpty(opts.Supports32BitFCnt))
                twinProperties.Desired[TwinProperty.Supports32BitFCnt] = ValidationHelper.ConvertToBoolTwinProperty(opts.Supports32BitFCnt);

            Console.WriteLine("done.");
            return new Twin
            {
                Properties = twinProperties
            };
        }

        public Twin UpdateDeviceTwin(Twin twin, UpdateOptions opts)
        {
            Console.WriteLine();
            Console.WriteLine($"Applying changes to device {opts.DevEui} twin...");

            // ******************************************************************************************
            // DevEui can NOT be updated!
            // ******************************************************************************************

            // ABP device properties
            if (!string.IsNullOrEmpty(opts.AppSKey))
                twin.Properties.Desired[TwinProperty.AppSKey] = ValidationHelper.ConvertToStringTwinProperty(opts.AppSKey);

            if (!string.IsNullOrEmpty(opts.NwkSKey))
                twin.Properties.Desired[TwinProperty.NwkSKey] = ValidationHelper.ConvertToStringTwinProperty(opts.NwkSKey);

            if (!string.IsNullOrEmpty(opts.DevAddr))
                twin.Properties.Desired[TwinProperty.DevAddr] = ValidationHelper.ConvertToStringTwinProperty(opts.DevAddr);

            if (!string.IsNullOrEmpty(opts.ABPRelaxMode))
                twin.Properties.Desired[TwinProperty.ABPRelaxMode] = ValidationHelper.ConvertToBoolTwinProperty(opts.ABPRelaxMode);

            if (!string.IsNullOrEmpty(opts.FCntUpStart))
                twin.Properties.Desired[TwinProperty.FCntUpStart] = ValidationHelper.ConvertToUIntTwinProperty(opts.FCntUpStart);

            if (!string.IsNullOrEmpty(opts.FCntDownStart))
                twin.Properties.Desired[TwinProperty.FCntDownStart] = ValidationHelper.ConvertToUIntTwinProperty(opts.FCntDownStart);

            if (!string.IsNullOrEmpty(opts.FCntResetCounter))
                twin.Properties.Desired[TwinProperty.FCntResetCounter] = ValidationHelper.ConvertToUIntTwinProperty(opts.FCntResetCounter);

            // OTAA device properties
            if (!string.IsNullOrEmpty(opts.AppEui))
                twin.Properties.Desired[TwinProperty.AppEUI] = ValidationHelper.ConvertToStringTwinProperty(opts.AppEui);

            if (!string.IsNullOrEmpty(opts.AppKey))
                twin.Properties.Desired[TwinProperty.AppKey] = ValidationHelper.ConvertToStringTwinProperty(opts.AppKey);

            // Shared, non optional properties
            if (opts.GatewayId != null)
                twin.Properties.Desired[TwinProperty.GatewayID] = ValidationHelper.ConvertToStringTwinProperty(opts.GatewayId);

            if (opts.SensorDecoder != null)
                twin.Properties.Desired[TwinProperty.SensorDecoder] = ValidationHelper.ConvertToStringTwinProperty(opts.SensorDecoder);

            // Shared, optional properties
            if (!string.IsNullOrEmpty(opts.ClassType))
                twin.Properties.Desired[TwinProperty.ClassType] = ValidationHelper.ConvertToStringTwinProperty(opts.ClassType);

            if (!string.IsNullOrEmpty(opts.DownlinkEnabled))
                twin.Properties.Desired[TwinProperty.DownlinkEnabled] = ValidationHelper.ConvertToBoolTwinProperty(opts.DownlinkEnabled);

            if (!string.IsNullOrEmpty(opts.PreferredWindow))
                twin.Properties.Desired[TwinProperty.PreferredWindow] = ValidationHelper.ConvertToUIntTwinProperty(opts.PreferredWindow);

            if (!string.IsNullOrEmpty(opts.Deduplication))
                twin.Properties.Desired[TwinProperty.Deduplication] = ValidationHelper.ConvertToStringTwinProperty(opts.Deduplication);

            if (!string.IsNullOrEmpty(opts.Rx2DataRate))
                twin.Properties.Desired[TwinProperty.RX2DataRate] = ValidationHelper.ConvertToStringTwinProperty(opts.Rx2DataRate);

            if (!string.IsNullOrEmpty(opts.Rx1DrOffset))
                twin.Properties.Desired[TwinProperty.RX1DROffset] = ValidationHelper.ConvertToUIntTwinProperty(opts.Rx1DrOffset);

            if (!string.IsNullOrEmpty(opts.Supports32BitFCnt))
                twin.Properties.Desired[TwinProperty.Supports32BitFCnt] = ValidationHelper.ConvertToBoolTwinProperty(opts.Supports32BitFCnt);

            Console.WriteLine("done.");
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
                    StatusConsole.WriteLine(MessageType.Info, "Success!");
                }
                else
                {
                    StatusConsole.WriteLine(MessageType.Error, "Adding device failed:");
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
                    StatusConsole.WriteLine(MessageType.Error, "Updating device failed: " + ex.Message);
                    return false;
                }

                StatusConsole.WriteLine(MessageType.Info, $"Device {devEui} updated.");
            }

            Console.WriteLine("done.");
            return true;
        }

        public async Task<bool> QueryDevices(ConfigurationHelper configurationHelper, int page, int total)
        {
            var count = 0;
            IEnumerable<string> currentPage;
            string totalString = (total == -1) ? "all" : total.ToString();

            page = Math.Max(1, page);

            Console.WriteLine();
            Console.WriteLine($"Listing devices...");
            Console.WriteLine($"Page: {page}, Total: {totalString}");
            Console.WriteLine();

            var query = configurationHelper.RegistryManager.CreateQuery("SELECT * FROM devices", page);

            while (query.HasMoreResults)
            {
                try
                {
                    currentPage = await query.GetNextAsJsonAsync();
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

                Console.WriteLine();
                Console.WriteLine("Press any key to continue.");
                Console.ReadKey();
            }

            Console.WriteLine("done.");
            return true;
        }

        public async Task<bool> QueryDevicesAndVerify(ConfigurationHelper configurationHelper, int page, int total)
        {
            var count = 0;
            bool isValid = true;
            IEnumerable<Twin> currentPage;
            string totalString = (total == -1) ? "all" : total.ToString();

            page = Math.Max(1, page);

            Console.WriteLine();
            Console.WriteLine($"Bulk Verifying devices...");
            Console.WriteLine($"Page: {page}, Total: {totalString}");

            var query = configurationHelper.RegistryManager.CreateQuery(
                "SELECT * FROM devices WHERE is_defined(properties.desired.AppKey) OR is_defined(properties.desired.AppSKey)", page);

            while (query.HasMoreResults)
            {
                try
                {
                    currentPage = await query.GetNextAsTwinAsync();
                }
                catch (Exception ex)
                {
                    StatusConsole.WriteLine(MessageType.Error, ex.Message);
                    return false;
                }

                foreach (var twin in currentPage)
                {
                    if (!this.VerifyDeviceTwin(twin.DeviceId, null, twin, configurationHelper, false))
                        isValid = false;

                    if (count++ >= total - 1 && total >= 0)
                        return isValid;
                }

                Console.WriteLine();
                Console.WriteLine("Press any key to continue.");
                Console.ReadKey();
            }

            return isValid;
        }

        public async Task<bool> RemoveDevice(string devEui, ConfigurationHelper configurationHelper)
        {
            Device device;

            Console.WriteLine();
            Console.WriteLine($"Finding existing device {devEui} in IoT Hub...");

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
                StatusConsole.WriteLine(MessageType.Info, $"Removing device {devEui} from IoT Hub...");

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
                StatusConsole.WriteLine(MessageType.Error, $"Device {devEui} not found in IoT Hub. Aborting.");
                return false;
            }

            StatusConsole.WriteLine(MessageType.Info, "Success!");
            Console.WriteLine("done.");
            return true;
        }
    }
}
