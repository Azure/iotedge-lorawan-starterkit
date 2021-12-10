// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoraKeysManagerFacade.IoTHubImp
{
    using System;
    using LoRaWan;
    using Microsoft.Azure.Devices.Shared;

    public sealed class IoTHubDeviceTwin : IDeviceTwin
    {
        private readonly Twin twin;
        private readonly DevAddr devAddr;

        public IoTHubDeviceTwin(Twin twin)
        {
            this.twin = twin;

            var rawDevAddr = string.Empty;

            if (this.twin.Properties.Desired.Contains(LoraKeysManagerFacadeConstants.TwinProperty_DevAddr))
            {
                rawDevAddr = this.twin.Properties.Desired[LoraKeysManagerFacadeConstants.TwinProperty_DevAddr].Value;
            }
            else if (this.twin.Properties.Reported.Contains(LoraKeysManagerFacadeConstants.TwinProperty_DevAddr))
            {
                rawDevAddr = this.twin.Properties.Reported[LoraKeysManagerFacadeConstants.TwinProperty_DevAddr].Value;
            }

            if (!DevAddr.TryParse(rawDevAddr, out devAddr))
            {
                throw new LoRaProcessingException($"Dev addr '{rawDevAddr}' is invalid.", LoRaProcessingErrorCode.InvalidFormat);
            }
        }

        public string DeviceId => this.twin.DeviceId;

        public DevAddr DevAddr => this.devAddr;

        public string GatewayID => this.twin.GetGatewayID();

        public DateTime LastUpdated => this.twin.Properties.Reported.GetLastUpdated();

        public string NwkSKey => this.twin.GetNwkSKey();
    }
}
