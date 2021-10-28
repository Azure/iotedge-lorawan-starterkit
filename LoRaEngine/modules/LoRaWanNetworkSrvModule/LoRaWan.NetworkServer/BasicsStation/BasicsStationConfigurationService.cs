namespace LoRaWan.NetworkServer.BasicsStation
{
    using System;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using LoRaWan.NetworkServer.BasicsStation.JsonHandlers;

    internal class BasicsStationConfigurationService : IBasicsStationConfigurationService
    {
        private const string RouterConfigPropertyName = "routerConfig";
        private readonly LoRaDeviceAPIServiceBase _loRaDeviceApiService;
        private readonly ILoRaDeviceFactory _loRaDeviceFactory;

        private static readonly IJsonReader<string> RouterConfigurationConverter =
            JsonReader.Object(JsonReader.Property("NetID", JsonReader.Array(from id in JsonReader.UInt32()
                                                                            select new NetId((int)id))),
                              JsonReader.Property("JoinEui",
                                                  JsonReader.Array(from arr in JsonReader.Array(from eui in JsonReader.String()
                                                                                                select JoinEui.Parse(eui))
                                                                   select (arr[0], arr[1]))),
                              JsonReader.Property("region", JsonReader.String()),
                              JsonReader.Property("hwspec", JsonReader.String()),
                              JsonReader.Property("freq_range", from r in JsonReader.Array(JsonReader.UInt32())
                                                                select (new Hertz(r[0]), new Hertz(r[1]))),
                              JsonReader.Property("DRs", JsonReader.Array(from arr in JsonReader.Array(JsonReader.UInt32())
                                                                          select ((SpreadingFactor)arr[0], (Bandwidth)arr[1], Convert.ToBoolean(arr[2])))),
                              (netId, joinEui, region, hwspec, freqRange, drs) => LnsData.WriteRouterConfig(netId, joinEui, region, hwspec, freqRange, drs));

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
            return RouterConfigurationConverter.Read(config);
        }
    }
}
