// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoraKeysManagerFacade.IoTHubImp
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices;

    internal sealed class DeviceTwinPageResult : IoTHubRegistryPageResult<IDeviceTwin>, IRegistryPageResult<IDeviceTwin>
    {
        public DeviceTwinPageResult(IQuery originalQuery)
            : base(originalQuery)
        {
        }

        public override async Task<IEnumerable<IDeviceTwin>> GetNextPageAsync()
        {
            var page = await this.OriginalQuery.GetNextAsTwinAsync();

            return page.Select(c => new IoTHubDeviceTwin(c));
        }
    }
}
