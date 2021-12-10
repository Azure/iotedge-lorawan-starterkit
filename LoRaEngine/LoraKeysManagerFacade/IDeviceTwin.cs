// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoraKeysManagerFacade
{
    using System;
    using LoRaWan;

    public interface IDeviceTwin
    {
        string DeviceId { get; }

        string GatewayID { get; }

        string NwkSKey { get; }

        DevAddr DevAddr { get; }

        DateTime LastUpdated { get; }
    }
}
