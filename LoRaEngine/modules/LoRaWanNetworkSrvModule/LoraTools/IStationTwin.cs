// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaTools
{
    public interface IStationTwin : IDeviceTwin
    {
        string NetworkId { get; }
    }
}
