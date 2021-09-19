// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoraKeysManagerFacade.IoTCentralImp
{
    using System;
    using System.Net;
    using System.Net.Http;
    using System.Security.Cryptography;
    using System.Threading.Tasks;
    using LoraKeysManagerFacade.IoTCentralImp.Definitions;

    internal class IoTCentralDeviceRegistryManager : IDeviceRegistryManager
    {
        private const string API_VERSION = "api-version=1.0";

        private readonly IDeviceProvisioningHelper provisioningHelper;
        private readonly HttpClient client;

        public IoTCentralDeviceRegistryManager(IDeviceProvisioningHelper provisioningHelper, HttpClient client)
        {
            this.provisioningHelper = provisioningHelper;
            this.client = client;
        }

        private SymmetricKeyAttestation GenerateNewAttestation()
        {
            var bytes = new byte[64];
            var random = new Random();

            using (var hmac = new HMACSHA256(bytes))
            {
                var attestation = new SymmetricKeyAttestation
                {
                    Type = "symmetricKey",
                    SymmetricKey = new SymmetricKey()
                };

                random.NextBytes(bytes);
                attestation.SymmetricKey.PrimaryKey = Convert.ToBase64String(hmac.ComputeHash(bytes));
                random.NextBytes(bytes);
                attestation.SymmetricKey.SecondaryKey = Convert.ToBase64String(hmac.ComputeHash(bytes));

                return attestation;
            }
        }

        public async Task<IDevice> GetDeviceAsync(string deviceName)
        {
            var deviceRequest = await this.client.GetAsync($"/api/devices/{deviceName}?{API_VERSION}");

            if (deviceRequest.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }

            var deviceResponse = await deviceRequest.Content.ReadAsAsync<Device>();
            HttpResponseMessage attestationRequest = null;

            if (!deviceResponse.Provisionned)
            {
                var attestation = this.GenerateNewAttestation();
                attestationRequest = await this.client.PutAsJsonAsync($"/api/devices/{deviceName}/attestation?{API_VERSION}", attestation);
                await this.provisioningHelper.ProvisionDeviceAsync(deviceName, attestation);
            }
            else
            {
                attestationRequest = await this.client.GetAsync($"/api/devices/{deviceName}/attestation?{API_VERSION}");
            }

            attestationRequest.EnsureSuccessStatusCode();

            var attestationResponse = await attestationRequest.Content.ReadAsAsync<SymmetricKeyAttestation>();

            return new IoTCentralDevice(deviceResponse, attestationResponse);
        }

        public Task<IDeviceTwin> GetTwinAsync(string deviceName)
        {
            throw new NotImplementedException();
        }

        public Task CreateEdgeDeviceAsync(string edgeDeviceName, bool deployEndDevice, string facadeUrl, string facadeKey, string region, string resetPin, string spiSpeed, string spiDev)
        {
            throw new NotImplementedException();
        }

        public Task<IRegistryPageResult<IDeviceTwin>> FindDeviceByAddrAsync(string devAddr)
        {
            throw new NotImplementedException();
        }

        public Task<IRegistryPageResult<IDeviceTwin>> FindDevicesByLastUpdateDate(string updatedSince)
        {
            throw new NotImplementedException();
        }

        public Task<IRegistryPageResult<IDeviceTwin>> FindConfiguredLoRaDevices()
        {
            throw new NotImplementedException();
        }
    }
}
