// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoraKeysManagerFacade
{
    using System;
    using System.Collections.Generic;
    using System.Text;

    public class DevAddrCacheInfo : IoTHubDeviceInfo, IComparable
    {
        public string GatewayId { get; set; }

        public DateTime LastUpdatedTwins { get; set; }

        public int CompareTo(object obj)
        {
            if (obj is DevAddrCacheInfo)
            {
                var oldElement = (DevAddrCacheInfo)obj;
                if (this.GatewayId == oldElement.GatewayId
                                && this.DevAddr == oldElement.DevAddr
                                && this.DevEUI == oldElement.DevEUI
                                && this.LastUpdatedTwins == oldElement.LastUpdatedTwins)
                {
                    return 0;
                }
            }

            return 1;
        }
    }
}
