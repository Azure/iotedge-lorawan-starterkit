// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoraKeysManagerFacade
{
    public class PreferredGatewayTableItem
    {
        public string GatewayID { get; private set; }

        public double Rssi { get; private set; }

        public PreferredGatewayTableItem(string gatewayID, double rssi)
        {
            GatewayID = gatewayID;
            Rssi = rssi;
        }

        /// <summary>
        /// Creates a string representation of the object for caching.
        /// </summary>
        /// <returns>A string containing {GatewayID};{Rssi}.</returns>
        public string ToCachedString() => string.Concat(GatewayID, ";", Rssi);

        /// <summary>
        /// Creates a <see cref="PreferredGatewayTableItem"/> from a string
        /// String format should be: {GatewayID};{Rssi}.
        /// </summary>
        public static PreferredGatewayTableItem CreateFromCachedString(string cachedString)
        {
            if (string.IsNullOrEmpty(cachedString))
                return null;

            var values = cachedString.Split(';');
            if (values?.Length != 2)
                return null;

            if (!double.TryParse(values[1], out var rssi))
                return null;

            return new PreferredGatewayTableItem(values[0], rssi);
        }
    }
}
