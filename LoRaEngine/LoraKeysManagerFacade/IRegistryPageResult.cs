// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoraKeysManagerFacade
{
    using System.Collections.Generic;
    using System.Threading.Tasks;

    public interface IRegistryPageResult<TResult>
        where TResult : class
    {
        Task<IEnumerable<TResult>> GetNextPageAsync();

        bool HasMoreResults { get; }
    }
}
