// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer.BasicsStation
{
    using System;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using LoRaWan.NetworkServer.BasicsStation.JsonHandlers;
    using Microsoft.Azure.Devices.Client.Exceptions;
    using Microsoft.Extensions.Logging;

    internal sealed class BasicsStationConfigurationService : IBasicsStationConfigurationService
    {
        private const string RouterConfigPropertyName = "routerConfig";

        private readonly LoRaDeviceAPIServiceBase loRaDeviceApiService;
        private readonly ILoRaDeviceFactory loRaDeviceFactory;

        public BasicsStationConfigurationService(LoRaDeviceAPIServiceBase loRaDeviceApiService,
                                                 ILoRaDeviceFactory loRaDeviceFactory)
        {
            this.loRaDeviceApiService = loRaDeviceApiService;
            this.loRaDeviceFactory = loRaDeviceFactory;
        }

        public async Task<string> GetRouterConfigMessageAsync(StationEui stationEui, CancellationToken cancellationToken)
        {
            var queryResult = await this.loRaDeviceApiService.SearchByDevEUIAsync(stationEui.ToString());
            var info = queryResult.Single();

            void Log(string message) => Logger.Log(stationEui.ToString(), message, LogLevel.Error);

            try
            {
                using var client = this.loRaDeviceFactory.CreateDeviceClient(info.DevEUI, info.PrimaryKey);
                var twin = await client.GetTwinAsync();
                var config = ((object)twin.Properties.Desired[RouterConfigPropertyName]).ToString();
                return LnsStationConfiguration.GetConfiguration(config);
            }
            catch (IotHubCommunicationException ex)
            {
                Log($"Error while communicating with IoT Hub during station discovery. {ex.Message}");
                throw;
            }
            catch (IotHubException ex)
            {
                Log($"An error occured in IoT Hub during station discovery. {ex.Message}");
                throw;
            }
            catch (ArgumentOutOfRangeException)
            {
                Log($"Property '{RouterConfigPropertyName}' was not present in device twin.");
                throw;
            }
        }
    }
}
