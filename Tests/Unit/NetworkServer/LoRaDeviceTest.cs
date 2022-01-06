// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Unit.NetworkServer
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using global::LoRaTools.Regions;
    using global::LoRaTools.Utils;
    using LoRaWan.NetworkServer;
    using LoRaWan.Tests.Common;
    using Microsoft.Azure.Devices.Client.Exceptions;
    using Microsoft.Azure.Devices.Shared;
    using Microsoft.Extensions.Caching.Memory;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Logging.Abstractions;
    using Moq;
    using MoreLinq;
    using Xunit;
    using static LoRaWan.DataRateIndex;

    /// <summary>
    /// Tests the <see cref="LoRaDevice"/>.
    /// </summary>
    public class LoRaDeviceTest
    {
        private readonly Mock<ILoRaDeviceClient> loRaDeviceClient;
        private readonly NetworkServerConfiguration configuration;

        public LoRaDeviceTest()
        {
            this.loRaDeviceClient = new Mock<ILoRaDeviceClient>(MockBehavior.Strict);
            this.loRaDeviceClient.Setup(ldc => ldc.Dispose());
            this.configuration = new NetworkServerConfiguration { GatewayID = "test-gateway" };
        }

        [Fact]
        public async Task When_No_Changes_Were_Made_Should_Not_Save_Frame_Counter()
        {
            using var target = CreateDefaultDevice();
            await target.SaveChangesAsync();
        }

        [Fact]
        public async Task When_Incrementing_FcntDown_Should_Save_Frame_Counter()
        {
            using var target = CreateDefaultDevice();

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
            using var target = CreateDefaultDevice();

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
            using var target = CreateDefaultDevice();

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
            using var target = CreateDefaultDevice();
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

            this.loRaDeviceClient.Setup(x => x.GetTwinAsync(CancellationToken.None))
                .ReturnsAsync(twin);

            using var connectionManager = new SingleDeviceConnectionManager(this.loRaDeviceClient.Object);
            using var loRaDevice = new LoRaDevice(string.Empty, "ABC0200000000009", connectionManager);
            await loRaDevice.InitializeAsync(this.configuration);
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
            Assert.Null(loRaDevice.DevNonce);
            Assert.Empty(loRaDevice.NetID ?? string.Empty);
            Assert.False(loRaDevice.IsABP);
            Assert.False(loRaDevice.IsOurDevice);
            Assert.Null(loRaDevice.ReportedDwellTimeSetting);
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

            this.loRaDeviceClient.Setup(x => x.GetTwinAsync(CancellationToken.None))
                .ReturnsAsync(twin);

            using var loRaDevice = CreateDefaultDevice();

            await loRaDevice.InitializeAsync(this.configuration);
            Assert.Equal("ABC0200000000009", loRaDevice.AppEUI);
            Assert.Equal("ABC02000000000000000000000000009", loRaDevice.AppKey);
            Assert.Equal("mygateway", loRaDevice.GatewayID);
            Assert.Equal("DecoderValueSensor", loRaDevice.SensorDecoder);
            Assert.False(loRaDevice.IsABP);
            Assert.False(loRaDevice.IsOurDevice);
            Assert.Equal("ABC02000000000000000000000000009ABC02000000000000000000000000009", loRaDevice.NwkSKey);
            Assert.Equal("ABCD2000000000000000000000000009ABC02000000000000000000000000009", loRaDevice.AppSKey);
            Assert.Equal(new DevNonce(123), loRaDevice.DevNonce);
            Assert.Equal("0000AABB", loRaDevice.DevAddr);
            Assert.Null(loRaDevice.ReportedDwellTimeSetting);
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
                    { "GatewayID", this.configuration.GatewayID },
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

            this.loRaDeviceClient.Setup(x => x.GetTwinAsync(CancellationToken.None))
                .ReturnsAsync(twin);

            using var loRaDevice = CreateDefaultDevice();

            await loRaDevice.InitializeAsync(this.configuration);
            Assert.Empty(loRaDevice.AppEUI ?? string.Empty);
            Assert.Empty(loRaDevice.AppKey ?? string.Empty);
            Assert.Equal(this.configuration.GatewayID, loRaDevice.GatewayID);
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
            Assert.Null(loRaDevice.DevNonce);
            Assert.Equal("0000AABB", loRaDevice.DevAddr);
            Assert.Null(loRaDevice.ReportedDwellTimeSetting);
        }

        [Theory]
        [InlineData("false")]
        [InlineData("FALSE")]
        [InlineData(0)]
        [InlineData(false)]
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

            this.loRaDeviceClient.Setup(x => x.GetTwinAsync(CancellationToken.None))
                .ReturnsAsync(twin);

            using var loRaDevice = CreateDefaultDevice();
            await loRaDevice.InitializeAsync(this.configuration);
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

            this.loRaDeviceClient.Setup(x => x.GetTwinAsync(CancellationToken.None))
                .ReturnsAsync(twin);

            using var loRaDevice = CreateDefaultDevice();
            await loRaDevice.InitializeAsync(this.configuration);
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

            this.loRaDeviceClient.Setup(x => x.GetTwinAsync(CancellationToken.None))
                .ReturnsAsync(twin);

            using var loRaDevice = CreateDefaultDevice();
            await loRaDevice.InitializeAsync(this.configuration);
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

            this.loRaDeviceClient.Setup(x => x.GetTwinAsync(CancellationToken.None))
                .ReturnsAsync(twin);

            using var loRaDevice = CreateDefaultDevice();
            await loRaDevice.InitializeAsync(this.configuration);
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

            this.loRaDeviceClient.Setup(x => x.GetTwinAsync(CancellationToken.None))
                .ReturnsAsync(twin);

            using var loRaDevice = CreateDefaultDevice();
            await loRaDevice.InitializeAsync(this.configuration);
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

            this.loRaDeviceClient.Setup(x => x.GetTwinAsync(CancellationToken.None))
                .ReturnsAsync(twin);

            using var loRaDevice = CreateDefaultDevice();
            await loRaDevice.InitializeAsync(this.configuration);
            Assert.Equal(1, loRaDevice.PreferredWindow);
        }

        [Fact]
        public async Task When_CN470JoinChannel_Is_In_Twin_Should_Have_JoinChannel_Set()
        {
            var twin = TestUtils.CreateTwin(
                desired: new Dictionary<string, object>
                {
                    { "NwkSKey", "ABC02000000000000000000000000009ABC02000000000000000000000000009" },
                    { "AppSKey", "ABCD2000000000000000000000000009ABC02000000000000000000000000009" },
                    { "DevAddr", "0000AABB" },
                    { "CN470JoinChannel", 10 }
                },
                reported: new Dictionary<string, object>
                {
                    { "NwkSKey", "ABC02000000000000000000000000009ABC02000000000000000000000000009" },
                    { "AppSKey", "ABCD2000000000000000000000000009ABC02000000000000000000000000009" },
                    { "DevAddr", "0000AABB" },
                    { "CN470JoinChannel", 2 }
                });

            this.loRaDeviceClient.Setup(x => x.GetTwinAsync(CancellationToken.None))
                .ReturnsAsync(twin);

            using var loRaDevice = CreateDefaultDevice();
            await loRaDevice.InitializeAsync(this.configuration);
            Assert.Equal(2, loRaDevice.ReportedCN470JoinChannel); // check that reported property is prioritized
        }

        [Fact]
        public void New_LoRaDevice_Should_Have_C2D_Enabled()
        {
            using var loRaDevice = CreateDefaultDevice();
            Assert.True(loRaDevice.DownlinkEnabled);
        }

        [Fact]
        public void New_LoRaDevice_Should_Have_PreferredWindow_As_1()
        {
            using var loRaDevice = CreateDefaultDevice();
            Assert.Equal(1, loRaDevice.PreferredWindow);
        }

        [Fact]
        public void After_3_Resubmits_Should_Not_Be_Valid_To_Resend_Ack()
        {
            using var target = CreateDefaultDevice();

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
            using var target = CreateDefaultDevice();

            // Setting from 0 to 0 should not trigger changes
            target.ResetFcnt();
            Assert.False(target.HasFrameCountChanges);
            Assert.Equal(0U, target.LastSavedFCntDown);
            Assert.Equal(0U, target.LastSavedFCntUp);
        }

        [Fact]
        public async Task When_Updating_LastUpdate_Is_Updated()
        {
            var twin = TestUtils.CreateTwin(desired: GetEssentialDesiredProperties());

            this.loRaDeviceClient.Setup(x => x.GetTwinAsync(CancellationToken.None))
                .ReturnsAsync(twin);

            using var loRaDevice = CreateDefaultDevice();
            var lastUpdate = loRaDevice.LastUpdate = DateTime.UtcNow - TimeSpan.FromDays(1);
            await loRaDevice.InitializeAsync(this.configuration);
            Assert.True(loRaDevice.LastUpdate > lastUpdate);
        }

        [Fact]
        public async Task When_Update_Fails_LastUpdate_Is_Not_Changed()
        {
            this.loRaDeviceClient.Setup(x => x.GetTwinAsync(CancellationToken.None))
                .ThrowsAsync(new IotHubException());

            using var loRaDevice = CreateDefaultDevice();
            var lastUpdate = loRaDevice.LastUpdate = DateTime.UtcNow - TimeSpan.FromDays(1);
            await Assert.ThrowsAsync<LoRaProcessingException>(async () => await loRaDevice.InitializeAsync(this.configuration));
            Assert.Equal(lastUpdate, loRaDevice.LastUpdate);
        }

        [Fact]
        public void When_ResetFcnt_In_Device_With_Pending_Changes_Should_Have_HasFrameCountChanges_True()
        {
            // Non zero fcnt up
            using var target = CreateDefaultDevice();
            target.SetFcntUp(1);
            target.AcceptFrameCountChanges();
            target.ResetFcnt();
            Assert.Equal(0U, target.FCntUp);
            Assert.Equal(0U, target.FCntDown);
            Assert.True(target.HasFrameCountChanges);

            // Non zero fcnt down
            using var secondConnectionManager = new SingleDeviceConnectionManager(this.loRaDeviceClient.Object);
            using var secondTarget = new LoRaDevice("1231", "12312", secondConnectionManager);
            secondTarget.SetFcntDown(1);
            secondTarget.AcceptFrameCountChanges();
            secondTarget.ResetFcnt();
            Assert.Equal(0U, secondTarget.FCntUp);
            Assert.Equal(0U, secondTarget.FCntDown);
            Assert.True(secondTarget.HasFrameCountChanges);

            // Non zero fcnt down and up
            using var thirdConnectionManager = new SingleDeviceConnectionManager(this.loRaDeviceClient.Object);
            using var thirdTarget = new LoRaDevice("1231", "12312", thirdConnectionManager);
            thirdTarget.SetFcntDown(1);
            thirdTarget.SetFcntDown(2);
            thirdTarget.AcceptFrameCountChanges();
            thirdTarget.ResetFcnt();
            Assert.Equal(0U, thirdTarget.FCntUp);
            Assert.Equal(0U, thirdTarget.FCntDown);
            Assert.True(thirdTarget.HasFrameCountChanges);
        }

        [Fact]
        public void When_ResetFcnt_In_NonZero_FcntUp_Or_FcntDown_Should_Have_HasFrameCountChanges_True()
        {
            // Non zero fcnt up
            using var target = CreateDefaultDevice();
            target.SetFcntUp(1);
            target.AcceptFrameCountChanges();
            target.ResetFcnt();
            Assert.Equal(0U, target.FCntUp);
            Assert.Equal(0U, target.FCntDown);
            Assert.True(target.HasFrameCountChanges);

            // Non zero fcnt down
            using var secondConnectionManager = new SingleDeviceConnectionManager(this.loRaDeviceClient.Object);
            using var secondTarget = new LoRaDevice("1231", "12312", secondConnectionManager);
            secondTarget.SetFcntDown(1);
            secondTarget.AcceptFrameCountChanges();
            secondTarget.ResetFcnt();
            Assert.Equal(0U, secondTarget.FCntUp);
            Assert.Equal(0U, secondTarget.FCntDown);
            Assert.Equal(0U, secondTarget.LastSavedFCntUp);
            Assert.Equal(1U, secondTarget.LastSavedFCntDown);
            Assert.True(secondTarget.HasFrameCountChanges);

            // Non zero fcnt down and up
            using var thirdConnectionManager = new SingleDeviceConnectionManager(this.loRaDeviceClient.Object);
            using var thirdTarget = new LoRaDevice("1231", "12312", thirdConnectionManager);
            thirdTarget.SetFcntDown(1);
            thirdTarget.SetFcntDown(2);
            thirdTarget.AcceptFrameCountChanges();
            thirdTarget.ResetFcnt();
            Assert.Equal(0U, thirdTarget.FCntUp);
            Assert.Equal(0U, thirdTarget.FCntDown);
            Assert.Equal(0U, thirdTarget.LastSavedFCntUp);
            Assert.Equal(2U, thirdTarget.LastSavedFCntDown);
            Assert.Empty(thirdTarget.PreferredGatewayID);
            Assert.Equal(LoRaRegionType.NotSet, thirdTarget.LoRaRegion);

            Assert.True(thirdTarget.HasFrameCountChanges);
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
                    { "GatewayID", this.configuration.GatewayID },
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

            this.loRaDeviceClient.Setup(x => x.GetTwinAsync(CancellationToken.None))
                .ReturnsAsync(twin);

            using var loRaDevice = CreateDefaultDevice();

            await loRaDevice.InitializeAsync(this.configuration);
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

            this.loRaDeviceClient.Setup(x => x.GetTwinAsync(CancellationToken.None))
                .ReturnsAsync(twin);

            using var loRaDevice = CreateDefaultDevice();
            await loRaDevice.InitializeAsync(this.configuration);

            if (string.Equals(LoRaRegionType.EU868.ToString(), regionValue, StringComparison.OrdinalIgnoreCase))
                Assert.Equal(LoRaRegionType.EU868, loRaDevice.LoRaRegion);
            else if (string.Equals(LoRaRegionType.US915.ToString(), regionValue, StringComparison.OrdinalIgnoreCase))
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

            this.loRaDeviceClient.Setup(x => x.GetTwinAsync(CancellationToken.None))
                .ReturnsAsync(twin);

            using var loRaDevice = CreateDefaultDevice();
            await loRaDevice.InitializeAsync(this.configuration);
            Assert.Equal(expectedKeepAliveTimeout, loRaDevice.KeepAliveTimeout);
        }

        [Fact]
        public void When_Device_Has_No_Connection_Timeout_Should_Disconnect()
        {
            var deviceClient = new Mock<ILoRaDeviceClient>(MockBehavior.Strict);
            deviceClient.Setup(dc => dc.Dispose());
            using var cache = new MemoryCache(new MemoryCacheOptions());
            using var manager = new LoRaDeviceClientConnectionManager(cache, NullLogger<LoRaDeviceClientConnectionManager>.Instance);
            using var device = new LoRaDevice("00000000", "0123456789", manager);
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
            deviceClient.Setup(dc => dc.Dispose());
            using var cache = new MemoryCache(new MemoryCacheOptions());
            using var manager = new LoRaDeviceClientConnectionManager(cache, NullLogger<LoRaDeviceClientConnectionManager>.Instance);
            using var device = new LoRaDevice("00000000", "0123456789", manager);
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
            deviceClient.Setup(dc => dc.Dispose());
            using var cache = new MemoryCache(new MemoryCacheOptions());
            using var manager = new LoRaDeviceClientConnectionManager(cache, NullLogger<LoRaDeviceClientConnectionManager>.Instance);
            using var device = new LoRaDevice("00000000", "0123456789", manager);
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

        [Fact]
        public async Task When_Initialized_With_Class_C_And_Custom_RX2DR_Should_Have_Correct_Properties()
        {
            const string appSKey = "ABCD2000000000000000000000000009ABC02000000000000000000000000009";
            const string nwkSKey = "ABC02000000000000000000000000009ABC02000000000000000000000000009";

            var twin = TestUtils.CreateTwin(
                desired: new Dictionary<string, object>
                {
                    { "AppEUI", "ABC02000000000000000000000000009ABC02000000000000000000000000009" },
                    { "AppKey", "ABCD2000000000000000000000000009ABC02000000000000000000000000009" },
                    { "ClassType", "C" },
                    { "GatewayID", "mygateway" },
                    { "SensorDecoder", "http://mydecoder" },
                    { "RX2DataRate", "10" },
                    { "$version", 1 },
                },
                reported: new Dictionary<string, object>
                {
                    { "$version", 1 },
                    { "NwkSKey", nwkSKey },
                    { "AppSKey", appSKey },
                    { "DevAddr", "0000AABB" },
                    { "FCntDown", 9 },
                    { "FCntUp", 100 },
                    { "DevEUI", "ABC0200000000009" },
                    { "NetId", "010000" },
                    { "DevNonce", "C872" },
                    { "RX2DataRate", 10 },
                    { "Region", "US915" },
                });

            this.loRaDeviceClient.Setup(x => x.GetTwinAsync(CancellationToken.None))
                .ReturnsAsync(twin);

            using var loRaDevice = CreateDefaultDevice();
            await loRaDevice.InitializeAsync(this.configuration);
            Assert.Equal(LoRaDeviceClassType.C, loRaDevice.ClassType);
            Assert.Equal("mygateway", loRaDevice.GatewayID);
            Assert.Equal(9u, loRaDevice.FCntDown);
            Assert.Equal(100u, loRaDevice.FCntUp);
            Assert.Equal(DR10, loRaDevice.ReportedRX2DataRate.Value);
            Assert.Equal(DR10, loRaDevice.DesiredRX2DataRate.Value);
            Assert.Equal(appSKey, loRaDevice.AppSKey);
            Assert.Equal(nwkSKey, loRaDevice.NwkSKey);
            Assert.Equal(LoRaRegionType.US915, loRaDevice.LoRaRegion);
            Assert.False(loRaDevice.IsABP);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void When_Updating_Dwell_Time_Settings_Should_Update(bool acceptChanges)
        {
            // arrange
            var dwellTimeSetting = new DwellTimeSetting(true, false, 3);
            using var loRaDevice = CreateDefaultDevice();

            // act
            loRaDevice.UpdateDwellTimeSetting(dwellTimeSetting, acceptChanges);

            // assert
            Assert.Equal(dwellTimeSetting, loRaDevice.ReportedDwellTimeSetting);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task When_Updating_Dwell_Time_Settings_Save_Success(bool acceptChanges)
        {
            // arrange
            var dwellTimeSetting = new DwellTimeSetting(true, false, 3);
            using var loRaDevice = CreateDefaultDevice();
            TwinCollection actualReportedProperties = null;
            this.loRaDeviceClient.Setup(x => x.UpdateReportedPropertiesAsync(It.IsAny<TwinCollection>()))
                                 .Callback((TwinCollection t) => actualReportedProperties = t)
                                 .ReturnsAsync(true);

            // act
            loRaDevice.UpdateDwellTimeSetting(dwellTimeSetting, acceptChanges);
            await loRaDevice.SaveChangesAsync();

            // assert
            Assert.Equal(dwellTimeSetting, loRaDevice.ReportedDwellTimeSetting);
            if (acceptChanges)
            {
                Assert.Null(actualReportedProperties);
                this.loRaDeviceClient.Verify(c => c.UpdateReportedPropertiesAsync(It.IsAny<TwinCollection>()), Times.Never);
            }
            else
            {
                Assert.NotNull(actualReportedProperties);
                Assert.Equal(dwellTimeSetting, JsonSerializer.Deserialize<DwellTimeSetting>(actualReportedProperties[TwinProperty.TxParam].ToString()));
            }
        }

        [Fact]
        public async Task InitializeAsync_Should_Initialize_TxParams()
        {
            // arrange
            using var loRaDevice = CreateDefaultDevice();
            var dwellTimeSetting = new DwellTimeSetting(true, false, 4);
            var twin = TestUtils.CreateTwin(GetEssentialDesiredProperties(),
                                            new Dictionary<string, object> { [TwinProperty.TxParam] = JsonSerializer.Serialize(dwellTimeSetting) });
            this.loRaDeviceClient.Setup(x => x.GetTwinAsync(CancellationToken.None))
                                 .ReturnsAsync(twin);

            // act
            _ = await loRaDevice.InitializeAsync(this.configuration);

            // assert
            Assert.Equal(dwellTimeSetting, loRaDevice.ReportedDwellTimeSetting);
        }

        private static Dictionary<string, object> GetEssentialDesiredProperties() =>
            new Dictionary<string, object>
            {
                ["AppEUI"] = "ABC02000000000000000000000000009ABC02000000000000000000000000009",
                ["AppKey"] = "ABCD2000000000000000000000000009ABC02000000000000000000000000009"
            };

#pragma warning disable CA2000 // Dispose objects before losing scope
        private LoRaDevice CreateDefaultDevice() => new LoRaDevice("FFFFFFFF", "0000000000000000", new SingleDeviceConnectionManager(this.loRaDeviceClient.Object));
#pragma warning restore CA2000 // Dispose objects before losing scope

        public class FrameCounterInitTests
        {
            private readonly Mock<ILoRaDeviceClient> loRaDeviceClient;

            public FrameCounterInitTests()
            {
                this.loRaDeviceClient = new Mock<ILoRaDeviceClient>();
            }

            [Fact]
            public void If_No_Reset_FcntUpDown_Initialized()
            {
                using var device = CreateDefault();

                const uint fcntUp = 10;
                const uint fcntDown = 2;
                var twin = TestUtils.CreateTwin(reported: new Dictionary<string, object>
                                                {
                                                    [TwinProperty.FCntUp] = fcntUp,
                                                    [TwinProperty.FCntDown] = fcntDown
                                                });

                device.ExecuteInitializeFrameCounters(twin);
                AssertFcntUp(fcntUp, device);
                AssertFcntDown(fcntDown, device);

                this.loRaDeviceClient.Verify(x => x.UpdateReportedPropertiesAsync(It.IsAny<TwinCollection>()), Times.Never);
            }

            [Theory]
            [InlineData(1, 1, 10, 10, 0,  0,  true)]   // fcnt start set, but wasn't reported - expect to be set to the start counter and saved
            [InlineData(1, 1, 10, 10, 10, 10, false)]  // all up to date - no update - expect set to last reported
            [InlineData(2, 1, 10, 10, 10, 10, true)]   // reset counter higher - expect to set to start counter and saved
            [InlineData(2, 3, 10, 10, 10, 10, false)]  // reset counter smaller - expect set to last reported
            public void When_Start_Specified_Initialized_Correctly(uint fcntResetDesired, uint fcntResetReported, uint startDesiredUp, uint startDesiredDown, uint startReportedUp, uint startReportedDown, bool expectStart)
            {
                using var device = CreateDefault();

                const uint fcntUp = 10;
                const uint fcntDown = 2;
                var twin = TestUtils.CreateTwin(desired: new Dictionary<string, object>
                                                {
                                                    [TwinProperty.FCntUpStart] = startDesiredUp,
                                                    [TwinProperty.FCntDownStart] = startDesiredDown,
                                                    [TwinProperty.FCntResetCounter] = fcntResetDesired
                                                },
                                                reported: new Dictionary<string, object>
                                                {
                                                    [TwinProperty.FCntUpStart] = startReportedUp,
                                                    [TwinProperty.FCntDownStart] = startReportedDown,
                                                    [TwinProperty.FCntUp] = fcntUp,
                                                    [TwinProperty.FCntDown] = fcntDown,
                                                    [TwinProperty.FCntResetCounter] = fcntResetReported
                                                });

                device.ExecuteInitializeFrameCounters(twin);

                this.loRaDeviceClient.Verify(x => x.UpdateReportedPropertiesAsync(It.IsAny<TwinCollection>()), expectStart ? Times.Once : Times.Never);

                if (expectStart)
                {
                    AssertFcntUp(startDesiredUp, device);
                    AssertFcntDown(startDesiredDown, device);
                }
                else
                {
                    AssertFcntUp(fcntUp, device);
                    AssertFcntDown(fcntDown, device);
                }
            }

            [Theory]
            [InlineData(10, 10, 0, 0, true)]
            [InlineData(10, 10, 10, 10, false)]
            public void When_Reset_Specified_Initialized_Correctly(uint startDesiredUp, uint startDesiredDown, uint startReportedUp, uint startReportedDown, bool expectStart)
            {
                using var device = CreateDefault();

                const uint fcntUp = 10;
                const uint fcntDown = 2;
                var twin = TestUtils.CreateTwin(desired: new Dictionary<string, object>
                                                {
                                                    [TwinProperty.FCntUpStart] = startDesiredUp,
                                                    [TwinProperty.FCntDownStart] = startDesiredDown
                                                },
                                                reported: new Dictionary<string, object>
                                                {
                                                    [TwinProperty.FCntUpStart] = startReportedUp,
                                                    [TwinProperty.FCntDownStart] = startReportedDown,
                                                    [TwinProperty.FCntUp] = fcntUp,
                                                    [TwinProperty.FCntDown] = fcntDown
                                                });

                device.ExecuteInitializeFrameCounters(twin);

                this.loRaDeviceClient.Verify(x => x.UpdateReportedPropertiesAsync(It.IsAny<TwinCollection>()), expectStart ? Times.Once : Times.Never);

                if (expectStart)
                {
                    AssertFcntUp(startDesiredUp, device);
                    AssertFcntDown(startDesiredDown, device);
                }
                else
                {
                    AssertFcntUp(fcntUp, device);
                    AssertFcntDown(fcntDown, device);
                }
            }

            private static void AssertFcntUp(uint expected, LoRaDevice device)
                => AssertEqual(expected, new[] { device.FCntUp, device.LastSavedFCntUp });
            
            private static void AssertFcntDown(uint expected, LoRaDevice device)
                => AssertEqual(expected, new[] { device.FCntDown, device.LastSavedFCntDown });

            private static void AssertEqual<T>(T expected, IEnumerable<T> it)
                => it.ForEach(x => Assert.Equal(expected, x));

            private LoRaDeviceTest CreateDefault()
                => new LoRaDeviceTest(this.loRaDeviceClient.Object);

            private class LoRaDeviceTest : LoRaDevice
            {
                private readonly ILogger<TwinCollectionReader> logger = NullLogger<TwinCollectionReader>.Instance;

                public LoRaDeviceTest(ILoRaDeviceClient deviceClient)
#pragma warning disable CA2000 // Dispose objects before losing scope - ownership is transferred
                    : base ("FFFFFFFF", "0000000000000000", new SingleDeviceConnectionManager(deviceClient))
#pragma warning restore CA2000 // Dispose objects before losing scope
                {

                }

                public void ExecuteInitializeFrameCounters(Twin twin)
                    => InitializeFrameCounters(new TwinCollectionReader(twin.Properties.Desired, this.logger),
                                               new TwinCollectionReader(twin.Properties.Reported, this.logger));
            }
        }
    }
}
