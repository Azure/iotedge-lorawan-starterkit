// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Unit.LoraKeysManagerFacade
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using global::LoraKeysManagerFacade;
    using global::LoRaTools;
    using Microsoft.Azure.Devices;
    using Microsoft.Azure.Devices.Shared;
    using Microsoft.Extensions.Logging.Abstractions;
    using Moq;
    using Xunit;

    public class EdgeDeviceGetterTests
    {
        private const string EdgeDevice1 = "edgeDevice1";
        private Mock<IDeviceRegistryManager> mockRegistryManager;
        private Mock<IQuery> mockQuery;

        public EdgeDeviceGetterTests()
        {
            InitRegistryManager();
        }

        [Theory]
        [InlineData(EdgeDevice1, true)]
        [InlineData("another", false)]
        public async Task IsEdgeDeviceAsync_Returns_Proper_Answer(string lnsId, bool isEdge)
        {
            var edgeDeviceGetter = new EdgeDeviceGetter(InitRegistryManager(), new LoRaInMemoryDeviceStore(), NullLogger<EdgeDeviceGetter>.Instance);
            Assert.Equal(isEdge, await edgeDeviceGetter.IsEdgeDeviceAsync(lnsId, default));
        }

        [Fact]
        public async Task IsEdgeDeviceAsync_Should_Not_Reach_IoTHub_Twice_If_Invoked_In_Less_Than_One_Minute()
        {
            var edgeDeviceGetter = new EdgeDeviceGetter(InitRegistryManager(), new LoRaInMemoryDeviceStore(), NullLogger<EdgeDeviceGetter>.Instance);
            Assert.True(await edgeDeviceGetter.IsEdgeDeviceAsync(EdgeDevice1, default));
            Assert.True(await edgeDeviceGetter.IsEdgeDeviceAsync(EdgeDevice1, default));
            Assert.False(await edgeDeviceGetter.IsEdgeDeviceAsync("anotherDevice", default));
            Assert.False(await edgeDeviceGetter.IsEdgeDeviceAsync("anotherDevice", default));

            _ = this.mockQuery.Invocations.Single(x => x.Method.Name.Equals(nameof(IQuery.GetNextAsTwinAsync), System.StringComparison.OrdinalIgnoreCase));
        }

        private IDeviceRegistryManager InitRegistryManager()
        {
            this.mockQuery = new Mock<IQuery>();
            this.mockRegistryManager = new Mock<IDeviceRegistryManager>();

            var twins = new List<Twin>()
            {
                new Twin(EdgeDevice1) { Capabilities = new DeviceCapabilities() { IotEdge = true }},
            };

            mockQuery.Setup(x => x.GetNextAsTwinAsync())
                .ReturnsAsync(twins);

            mockRegistryManager
                .Setup(x => x.CreateQuery(It.IsAny<string>()))
                .Returns(mockQuery.Object);

            return mockRegistryManager.Object;
        }
    }
}
