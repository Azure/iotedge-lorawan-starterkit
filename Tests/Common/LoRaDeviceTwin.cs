// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

namespace LoRaWan.Tests.Common
{
    using System;
    using System.Globalization;
    using LoRaTools.Regions;
    using LoRaWan.NetworkServer;
    using Microsoft.Azure.Devices.Shared;

    public static class LoRaDeviceTwin
    {
        public static Twin Create(LoRaDesiredTwinProperties? desiredProperties = null, LoRaReportedTwinProperties? reportedProperties = null)
        {
            var twin = new Twin();

            if (desiredProperties is { } someDesiredProperties)
            {
                SetDesiredPropertyIfExists(twin, TwinProperty.DevEUI, someDesiredProperties.DevEui?.ToString());
                SetDesiredPropertyIfExists(twin, TwinProperty.DevAddr, someDesiredProperties.DevAddr?.ToString());
                SetDesiredPropertyIfExists(twin, TwinProperty.AppEui, someDesiredProperties.JoinEui?.ToString());
                SetDesiredPropertyIfExists(twin, TwinProperty.AppKey, someDesiredProperties.AppKey?.ToString());
                SetDesiredPropertyIfExists(twin, TwinProperty.AppSKey, someDesiredProperties.AppSessionKey?.ToString());
                SetDesiredPropertyIfExists(twin, TwinProperty.NwkSKey, someDesiredProperties.NetworkSessionKey?.ToString());
                SetDesiredPropertyIfExists(twin, TwinProperty.GatewayID, someDesiredProperties.GatewayId?.ToString());
                SetDesiredPropertyIfExists(twin, TwinProperty.SensorDecoder, someDesiredProperties.SensorDecoder?.ToString());
                SetDesiredPropertyIfExists(twin, TwinProperty.Supports32BitFCnt, someDesiredProperties.Supports32BitFCnt?.ToString());
                SetDesiredPropertyIfExists(twin, TwinProperty.ABPRelaxMode, someDesiredProperties.AbpRelaxMode?.ToString());
                SetDesiredPropertyIfExists(twin, TwinProperty.FCntUpStart, someDesiredProperties.FCntUpStart?.ToString(CultureInfo.InvariantCulture));
                SetDesiredPropertyIfExists(twin, TwinProperty.FCntDownStart, someDesiredProperties.FCntDownStart?.ToString(CultureInfo.InvariantCulture));
                SetDesiredPropertyIfExists(twin, TwinProperty.FCntResetCounter, someDesiredProperties.FCntResetCounter?.ToString(CultureInfo.InvariantCulture));
                SetDesiredPropertyIfExists(twin, TwinProperty.RX1DROffset, someDesiredProperties.Rx1DROffset?.ToString(CultureInfo.InvariantCulture));
                SetDesiredPropertyIfExists(twin, TwinProperty.RX2DataRate, someDesiredProperties.Rx2DataRate?.ToString());
                SetDesiredPropertyIfExists(twin, TwinProperty.PreferredWindow, someDesiredProperties.PreferredWindow?.ToString(CultureInfo.InvariantCulture));
                SetDesiredPropertyIfExists(twin, TwinProperty.RXDelay, someDesiredProperties.RxDelay?.ToString(CultureInfo.InvariantCulture));
                SetDesiredPropertyIfExists(twin, TwinProperty.ClassType, someDesiredProperties.ClassType?.ToString());
            }

            if (reportedProperties is { } someReportedProperties)
            {
                SetReportedPropertyIfExists(twin, TwinProperty.FCntUp, someReportedProperties.FCntUp?.ToString(CultureInfo.InvariantCulture));
                SetReportedPropertyIfExists(twin, TwinProperty.FCntUpStart, someReportedProperties.FCntUpStart?.ToString(CultureInfo.InvariantCulture));
                SetReportedPropertyIfExists(twin, TwinProperty.FCntDown, someReportedProperties.FCntDown?.ToString(CultureInfo.InvariantCulture));
                SetReportedPropertyIfExists(twin, TwinProperty.FCntDownStart, someReportedProperties.FCntDownStart?.ToString(CultureInfo.InvariantCulture));
                SetReportedPropertyIfExists(twin, TwinProperty.AppSKey, someReportedProperties.AppSessionKey?.ToString());
                SetReportedPropertyIfExists(twin, TwinProperty.NwkSKey, someReportedProperties.NetworkSessionKey?.ToString());
                SetReportedPropertyIfExists(twin, TwinProperty.DevAddr, someReportedProperties.DevAddr?.ToString());
                SetReportedPropertyIfExists(twin, TwinProperty.DevNonce, someReportedProperties.DevNonce?.ToString());
                SetReportedPropertyIfExists(twin, TwinProperty.FCntResetCounter, someReportedProperties.FCntResetCounter?.ToString(CultureInfo.InvariantCulture));
                SetReportedPropertyIfExists(twin, TwinProperty.PreferredGatewayID, someReportedProperties.PreferredGatewayId?.ToString());
                SetReportedPropertyIfExists(twin, TwinProperty.Region, someReportedProperties.Region?.ToString());
                SetReportedPropertyIfExists(twin, TwinProperty.NetId, someReportedProperties.NetId?.ToString());
                SetReportedPropertyIfExists(twin, TwinProperty.RX2DataRate, someReportedProperties.Rx2DataRate?.ToString());
                SetReportedPropertyIfExists(twin, TwinProperty.LastProcessingStationEui, someReportedProperties.LastProcessingStation?.ToString());
            }

            return twin;

            static void SetDesiredPropertyIfExists(Twin twin, string key, string? value) =>
                SetPropertyIfExists(twin.Properties.Desired, key, value);

            static void SetReportedPropertyIfExists(Twin twin, string key, string? value) =>
                SetPropertyIfExists(twin.Properties.Reported, key, value);

            static void SetPropertyIfExists(TwinCollection twinCollection, string key, string? value)
            {
                if (value is { } someValue)
                {
                    twinCollection[key] = value;
                }
            }
        }
    }

