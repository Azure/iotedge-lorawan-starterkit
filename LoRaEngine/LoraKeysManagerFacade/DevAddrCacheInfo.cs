// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoraKeysManagerFacade
{
    using System;

    public class DevAddrCacheInfo : IoTHubDeviceInfo
    {
        public string GatewayId { get; set; }

        public DateTime LastUpdatedTwins { get; set; }

        public string NwkSKey { get; set; }
    }
}
