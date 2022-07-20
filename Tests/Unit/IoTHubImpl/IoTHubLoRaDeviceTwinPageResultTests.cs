// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Unit.IoTHubImpl
{
    using System.Linq;
    using System.Threading.Tasks;
    using global::LoRaTools.IoTHubImpl;
    using Microsoft.Azure.Devices;
    using Microsoft.Azure.Devices.Shared;
    using Moq;
    using Xunit;

    public class IoTHubLoRaDeviceTwinPageResultTests
    {
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void HasMoreResults(bool expectedResult)
        {
            // Arrange
            var mockQuery = new Mock<IQuery>();
            mockQuery.SetupGet(c => c.HasMoreResults)
                .Returns(expectedResult);

            var instance = new IoTHubLoRaDeviceTwinPageResult(mockQuery.Object);

            // Assert
            Assert.Equal(expectedResult, instance.HasMoreResults);
            mockQuery.Verify(c => c.HasMoreResults, Times.Once);
        }

        [Fact]
        public async Task GetNextPageAsync()
        {
            // Arrange
            var mockQuery = new Mock<IQuery>();
            mockQuery.Setup(c => c.GetNextAsTwinAsync())
                .ReturnsAsync(new[]
                {
                    new Twin("11111"),
                    new Twin("22222")
                });

            var instance = new IoTHubLoRaDeviceTwinPageResult(mockQuery.Object);

            // Act
            var result = await instance.GetNextPageAsync();

            // Assert
            Assert.Equal(2, result.Count());
            mockQuery.Verify(c => c.GetNextAsTwinAsync(), Times.Once);
        }
    }
}
