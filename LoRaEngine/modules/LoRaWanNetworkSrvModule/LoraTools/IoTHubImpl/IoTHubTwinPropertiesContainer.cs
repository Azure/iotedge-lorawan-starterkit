// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaTools.IoTHubImpl
{
    using Microsoft.Azure.Devices.Shared;

    public class IoTHubTwinPropertiesContainer : ITwinPropertiesContainer
    {
        public ITwinProperties Desired { get; }

        public ITwinProperties Reported { get; }

        public IoTHubTwinPropertiesContainer(Twin twin)
        {
            this.Desired = new IoTHubTwinProperties(twin?.Properties?.Desired ?? new TwinCollection());
            this.Reported = new IoTHubTwinProperties(twin?.Properties?.Reported ?? new TwinCollection());
        }
    }
}
