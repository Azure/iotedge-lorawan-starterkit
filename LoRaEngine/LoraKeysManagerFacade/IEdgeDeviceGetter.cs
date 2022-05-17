// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoraKeysManagerFacade
{
    using System.Threading;
    using System.Threading.Tasks;

    public interface IEdgeDeviceGetter
    {
        public Task<bool> IsEdgeDeviceAsync(string lnsId, CancellationToken cancellationToken);
    }
}
