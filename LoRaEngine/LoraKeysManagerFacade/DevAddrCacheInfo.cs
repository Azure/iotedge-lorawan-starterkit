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

        public int CompareTo(object obj) => this.Equals(obj) ? 0 : 1;

        public override bool Equals(object obj) =>
            obj is DevAddrCacheInfo info &&
            this.DevAddr == info.DevAddr &&
            this.DevEUI == info.DevEUI &&
            this.PrimaryKey == info.PrimaryKey &&
            this.GatewayId == info.GatewayId &&
            this.LastUpdatedTwins == info.LastUpdatedTwins &&
            this.NwkSKey == info.NwkSKey;

        public override int GetHashCode() =>
            HashCode.Combine(this.DevAddr, this.DevEUI, this.PrimaryKey, this.GatewayId, this.LastUpdatedTwins, this.NwkSKey);

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
