namespace LoRaWan.Tests.Unit.NetworkServerTests
{
    using System.Linq;
    using LoRaWan.NetworkServer;
    using LoRaWan.Tests.Shared;
    using Microsoft.Extensions.Caching.Memory;
    using Moq;
    using Xunit;

    public class LoRaDeviceClientConnectionManagerTests
    {
        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(5)]
        public void When_Disposing_Should_Dispose_All_Managed_Connections(int numberOfDevices)
        {
            // arrange
            using var cache = new MemoryCache(new MemoryCacheOptions());
            using var connectionManager = new LoRaDeviceClientConnectionManager(cache);

            var deviceRegistrations =
                Enumerable.Range(0, numberOfDevices)
                          .Select(i => TestUtils.CreateFromSimulatedDevice(new SimulatedDevice(TestDeviceInfo.CreateABPDevice((uint)i)), connectionManager))
                          .Select(d => (d, new Mock<ILoRaDeviceClient>()))
                          .ToList();

            foreach (var (d, c) in deviceRegistrations)
            {
                connectionManager.Register(d, c.Object);
            }

            // act
            connectionManager.Dispose();

            // assert
            foreach (var (_, c) in deviceRegistrations)
            {
                c.Verify(client => client.Dispose(), Times.Exactly(2));
            }
        }
    }
}
