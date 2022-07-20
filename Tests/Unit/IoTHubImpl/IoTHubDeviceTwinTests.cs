// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Unit.IoTHubImpl
{
    using global::LoRaTools;
    using global::LoRaTools.IoTHubImpl;
    using Microsoft.Azure.Devices.Shared;
    using Moq;
    using Xunit;

    public class IoTHubDeviceTwinTests
    {
        private readonly MockRepository mockRepository;

        public IoTHubDeviceTwinTests()
        {
            this.mockRepository = new MockRepository(MockBehavior.Strict);
        }

        [Fact]
        public void GetGatewayID()
        {
            // Arrange
            const string gatewayId = "mygatewayid";
            var twinCollection = new TwinCollection();
            twinCollection[TwinPropertiesConstants.GatewayID] = gatewayId;

            var twin = new Twin();
            twin.Properties.Desired = twinCollection;

            var ioTHubDeviceTwin = new IoTHubLoRaDeviceTwin(twin);

            // Act
            var result = ioTHubDeviceTwin.GetGatewayID();

            // Assert
            Assert.Equal(gatewayId, result);
            this.mockRepository.VerifyAll();
        }

        [Fact]
        public void GetGatewayIDShouldReturnEmptyStringIfGatewayIdNotPresentInTwin()
        {
            // Arrange
            var twinCollection = new TwinCollection();

            var twin = new Twin();
            twin.Properties.Desired = twinCollection;

            var ioTHubDeviceTwin = new IoTHubLoRaDeviceTwin(twin);

            // Act
            var result = ioTHubDeviceTwin.GetGatewayID();

            // Assert
            Assert.True(string.IsNullOrEmpty(result));
            this.mockRepository.VerifyAll();
        }

        [Fact]
        public void EqualsShouldReturnFalseIfTwinInstancesAreNotEquals()
        {
            // Arrange
            var twin1 = new Twin("device1");
            var device1 = new IoTHubDeviceTwin(twin1);

            var twin2 = new Twin("device2");
            var device2 = new IoTHubDeviceTwin(twin2);

            // Act
            var result = device1.Equals(device2);

            // Assert
            Assert.False(result);
            this.mockRepository.VerifyAll();
        }

        [Fact]
        public void EqualsShouldReturnTrueIfTwinInstancesAreEquals()
        {
            // Arrange
            var twin = new Twin("device1");
            var device1 = new IoTHubDeviceTwin(twin);
            var device2 = new IoTHubDeviceTwin(twin);

            // Act
            var result = device1.Equals(device2);

            // Assert
            Assert.True(result);
            this.mockRepository.VerifyAll();
        }

        [Fact]
        public void WhenSecondIsNotIoTHubDeviceTwinEqualsShouldReturnFalse()
        {
            // Arrange
            var twin = new Twin("device1");
            var device1 = new IoTHubDeviceTwin(twin);
            IDeviceTwin device2 = new FakeIoTHubDeviceTwinTests();

            // Act
            var result = device1.Equals(device2);

            // Assert
            Assert.False(result);
            this.mockRepository.VerifyAll();
        }

        [Fact]
        public void GetHashCodeShouldRetuenTheTwinInstancesHashCode()
        {
            // Arrange
            var twin = new Twin();
            var ioTHubDeviceTwin = new IoTHubDeviceTwin(twin);

            // Act
            var result = ioTHubDeviceTwin.GetHashCode();

            // Assert
            Assert.Equal(twin.GetHashCode(), result);
            this.mockRepository.VerifyAll();
        }
    }
}
