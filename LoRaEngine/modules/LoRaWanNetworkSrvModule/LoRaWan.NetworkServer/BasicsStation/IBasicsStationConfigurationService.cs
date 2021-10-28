namespace LoRaWan.NetworkServer.BasicsStation
{
    using System.Threading;
    using System.Threading.Tasks;

    internal interface IBasicsStationConfigurationService
    {
        Task<string> GetRouterConfigMessageAsync(StationEui stationEui, CancellationToken cancellationToken);
    }
}
