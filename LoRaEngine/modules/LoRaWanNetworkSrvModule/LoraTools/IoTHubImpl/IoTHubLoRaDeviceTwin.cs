// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaTools.IoTHubImpl
{
    using LoRaTools.Utils;
    using Microsoft.Azure.Devices.Shared;

    internal class IoTHubLoRaDeviceTwin : IoTHubDeviceTwin, ILoRaDeviceTwin
    {
        public IoTHubLoRaDeviceTwin(Twin twin) : base(twin)
        {
        }

        public string GetGatewayID()
            => this.Properties.Desired.TryRead<string>(TwinPropertiesConstants.GatewayID, null, out var someGatewayId)
             ? someGatewayId
             : string.Empty;

        public string GetNwkSKey()
        {
            return this.Properties.Desired.TryRead(TwinPropertiesConstants.NwkSKey, null, out string nwkSKey)
                ? nwkSKey
                : this.Properties.Reported.TryRead(TwinPropertiesConstants.NwkSKey, null, out nwkSKey)
                ? nwkSKey
                : null;
        }
    }
}
