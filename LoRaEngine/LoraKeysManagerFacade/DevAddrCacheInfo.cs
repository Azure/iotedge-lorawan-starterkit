// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoraKeysManagerFacade
{
    using System;
    using System.Collections.Generic;
    using System.Text;

    public class DevAddrCacheInfo : IoTHubDeviceInfo
    {
        public string GatewayId { get; set; }

        internal bool IsEqual(DevAddrCacheInfo oldElement)
        {
            return this.GatewayId == oldElement.GatewayId
                && this.DevAddr == oldElement.DevAddr
                && this.DevEUI == oldElement.DevEUI;
        }
    }
}
