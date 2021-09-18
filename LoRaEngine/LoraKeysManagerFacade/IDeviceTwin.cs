// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoraKeysManagerFacade
{
    using System;

    public interface IDeviceTwin
    {
        string DeviceId { get; }

        string GetGatewayID();

        string GetNwkSKey();

        string GetDevAddr();

        DateTime GetLastUpdated();
    }
}
