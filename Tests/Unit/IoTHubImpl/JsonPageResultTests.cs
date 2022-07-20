// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Unit.IoTHubImpl
{
    using System.Linq;
    using System.Threading.Tasks;
    using global::LoRaTools.IoTHubImpl;
    using Microsoft.Azure.Devices;
    using Moq;
    using Xunit;

    public class JsonPageResultTests
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

            var instance = new JsonPageResult(mockQuery.Object);

            // Assert
            Assert.Equal(expectedResult, instance.HasMoreResults);
            mockQuery.Verify(c => c.HasMoreResults, Times.Once);
        }

        [Fact]
        public async Task GetNextPageAsync()
        {
            // Arrange
            var mockQuery = new Mock<IQuery>();
            mockQuery.Setup(c => c.GetNextAsJsonAsync())
                .ReturnsAsync(new[]
                {
                    "aaa",
                    "bbb"
                });

            var instance = new JsonPageResult(mockQuery.Object);

            // Act
            var result = await instance.GetNextPageAsync();

            // Assert
            Assert.Equal(2, result.Count());
            mockQuery.Verify(c => c.GetNextAsJsonAsync(), Times.Once);
        }
    }
}