    public sealed record LoRaDesiredTwinProperties
    {
        public DevEui? DevEui { get; init; }
        public DevAddr? DevAddr { get; init; }
        public JoinEui? JoinEui { get; init; }
        public AppKey? AppKey { get; init; }
        public AppSessionKey? AppSessionKey { get; init; }
        public NetworkSessionKey? NetworkSessionKey { get; init; }
        public string? GatewayId { get; init; }
        public string? SensorDecoder { get; init; }
        public bool? Supports32BitFCnt { get; init; }
        public bool? AbpRelaxMode { get; init; }
        public uint? FCntUpStart { get; init; }
        public uint? FCntDownStart { get; init; }
        public int? FCntResetCounter { get; init; }
        public int? Rx1DROffset { get; init; }
        public DataRateIndex? Rx2DataRate { get; init; }
        public int? PreferredWindow { get; init; }
        public int? RxDelay { get; init; }
        public char? ClassType { get; init; }
    }

    public sealed record LoRaReportedTwinProperties
    {
        public uint? FCntUp { get; init; }
        public uint? FCntUpStart { get; init; }
        public uint? FCntDown { get; init; }
        public uint? FCntDownStart { get; init; }
        public AppSessionKey? AppSessionKey { get; init; }
        public NetworkSessionKey? NetworkSessionKey { get; init; }
        public DevAddr? DevAddr { get; init; }
        public DevNonce? DevNonce { get; init; }
        public int? FCntResetCounter { get; init; }
        public string? PreferredGatewayId { get; init; }
        public LoRaRegionType? Region { get; init; }
        public NetId? NetId { get; init; }
        public DataRateIndex? Rx2DataRate { get; init; }
        public StationEui? LastProcessingStation { get; init; }
    }

    public static class LoRaDeviceTwinExtensions
    {
        public static LoRaDesiredTwinProperties GetOtaaDesiredTwinProperties(this TestDeviceInfo testDeviceInfo) =>
            new LoRaDesiredTwinProperties
            {
                DevEui = testDeviceInfo.DevEui,
                JoinEui = testDeviceInfo.AppEui ?? throw new InvalidOperationException($"{nameof(testDeviceInfo.AppEui)} must not be null."),
                AppKey = testDeviceInfo.AppKey ?? throw new InvalidOperationException($"{nameof(testDeviceInfo.AppKey)} must not be null."),
                SensorDecoder = testDeviceInfo.SensorDecoder ?? throw new InvalidOperationException($"{nameof(testDeviceInfo.SensorDecoder)} must not be null."),
                ClassType = testDeviceInfo.ClassType,
                GatewayId = testDeviceInfo.GatewayID,
            };

        public static LoRaReportedTwinProperties GetOtaaReportedTwinProperties(this SimulatedDevice simulatedDevice) =>
            new LoRaReportedTwinProperties
            {
                DevAddr = simulatedDevice.DevAddr,
                AppSessionKey = simulatedDevice.AppSKey,
                NetworkSessionKey = simulatedDevice.NwkSKey,
                DevNonce = simulatedDevice.DevNonce,
                NetId = simulatedDevice.NetId,
                FCntDown = simulatedDevice.FrmCntDown,
                FCntUp = simulatedDevice.FrmCntUp,
            };

        public static LoRaDesiredTwinProperties GetAbpDesiredTwinProperties(this TestDeviceInfo testDeviceInfo) =>
            new LoRaDesiredTwinProperties
            {
                AppSessionKey = testDeviceInfo.AppSKey ?? throw new InvalidOperationException($"{nameof(testDeviceInfo.AppSKey)} must not be null."),
                NetworkSessionKey = testDeviceInfo.NwkSKey ?? throw new InvalidOperationException($"{nameof(testDeviceInfo.NwkSKey)} must not be null."),
                DevAddr = testDeviceInfo.DevAddr ?? throw new InvalidOperationException($"{nameof(testDeviceInfo.DevAddr)} must not be null."),
                SensorDecoder = testDeviceInfo.SensorDecoder ?? throw new InvalidOperationException($"{nameof(testDeviceInfo.SensorDecoder)} must not be null."),
                ClassType = testDeviceInfo.ClassType,
                GatewayId = testDeviceInfo.GatewayID,
            };
    }
}
