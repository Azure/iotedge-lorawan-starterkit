// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaTools.IoTHubImpl
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices;

    public class IoTHubLoRaDeviceTwinPageResult : IoTHubRegistryPageResult<ILoRaDeviceTwin>
    {
        public IoTHubLoRaDeviceTwinPageResult(IQuery originalQuery)
            : base(originalQuery)
        {
        }

        public override async Task<IEnumerable<ILoRaDeviceTwin>> GetNextPageAsync()
        {
            var page = await this.OriginalQuery.GetNextAsTwinAsync();

            return page.Select(c => new IoTHubLoRaDeviceTwin(c));
        }
    }
}
