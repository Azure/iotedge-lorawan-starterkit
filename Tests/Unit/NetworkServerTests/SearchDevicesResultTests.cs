// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Unit.NetworkServerTests
{
    using System.Globalization;
    using System.Linq;
    using LoRaWan.NetworkServer;
    using Xunit;

    public class SearchDevicesResultTests
    {
        [Fact]
        public void Iterate()
        {
            // arrange
            var sut = new SearchDevicesResult(GenerateIoTHubDeviceInfo(5));

            // act + assert
            foreach (var (el, i) in sut.Select((el, i) => (el, i)))
                Assert.Equal(i.ToString(CultureInfo.InvariantCulture), el.DevEUI);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(3)]
        public void Count(int numberOfElements)
        {
            // arrange
            var searchDevicesResult = new SearchDevicesResult(GenerateIoTHubDeviceInfo(numberOfElements));

            // act + assert
            Assert.Equal(numberOfElements, searchDevicesResult.Count);
        }

        [Fact]
        public void When_Initialized_With_Null_Falls_Back_To_Empty_Array()
        {
            // arrange
            var searchDevicesResult = new SearchDevicesResult(null);

            // act + assert
            Assert.Empty(searchDevicesResult);
        }

        [Fact]
        public void Index()
        {
            // arrange
            var sut = new SearchDevicesResult(GenerateIoTHubDeviceInfo(5));

            // act + assert
            Assert.Equal("1", sut[1].DevEUI);
        }

        [Fact]
        public void No_Args_Constructor()
        {
            Assert.Empty(new SearchDevicesResult());
        }

        private static IoTHubDeviceInfo[] GenerateIoTHubDeviceInfo(int number) =>
            Enumerable.Range(0, number)
                      .Select(i => i.ToString(CultureInfo.InvariantCulture))
                      .Select(i => new IoTHubDeviceInfo { PrimaryKey = i, DevEUI = i, DevAddr = i })
                      .ToArray();
    }
}
