// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoraKeysManagerFacade
{
    using System;

    public class DevAddrCacheInfo : IoTHubDeviceInfo, IComparable
    {
        public string GatewayId { get; set; }

        public DateTime LastUpdatedTwins { get; set; }

        public string NwkSKey { get; set; }

        public int CompareTo(object obj) => Equals(obj) ? 0 : 1;

        public override bool Equals(object obj) =>
            obj is DevAddrCacheInfo info &&
            DevAddr == info.DevAddr &&
            DevEUI == info.DevEUI &&
            PrimaryKey == info.PrimaryKey &&
            GatewayId == info.GatewayId &&
            LastUpdatedTwins == info.LastUpdatedTwins &&
            NwkSKey == info.NwkSKey;

        public override int GetHashCode() =>
            HashCode.Combine(DevAddr, DevEUI, PrimaryKey, GatewayId, LastUpdatedTwins, NwkSKey);

        public static bool operator ==(DevAddrCacheInfo left, DevAddrCacheInfo right) =>
            left is null ? right is null : left.Equals(right);

        public static bool operator !=(DevAddrCacheInfo left, DevAddrCacheInfo right) =>
            !(left == right);

        public static bool operator <(DevAddrCacheInfo left, DevAddrCacheInfo right) =>
            ReferenceEquals(left, null) ? !ReferenceEquals(right, null) : left.CompareTo(right) < 0;

        public static bool operator <=(DevAddrCacheInfo left, DevAddrCacheInfo right) =>
            ReferenceEquals(left, null) || left.CompareTo(right) <= 0;

        public static bool operator >(DevAddrCacheInfo left, DevAddrCacheInfo right) =>
            !ReferenceEquals(left, null) && left.CompareTo(right) > 0;

        public static bool operator >=(DevAddrCacheInfo left, DevAddrCacheInfo right) =>
            ReferenceEquals(left, null) ? ReferenceEquals(right, null) : left.CompareTo(right) >= 0;
    }
}
