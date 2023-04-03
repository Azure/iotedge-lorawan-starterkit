// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaTools.IoTHubImpl
{
    using System;
    using Microsoft.Azure.Devices.Shared;

    public class IoTHubTwinProperties : ITwinProperties
    {
        private readonly TwinCollection twinCollection;

        public long Version => this.twinCollection.Version;

        public dynamic this[string propertyName] { get => this.twinCollection[propertyName]; set => this.twinCollection[propertyName] = value; }

        public IoTHubTwinProperties(TwinCollection twinCollection)
        {
            this.twinCollection = twinCollection;
        }

        public DateTime GetLastUpdated() =>
            this.twinCollection.GetLastUpdated();

        public Metadata GetMetadata() => this.twinCollection.GetMetadata();

        public bool ContainsKey(string propertyName)
            => this.twinCollection.Contains(propertyName);

        public bool TryGetValue(string propertyName, out object item)
        {
            item = null;

            if (!this.twinCollection.Contains(propertyName))
                return false;

            item = this.twinCollection[propertyName];

            return true;
        }
    }
}
