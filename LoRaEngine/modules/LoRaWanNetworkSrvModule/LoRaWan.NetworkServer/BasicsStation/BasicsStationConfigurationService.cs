namespace LoRaWan.NetworkServer.BasicsStation
{
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using LoRaWan.NetworkServer.BasicsStation.JsonHandlers;

    internal class BasicsStationConfigurationService : IBasicsStationConfigurationService
    {
        private const string RouterConfigPropertyName = "routerConfig";
        private readonly LoRaDeviceAPIServiceBase _loRaDeviceApiService;
        private readonly ILoRaDeviceFactory _loRaDeviceFactory;

        

        public BasicsStationConfigurationService(LoRaDeviceAPIServiceBase loRaDeviceApiService,
                                                 ILoRaDeviceFactory loRaDeviceFactory)
        {
            this._loRaDeviceApiService = loRaDeviceApiService;
            this._loRaDeviceFactory = loRaDeviceFactory;
        }

        public async Task<string> GetRouterConfigMessageAsync(StationEui stationEui, CancellationToken cancellationToken)
        {
            var queryResult = await this._loRaDeviceApiService.SearchByDevEUIAsync(stationEui.ToString());
            var info = queryResult.Single();
            using var client = this._loRaDeviceFactory.CreateDeviceClient(info.DevEUI, info.PrimaryKey);
            var twin = await client.GetTwinAsync();

            // Clone the original Json object
            string config = twin.Properties.Desired[RouterConfigPropertyName].ToString();
            return LnsStationConfiguration.GetConfiguration(config);
        }
    }
}
