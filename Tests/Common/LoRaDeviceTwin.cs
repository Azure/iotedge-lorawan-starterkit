// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

namespace LoRaWan.Tests.Common
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using LoRaTools.Regions;
    using LoRaWan.NetworkServer;
    using Microsoft.Azure.Devices.Shared;

    public static class LoRaDeviceTwin
    {
        public static Twin Create(LoRaDesiredTwinProperties? desiredProperties = null, LoRaReportedTwinProperties? reportedProperties = null)
        {
            var twin = new Twin();

            var zeroProperties = Enumerable.Empty<KeyValuePair<string, object?>>();

            var properties =
                from ps in new[]
                {
                    from e in desiredProperties ?? zeroProperties
                    select (twin.Properties.Desired, e.Key, e.Value),
                    from e in reportedProperties ?? zeroProperties
                    select (twin.Properties.Reported, e.Key, e.Value),
                }
                from p in ps
                select p;

            foreach (var (target, key, value) in properties)
            {
#pragma warning disable IDE0010 // Add missing cases (false positive)
                switch (value)
                {
                    case null:
                        break;
                    case uint or int or bool or (Enum and not (LoRaRegionType or LoRaDeviceClassType)):
                        target[key] = value;
                        break;
                    case IConvertible convertible:
                        target[key] = convertible.ToString(CultureInfo.InvariantCulture);
                        break;
                    case var obj:
                        target[key] = obj.ToString();
                        break;
                }
#pragma warning restore IDE0010 // Add missing cases
            }

            return twin;
        }
    }

    public abstract record LoRaTwinProperties : IEnumerable<KeyValuePair<string, object?>>
    {
        public abstract IEnumerator<KeyValuePair<string, object?>> GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    public sealed record LoRaDesiredTwinProperties : LoRaTwinProperties
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
        public uint? FCntResetCounter { get; init; }
        public int? Rx1DROffset { get; init; }
        public DataRateIndex? Rx2DataRate { get; init; }
        public ReceiveWindowNumber? PreferredWindow { get; init; }
        public int? RxDelay { get; init; }
        public LoRaDeviceClassType? ClassType { get; init; }
        public TimeSpan? KeepAliveTimeout { get; init; }
        public uint? Version { get; init; }
        public bool? DownlinkEnabled { get; init; }
        public int? Cn470JoinChannel { get; init; }

        public override IEnumerator<KeyValuePair<string, object?>> GetEnumerator()
        {
            yield return KeyValuePair.Create("DevEUI", (object?)DevEui);
            yield return KeyValuePair.Create("DevAddr", (object?)DevAddr);
            yield return KeyValuePair.Create("AppEUI", (object?)JoinEui);
            yield return KeyValuePair.Create("AppKey", (object?)AppKey);
            yield return KeyValuePair.Create("AppSKey", (object?)AppSessionKey);
            yield return KeyValuePair.Create("NwkSKey", (object?)NetworkSessionKey);
            yield return KeyValuePair.Create("GatewayID", (object?)GatewayId);
            yield return KeyValuePair.Create("SensorDecoder", (object?)SensorDecoder);
            yield return KeyValuePair.Create("Supports32BitFCnt", (object?)Supports32BitFCnt);
            yield return KeyValuePair.Create("ABPRelaxMode", (object?)AbpRelaxMode);
            yield return KeyValuePair.Create("FCntUpStart", (object?)FCntUpStart);
            yield return KeyValuePair.Create("FCntDownStart", (object?)FCntDownStart);
            yield return KeyValuePair.Create("FCntResetCounter", (object?)FCntResetCounter);
            yield return KeyValuePair.Create("RX1DROffset", (object?)Rx1DROffset);
            yield return KeyValuePair.Create("RX2DataRate", (object?)Rx2DataRate);
            yield return KeyValuePair.Create("PreferredWindow", PreferredWindow switch
            {
                ReceiveWindowNumber.ReceiveWindow1 => 1,
                ReceiveWindowNumber.ReceiveWindow2 => 2,
                _ => (object?)null
            });
            yield return KeyValuePair.Create("RXDelay", (object?)RxDelay);
            yield return KeyValuePair.Create("ClassType", (object?)ClassType);
            yield return KeyValuePair.Create("KeepAliveTimeout", (object?)KeepAliveTimeout?.TotalSeconds);
            yield return KeyValuePair.Create("$version", (object?)Version);
            yield return KeyValuePair.Create("Downlink", (object?)DownlinkEnabled);
            yield return KeyValuePair.Create("CN470JoinChannel", (object?)Cn470JoinChannel);
        }
    }

    public sealed record LoRaReportedTwinProperties : LoRaTwinProperties
    {
        public uint? FCntUp { get; init; }
        public uint? FCntUpStart { get; init; }
        public uint? FCntDown { get; init; }
        public uint? FCntDownStart { get; init; }
        public AppSessionKey? AppSessionKey { get; init; }
        public NetworkSessionKey? NetworkSessionKey { get; init; }
        public DevAddr? DevAddr { get; init; }
        public DevNonce? DevNonce { get; init; }
        public uint? FCntResetCounter { get; init; }
        public string? PreferredGatewayId { get; init; }
        public LoRaRegionType? Region { get; init; }
        public NetId? NetId { get; init; }
        public DataRateIndex? Rx2DataRate { get; init; }
        public StationEui? LastProcessingStation { get; init; }
        public uint? Version { get; init; }
        public int? Cn470JoinChannel { get; init; }

        public override IEnumerator<KeyValuePair<string, object?>> GetEnumerator()
        {
            yield return KeyValuePair.Create("FCntUp", (object?)FCntUp);
            yield return KeyValuePair.Create("FCntUpStart", (object?)FCntUpStart);
            yield return KeyValuePair.Create("FCntDown", (object?)FCntDown);
            yield return KeyValuePair.Create("FCntDownStart", (object?)FCntDownStart);
            yield return KeyValuePair.Create("AppSKey", (object?)AppSessionKey);
            yield return KeyValuePair.Create("NwkSKey", (object?)NetworkSessionKey);
            yield return KeyValuePair.Create("DevAddr", (object?)DevAddr);
            yield return KeyValuePair.Create("DevNonce", (object?)DevNonce);
            yield return KeyValuePair.Create("FCntResetCounter", (object?)FCntResetCounter);
            yield return KeyValuePair.Create("PreferredGatewayID", (object?)PreferredGatewayId);
            yield return KeyValuePair.Create("Region", (object?)Region);
            yield return KeyValuePair.Create("NetId", (object?)NetId);
            yield return KeyValuePair.Create("RX2DataRate", (object?)Rx2DataRate);
            yield return KeyValuePair.Create("LastProcessingStationEui", (object?)LastProcessingStation);
            yield return KeyValuePair.Create("$version", (object?)Version);
            yield return KeyValuePair.Create("CN470JoinChannel", (object?)Cn470JoinChannel);
        }
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

        public static LoRaReportedTwinProperties GetAbpReportedTwinProperties(this SimulatedDevice simulatedDevice) =>
            new LoRaReportedTwinProperties
            {
                FCntDown = simulatedDevice.FrmCntDown,
                FCntUp = simulatedDevice.FrmCntUp,
            };

        public static Twin GetDefaultAbpTwin(this SimulatedDevice simulatedDevice) =>
            LoRaDeviceTwin.Create(simulatedDevice.LoRaDevice.GetAbpDesiredTwinProperties(), simulatedDevice.GetAbpReportedTwinProperties());
    }
}
