// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoraKeysManagerFacade
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    public interface IEdgeDeviceGetter
    {
        Task<bool> IsEdgeDeviceAsync(string lnsId, CancellationToken cancellationToken);
        Task<ICollection<string>> ListEdgeDevicesAsync(CancellationToken cancellationToken);
    }
}
