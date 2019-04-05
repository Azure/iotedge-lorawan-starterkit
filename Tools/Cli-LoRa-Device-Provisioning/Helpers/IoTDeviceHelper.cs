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
        private static readonly string[] ClassTypes = { "A", "C" };
        private static readonly string[] DeduplicationModes = { "None", "Drop", "Mark" };

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
                StatusConsole.WriteLogLine(MessageType.Error, ex.Message);
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

            StatusConsole.WriteLineIfVerbose(null, isVerbose);
            StatusConsole.WriteLineIfVerbose($"Analyzing device {devEui}...", isVerbose);

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
            string rxDelay = this.ReadTwin(twin.Properties.Desired, TwinProperty.RXDelay);
            string keepAliveTimeout = this.ReadTwin(twin.Properties.Desired, TwinProperty.KeepAliveTimeout);
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
                StatusConsole.WriteLogLineIfVerbose(MessageType.Info, "ABP device configuration detected.", isVerbose);
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
                        RxDelay = rxDelay,
                        KeepAliveTimeout = keepAliveTimeout,
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
                StatusConsole.WriteLogLineIfVerbose(MessageType.Info, "OTAA device configuration detected.", isVerbose);
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
                        RxDelay = rxDelay,
                        KeepAliveTimeout = keepAliveTimeout,
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
                StatusConsole.WriteLogLineWithDevEuiWhenVerbose(MessageType.Error, $"Can't determine if ABP or OTAA device.", devEui, isVerbose);
                StatusConsole.WriteLogLineIfVerbose(MessageType.Info, "ABP devices should contain NwkSKey, AppSKey and DevAddr, not AppEUI and AppKey.", isVerbose);
                StatusConsole.WriteLogLineIfVerbose(MessageType.Info, "OTAA devices should contain AppEUI and AppKey, not NwkSKey, AppSKey and DevAddr.", isVerbose);

                // Add blank line only if not verbose
                StatusConsole.WriteLineIfVerbose(null, !isVerbose);

                isValid = false;
            }

            StatusConsole.WriteLineIfVerbose(null, isVerbose);
            StatusConsole.WriteLineIfVerbose("Verification Result:", isVerbose);

            if (isValid)
            {
                StatusConsole.WriteLogLineIfVerbose(MessageType.Info, $"Device {devEui}: configuration is valid.", isVerbose);
            }
            else
            {
                StatusConsole.WriteLogLineIfVerbose(MessageType.Error, $"Device {devEui}: configuration is not valid.", isVerbose);
            }

            StatusConsole.WriteLineIfVerbose("done.", isVerbose);

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

            if (!string.IsNullOrEmpty(opts.RxDelay))
                opts.RxDelay = ValidationHelper.CleanString(opts.RxDelay);

            if (!string.IsNullOrEmpty(opts.KeepAliveTimeout))
                opts.KeepAliveTimeout = ValidationHelper.CleanString(opts.KeepAliveTimeout);

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
                StatusConsole.WriteLogLine(MessageType.Info, $"Generating missing DevEUI: {opts.DevEui}");
            }

            // ABP device specific properties
            if (string.Equals(opts.Type, "ABP", StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrEmpty(opts.NwkSKey))
                {
                    opts.NwkSKey = Keygen.Generate(16);
                    StatusConsole.WriteLogLine(MessageType.Info, $"Generating missing NwkSKey: {opts.NwkSKey}");
                }

                if (string.IsNullOrEmpty(opts.AppSKey))
                {
                    opts.AppSKey = Keygen.Generate(16);
                    StatusConsole.WriteLogLine(MessageType.Info, $"Generating missing AppSKey: {opts.AppSKey}");
                }

                if (string.IsNullOrEmpty(opts.DevAddr))
                {
                    opts.DevAddr = Keygen.Generate(4);
                    StatusConsole.WriteLogLine(MessageType.Info, $"Generating missing DevAddr: {opts.DevAddr}");
                }

                if (ValidationHelper.ValidateHexStringTwinProperty(opts.DevAddr, 4, out string _))
                {
                    var newDevAddr = NetIdHelper.SetNwkIdPart(opts.DevAddr, opts.NetId, configurationHelper);
                    if (!string.Equals(newDevAddr, opts.DevAddr, StringComparison.OrdinalIgnoreCase))
                    {
                        opts.DevAddr = newDevAddr;
                        StatusConsole.WriteLogLine(MessageType.Info, $"Adapting DevAddr to: {opts.DevAddr} based on NetId {(string.IsNullOrEmpty(opts.NetId) ? configurationHelper.NetId : opts.NetId)}");
                    }
                }
            }

            // OTAA device specific properties
            else
            {
                if (string.IsNullOrEmpty(opts.AppEui))
                {
                    opts.AppEui = Keygen.Generate(8);
                    StatusConsole.WriteLogLine(MessageType.Info, $"Generating missing AppEUI: {opts.AppEui}");
                }

                if (string.IsNullOrEmpty(opts.AppKey))
                {
                    opts.AppKey = Keygen.Generate(16);
                    StatusConsole.WriteLogLine(MessageType.Info, $"Generating missing AppKey: {opts.AppKey}");
                }
            }

            // Shared, non optional device properties
            if (opts.GatewayId == null)
            {
                opts.GatewayId = string.Empty;
                StatusConsole.WriteLogLine(MessageType.Info, $"GatewayId is missing. Adding empty property.");
            }

            if (opts.SensorDecoder == null)
            {
                opts.SensorDecoder = string.Empty;
                StatusConsole.WriteLogLine(MessageType.Info, $"SensorDecoder is missing. Adding empty property.");
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
                    StatusConsole.WriteLogLine(MessageType.Warning, $"Adapting DevAddr: {opts.DevAddr} based on NetId {(string.IsNullOrEmpty(opts.NetId) ? configurationHelper.NetId : opts.NetId)}.");
                }
            }

            // Shared, non optional device properties
            if (string.Equals("null", opts.GatewayId, StringComparison.OrdinalIgnoreCase))
            {
                opts.GatewayId = string.Empty;
                StatusConsole.WriteLogLine(MessageType.Info, $"GatewayId is set to \"null\". Adding empty property.");
            }

            if (string.Equals("null", opts.SensorDecoder, StringComparison.OrdinalIgnoreCase))
            {
                opts.SensorDecoder = string.Empty;
                StatusConsole.WriteLogLine(MessageType.Info, $"SensorDecoder is set to \"null\". Adding empty property.");
            }

            Console.WriteLine("done.");
            return opts;
        }

        public bool VerifyDevice(AddOptions opts, string fCntUpStartReported, string fCntDownStartReported, string fCntResetCounterReported, ConfigurationHelper configurationHelper, bool isVerbose)
        {
            string validationError = string.Empty;
            bool isValid = true;

            StatusConsole.WriteLineIfVerbose(null, isVerbose);
            StatusConsole.WriteLineIfVerbose($"Verifying device {opts.DevEui} twin data...", isVerbose);

            // ******************************************************************************************
            // DevEui
            // ******************************************************************************************
            if (string.IsNullOrEmpty(opts.DevEui))
            {
                StatusConsole.WriteLogLine(MessageType.Error, "DevEui is missing.");
                isValid = false;
            }
            else if (!ValidationHelper.ValidateHexStringTwinProperty(opts.DevEui, 8, out validationError))
            {
                StatusConsole.WriteLogLineWithDevEuiWhenVerbose(MessageType.Error, $"DevEui {opts.DevEui} is invalid: {validationError}.", opts.DevEui, isVerbose);
                isValid = false;
            }
            else
            {
                StatusConsole.WriteLogLineIfVerbose(MessageType.Info, $"DevEui {opts.DevEui} is valid.", isVerbose);
            }

            // ******************************************************************************************
            // ABP device specific properties
            // ******************************************************************************************
            if (string.Equals(opts.Type, "ABP", StringComparison.OrdinalIgnoreCase))
            {
                // NwkSKey
                if (string.IsNullOrEmpty(opts.NwkSKey))
                {
                    StatusConsole.WriteLogLineWithDevEuiWhenVerbose(MessageType.Error, "NwkSKey is missing.", opts.DevEui, isVerbose);
                    isValid = false;
                }
                else if (!ValidationHelper.ValidateHexStringTwinProperty(opts.NwkSKey, 16, out validationError))
                {
                    StatusConsole.WriteLogLineWithDevEuiWhenVerbose(MessageType.Error, $"NwkSKey {opts.NwkSKey} is invalid: {validationError}.", opts.DevEui, isVerbose);
                    isValid = false;
                }
                else
                {
                    StatusConsole.WriteLogLineIfVerbose(MessageType.Info, $"NwkSKey {opts.NwkSKey} is valid.", isVerbose);
                }

                // AppSKey
                if (string.IsNullOrEmpty(opts.AppSKey))
                {
                    StatusConsole.WriteLogLineWithDevEuiWhenVerbose(MessageType.Error, "AppSKey is missing.", opts.DevEui, isVerbose);
                    isValid = false;
                }
                else if (!ValidationHelper.ValidateHexStringTwinProperty(opts.AppSKey, 16, out validationError))
                {
                    StatusConsole.WriteLogLineWithDevEuiWhenVerbose(MessageType.Error, $"AppSKey {opts.AppSKey} is invalid: {validationError}.", opts.DevEui, isVerbose);
                    isValid = false;
                }
                else
                {
                    StatusConsole.WriteLogLineIfVerbose(MessageType.Info, $"AppSKey {opts.AppSKey} is valid.", isVerbose);
                }

                // NetId
                if (!string.IsNullOrEmpty(opts.NetId))
                {
                    if (!ValidationHelper.ValidateHexStringTwinProperty(opts.NetId, 3, out validationError))
                    {
                        StatusConsole.WriteLogLineWithDevEuiWhenVerbose(MessageType.Error, $"NetId {opts.NetId} is invalid: {validationError}.", opts.DevEui, isVerbose);
                        isValid = false;
                    }
                    else
                    {
                        StatusConsole.WriteLogLineIfVerbose(MessageType.Info, $"NetId {opts.NetId} is valid.", isVerbose);
                    }
                }

                // DevAddr
                if (string.IsNullOrEmpty(opts.DevAddr))
                {
                    StatusConsole.WriteLogLineWithDevEuiWhenVerbose(MessageType.Error, "DevAddr is missing.", opts.DevEui, isVerbose);
                    isValid = false;
                }
                else if (!ValidationHelper.ValidateHexStringTwinProperty(opts.DevAddr, 4, out validationError))
                {
                    StatusConsole.WriteLogLineWithDevEuiWhenVerbose(MessageType.Error, $"DevAddr {opts.DevAddr} is invalid: {validationError}.", opts.DevEui, isVerbose);
                    isValid = false;
                }
                else
                {
                    var devAddrCorrect = NetIdHelper.SetNwkIdPart(opts.DevAddr, opts.NetId, configurationHelper);

                    if (string.Equals(devAddrCorrect, opts.DevAddr))
                    {
                        StatusConsole.WriteLogLineIfVerbose(MessageType.Info, $"DevAddr {opts.DevAddr} is valid based on NetId {(string.IsNullOrEmpty(opts.NetId) ? configurationHelper.NetId : opts.NetId)}.", isVerbose);
                    }
                    else
                    {
                        StatusConsole.WriteLogLineWithDevEuiWhenVerbose(MessageType.Error, $"DevAddr {opts.DevAddr} is invalid based on NetId {(string.IsNullOrEmpty(opts.NetId) ? configurationHelper.NetId : opts.NetId)}.", opts.DevEui, isVerbose);
                        StatusConsole.WriteLogLineIfVerbose(MessageType.Warning, $"DevAddr {opts.DevAddr} belongs to NetId ending in byte {NetIdHelper.GetNwkIdPart(opts.DevAddr).ToString("X2")}.", isVerbose);
                        StatusConsole.WriteLogLineIfVerbose(MessageType.Info, $"To stop seeing this error, provide the --netid parameter or set the NetId in the settings file.", isVerbose);

                        isValid = false;
                    }
                }

                // AbpRelaxMode
                if (!string.IsNullOrEmpty(opts.ABPRelaxMode))
                {
                    if (!ValidationHelper.ValidateBoolTwinProperty(opts.ABPRelaxMode, out validationError))
                    {
                        StatusConsole.WriteLogLineWithDevEuiWhenVerbose(MessageType.Error, $"ABPRelaxMode {opts.ABPRelaxMode} is invalid: {validationError}.", opts.DevEui, isVerbose);
                        isValid = false;
                    }
                    else
                    {
                        StatusConsole.WriteLogLineIfVerbose(MessageType.Info, $"ABPRelaxMode {opts.ABPRelaxMode} is valid.", isVerbose);
                    }
                }

                // FCntUpStart
                if (!string.IsNullOrEmpty(opts.FCntUpStart))
                {
                    if (!ValidationHelper.ValidateUIntRangeTwinProperty(opts.FCntUpStart, 0, uint.MaxValue, out validationError))
                    {
                        StatusConsole.WriteLogLineWithDevEuiWhenVerbose(MessageType.Error, $"FCntUpStart {opts.FCntUpStart} is invalid: {validationError}.", opts.DevEui, isVerbose);
                        isValid = false;
                    }
                    else
                    {
                        StatusConsole.WriteLogLineIfVerbose(MessageType.Info, $"FCntUpStart {opts.FCntUpStart} is valid.", isVerbose);
                    }
                }

                // FCntDownStart
                if (!string.IsNullOrEmpty(opts.FCntDownStart))
                {
                    if (!ValidationHelper.ValidateUIntRangeTwinProperty(opts.FCntDownStart, 0, uint.MaxValue, out validationError))
                    {
                        StatusConsole.WriteLogLineWithDevEuiWhenVerbose(MessageType.Error, $"FCntDownStart {opts.FCntDownStart} is invalid: {validationError}.", opts.DevEui, isVerbose);
                        isValid = false;
                    }
                    else
                    {
                        StatusConsole.WriteLogLineIfVerbose(MessageType.Info, $"FCntDownStart {opts.FCntDownStart} is valid.", isVerbose);
                    }
                }

                // FCntResetCounter
                if (!string.IsNullOrEmpty(opts.FCntResetCounter))
                {
                    if (!ValidationHelper.ValidateUIntRangeTwinProperty(opts.FCntResetCounter, 0, uint.MaxValue, out validationError))
                    {
                        StatusConsole.WriteLogLineWithDevEuiWhenVerbose(MessageType.Error, $"FCntResetCounter {opts.FCntResetCounter} is invalid: {validationError}.", opts.DevEui, isVerbose);
                        isValid = false;
                    }
                    else
                    {
                        StatusConsole.WriteLogLineIfVerbose(MessageType.Info, $"FCntResetCounter {opts.FCntResetCounter} is valid.", isVerbose);
                    }
                }

                // Frame Counter Settings
                // Warnings only, suppress if not Verbose.
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
                    StatusConsole.WriteLogLineWithDevEuiWhenVerbose(MessageType.Error, $"AppEUI is an invalid property for ABP devices.", opts.DevEui, isVerbose);
                    isValid = false;
                }

                // AppKey
                if (!string.IsNullOrEmpty(opts.AppKey))
                {
                    StatusConsole.WriteLogLineWithDevEuiWhenVerbose(MessageType.Error, $"AppKey is an invalid property for ABP devices.", opts.DevEui, isVerbose);
                    isValid = false;
                }

                // Rx2DataRate
                if (!string.IsNullOrEmpty(opts.Rx2DataRate))
                {
                    if (!ValidationHelper.ValidateDataRateTwinProperty(opts.Rx2DataRate, out validationError))
                    {
                        StatusConsole.WriteLogLineWithDevEuiWhenVerbose(MessageType.Error, $"Rx2DataRate {opts.Rx2DataRate} is invalid: {validationError}.", opts.DevEui, isVerbose);
                        isValid = false;
                    }

                    StatusConsole.WriteLogLineWithDevEuiWhenVerbose(MessageType.Warning, $"Rx2DataRate is currently not supported for ABP devices.", opts.DevEui, isVerbose);
                }

                // Rx1DrOffset
                if (!string.IsNullOrEmpty(opts.Rx1DrOffset))
                {
                    if (!ValidationHelper.ValidateUIntRangeTwinProperty(opts.Rx1DrOffset, 0, 15, out validationError))
                    {
                        StatusConsole.WriteLogLineWithDevEuiWhenVerbose(MessageType.Error, $"Rx1DrOffset {opts.Rx1DrOffset} is invalid: {validationError}.", opts.DevEui, isVerbose);
                        isValid = false;
                    }

                    StatusConsole.WriteLogLineWithDevEuiWhenVerbose(MessageType.Warning, $"Rx1DrOffset is currently not supported for ABP devices.", opts.DevEui, isVerbose);
                }

                // RxDelay
                if (!string.IsNullOrEmpty(opts.RxDelay))
                {
                    if (!ValidationHelper.ValidateUIntRangeTwinProperty(opts.RxDelay, 0, 15, out validationError))
                    {
                        StatusConsole.WriteLogLineWithDevEuiWhenVerbose(MessageType.Error, $"RxDelay {opts.RxDelay} is invalid: {validationError}.", opts.DevEui, isVerbose);
                        isValid = false;
                    }

                    StatusConsole.WriteLogLineWithDevEuiWhenVerbose(MessageType.Warning, $"RxDelay is currently not supported for ABP devices.", opts.DevEui, isVerbose);
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
                    StatusConsole.WriteLogLineWithDevEuiWhenVerbose(MessageType.Error, "AppEUI is missing.", opts.DevEui, isVerbose);
                    isValid = false;
                }
                else if (!ValidationHelper.ValidateHexStringTwinProperty(opts.AppEui, 8, out validationError))
                {
                    StatusConsole.WriteLogLineWithDevEuiWhenVerbose(MessageType.Error, $"AppEUI {opts.AppEui} is invalid: {validationError}.", opts.DevEui, isVerbose);
                    isValid = false;
                }
                else
                {
                    StatusConsole.WriteLogLineIfVerbose(MessageType.Info, $"AppEui {opts.AppEui} is valid.", isVerbose);
                }

                // AppKey
                if (string.IsNullOrEmpty(opts.AppKey))
                {
                    StatusConsole.WriteLogLineWithDevEuiWhenVerbose(MessageType.Error, "AppKey is missing.", opts.DevEui, isVerbose);
                    isValid = false;
                }
                else if (!ValidationHelper.ValidateHexStringTwinProperty(opts.AppKey, 16, out validationError))
                {
                    StatusConsole.WriteLogLineWithDevEuiWhenVerbose(MessageType.Error, $"AppKey {opts.AppKey} is invalid: {validationError}.", opts.DevEui, isVerbose);
                    isValid = false;
                }
                else
                {
                    StatusConsole.WriteLogLineIfVerbose(MessageType.Info, $"AppKey {opts.AppKey} is valid.", isVerbose);
                }

                // Rx2DataRate
                if (!string.IsNullOrEmpty(opts.Rx2DataRate))
                {
                    if (!ValidationHelper.ValidateDataRateTwinProperty(opts.Rx2DataRate, out validationError))
                    {
                        StatusConsole.WriteLogLineWithDevEuiWhenVerbose(MessageType.Error, $"Rx2DataRate {opts.Rx2DataRate} is invalid: {validationError}.", opts.DevEui, isVerbose);
                        isValid = false;
                    }
                    else
                    {
                        StatusConsole.WriteLogLineIfVerbose(MessageType.Info, $"Rx2DataRate {opts.Rx2DataRate} is valid.", isVerbose);
                    }
                }

                // Rx1DrOffset
                if (!string.IsNullOrEmpty(opts.Rx1DrOffset))
                {
                    if (!ValidationHelper.ValidateUIntRangeTwinProperty(opts.Rx1DrOffset, 0, 15, out validationError))
                    {
                        StatusConsole.WriteLogLineWithDevEuiWhenVerbose(MessageType.Error, $"Rx1DrOffset {opts.Rx1DrOffset} is invalid: {validationError}.", opts.DevEui, isVerbose);
                        isValid = false;
                    }
                    else
                    {
                        StatusConsole.WriteLogLineIfVerbose(MessageType.Info, $"Rx1DrOffset {opts.Rx1DrOffset} is valid.", isVerbose);
                    }
                }

                // RxDelay
                if (!string.IsNullOrEmpty(opts.RxDelay))
                {
                    if (!ValidationHelper.ValidateUIntRangeTwinProperty(opts.RxDelay, 0, 15, out validationError))
                    {
                        StatusConsole.WriteLogLineWithDevEuiWhenVerbose(MessageType.Error, $"RxDelay {opts.RxDelay} is invalid: {validationError}.", opts.DevEui, isVerbose);
                        isValid = false;
                    }

                    StatusConsole.WriteLogLineIfVerbose(MessageType.Info, $"RxDelay {opts.RxDelay} is valid.", isVerbose);
                }

                // ******************************************************************************************
                // Invalid properties for OTAA devices
                // ******************************************************************************************

                // NwkSKey
                if (!string.IsNullOrEmpty(opts.NwkSKey))
                {
                    StatusConsole.WriteLogLineWithDevEuiWhenVerbose(MessageType.Error, $"NwkSKey is invalid for OTAA devices.", opts.DevEui, isVerbose);
                    isValid = false;
                }

                // AppSKey
                if (!string.IsNullOrEmpty(opts.AppSKey))
                {
                    StatusConsole.WriteLogLineWithDevEuiWhenVerbose(MessageType.Error, $"AppSKey is invalid for OTAA devices.", opts.DevEui, isVerbose);
                    isValid = false;
                }

                // DevAddr
                if (!string.IsNullOrEmpty(opts.DevAddr))
                {
                    StatusConsole.WriteLogLineWithDevEuiWhenVerbose(MessageType.Error, $"DevAddr is invalid for OTAA devices.", opts.DevEui, isVerbose);
                    isValid = false;
                }

                // NetId
                if (!string.IsNullOrEmpty(opts.NetId))
                {
                    if (!ValidationHelper.ValidateHexStringTwinProperty(opts.NetId, 3, out validationError))
                    {
                        StatusConsole.WriteLogLineWithDevEuiWhenVerbose(MessageType.Error, $"NetId {opts.NetId} is invalid: {validationError}.", opts.DevEui, isVerbose);
                        isValid = false;
                    }

                    StatusConsole.WriteLogLineWithDevEuiWhenVerbose(MessageType.Warning, $"NetId is not used in OTAA devices.", opts.DevEui, isVerbose);
                }

                // ABPRelaxMode
                if (!string.IsNullOrEmpty(opts.ABPRelaxMode))
                {
                    if (!ValidationHelper.ValidateBoolTwinProperty(opts.ABPRelaxMode, out validationError))
                    {
                        StatusConsole.WriteLogLineWithDevEuiWhenVerbose(MessageType.Error, $"ABPRelaxMode {opts.ABPRelaxMode} is invalid: {validationError}.", opts.DevEui, isVerbose);
                        isValid = false;
                    }

                    StatusConsole.WriteLogLineWithDevEuiWhenVerbose(MessageType.Warning, $"ABPRelaxMode is invalid/ignored for OTAA devices.", opts.DevEui, isVerbose);
                }

                // FCntUpStart
                if (!string.IsNullOrEmpty(opts.FCntUpStart))
                {
                    if (!ValidationHelper.ValidateUIntRangeTwinProperty(opts.FCntUpStart, 0, uint.MaxValue, out validationError))
                    {
                        StatusConsole.WriteLogLineWithDevEuiWhenVerbose(MessageType.Error, $"FCntUpStart {opts.FCntUpStart} is invalid: {validationError}.", opts.DevEui, isVerbose);
                        isValid = false;
                    }

                    StatusConsole.WriteLogLineWithDevEuiWhenVerbose(MessageType.Warning, $"FCntUpStart is invalid/ignored for OTAA devices.", opts.DevEui, isVerbose);
                }

                // FCntDownStart
                if (!string.IsNullOrEmpty(opts.FCntDownStart))
                {
                    if (!ValidationHelper.ValidateUIntRangeTwinProperty(opts.FCntDownStart, 0, uint.MaxValue, out validationError))
                    {
                        StatusConsole.WriteLogLineWithDevEuiWhenVerbose(MessageType.Error, $"FCntDownStart {opts.FCntDownStart} is invalid: {validationError}.", opts.DevEui, isVerbose);
                        isValid = false;
                    }

                    StatusConsole.WriteLogLineWithDevEuiWhenVerbose(MessageType.Warning, $"FCntDownStart is invalid/ignored for OTAA devices.", opts.DevEui, isVerbose);
                }

                // FCntResetCounter
                if (!string.IsNullOrEmpty(opts.FCntResetCounter))
                {
                    if (!ValidationHelper.ValidateUIntRangeTwinProperty(opts.FCntResetCounter, 0, uint.MaxValue, out validationError))
                    {
                        StatusConsole.WriteLogLineWithDevEuiWhenVerbose(MessageType.Error, $"FCntResetCounter {opts.FCntResetCounter} is invalid: {validationError}.", opts.DevEui, isVerbose);
                        isValid = false;
                    }

                    StatusConsole.WriteLogLineWithDevEuiWhenVerbose(MessageType.Warning, $"FCntResetCounter is invalid/ignored for OTAA devices.", opts.DevEui, isVerbose);
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
                StatusConsole.WriteLogLineWithDevEuiWhenVerbose(MessageType.Error, $"GatewayId is missing.", opts.DevEui, isVerbose);
                isValid = false;
            }
            else if (opts.GatewayId == string.Empty)
            {
                StatusConsole.WriteLogLineIfVerbose(MessageType.Info, $"GatewayId is empty. This is valid.", isVerbose);
            }
            else
            {
                StatusConsole.WriteLogLineIfVerbose(MessageType.Info, $"GatewayId {opts.GatewayId} is valid.", isVerbose);
            }

            // ******************************************************************************************
            // // Shared, optional device properties
            // ******************************************************************************************

            // ClassType
            if (!string.IsNullOrEmpty(opts.ClassType))
            {
                if (!Array.Exists(ClassTypes, classType => string.Equals(
                    classType, opts.ClassType, StringComparison.OrdinalIgnoreCase)))
                {
                    StatusConsole.WriteLogLineWithDevEuiWhenVerbose(MessageType.Error, $"ClassType {opts.ClassType} is invalid: If set, it needs to be \"A\" or \"C\".", opts.DevEui, isVerbose);
                    isValid = false;
                }
                else
                {
                    StatusConsole.WriteLogLineIfVerbose(MessageType.Info, $"ClassType {opts.ClassType} is valid.", isVerbose);
                }
            }

            // DownlinkEnabled
            if (!string.IsNullOrEmpty(opts.DownlinkEnabled))
            {
                if (!ValidationHelper.ValidateBoolTwinProperty(opts.DownlinkEnabled, out validationError))
                {
                    StatusConsole.WriteLogLineWithDevEuiWhenVerbose(MessageType.Error, $"DownlinkEnabled {opts.DownlinkEnabled} is invalid: {validationError}.", opts.DevEui, isVerbose);
                    isValid = false;
                }
                else
                {
                    StatusConsole.WriteLogLineIfVerbose(MessageType.Info, $"DownlinkEnabled {opts.DownlinkEnabled} is valid.", isVerbose);
                }
            }

            // PreferredWindow
            if (!string.IsNullOrEmpty(opts.PreferredWindow))
            {
                if (!ValidationHelper.ValidateUIntRangeTwinProperty(opts.PreferredWindow, 1, 2, out validationError))
                {
                    StatusConsole.WriteLogLineWithDevEuiWhenVerbose(MessageType.Error, $"PreferredWindow {opts.PreferredWindow} is invalid: {validationError}", opts.DevEui, isVerbose);
                    isValid = false;
                }
                else
                {
                    StatusConsole.WriteLogLineIfVerbose(MessageType.Info, $"PreferredWindow {opts.PreferredWindow} is valid.", isVerbose);
                }
            }

            // Deduplication
            if (!string.IsNullOrEmpty(opts.Deduplication))
            {
                if (!Array.Exists(DeduplicationModes, deduplicationMode => string.Equals(
                    deduplicationMode, opts.Deduplication, StringComparison.OrdinalIgnoreCase)))
                {
                    StatusConsole.WriteLogLineWithDevEuiWhenVerbose(MessageType.Error, $"Deduplication {opts.Deduplication} is invalid: If set, it needs to be \"None\", \"Drop\" or \"Mark\".", opts.DevEui, isVerbose);
                    isValid = false;
                }
                else
                {
                    StatusConsole.WriteLogLineIfVerbose(MessageType.Info, $"Deduplication {opts.Deduplication} is valid.", isVerbose);
                }
            }

            // Supports32BitFCnt
            if (!string.IsNullOrEmpty(opts.Supports32BitFCnt))
            {
                if (!ValidationHelper.ValidateBoolTwinProperty(opts.Supports32BitFCnt, out validationError))
                {
                    StatusConsole.WriteLogLineWithDevEuiWhenVerbose(MessageType.Error, $"Supports32BitFCnt {opts.Supports32BitFCnt} is invalid: {validationError}.", opts.DevEui, isVerbose);
                    isValid = false;
                }
                else
                {
                    StatusConsole.WriteLogLineIfVerbose(MessageType.Info, $"Supports32BitFCnt {opts.Supports32BitFCnt} is valid.", isVerbose);
                }
            }

            // RxDelay
            if (!string.IsNullOrEmpty(opts.RxDelay))
            {
                if (!(ValidationHelper.ValidateUIntTwinProperty(opts.RxDelay, 0, out validationError)
                    || ValidationHelper.ValidateUIntRangeTwinProperty(opts.RxDelay, 60, uint.MaxValue, out validationError)))
                {
                    StatusConsole.WriteLogLineWithDevEuiWhenVerbose(MessageType.Error, $"RxDelay {opts.RxDelay} is invalid: Needs to be a number, either 0 or 60 and above.", opts.DevEui, isVerbose);
                    isValid = false;
                }
                else
                {
                    StatusConsole.WriteLogLineIfVerbose(MessageType.Info, $"RxDelay {opts.RxDelay} is valid.", isVerbose);
                }
            }

            StatusConsole.WriteLineIfVerbose("done.", isVerbose);

            // Add blank line only if not verbose and not valid
            if (!isValid)
            {
                StatusConsole.WriteLineIfVerbose(null, !isVerbose);
            }

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

            if (!string.IsNullOrEmpty(opts.RxDelay))
                twinProperties.Desired[TwinProperty.RXDelay] = ValidationHelper.ConvertToBoolTwinProperty(opts.RxDelay);

            if (!string.IsNullOrEmpty(opts.KeepAliveTimeout))
                twinProperties.Desired[TwinProperty.KeepAliveTimeout] = ValidationHelper.ConvertToBoolTwinProperty(opts.KeepAliveTimeout);

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

            if (!string.IsNullOrEmpty(opts.RxDelay))
                twin.Properties.Desired[TwinProperty.RXDelay] = ValidationHelper.ConvertToBoolTwinProperty(opts.RxDelay);

            if (!string.IsNullOrEmpty(opts.KeepAliveTimeout))
                twin.Properties.Desired[TwinProperty.KeepAliveTimeout] = ValidationHelper.ConvertToBoolTwinProperty(opts.KeepAliveTimeout);

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
                    StatusConsole.WriteLogLine(MessageType.Error, ex.Message);
                    return false;
                }

                if (result.IsSuccessful)
                {
                    StatusConsole.WriteLogLine(MessageType.Info, "Success!");
                }
                else
                {
                    StatusConsole.WriteLogLine(MessageType.Error, "Adding device failed:");
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
                    StatusConsole.WriteLogLine(MessageType.Error, "Updating device failed: " + ex.Message);
                    return false;
                }

                StatusConsole.WriteLogLine(MessageType.Info, $"Device {devEui} updated.");
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

            var query = configurationHelper.RegistryManager.CreateQuery(
                "SELECT * FROM devices WHERE is_defined(properties.desired.AppKey) OR is_defined(properties.desired.AppSKey) OR is_defined(properties.desired.NwkSKey)",
                page);

            while (query.HasMoreResults)
            {
                try
                {
                    currentPage = await query.GetNextAsJsonAsync();
                }
                catch (Exception ex)
                {
                    StatusConsole.WriteLogLine(MessageType.Error, ex.Message);
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
                    {
                        Console.WriteLine("done.");
                        return true;
                    }
                }

                if (count > 0)
                {
                    Console.WriteLine("Press any key to continue.");
                    Console.ReadKey();
                    Console.WriteLine();
                }
                else
                {
                    Console.WriteLine("No devices with LoRa twin properties found in IoT hub.");
                }
            }

            Console.WriteLine("done.");
            return true;
        }

        public async Task<bool> QueryDevicesAndVerify(ConfigurationHelper configurationHelper, int page)
        {
            var countValid = 0;
            var countInvalid = 0;
            bool isValid = true;
            IEnumerable<Twin> fullList;

            Console.WriteLine();
            Console.WriteLine($"Bulk Verifying devices...");

            var query = configurationHelper.RegistryManager.CreateQuery(
                "SELECT * FROM devices WHERE is_defined(properties.desired.AppKey) OR is_defined(properties.desired.AppSKey) OR is_defined(properties.desired.NwkSKey)");

            while (query.HasMoreResults)
            {
                try
                {
                    fullList = await query.GetNextAsTwinAsync();
                }
                catch (Exception ex)
                {
                    StatusConsole.WriteLogLine(MessageType.Error, ex.Message);
                    return false;
                }

                foreach (var twin in fullList)
                {
                    if (!this.VerifyDeviceTwin(twin.DeviceId, null, twin, configurationHelper, false))
                    {
                        isValid = false;
                        countInvalid++;

                        if (page > 0)
                        {
                            if (countInvalid % page == 0)
                            {
                                Console.WriteLine("Press any key to continue...");
                                Console.ReadKey();
                                Console.WriteLine();
                            }
                        }
                    }
                    else
                    {
                        countValid++;
                    }
                }
            }

            StatusConsole.WriteLogLine(MessageType.Info, $"Valid devices found in IoT Hub: {countValid}.");
            StatusConsole.WriteLogLine(MessageType.Info, $"Invalid devices found in IoT Hub: {countInvalid}.");

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
                StatusConsole.WriteLogLine(MessageType.Error, ex.Message);
                return false;
            }

            if (device != null)
            {
                StatusConsole.WriteLogLine(MessageType.Info, $"Removing device {devEui} from IoT Hub...");

                try
                {
                    await configurationHelper.RegistryManager.RemoveDeviceAsync(device);
                }
                catch (Exception ex)
                {
                    StatusConsole.WriteLogLine(MessageType.Error, ex.Message);
                    return false;
                }
            }
            else
            {
                StatusConsole.WriteLogLine(MessageType.Error, $"Device {devEui} not found in IoT Hub. Aborting.");
                return false;
            }

            StatusConsole.WriteLogLine(MessageType.Info, "Success!");
            Console.WriteLine("done.");
            return true;
        }
    }
}
