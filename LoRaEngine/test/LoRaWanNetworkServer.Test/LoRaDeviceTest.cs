// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer.Test
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Shared;
    using Moq;
    using Xunit;

    /// <summary>
    /// Tests the <see cref="LoRaDevice"/>
    /// </summary>
    public class LoRaDeviceTest
    {
        Mock<ILoRaDeviceClient> loRaDeviceClient;

        public LoRaDeviceTest()
        {
            this.loRaDeviceClient = new Mock<ILoRaDeviceClient>(MockBehavior.Strict);
        }

        [Fact]
        public async Task When_No_Changes_Were_Made_Should_Not_Save_Frame_Counter()
        {
            var target = new LoRaDevice("1231", "12312", this.loRaDeviceClient.Object);
            await target.SaveFrameCountChangesAsync();
        }

        [Fact]
        public async Task When_Incrementing_FcntDown_Should_Save_Frame_Counter()
        {
            var target = new LoRaDevice("1231", "12312", this.loRaDeviceClient.Object);

            this.loRaDeviceClient.Setup(x => x.UpdateReportedPropertiesAsync(It.IsAny<TwinCollection>()))
                .ReturnsAsync(true);

            Assert.Equal(10, target.IncrementFcntDown(10));
            await target.SaveFrameCountChangesAsync();
        }

        [Fact]
        public async Task When_Setting_FcntDown_Should_Save_Frame_Counter()
        {
            var target = new LoRaDevice("1231", "12312", this.loRaDeviceClient.Object);

            this.loRaDeviceClient.Setup(x => x.UpdateReportedPropertiesAsync(It.IsAny<TwinCollection>()))
                .ReturnsAsync(true);

            target.SetFcntDown(12);
            Assert.Equal(12, target.FCntDown);
            await target.SaveFrameCountChangesAsync();
        }

        [Fact]
        public async Task When_Setting_FcntUp_Should_Save_Frame_Counter()
        {
            var target = new LoRaDevice("1231", "12312", this.loRaDeviceClient.Object);

            this.loRaDeviceClient.Setup(x => x.UpdateReportedPropertiesAsync(It.IsAny<TwinCollection>()))
                .ReturnsAsync(true);

            target.SetFcntUp(12);
            Assert.Equal(12, target.FCntUp);
            await target.SaveFrameCountChangesAsync();
        }

        [Fact]
        public async Task After_Saving_Frame_Counter_Changes_Should_Not_Have_Pending_Changes()
        {
            var target = new LoRaDevice("1231", "12312", this.loRaDeviceClient.Object);

            this.loRaDeviceClient.Setup(x => x.UpdateReportedPropertiesAsync(It.IsAny<TwinCollection>()))
                .ReturnsAsync(true);

            target.SetFcntUp(12);
            Assert.Equal(12, target.FCntUp);
            await target.SaveFrameCountChangesAsync();
            Assert.False(target.HasFrameCountChanges);
        }

        [Fact]
        public async Task When_Initialized_New_OTAA_Device_Should_Have_All_Properties()
        {
            var twin = TestUtils.CreateTwin(
                desired: new Dictionary<string, object>
                {
                    { "AppEUI", "ABC0200000000009" },
                    { "AppKey", "ABC02000000000000000000000000009" },
                    { "GatewayID", "mygateway" },
                    { "SensorDecoder", "DecoderValueSensor" },
                    { "$version", 1 },
                },
                reported: new Dictionary<string, object>
                {
                    { "$version", 1 },
                });

            this.loRaDeviceClient.Setup(x => x.GetTwinAsync())
                .ReturnsAsync(twin);

            var loRaDevice = new LoRaDevice(string.Empty, "ABC0200000000009", this.loRaDeviceClient.Object);
            await loRaDevice.InitializeAsync();
            Assert.Equal("ABC0200000000009", loRaDevice.AppEUI);
            Assert.Equal("ABC02000000000000000000000000009", loRaDevice.AppKey);
            Assert.Equal("mygateway", loRaDevice.GatewayID);
            Assert.Equal("DecoderValueSensor", loRaDevice.SensorDecoder);
            Assert.Empty(loRaDevice.AppSKey ?? string.Empty);
            Assert.Empty(loRaDevice.NwkSKey ?? string.Empty);
            Assert.Empty(loRaDevice.DevAddr ?? string.Empty);
            Assert.Empty(loRaDevice.DevNonce ?? string.Empty);
            Assert.Empty(loRaDevice.NetID ?? string.Empty);
            Assert.False(loRaDevice.IsABP);
            Assert.False(loRaDevice.IsOurDevice);
        }

        [Fact]
        public async Task When_Initialized_Joined_OTAA_Device_Should_Have_All_Properties()
        {
            var twin = TestUtils.CreateTwin(
                desired: new Dictionary<string, object>
                {
                    { "AppEUI", "ABC0200000000009" },
                    { "AppKey", "ABC02000000000000000000000000009" },
                    { "GatewayID", "mygateway" },
                    { "SensorDecoder", "DecoderValueSensor" },
                    { "$version", 1 },
                },
                reported: new Dictionary<string, object>
                {
                    { "$version", 1 },
                    { "NwkSKey", "ABC02000000000000000000000000009ABC02000000000000000000000000009" },
                    { "AppSKey", "ABCD2000000000000000000000000009ABC02000000000000000000000000009" },
                    { "DevNonce", "0123" },
                    { "DevAddr", "0000AABB" },
                });

            this.loRaDeviceClient.Setup(x => x.GetTwinAsync())
                .ReturnsAsync(twin);

            var loRaDevice = new LoRaDevice("00000001", "ABC0200000000009", this.loRaDeviceClient.Object);
            await loRaDevice.InitializeAsync();
            Assert.Equal("ABC0200000000009", loRaDevice.AppEUI);
            Assert.Equal("ABC02000000000000000000000000009", loRaDevice.AppKey);
            Assert.Equal("mygateway", loRaDevice.GatewayID);
            Assert.Equal("DecoderValueSensor", loRaDevice.SensorDecoder);
            Assert.False(loRaDevice.IsABP);
            Assert.False(loRaDevice.IsOurDevice);
            Assert.Equal("ABC02000000000000000000000000009ABC02000000000000000000000000009", loRaDevice.NwkSKey);
            Assert.Equal("ABCD2000000000000000000000000009ABC02000000000000000000000000009", loRaDevice.AppSKey);
            Assert.Equal("0123", loRaDevice.DevNonce);
            Assert.Equal("0000AABB", loRaDevice.DevAddr);
        }

        [Fact]
        public async Task When_Initialized_ABP_Device_Should_Have_All_Properties()
        {
            var twin = TestUtils.CreateTwin(
                desired: new Dictionary<string, object>
                {
                    { "NwkSKey", "ABC02000000000000000000000000009ABC02000000000000000000000000009" },
                    { "AppSKey", "ABCD2000000000000000000000000009ABC02000000000000000000000000009" },
                    { "DevAddr", "0000AABB" },
                    { "GatewayID", "mygateway" },
                    { "SensorDecoder", "DecoderValueSensor" },
                    { "$version", 1 },
                },
                reported: new Dictionary<string, object>
                {
                    { "$version", 1 },
                    { "NwkSKey", "ABC02000000000000000000000000009ABC02000000000000000000000000009" },
                    { "AppSKey", "ABCD2000000000000000000000000009ABC02000000000000000000000000009" },
                    { "DevAddr", "0000AABB" },
                });

            this.loRaDeviceClient.Setup(x => x.GetTwinAsync())
                .ReturnsAsync(twin);

            var loRaDevice = new LoRaDevice("00000001", "ABC0200000000009", this.loRaDeviceClient.Object);
            await loRaDevice.InitializeAsync();
            Assert.Empty(loRaDevice.AppEUI ?? string.Empty);
            Assert.Empty(loRaDevice.AppKey ?? string.Empty);
            Assert.Equal("mygateway", loRaDevice.GatewayID);
            Assert.Equal("DecoderValueSensor", loRaDevice.SensorDecoder);
            Assert.True(loRaDevice.IsABP);
            Assert.True(loRaDevice.IsOurDevice);
            Assert.Equal("ABC02000000000000000000000000009ABC02000000000000000000000000009", loRaDevice.NwkSKey);
            Assert.Equal("ABCD2000000000000000000000000009ABC02000000000000000000000000009", loRaDevice.AppSKey);
            Assert.Empty(loRaDevice.DevNonce ?? string.Empty);
            Assert.Equal("0000AABB", loRaDevice.DevAddr);
        }

        [Theory]
        [InlineData("false")]
        [InlineData("FALSE")]
        [InlineData("0")]
        [InlineData(0)]
        [InlineData(false)]
        [InlineData("ture")] // misspelled "true"
        public async Task When_Downlink_Is_Disabled_In_Twin_Should_Have_DownlinkEnabled_Equals_False(object downlinkEnabledTwinValue)
        {
            var twin = TestUtils.CreateTwin(
                desired: new Dictionary<string, object>
                {
                    { "NwkSKey", "ABC02000000000000000000000000009ABC02000000000000000000000000009" },
                    { "AppSKey", "ABCD2000000000000000000000000009ABC02000000000000000000000000009" },
                    { "DevAddr", "0000AABB" },
                    { "GatewayID", "mygateway" },
                    { "SensorDecoder", "DecoderValueSensor" },
                    { TwinProperty.DownlinkEnabled, downlinkEnabledTwinValue },
                    { "$version", 1 },
                },
                reported: new Dictionary<string, object>
                {
                    { "$version", 1 },
                    { "NwkSKey", "ABC02000000000000000000000000009ABC02000000000000000000000000009" },
                    { "AppSKey", "ABCD2000000000000000000000000009ABC02000000000000000000000000009" },
                    { "DevAddr", "0000AABB" },
                });

            this.loRaDeviceClient.Setup(x => x.GetTwinAsync())
                .ReturnsAsync(twin);

            var loRaDevice = new LoRaDevice("00000001", "ABC0200000000009", this.loRaDeviceClient.Object);
            await loRaDevice.InitializeAsync();
            Assert.False(loRaDevice.DownlinkEnabled);
        }

        [Theory]
        [InlineData("true")]
        [InlineData("TRUE")]
        [InlineData("1")]
        [InlineData(1)]
        [InlineData(true)]
        public async Task When_Downlink_Is_Enabled_In_Twin_Should_Have_DownlinkEnabled_Equals_True(object downlinkTwinPropertyValue)
        {
            var twin = TestUtils.CreateTwin(
                desired: new Dictionary<string, object>
                {
                    { "NwkSKey", "ABC02000000000000000000000000009ABC02000000000000000000000000009" },
                    { "AppSKey", "ABCD2000000000000000000000000009ABC02000000000000000000000000009" },
                    { "DevAddr", "0000AABB" },
                    { "GatewayID", "mygateway" },
                    { "SensorDecoder", "DecoderValueSensor" },
                    { TwinProperty.DownlinkEnabled, downlinkTwinPropertyValue },
                    { "$version", 1 },
                },
                reported: new Dictionary<string, object>
                {
                    { "$version", 1 },
                    { "NwkSKey", "ABC02000000000000000000000000009ABC02000000000000000000000000009" },
                    { "AppSKey", "ABCD2000000000000000000000000009ABC02000000000000000000000000009" },
                    { "DevAddr", "0000AABB" },
                });

            this.loRaDeviceClient.Setup(x => x.GetTwinAsync())
                .ReturnsAsync(twin);

            var loRaDevice = new LoRaDevice("00000001", "ABC0200000000009", this.loRaDeviceClient.Object);
            await loRaDevice.InitializeAsync();
            Assert.True(loRaDevice.DownlinkEnabled);
        }

        [Fact]
        public async Task When_Downlink_Is_Not_Defined_In_Twin_Should_Have_DownlinkEnabled_Equals_True()
        {
            var twin = TestUtils.CreateTwin(
                desired: new Dictionary<string, object>
                {
                    { "NwkSKey", "ABC02000000000000000000000000009ABC02000000000000000000000000009" },
                    { "AppSKey", "ABCD2000000000000000000000000009ABC02000000000000000000000000009" },
                    { "DevAddr", "0000AABB" },
                    { "GatewayID", "mygateway" },
                    { "SensorDecoder", "DecoderValueSensor" },
                    { "$version", 1 },
                },
                reported: new Dictionary<string, object>
                {
                    { "$version", 1 },
                    { "NwkSKey", "ABC02000000000000000000000000009ABC02000000000000000000000000009" },
                    { "AppSKey", "ABCD2000000000000000000000000009ABC02000000000000000000000000009" },
                    { "DevAddr", "0000AABB" },
                });

            this.loRaDeviceClient.Setup(x => x.GetTwinAsync())
                .ReturnsAsync(twin);

            var loRaDevice = new LoRaDevice("00000001", "ABC0200000000009", this.loRaDeviceClient.Object);
            await loRaDevice.InitializeAsync();
            Assert.True(loRaDevice.DownlinkEnabled);
        }

        [Theory]
        [InlineData("1")]
        [InlineData("")]
        [InlineData("BLA")]
        [InlineData(false)]
        [InlineData(true)]
        public async Task When_PreferredWindow_Is_Not_2_In_Twin_Should_Have_Window1_As_Preferred(object preferredWindowTwinValue)
        {
            var twin = TestUtils.CreateTwin(
                desired: new Dictionary<string, object>
                {
                    { "NwkSKey", "ABC02000000000000000000000000009ABC02000000000000000000000000009" },
                    { "AppSKey", "ABCD2000000000000000000000000009ABC02000000000000000000000000009" },
                    { "DevAddr", "0000AABB" },
                    { "GatewayID", "mygateway" },
                    { "SensorDecoder", "DecoderValueSensor" },
                    { TwinProperty.PreferredWindow, preferredWindowTwinValue },
                    { "$version", 1 },
                },
                reported: new Dictionary<string, object>
                {
                    { "$version", 1 },
                    { "NwkSKey", "ABC02000000000000000000000000009ABC02000000000000000000000000009" },
                    { "AppSKey", "ABCD2000000000000000000000000009ABC02000000000000000000000000009" },
                    { "DevAddr", "0000AABB" },
                });

            this.loRaDeviceClient.Setup(x => x.GetTwinAsync())
                .ReturnsAsync(twin);

            var loRaDevice = new LoRaDevice("00000001", "ABC0200000000009", this.loRaDeviceClient.Object);
            await loRaDevice.InitializeAsync();
            Assert.Equal(1, loRaDevice.PreferredWindow);
        }

        [Theory]
        [InlineData("2")]
        [InlineData(2)]
        [InlineData(2.0)]
        public async Task When_PreferredWindow_Is_2_In_Twin_Should_Have_Window2_As_Preferred(object preferredWindowTwinProperty)
        {
            var twin = TestUtils.CreateTwin(
                desired: new Dictionary<string, object>
                {
                    { "NwkSKey", "ABC02000000000000000000000000009ABC02000000000000000000000000009" },
                    { "AppSKey", "ABCD2000000000000000000000000009ABC02000000000000000000000000009" },
                    { "DevAddr", "0000AABB" },
                    { "GatewayID", "mygateway" },
                    { "SensorDecoder", "DecoderValueSensor" },
                    { TwinProperty.PreferredWindow, preferredWindowTwinProperty },
                    { "$version", 1 },
                },
                reported: new Dictionary<string, object>
                {
                    { "$version", 1 },
                    { "NwkSKey", "ABC02000000000000000000000000009ABC02000000000000000000000000009" },
                    { "AppSKey", "ABCD2000000000000000000000000009ABC02000000000000000000000000009" },
                    { "DevAddr", "0000AABB" },
                });

            this.loRaDeviceClient.Setup(x => x.GetTwinAsync())
                .ReturnsAsync(twin);

            var loRaDevice = new LoRaDevice("00000001", "ABC0200000000009", this.loRaDeviceClient.Object);
            await loRaDevice.InitializeAsync();
            Assert.Equal(2, loRaDevice.PreferredWindow);
        }

        [Fact]
        public async Task When_PreferredWindow_Is_Not_Define_In_Twin_Should_Have_Window1_As_Preferred()
        {
            var twin = TestUtils.CreateTwin(
                desired: new Dictionary<string, object>
                {
                    { "NwkSKey", "ABC02000000000000000000000000009ABC02000000000000000000000000009" },
                    { "AppSKey", "ABCD2000000000000000000000000009ABC02000000000000000000000000009" },
                    { "DevAddr", "0000AABB" },
                    { "GatewayID", "mygateway" },
                    { "SensorDecoder", "DecoderValueSensor" },
                    { "$version", 1 },
                },
                reported: new Dictionary<string, object>
                {
                    { "$version", 1 },
                    { "NwkSKey", "ABC02000000000000000000000000009ABC02000000000000000000000000009" },
                    { "AppSKey", "ABCD2000000000000000000000000009ABC02000000000000000000000000009" },
                    { "DevAddr", "0000AABB" },
                });

            this.loRaDeviceClient.Setup(x => x.GetTwinAsync())
                .ReturnsAsync(twin);

            var loRaDevice = new LoRaDevice("00000001", "ABC0200000000009", this.loRaDeviceClient.Object);
            await loRaDevice.InitializeAsync();
            Assert.Equal(1, loRaDevice.PreferredWindow);
        }

        [Fact]
        public void New_LoRaDevice_Should_Have_C2D_Enabled()
        {
            Assert.True(new LoRaDevice("12312", "31231", new Mock<ILoRaDeviceClient>().Object).DownlinkEnabled);
        }

        [Fact]
        public void New_LoRaDevice_Should_Have_PreferredWindow_As_1()
        {
            Assert.Equal(1, new LoRaDevice("12312", "31231", new Mock<ILoRaDeviceClient>().Object).PreferredWindow);
        }
    }
}
