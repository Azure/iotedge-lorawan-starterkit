// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoraKeysManagerFacade.IoTHubImp
{
    using System;
    using Microsoft.Azure.Devices.Shared;

    internal class IoTHubDeviceTwin : IDeviceTwin
    {
        private readonly Twin twin;

        public IoTHubDeviceTwin(Twin twin)
        {
            this.twin = twin;
        }

        public string DeviceId => this.twin.DeviceId;

        public string GetDevAddr()
        {
            if (this.twin.Properties.Desired.Contains(LoraKeysManagerFacadeConstants.TwinProperty_DevAddr))
            {
                return this.twin.Properties.Desired[LoraKeysManagerFacadeConstants.TwinProperty_DevAddr].Value as string;
            }
            else if (this.twin.Properties.Reported.Contains(LoraKeysManagerFacadeConstants.TwinProperty_DevAddr))
            {
                return this.twin.Properties.Reported[LoraKeysManagerFacadeConstants.TwinProperty_DevAddr].Value as string;
            }

            return string.Empty;
        }

        public string GetGatewayID()
        {
            return this.twin.GetGatewayID();
        }

        public DateTime GetLastUpdated()
        {
            return this.twin.Properties.Reported.GetLastUpdated();
        }

        public string GetNwkSKey()
        {
            return this.twin.GetNwkSKey();
        }
    }
}
