//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System.Collections.Concurrent;

namespace LoRaWan.NetworkServer
{
    /// <summary>
    /// Dictionary of <see cref="LoRaDevice"/> where key is the DevEUI
    /// </summary>
    public class DevEUIToLoRaDeviceDictionary : ConcurrentDictionary<string, LoRaDevice>
    {

    }
}