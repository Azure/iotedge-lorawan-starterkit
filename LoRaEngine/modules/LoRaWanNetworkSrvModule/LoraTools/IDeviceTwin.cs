// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaTools
{
    using Microsoft.Azure.Devices.Shared;

    public interface IDeviceTwin
    {
        string ETag { get; }

        TwinProperties Properties { get; }

        TwinCollection Tags { get; }

        string GetGatewayID();
    }
}
