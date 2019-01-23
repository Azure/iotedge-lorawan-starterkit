// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer
{
    /// <summary>
    /// Defines a <see cref="LoRaDevice"/> initializer
    /// A concrete implementation is the frame counter initializer
    /// </summary>
    public interface ILoRaDeviceInitializer
    {
        void Initialize(LoRaDevice loRaDevice);
    }
}