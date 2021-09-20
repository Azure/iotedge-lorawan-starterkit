// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoraKeysManagerFacade.IoTCentralImp
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net.Http;
    using System.Threading.Tasks;
    using LoraKeysManagerFacade.IoTCentralImp.Definitions;

    internal class DeviceTwinPageResult : IRegistryPageResult<IDeviceTwin>
    {
        private readonly HttpClient client;
        private readonly Func<IDeviceTwin, bool> predicate;
        private readonly string apiVersion;

        private DeviceCollection currentResults;

        public DeviceTwinPageResult(HttpClient client, string apiVersion, Func<IDeviceTwin, bool> predicate)
        {
            this.client = client;
            this.apiVersion = apiVersion;
            this.predicate = predicate;
        }

        public bool HasMoreResults => this.currentResults == null || !string.IsNullOrEmpty(this.currentResults.NextLink);

        public async Task<IEnumerable<IDeviceTwin>> GetNextPageAsync()
        {
            var results = new List<IDeviceTwin>();
            if (!this.HasMoreResults)
            {
                return results;
            }

            HttpResponseMessage result = null;

            if (this.currentResults == null)
            {
                result = await this.client.GetAsync($"/api/devices?{this.apiVersion}");
            }
            else
            {
                result = await this.client.GetAsync(this.currentResults.NextLink);
            }

            result.EnsureSuccessStatusCode();

            this.currentResults = await result.Content.ReadAsAsync<DeviceCollection>();

            foreach (var item in this.currentResults.Value.Where(c => !c.Simulated))
            {
                var propertiesRequest = await this.client.GetAsync($"/api/devices/{item.Id}/properties?{this.apiVersion}");

                propertiesRequest.EnsureSuccessStatusCode();
                var propertiesResponse = await propertiesRequest.Content.ReadAsAsync<DesiredProperties>();

                var deviceItem = new DeviceTwin(item.Id, propertiesResponse);

                if (this.predicate(deviceItem))
                {
                    results.Add(deviceItem);
                }
            }

            return results;
        }
    }
}
