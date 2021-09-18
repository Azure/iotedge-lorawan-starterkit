// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoraKeysManagerFacade
{
    public interface IDevice
    {
        string DeviceId { get; }

        string PrimaryKey { get; }
    }
}
