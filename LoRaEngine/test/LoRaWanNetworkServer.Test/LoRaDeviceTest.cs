// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer.Test
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using LoRaTools.Regions;
    using Microsoft.Azure.Devices.Shared;
    using Microsoft.Extensions.Caching.Memory;
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
            var target = new LoRaDevice("1231", "12312", new SingleDeviceConnectionManager(this.loRaDeviceClient.Object));
            await target.SaveChangesAsync();
        }

        [Fact]
        public async Task When_Incrementing_FcntDown_Should_Save_Frame_Counter()
        {
            var target = new LoRaDevice("1231", "12312", new SingleDeviceConnectionManager(this.loRaDeviceClient.Object));

            this.loRaDeviceClient.Setup(x => x.UpdateReportedPropertiesAsync(It.IsAny<TwinCollection>()))
                .ReturnsAsync(true);

            Assert.Equal(10U, target.IncrementFcntDown(10));
            Assert.Equal(0U, target.LastSavedFCntDown);
            await target.SaveChangesAsync();
            Assert.Equal(10U, target.LastSavedFCntDown);
        }

        [Fact]
        public async Task When_Setting_FcntDown_Should_Save_Frame_Counter()
        {
            var target = new LoRaDevice("1231", "12312", new SingleDeviceConnectionManager(this.loRaDeviceClient.Object));

            this.loRaDeviceClient.Setup(x => x.UpdateReportedPropertiesAsync(It.IsAny<TwinCollection>()))
                .ReturnsAsync(true);

            target.SetFcntDown(12);
            Assert.Equal(12U, target.FCntDown);
            Assert.Equal(0U, target.LastSavedFCntDown);
            await target.SaveChangesAsync();
            Assert.Equal(12U, target.LastSavedFCntDown);
        }

        [Fact]
        public async Task When_Setting_FcntUp_Should_Save_Frame_Counter()
        {
            var target = new LoRaDevice("1231", "12312", new SingleDeviceConnectionManager(this.loRaDeviceClient.Object));

            this.loRaDeviceClient.Setup(x => x.UpdateReportedPropertiesAsync(It.IsAny<TwinCollection>()))
                .ReturnsAsync(true);

            target.SetFcntUp(12);
            Assert.Equal(12U, target.FCntUp);
            Assert.Equal(0U, target.LastSavedFCntUp);
            await target.SaveChangesAsync();
            Assert.Equal(12U, target.LastSavedFCntUp);
        }

        [Fact]
        public async Task After_Saving_Frame_Counter_Changes_Should_Not_Have_Pending_Changes()
        {
            var target = new LoRaDevice("1231", "12312", new SingleDeviceConnectionManager(this.loRaDeviceClient.Object));

            this.loRaDeviceClient.Setup(x => x.UpdateReportedPropertiesAsync(It.IsAny<TwinCollection>()))
                .ReturnsAsync(true);

            target.SetFcntUp(12);
            Assert.Equal(12U, target.FCntUp);
            Assert.Equal(0U, target.LastSavedFCntUp);
            await target.SaveChangesAsync();
            Assert.False(target.HasFrameCountChanges);
            Assert.Equal(12U, target.LastSavedFCntUp);
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

            var loRaDevice = new LoRaDevice(string.Empty, "ABC0200000000009", new SingleDeviceConnectionManager(this.loRaDeviceClient.Object));
            await loRaDevice.InitializeAsync();
            Assert.Equal("ABC0200000000009", loRaDevice.AppEUI);
            Assert.Equal("ABC02000000000000000000000000009", loRaDevice.AppKey);
            Assert.Equal("mygateway", loRaDevice.GatewayID);
            Assert.Equal("DecoderValueSensor", loRaDevice.SensorDecoder);
            Assert.Equal(0U, loRaDevice.FCntDown);
            Assert.Equal(0U, loRaDevice.LastSavedFCntDown);
            Assert.Equal(0U, loRaDevice.FCntUp);
            Assert.Equal(0U, loRaDevice.LastSavedFCntUp);
            Assert.False(loRaDevice.HasFrameCountChanges);
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

            var loRaDevice = new LoRaDevice("00000001", "ABC0200000000009", new SingleDeviceConnectionManager(this.loRaDeviceClient.Object));
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

            var loRaDevice = new LoRaDevice("00000001", "ABC0200000000009", new SingleDeviceConnectionManager(this.loRaDeviceClient.Object));
            await loRaDevice.InitializeAsync();
            Assert.Empty(loRaDevice.AppEUI ?? string.Empty);
            Assert.Empty(loRaDevice.AppKey ?? string.Empty);
            Assert.Equal("mygateway", loRaDevice.GatewayID);
            Assert.Equal("DecoderValueSensor", loRaDevice.SensorDecoder);
            Assert.True(loRaDevice.IsABP);
            Assert.True(loRaDevice.IsOurDevice);
            Assert.Equal(0U, loRaDevice.FCntDown);
            Assert.Equal(0U, loRaDevice.LastSavedFCntDown);
            Assert.Equal(0U, loRaDevice.FCntUp);
            Assert.Equal(0U, loRaDevice.LastSavedFCntUp);
            Assert.False(loRaDevice.HasFrameCountChanges);
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

            var loRaDevice = new LoRaDevice("00000001", "ABC0200000000009", new SingleDeviceConnectionManager(this.loRaDeviceClient.Object));
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

            var loRaDevice = new LoRaDevice("00000001", "ABC0200000000009", new SingleDeviceConnectionManager(this.loRaDeviceClient.Object));
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

            var loRaDevice = new LoRaDevice("00000001", "ABC0200000000009", new SingleDeviceConnectionManager(this.loRaDeviceClient.Object));
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

            var loRaDevice = new LoRaDevice("00000001", "ABC0200000000009", new SingleDeviceConnectionManager(this.loRaDeviceClient.Object));
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

            var loRaDevice = new LoRaDevice("00000001", "ABC0200000000009", new SingleDeviceConnectionManager(this.loRaDeviceClient.Object));
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

            var loRaDevice = new LoRaDevice("00000001", "ABC0200000000009", new SingleDeviceConnectionManager(this.loRaDeviceClient.Object));
            await loRaDevice.InitializeAsync();
            Assert.Equal(1, loRaDevice.PreferredWindow);
        }

        [Fact]
        public void New_LoRaDevice_Should_Have_C2D_Enabled()
        {
            Assert.True(new LoRaDevice("12312", "31231", new SingleDeviceConnectionManager(new Mock<ILoRaDeviceClient>().Object)).DownlinkEnabled);
        }

        [Fact]
        public void New_LoRaDevice_Should_Have_PreferredWindow_As_1()
        {
            Assert.Equal(1, new LoRaDevice("12312", "31231", new SingleDeviceConnectionManager(new Mock<ILoRaDeviceClient>().Object)).PreferredWindow);
        }

        [Fact]
        public void After_3_Resubmits_Should_Not_Be_Valid_To_Resend_Ack()
        {
            var target = new LoRaDevice("1231", "12312", new SingleDeviceConnectionManager(this.loRaDeviceClient.Object));

            // 1st time
            target.SetFcntUp(12);

            // 1st resubmit
            target.SetFcntUp(12);
            Assert.True(target.ValidateConfirmResubmit(12));

            // 2nd resubmit
            target.SetFcntUp(12);
            Assert.True(target.ValidateConfirmResubmit(12));

            // 3rd resubmit
            target.SetFcntUp(12);
            Assert.True(target.ValidateConfirmResubmit(12));

            // 4rd resubmit
            target.SetFcntUp(12);
            Assert.False(target.ValidateConfirmResubmit(12));

            // new fcnt up
            target.SetFcntUp(13);

            Assert.False(target.ValidateConfirmResubmit(12), "Should not be valid to resubmit old fcntUp");

            // resubmit new fcnt up
            target.SetFcntUp(13);
            Assert.True(target.ValidateConfirmResubmit(13));

            Assert.False(target.ValidateConfirmResubmit(12), "Should not be valid to resubmit old fcntUp");
        }

        [Fact]
        public void When_ResetFcnt_In_New_Instance_Should_Have_HasFrameCountChanges_False()
        {
            var target = new LoRaDevice("1231", "12312", new SingleDeviceConnectionManager(this.loRaDeviceClient.Object));

            // Setting from 0 to 0 should not trigger changes
            target.ResetFcnt();
            Assert.False(target.HasFrameCountChanges);
            Assert.Equal(0U, target.LastSavedFCntDown);
            Assert.Equal(0U, target.LastSavedFCntUp);
        }

        [Fact]
        public void When_ResetFcnt_In_Device_With_Pending_Changes_Should_Have_HasFrameCountChanges_True()
        {
            // Non zero fcnt up
            var target = new LoRaDevice("1231", "12312", new SingleDeviceConnectionManager(this.loRaDeviceClient.Object));
            target.SetFcntUp(1);
            target.AcceptFrameCountChanges();
            target.ResetFcnt();
            Assert.Equal(0U, target.FCntUp);
            Assert.Equal(0U, target.FCntDown);
            Assert.True(target.HasFrameCountChanges);

            // Non zero fcnt down
            target = new LoRaDevice("1231", "12312", new SingleDeviceConnectionManager(this.loRaDeviceClient.Object));
            target.SetFcntDown(1);
            target.AcceptFrameCountChanges();
            target.ResetFcnt();
            Assert.Equal(0U, target.FCntUp);
            Assert.Equal(0U, target.FCntDown);
            Assert.True(target.HasFrameCountChanges);

            // Non zero fcnt down and up
            target = new LoRaDevice("1231", "12312", new SingleDeviceConnectionManager(this.loRaDeviceClient.Object));
            target.SetFcntDown(1);
            target.SetFcntDown(2);
            target.AcceptFrameCountChanges();
            target.ResetFcnt();
            Assert.Equal(0U, target.FCntUp);
            Assert.Equal(0U, target.FCntDown);
            Assert.True(target.HasFrameCountChanges);
        }

        [Fact]
        public void When_ResetFcnt_In_NonZero_FcntUp_Or_FcntDown_Should_Have_HasFrameCountChanges_True()
        {
            // Non zero fcnt up
            var target = new LoRaDevice("1231", "12312", new SingleDeviceConnectionManager(this.loRaDeviceClient.Object));
            target.SetFcntUp(1);
            target.AcceptFrameCountChanges();
            target.ResetFcnt();
            Assert.Equal(0U, target.FCntUp);
            Assert.Equal(0U, target.FCntDown);
            Assert.True(target.HasFrameCountChanges);

            // Non zero fcnt down
            target = new LoRaDevice("1231", "12312", new SingleDeviceConnectionManager(this.loRaDeviceClient.Object));
            target.SetFcntDown(1);
            target.AcceptFrameCountChanges();
            target.ResetFcnt();
            Assert.Equal(0U, target.FCntUp);
            Assert.Equal(0U, target.FCntDown);
            Assert.Equal(0U, target.LastSavedFCntUp);
            Assert.Equal(1U, target.LastSavedFCntDown);
            Assert.True(target.HasFrameCountChanges);

            // Non zero fcnt down and up
            target = new LoRaDevice("1231", "12312", new SingleDeviceConnectionManager(this.loRaDeviceClient.Object));
            target.SetFcntDown(1);
            target.SetFcntDown(2);
            target.AcceptFrameCountChanges();
            target.ResetFcnt();
            Assert.Equal(0U, target.FCntUp);
            Assert.Equal(0U, target.FCntDown);
            Assert.Equal(0U, target.LastSavedFCntUp);
            Assert.Equal(2U, target.LastSavedFCntDown);
            Assert.Empty(target.PreferredGatewayID);
            Assert.Equal(LoRaRegionType.NotSet, target.LoRaRegion);

            Assert.True(target.HasFrameCountChanges);
        }

        [Fact]
        public async Task When_Initialized_ABP_Device_Has_Fcnt_Should_Have_Non_Zero_Fcnt_Values()
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
                    { "FCntDown", 10 },
                    { "FCntUp", 20 },
                });

            this.loRaDeviceClient.Setup(x => x.GetTwinAsync())
                .ReturnsAsync(twin);

            var loRaDevice = new LoRaDevice("00000001", "ABC0200000000009", new SingleDeviceConnectionManager(this.loRaDeviceClient.Object));
            await loRaDevice.InitializeAsync();
            Assert.True(loRaDevice.IsOurDevice);
            Assert.Equal(10U, loRaDevice.FCntDown);
            Assert.Equal(10U, loRaDevice.LastSavedFCntDown);
            Assert.Equal(20U, loRaDevice.FCntUp);
            Assert.Equal(20U, loRaDevice.LastSavedFCntUp);
            Assert.Empty(loRaDevice.PreferredGatewayID);
            Assert.Equal(LoRaRegionType.NotSet, loRaDevice.LoRaRegion);
            Assert.False(loRaDevice.HasFrameCountChanges);
        }

        [Theory]
        [CombinatorialData]
        public async Task When_Initialized_With_PreferredGateway_And_Region_Should_Get_Properties(
            [CombinatorialValues("EU868", "3132", "eu868", "US915", "us915", "eu")] string regionValue)
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
                    { "FCntDown", 10 },
                    { "FCntUp", 20 },
                    { "PreferredGatewayID", "gateway1" },
                    { "Region", regionValue }
                });

            this.loRaDeviceClient.Setup(x => x.GetTwinAsync())
                .ReturnsAsync(twin);

            var loRaDevice = new LoRaDevice("00000001", "ABC0200000000009", new SingleDeviceConnectionManager(this.loRaDeviceClient.Object));
            await loRaDevice.InitializeAsync();
            Assert.Equal("gateway1", loRaDevice.PreferredGatewayID);

            if (string.Equals(LoRaRegionType.EU868.ToString(), regionValue, StringComparison.InvariantCultureIgnoreCase))
                Assert.Equal(LoRaRegionType.EU868, loRaDevice.LoRaRegion);
            else if (string.Equals(LoRaRegionType.US915.ToString(), regionValue, StringComparison.InvariantCultureIgnoreCase))
                Assert.Equal(LoRaRegionType.US915, loRaDevice.LoRaRegion);
            else
                Assert.Equal(LoRaRegionType.NotSet, loRaDevice.LoRaRegion);
        }

        [Theory]
        [InlineData("dasda", 0)]
        [InlineData(0, 0)]
        [InlineData("59", 60)] // min => 60
        [InlineData(59, 60)] // min => 60
        [InlineData(120, 120)]
        public async Task When_Initialized_With_Keep_Alive_Should_Read_Value_From_Twin(object keepAliveTimeoutValue, int expectedKeepAliveTimeout)
        {
            var twin = TestUtils.CreateTwin(
                desired: new Dictionary<string, object>
                {
                    { "NwkSKey", "ABC02000000000000000000000000009ABC02000000000000000000000000009" },
                    { "AppSKey", "ABCD2000000000000000000000000009ABC02000000000000000000000000009" },
                    { "DevAddr", "0000AABB" },
                    { "GatewayID", "mygateway" },
                    { "SensorDecoder", "DecoderValueSensor" },
                    { "KeepAliveTimeout", keepAliveTimeoutValue },
                    { "$version", 1 },
                },
                reported: new Dictionary<string, object>
                {
                    { "$version", 1 },
                    { "NwkSKey", "ABC02000000000000000000000000009ABC02000000000000000000000000009" },
                    { "AppSKey", "ABCD2000000000000000000000000009ABC02000000000000000000000000009" },
                    { "DevAddr", "0000AABB" }
                });

            this.loRaDeviceClient.Setup(x => x.GetTwinAsync())
                .ReturnsAsync(twin);

            var loRaDevice = new LoRaDevice("00000001", "ABC0200000000009", new SingleDeviceConnectionManager(this.loRaDeviceClient.Object));
            await loRaDevice.InitializeAsync();
            Assert.Equal(expectedKeepAliveTimeout, loRaDevice.KeepAliveTimeout);
        }

        [Fact]
        public void When_Device_Has_No_Connection_Timeout_Should_Disconnect()
        {
            var deviceClient = new Mock<ILoRaDeviceClient>(MockBehavior.Strict);
            var cache = new MemoryCache(new MemoryCacheOptions());
            var manager = new LoRaDeviceClientConnectionManager(cache);
            var device = new LoRaDevice("00000000", "0123456789", manager);
            manager.Register(device, deviceClient.Object);

            var activity = device.BeginDeviceClientConnectionActivity();
            Assert.NotNull(activity);

            deviceClient.Setup(x => x.Disconnect())
                .Returns(true);

            Assert.True(device.TryDisconnect());

            deviceClient.Verify(x => x.Disconnect(), Times.Once());
        }

        [Fact]
        public void When_Device_Connection_Not_In_Use_Should_Disconnect()
        {
            var deviceClient = new Mock<ILoRaDeviceClient>(MockBehavior.Strict);
            var cache = new MemoryCache(new MemoryCacheOptions());
            var manager = new LoRaDeviceClientConnectionManager(cache);
            var device = new LoRaDevice("00000000", "0123456789", manager);
            device.KeepAliveTimeout = 60;
            manager.Register(device, deviceClient.Object);

            deviceClient.Setup(x => x.EnsureConnected())
                .Returns(true);

            var activity1 = device.BeginDeviceClientConnectionActivity();
            Assert.NotNull(activity1);

            Assert.False(device.TryDisconnect());

            var activity2 = device.BeginDeviceClientConnectionActivity();
            Assert.NotNull(activity2);

            Assert.False(device.TryDisconnect());
            activity1.Dispose();
            Assert.False(device.TryDisconnect());

            activity2.Dispose();
            deviceClient.Setup(x => x.Disconnect())
                .Returns(true);

            Assert.True(device.TryDisconnect());

            deviceClient.Verify(x => x.EnsureConnected(), Times.Exactly(2));
        }

        [Fact]
        public void When_Needed_Should_Reconnect_Client()
        {
            var deviceClient = new Mock<ILoRaDeviceClient>(MockBehavior.Strict);
            var cache = new MemoryCache(new MemoryCacheOptions());
            var manager = new LoRaDeviceClientConnectionManager(cache);
            var device = new LoRaDevice("00000000", "0123456789", manager);
            device.KeepAliveTimeout = 60;
            manager.Register(device, deviceClient.Object);

            deviceClient.Setup(x => x.EnsureConnected())
                .Returns(true);

            deviceClient.Setup(x => x.Disconnect())
                .Returns(true);

            using (var activity1 = device.BeginDeviceClientConnectionActivity())
            {
                Assert.NotNull(activity1);
            }

            Assert.True(device.TryDisconnect());

            using (var activity2 = device.BeginDeviceClientConnectionActivity())
            {
                Assert.NotNull(activity2);

                Assert.False(device.TryDisconnect());
            }

            Assert.True(device.TryDisconnect());

            deviceClient.Verify(x => x.EnsureConnected(), Times.Exactly(2));
            deviceClient.Verify(x => x.Disconnect(), Times.Exactly(2));
        }
    }
}
