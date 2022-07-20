// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaTools
{
    using System.Collections.Generic;
    using System.Threading.Tasks;

    public interface IRegistryPageResult<TResult>
    {
        Task<IEnumerable<TResult>> GetNextPageAsync();

        bool HasMoreResults { get; }
    }
}
