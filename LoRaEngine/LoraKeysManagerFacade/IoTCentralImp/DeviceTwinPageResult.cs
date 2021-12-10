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
    using Newtonsoft.Json.Linq;

    public sealed class DeviceTwinPageResult : IRegistryPageResult<IDeviceTwin>
    {
        private readonly HttpClient client;
        private readonly string apiVersion;
        private readonly Func<DeviceTemplateInfo, string> query;
        private readonly IEnumerator<DeviceTemplateInfo> deviceTemplatesEnumerator;

        public DeviceTwinPageResult(HttpClient client, IEnumerable<DeviceTemplateInfo> deviceTemplateInfos, string apiVersion, Func<DeviceTemplateInfo, string> query)
        {
            this.client = client;
            this.apiVersion = apiVersion;
            this.query = query;

            if (deviceTemplateInfos == null)
            {
                throw new ArgumentNullException(nameof(deviceTemplateInfos));
            }

            deviceTemplatesEnumerator = deviceTemplateInfos.GetEnumerator();
            HasMoreResults = deviceTemplatesEnumerator.MoveNext();
        }

        public bool HasMoreResults { get; private set; }

        public void Dispose()
        {
            if (this.deviceTemplatesEnumerator != null)
            {
                this.deviceTemplatesEnumerator.Dispose();
            }
        }

        public async Task<IEnumerable<IDeviceTwin>> GetNextPageAsync()
        {
            var results = new List<IDeviceTwin>();

            if (!this.HasMoreResults)
            {
                return results;
            }

            HttpResponseMessage result = null;

            result = await this.client.PostAsJsonAsync(new Uri(this.client.BaseAddress, $"/api/query?{this.apiVersion}"), new
            {
                query = this.query(deviceTemplatesEnumerator.Current)
            });

            _ = result.EnsureSuccessStatusCode();

            var queryResponse = await result.Content.ReadAsAsync<JObject>();

            foreach (var item in queryResponse["results"].OfType<JObject>())
            {
                results.Add(new QueryDeviceTwin(deviceTemplatesEnumerator.Current.ComponentName, item));
            }

            HasMoreResults = deviceTemplatesEnumerator.MoveNext();

            return results;
        }
    }
}
