//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//
using System.Collections.Generic;

namespace LoRaWan.NetworkServer
{
    /// <summary>
    /// Results of a <see cref="LoRaDeviceAPIServiceBase.SearchDevicesAsync"/> call
    /// </summary>
    public class SearchDevicesResult
    {
        /// <summary>
        /// List of devices that match the criteria
        /// </summary>
        public IReadOnlyList<IoTHubDeviceInfo> Devices { get; }

        /// <summary>
        /// Indicates dev nonce already used
        /// </summary>
        public bool IsDevNonceAlreadyUsed { get; set; }

        public SearchDevicesResult()
        {

        }

        public SearchDevicesResult(IReadOnlyList<IoTHubDeviceInfo> devices)
        {
            this.Devices = devices;
        }
    }
}
