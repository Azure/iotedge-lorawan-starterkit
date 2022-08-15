// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaTools.IoTHubImpl
{
    using System;
    using Microsoft.Azure.Devices.Shared;

    public class IoTHubDeviceTwin : IDeviceTwin
    {
        internal Twin TwinInstance { get; }

        public TwinProperties Properties => this.TwinInstance.Properties;

        public IoTHubDeviceTwin(Twin twin)
        {
            ArgumentNullException.ThrowIfNull(twin, nameof(twin));

            this.TwinInstance = twin;
        }

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
    }
}
