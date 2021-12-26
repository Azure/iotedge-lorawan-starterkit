// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoraKeysManagerFacade.IoTHubImp
{
    using System;
    using LoRaTools.Utils;
    using LoRaWan;
    using Microsoft.Azure.Devices.Shared;

    public sealed class IoTHubDeviceTwin : IDeviceTwin
    {
        private readonly Twin twin;
        private readonly DevAddr devAddr;

        public IoTHubDeviceTwin(Twin twin)
        {
            this.twin = twin;

            if (twin == null)
            {
                throw new ArgumentNullException(nameof(twin));
            }

            if (!twin.Properties.Desired.TryRead(LoraKeysManagerFacadeConstants.TwinProperty_DevAddr, null, out this.devAddr))
            {
                _ = twin.Properties.Reported.TryRead(LoraKeysManagerFacadeConstants.TwinProperty_DevAddr, null, out this.devAddr);
            }
        }

        public string DeviceId => this.twin.DeviceId;

        public DevAddr DevAddr => this.devAddr;

        public string GatewayID => this.twin.GetGatewayID();

        public DateTime LastUpdated => this.twin.Properties.Reported.GetLastUpdated();

        public string NwkSKey => this.twin.GetNwkSKey();
    }
}
