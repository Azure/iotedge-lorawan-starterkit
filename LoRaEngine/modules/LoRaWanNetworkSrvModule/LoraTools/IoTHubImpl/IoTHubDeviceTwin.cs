// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaTools.IoTHubImpl
{
    using System;
    using LoRaTools.Utils;
    using Microsoft.Azure.Devices.Shared;

    public class IoTHubDeviceTwin : IDeviceTwin
    {
        internal Twin TwinInstance { get; }

        public TwinProperties Properties => this.TwinInstance.Properties;

        public TwinCollection Tags => this.TwinInstance.Tags;

        public IoTHubDeviceTwin(Twin twin)
        {
            this.TwinInstance = twin;
        }

        public string GetGatewayID()
            => TwinInstance.Properties.Desired.TryRead<string>(TwinPropertiesConstants.GatewayID, null, out var someGatewayId)
             ? someGatewayId
             : string.Empty;

        public string ETag => this.TwinInstance.ETag;

        public string DeviceId => this.TwinInstance.DeviceId;

        public override bool Equals(object obj)
        {
            ArgumentNullException.ThrowIfNull(obj, nameof(obj));

            if (obj is not IoTHubDeviceTwin)
            {
                return false;
            }

            return (obj as IoTHubDeviceTwin)?.TwinInstance == this.TwinInstance;
        }

        public override int GetHashCode()
        {
            return TwinInstance.GetHashCode();
        }

        public string GetNwkSKey()
        {
            if (TwinInstance.Properties.Desired.TryRead(TwinPropertiesConstants.NwkSKey, null, out string nwkSKey))
            {
                return nwkSKey;
            }
            else if (TwinInstance.Properties.Reported.TryRead(TwinPropertiesConstants.NwkSKey, null, out nwkSKey))
            {
                return nwkSKey;
            }
            else
            {
                return null;
            }
        }
    }
}
