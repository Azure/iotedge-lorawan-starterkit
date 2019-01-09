using LoRaWan.NetworkServer.V2;

namespace LoRaWan.NetworkServer.Test
{
    internal class TestLoRaDeviceFactory : ILoRaDeviceFactory
    {
        private readonly ILoRaDeviceClient loRaDeviceClient;

        public TestLoRaDeviceFactory(ILoRaDeviceClient loRaDeviceClient)
        {
            this.loRaDeviceClient = loRaDeviceClient;
        }

        public LoRaDevice Create(IoTHubDeviceInfo deviceInfo)
        {
            return new LoRaDevice(
                deviceInfo.DevAddr,
                deviceInfo.DevEUI,
                this.loRaDeviceClient
            );
        }
    }
}