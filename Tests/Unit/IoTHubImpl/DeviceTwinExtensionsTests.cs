// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Unit.IoTHubImpl
{
    using System;
    using global::LoRaTools;
    using global::LoRaTools.IoTHubImpl;
    using Microsoft.Azure.Devices.Shared;
    using Xunit;

    public class DeviceTwinExtensionsTests
    {
        [Fact]
        public void ToIoTHubDeviceTwinShouldReturnTwinInstance()
        {
            // Arrange
            var twin = new Twin();

            var deviceTwin = new IoTHubDeviceTwin(twin);

            // Act
            var result = deviceTwin.ToIoTHubDeviceTwin();

            // Assert
            Assert.Equal(twin, result);
        }

        [Fact]
        public void WhenDeviceTwinIsNullShouldThrowArgumentNullException()
        {
            // Arrange
            IoTHubDeviceTwin deviceTwin = null;

            // Act
            Assert.Throws<ArgumentNullException>(() => deviceTwin.ToIoTHubDeviceTwin());
        }

        [Fact]
        public void WhenDeviceTwinIsNotIoTHubDeviceTwinShouldThrowArgumentException()
        {
            // Arrange
            IDeviceTwin deviceTwin = new FakeIoTHubDeviceTwinTests();

            // Act
            Assert.Throws<ArgumentException>(() => deviceTwin.ToIoTHubDeviceTwin());
        }
    }
}
