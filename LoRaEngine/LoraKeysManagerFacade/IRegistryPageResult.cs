// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

namespace LoraKeysManagerFacade
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    public interface IRegistryPageResult<TResult> : IDisposable
    {
        Task<IEnumerable<TResult>> GetNextPageAsync();

        bool HasMoreResults { get; }
    }
}
