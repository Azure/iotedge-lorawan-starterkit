// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer
{
    using System.Collections.Generic;

    /// <summary>
    /// Results of a <see cref="LoRaDeviceAPIServiceBase.SearchDevicesAsync"/> call.
    /// </summary>
    public class SearchDevicesResult
    {
        /// <summary>
        /// Gets list of devices that match the criteria.
        /// </summary>
        public IReadOnlyList<IoTHubDeviceInfo> Devices { get; }

        /// <summary>
        /// Gets or sets a value indicating whether the dev nonce was already used.
        /// </summary>
        public bool IsDevNonceAlreadyUsed { get; set; }

        public string RefusedMessage { get; set; }

        public SearchDevicesResult()
        {
        }

        public SearchDevicesResult(IReadOnlyList<IoTHubDeviceInfo> devices)
        {
            Devices = devices;
        }
    }
}
