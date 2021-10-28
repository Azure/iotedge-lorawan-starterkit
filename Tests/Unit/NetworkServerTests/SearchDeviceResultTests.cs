namespace LoRaWan.Tests.Unit.NetworkServerTests
{
    using LoRaWan.NetworkServer;
    using System;
    using System.Globalization;
    using System.Linq;
    using Xunit;

    public class SearchDeviceResultTests
    {
        [Fact]
        public void FirstOrDefault_Default_Cases()
        {
            Assert.Null(new SearchDevicesResult(Array.Empty<IoTHubDeviceInfo>()).FirstOrDefault());
            Assert.Null(new SearchDevicesResult().FirstOrDefault());
        }

        [Theory]
        [InlineData(1)]
        [InlineData(3)]
        public void FirstOrDefault_First_Cases(int numberOfElements)
        {
            // arrange
            var devices = GenerateIoTHubDeviceInfo(numberOfElements);
            var searchDevicesResult = new SearchDevicesResult(devices);

            // act
            var result = searchDevicesResult.FirstOrDefault();

            // assert
            Assert.Equal("0", result.DevEUI);
            Assert.Equal(devices.First(), result);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(5)]
        public void Single_FailureCase(int numberOfElements)
        {
            // arrange
            var devices = GenerateIoTHubDeviceInfo(numberOfElements);
            var searchDevicesResult = new SearchDevicesResult(devices);

            // act
            void Act() => searchDevicesResult.Single();

            // assert
            Assert.Throws<InvalidOperationException>(Act);
        }

        [Fact]
        public void Single_NullCase()
        {
            // arrange + act
            static void Act() => new SearchDevicesResult().Single();

            // assert
            Assert.Throws<InvalidOperationException>(Act);
        }

        [Fact]
        public void Single_SuccessCase()
        {
            // arrange
            var devices = GenerateIoTHubDeviceInfo(1);
            var searchDevicesResult = new SearchDevicesResult(devices);

            // act
            var result = searchDevicesResult.Single();

            // assert
            Assert.Equal(devices.Single(), result);

        }

        private static IoTHubDeviceInfo[] GenerateIoTHubDeviceInfo(int number) =>
            Enumerable.Range(0, number)
                      .Select(i => i.ToString(CultureInfo.InvariantCulture))
                      .Select(i => new IoTHubDeviceInfo { PrimaryKey = i, DevEUI = i, DevAddr = i })
                      .ToArray();
    }
}
