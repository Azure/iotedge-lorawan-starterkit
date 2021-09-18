// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoraKeysManagerFacade.IoTHubImp
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices;

    internal abstract class IoTHubRegistryPageResult<TResultType> : IRegistryPageResult<TResultType>
        where TResultType : class
    {
        private readonly IQuery originalQuery;

        protected IQuery OriginalQuery => this.originalQuery;

        public bool HasMoreResults => this.originalQuery.HasMoreResults;

        public IoTHubRegistryPageResult(IQuery originalQuery)
        {
            this.originalQuery = originalQuery;
        }

        public abstract Task<IEnumerable<TResultType>> GetNextPageAsync();
    }
}
