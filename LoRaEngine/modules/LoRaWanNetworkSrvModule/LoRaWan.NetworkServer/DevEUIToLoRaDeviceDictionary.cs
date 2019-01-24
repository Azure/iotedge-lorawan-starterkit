// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
namespace LoRaWan.NetworkServer
{
    using System.Collections.Concurrent;

    /// <summary>
    /// Dictionary of <see cref="LoRaDevice"/> where key is the DevEUI
    /// </summary>
    public class DevEUIToLoRaDeviceDictionary : ConcurrentDictionary<string, LoRaDevice>
    {
    }
}