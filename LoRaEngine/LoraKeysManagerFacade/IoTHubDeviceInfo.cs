// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoraKeysManagerFacade
{
    using LoRaWan;
    using Newtonsoft.Json;

    public class IoTHubDeviceInfo
    {
        public int NetworkId { get; set; }

        public int NetworkAddress { get; set; }

        [JsonIgnore]
        public DevAddr DevAddr
        {
            get => new DevAddr(NetworkId, NetworkAddress);
            set
            {
                NetworkId = value.NetworkId;
                NetworkAddress = value.NetworkAddress;
            }
        }

        public string DevEUI { get; set; }

        public string PrimaryKey { get; set; }
    }
}
