// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Unit.LoraKeysManagerFacade
{
    using System;
    using System.Threading.Tasks;
    using global::LoraKeysManagerFacade;
    using global::LoRaTools;
    using Microsoft.Extensions.Logging.Abstractions;
    using Moq;
    using Xunit;

    public class EdgeDeviceGetterTests
    {
        private const string EdgeDevice1 = "edgeDevice1";
        private Mock<IDeviceRegistryManager> mockRegistryManager;

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

            this.mockRegistryManager.Verify(x => x.GetEdgeDevices(), Times.Once);
        }

        [Fact]
        public async Task ListEdgeDevicesAsync_Returns_Expected_Device_List()
        {
            var edgeDeviceGetter = new EdgeDeviceGetter(InitRegistryManager(), new LoRaInMemoryDeviceStore(), NullLogger<EdgeDeviceGetter>.Instance);

            var list = await edgeDeviceGetter.ListEdgeDevicesAsync(default);

            Assert.Contains(EdgeDevice1, list);
        }

        [Fact]
        public async Task ListEdgeDevicesAsync_Returns_Empty_Device_List()
        {
            this.mockRegistryManager = new Mock<IDeviceRegistryManager>();
            var query = new Mock<IRegistryPageResult<IDeviceTwin>>(MockBehavior.Strict);

            query.Setup(x => x.HasMoreResults).Returns(false);
            query.Setup(x => x.GetNextPageAsync())
                .ReturnsAsync(Array.Empty<IDeviceTwin>());

            this.mockRegistryManager.Setup(c => c.GetEdgeDevices())
                .Returns(query.Object);

            var edgeDeviceGetter = new EdgeDeviceGetter(this.mockRegistryManager.Object, new LoRaInMemoryDeviceStore(), NullLogger<EdgeDeviceGetter>.Instance);

            var list = await edgeDeviceGetter.ListEdgeDevicesAsync(default);

            Assert.Empty(list);
        }

        private IDeviceRegistryManager InitRegistryManager()
        {
            this.mockRegistryManager = new Mock<IDeviceRegistryManager>();

            var mockDeviceTwin = new Mock<IDeviceTwin>();
            mockDeviceTwin.SetupGet(c => c.DeviceId).Returns(EdgeDevice1);
            var query = new Mock<IRegistryPageResult<IDeviceTwin>>(MockBehavior.Strict);

            query.Setup(x => x.HasMoreResults).Returns(false);
            query.Setup(x => x.GetNextPageAsync())
                .ReturnsAsync(new[] { mockDeviceTwin.Object });

            mockRegistryManager.Setup(c => c.GetEdgeDevices())
                .Returns(query.Object);

            return mockRegistryManager.Object;
        }
    }
}
