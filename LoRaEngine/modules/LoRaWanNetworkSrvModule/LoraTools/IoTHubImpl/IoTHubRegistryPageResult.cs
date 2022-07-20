// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaTools.IoTHubImpl
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices;

    public abstract class IoTHubRegistryPageResult<TResultType> : IRegistryPageResult<TResultType>
        where TResultType : class
    {
        protected IQuery OriginalQuery { get; }

        public bool HasMoreResults => this.OriginalQuery.HasMoreResults;

        protected IoTHubRegistryPageResult(IQuery originalQuery)
        {
            this.OriginalQuery = originalQuery;
        }

        public abstract Task<IEnumerable<TResultType>> GetNextPageAsync();
    }
}
