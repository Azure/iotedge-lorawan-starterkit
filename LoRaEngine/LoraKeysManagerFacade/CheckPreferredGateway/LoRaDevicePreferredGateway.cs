// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoraKeysManagerFacade
{
    using System;
    using LoRaWan;

    /// <summary>
    /// Contains the preferred gateway for a class C device
    /// This information is kept in cache where key is "preferredGateway:{devEUI}".
    /// </summary>
    public class LoRaDevicePreferredGateway
    {
        public string GatewayID { get; set; }

        public uint FcntUp { get; set; }

        public long UpdateTime { get; set; }

        public LoRaDevicePreferredGateway()
        {
        }

        public LoRaDevicePreferredGateway(string gatewayID, uint fcntUp)
        {
            GatewayID = gatewayID;
            FcntUp = fcntUp;
            UpdateTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        }

        /// <summary>
        /// Creates a string representation of the object for caching.
        /// </summary>
        /// <returns>A string containing {GatewayID};{FcntUp};{UpdateTime}.</returns>
        private string ToCachedString() => string.Concat(GatewayID, ";", FcntUp, ";", UpdateTime);

        /// <summary>
        /// Creates a <see cref="LoRaDevicePreferredGateway"/> from a string
        /// String format should be: {GatewayID};{FcntUp};{UpdateTime}.
        /// </summary>
        private static LoRaDevicePreferredGateway CreateFromCachedString(string cachedString)
        {
            if (string.IsNullOrEmpty(cachedString))
                return null;

            var values = cachedString.Split(';');
            if (values?.Length != 3)
                return null;

            if (!uint.TryParse(values[1], out var fcntUp))
                return null;

            if (!long.TryParse(values[2], out var updateTime))
                return null;

            return new LoRaDevicePreferredGateway()
            {
                GatewayID = values[0],
                FcntUp = fcntUp,
                UpdateTime = updateTime,
            };
        }

        internal static string PreferredGatewayCacheKey(DevEui devEUI) => FormattableString.Invariant($"preferredGateway:{devEUI}");

        internal static string PreferredGatewayFcntUpItemListCacheKey(DevEui devEUI, uint fcntUp) => FormattableString.Invariant($"preferredGateway:{devEUI}:{fcntUp}");

        internal static LoRaDevicePreferredGateway LoadFromCache(ILoRaDeviceCacheStore cacheStore, DevEui devEUI)
        {
            return CreateFromCachedString(cacheStore.StringGet(PreferredGatewayCacheKey(devEUI)));
        }

        internal static bool SaveToCache(ILoRaDeviceCacheStore cacheStore, DevEui devEUI, LoRaDevicePreferredGateway preferredGateway, bool onlyIfNotExists = false)
        {
            return cacheStore.StringSet(PreferredGatewayCacheKey(devEUI), preferredGateway.ToCachedString(), null, onlyIfNotExists);
        }
    }
}
