// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Unit.NetworkServer
{
    using System;
    using System.Collections.Generic;
    using System.Security.Cryptography;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using global::LoRaTools.Regions;
    using global::LoRaTools.Utils;
    using LoRaWan.NetworkServer;
    using LoRaWan.Tests.Common;
    using Microsoft.Azure.Devices.Client.Exceptions;
    using Microsoft.Azure.Devices.Shared;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Logging.Abstractions;
    using Moq;
    using MoreLinq;
    using Xunit;
    using static LoRaWan.DataRateIndex;
    using static LoRaWan.ReceiveWindowNumber;

    /// <summary>
    /// Tests the <see cref="LoRaDevice"/>.
    /// </summary>
    public class LoRaDeviceTest
    {
        private static readonly NetworkServerConfiguration Configuration = new() { GatewayID = "test-gateway" };
        private static readonly LoRaDesiredTwinProperties OtaaDesiredTwinProperties = new()
        {
            JoinEui = new JoinEui((ulong)RandomNumberGenerator.GetInt32(0, int.MaxValue)),
            AppKey = TestKeys.CreateAppKey(),
            GatewayId = Configuration.GatewayID,
            SensorDecoder = "DecoderValueSensor",
            Version = 1,
        };
        private static readonly LoRaReportedTwinProperties OtaaReportedTwinProperties = new()
        {
            Version = 1,
            NetworkSessionKey = TestKeys.CreateNetworkSessionKey(),
            AppSessionKey = TestKeys.CreateAppSessionKey(),
            DevNonce = new DevNonce((ushort)RandomNumberGenerator.GetInt32(0, ushort.MaxValue)),
            DevAddr = new DevAddr((uint)RandomNumberGenerator.GetInt32(0, int.MaxValue)),
        };
        private static readonly LoRaDesiredTwinProperties AbpDesiredTwinProperties = new()
        {
            NetworkSessionKey = TestKeys.CreateNetworkSessionKey(),
            AppSessionKey = TestKeys.CreateAppSessionKey(),
            DevAddr = new DevAddr((uint)RandomNumberGenerator.GetInt32(0, int.MaxValue)),
            GatewayId = Configuration.GatewayID,
            SensorDecoder = "DecoderValueSensor",
            Version = 1,
        };
        private static readonly LoRaReportedTwinProperties AbpReportedTwinProperties = new()
        {
            Version = 1,
            NetworkSessionKey = TestKeys.CreateNetworkSessionKey(),
            AppSessionKey = TestKeys.CreateAppSessionKey(),
            DevAddr = AbpDesiredTwinProperties.DevAddr,
        };

        private readonly Mock<ILoRaDeviceClient> loRaDeviceClient;

        public LoRaDeviceTest()
        {
            this.loRaDeviceClient = new Mock<ILoRaDeviceClient>(MockBehavior.Strict);
            this.loRaDeviceClient.Setup(ldc => ldc.DisposeAsync()).Returns(ValueTask.CompletedTask);
        }

        [Fact]
        public async Task When_Disposing_Device_ConnectionManager_Should_Release_It()
        {
            var connectionManager = new Mock<ILoRaDeviceClientConnectionManager>();
            var target = CreateDefaultDevice(connectionManager.Object);

            // act
            await target.DisposeAsync();

            // assert
            connectionManager.Verify(x => x.ReleaseAsync(target), Times.Once());
        }

        [Fact]
        public async Task When_No_Changes_Were_Made_Should_Not_Save_Frame_Counter()
        {
            await using var target = CreateDefaultDevice();
            await target.SaveChangesAsync();
        }

        [Fact]
        public async Task When_Incrementing_FcntDown_Should_Save_Frame_Counter()
        {
            await using var target = CreateDefaultDevice();

            this.loRaDeviceClient.Setup(x => x.UpdateReportedPropertiesAsync(It.IsAny<TwinCollection>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            Assert.Equal(10U, target.IncrementFcntDown(10));
            Assert.Equal(0U, target.LastSavedFCntDown);
            await target.SaveChangesAsync();
            Assert.Equal(10U, target.LastSavedFCntDown);
        }

        [Fact]
        public async Task When_Setting_FcntDown_Should_Save_Frame_Counter()
        {
            await using var target = CreateDefaultDevice();

            this.loRaDeviceClient.Setup(x => x.UpdateReportedPropertiesAsync(It.IsAny<TwinCollection>(), It.IsAny<CancellationToken>()))
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
            await using var target = CreateDefaultDevice();

            this.loRaDeviceClient.Setup(x => x.UpdateReportedPropertiesAsync(It.IsAny<TwinCollection>(), It.IsAny<CancellationToken>()))
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
            await using var target = CreateDefaultDevice();
            this.loRaDeviceClient.Setup(x => x.UpdateReportedPropertiesAsync(It.IsAny<TwinCollection>(), It.IsAny<CancellationToken>()))
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
            var twin = LoRaDeviceTwin.Create(OtaaDesiredTwinProperties, new LoRaReportedTwinProperties { Version = 1 });

            this.loRaDeviceClient.Setup(x => x.GetTwinAsync(CancellationToken.None))
                .ReturnsAsync(twin);

            await using var connectionManager = new SingleDeviceConnectionManager(this.loRaDeviceClient.Object);
            await using var loRaDevice = new LoRaDevice(null, new DevEui(0xabc0200000000009), connectionManager);
            await loRaDevice.InitializeAsync(Configuration);
            Assert.Equal(OtaaDesiredTwinProperties.JoinEui, loRaDevice.AppEui);
            Assert.Equal(OtaaDesiredTwinProperties.AppKey, loRaDevice.AppKey);
            Assert.Equal(OtaaDesiredTwinProperties.GatewayId, loRaDevice.GatewayID);
            Assert.Equal(OtaaDesiredTwinProperties.SensorDecoder, loRaDevice.SensorDecoder);
            Assert.Equal(0U, loRaDevice.FCntDown);
            Assert.Equal(0U, loRaDevice.LastSavedFCntDown);
            Assert.Equal(0U, loRaDevice.FCntUp);
            Assert.Equal(0U, loRaDevice.LastSavedFCntUp);
            Assert.False(loRaDevice.HasFrameCountChanges);
            Assert.Null(loRaDevice.AppSKey);
            Assert.Null(loRaDevice.NwkSKey);
            Assert.Null(loRaDevice.DevAddr);
            Assert.Null(loRaDevice.DevNonce);
            Assert.Null(loRaDevice.NetId);
            Assert.False(loRaDevice.IsABP);
            Assert.True(loRaDevice.IsOurDevice);
            Assert.Null(loRaDevice.ReportedDwellTimeSetting);
        }

        [Fact]
        public Task Initializing_Otaa_Device_Should_Determine_Device_Ownership() =>
            Initializing_Device_Should_Determine_Device_Ownership(OtaaDesiredTwinProperties);

        [Fact]
        public Task Initializing_Abp_Device_Should_Determine_Device_Ownership() =>
            Initializing_Device_Should_Determine_Device_Ownership(AbpDesiredTwinProperties);

        private async Task Initializing_Device_Should_Determine_Device_Ownership(LoRaDesiredTwinProperties desiredProperties)
        {
            // arrange
            const string gateway = "mygateway";
            Assert.NotEqual(gateway, desiredProperties.GatewayId);
            this.loRaDeviceClient.Setup(x => x.GetTwinAsync(CancellationToken.None))
                                 .ReturnsAsync(LoRaDeviceTwin.Create(desiredProperties with { GatewayId = gateway }));
            await using var loRaDevice = CreateDefaultDevice();

            // act
            _ = await loRaDevice.InitializeAsync(Configuration);

            // assert
            Assert.False(loRaDevice.IsOurDevice);
            Assert.Equal(gateway, loRaDevice.GatewayID);
        }

        [Fact]
        public async Task When_Initialized_Joined_OTAA_Device_Should_Have_All_Properties()
        {
            var twin = LoRaDeviceTwin.Create(OtaaDesiredTwinProperties, OtaaReportedTwinProperties);

            this.loRaDeviceClient.Setup(x => x.GetTwinAsync(CancellationToken.None))
                .ReturnsAsync(twin);

            await using var loRaDevice = CreateDefaultDevice();

            await loRaDevice.InitializeAsync(Configuration);
            Assert.Equal(OtaaDesiredTwinProperties.JoinEui, loRaDevice.AppEui);
            Assert.Equal(OtaaDesiredTwinProperties.AppKey, loRaDevice.AppKey);
            Assert.Equal(OtaaDesiredTwinProperties.GatewayId, loRaDevice.GatewayID);
            Assert.Equal(OtaaDesiredTwinProperties.SensorDecoder, loRaDevice.SensorDecoder);
            Assert.False(loRaDevice.IsABP);
            Assert.True(loRaDevice.IsOurDevice);
            Assert.Equal(OtaaReportedTwinProperties.NetworkSessionKey, loRaDevice.NwkSKey);
            Assert.Equal(OtaaReportedTwinProperties.AppSessionKey, loRaDevice.AppSKey);
            Assert.Equal(OtaaReportedTwinProperties.DevNonce, loRaDevice.DevNonce);
            Assert.Equal(OtaaReportedTwinProperties.DevAddr, loRaDevice.DevAddr);
            Assert.Null(loRaDevice.ReportedDwellTimeSetting);
        }

        [Fact]
        public async Task When_Initialized_ABP_Device_Should_Have_All_Properties()
        {
            var twin = LoRaDeviceTwin.Create(AbpDesiredTwinProperties, AbpReportedTwinProperties);

            this.loRaDeviceClient.Setup(x => x.GetTwinAsync(CancellationToken.None))
                .ReturnsAsync(twin);

            await using var loRaDevice = CreateDefaultDevice();

            await loRaDevice.InitializeAsync(Configuration);
            Assert.Null(loRaDevice.AppEui);
            Assert.Null(loRaDevice.AppKey);
            Assert.Equal(Configuration.GatewayID, loRaDevice.GatewayID);
            Assert.Equal(AbpDesiredTwinProperties.SensorDecoder, loRaDevice.SensorDecoder);
            Assert.True(loRaDevice.IsABP);
            Assert.True(loRaDevice.IsOurDevice);
            Assert.Equal(0U, loRaDevice.FCntDown);
            Assert.Equal(0U, loRaDevice.LastSavedFCntDown);
            Assert.Equal(0U, loRaDevice.FCntUp);
            Assert.Equal(0U, loRaDevice.LastSavedFCntUp);
            Assert.False(loRaDevice.HasFrameCountChanges);
            Assert.Equal(AbpDesiredTwinProperties.NetworkSessionKey, loRaDevice.NwkSKey);
            Assert.Equal(AbpDesiredTwinProperties.AppSessionKey, loRaDevice.AppSKey);
            Assert.Null(loRaDevice.DevNonce);
            Assert.Equal(AbpDesiredTwinProperties.DevAddr, loRaDevice.DevAddr);
            Assert.Null(loRaDevice.ReportedDwellTimeSetting);
        }

        [Theory]
        [InlineData("false", false)]
        [InlineData("FALSE", false)]
        [InlineData(0, false)]
        [InlineData(false, false)]
        [InlineData("true", true)]
        [InlineData("TRUE", true)]
        [InlineData("1", true)]
        [InlineData(1, true)]
        [InlineData(true, true)]
        public async Task Downlink_Should_Be_Deserialized_Correctly(object downlinkEnabledTwinValue, bool expectedDownlink)
        {
            var twin = LoRaDeviceTwin.Create(AbpDesiredTwinProperties, AbpReportedTwinProperties);
            twin.Properties.Desired["Downlink"] = downlinkEnabledTwinValue;

            this.loRaDeviceClient.Setup(x => x.GetTwinAsync(CancellationToken.None))
                .ReturnsAsync(twin);

            await using var loRaDevice = CreateDefaultDevice();
            await loRaDevice.InitializeAsync(Configuration);
            Assert.Equal(expectedDownlink, loRaDevice.DownlinkEnabled);
        }

        [Fact]
        public async Task When_Downlink_Is_Not_Defined_In_Twin_Should_Have_DownlinkEnabled_Equals_True()
        {
            var twin = LoRaDeviceTwin.Create(AbpDesiredTwinProperties, AbpReportedTwinProperties);

            this.loRaDeviceClient.Setup(x => x.GetTwinAsync(CancellationToken.None))
                .ReturnsAsync(twin);

            await using var loRaDevice = CreateDefaultDevice();
            await loRaDevice.InitializeAsync(Configuration);
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
            var twin = LoRaDeviceTwin.Create(AbpDesiredTwinProperties, AbpReportedTwinProperties);
            twin.Properties.Desired[TwinProperty.PreferredWindow] = preferredWindowTwinValue;

            this.loRaDeviceClient.Setup(x => x.GetTwinAsync(CancellationToken.None))
                .ReturnsAsync(twin);

            await using var loRaDevice = CreateDefaultDevice();
            await loRaDevice.InitializeAsync(Configuration);
            Assert.Equal(ReceiveWindow1, loRaDevice.PreferredWindow);
        }

        [Theory]
        [InlineData("2")]
        [InlineData(2)]
        [InlineData(2.0)]
        public async Task When_PreferredWindow_Is_2_In_Twin_Should_Have_Window2_As_Preferred(object preferredWindowTwinProperty)
        {
            var twin = LoRaDeviceTwin.Create(AbpDesiredTwinProperties, AbpReportedTwinProperties);
            twin.Properties.Desired["PreferredWindow"] = preferredWindowTwinProperty;

            this.loRaDeviceClient.Setup(x => x.GetTwinAsync(CancellationToken.None))
                .ReturnsAsync(twin);

            await using var loRaDevice = CreateDefaultDevice();
            await loRaDevice.InitializeAsync(Configuration);
            Assert.Equal(ReceiveWindow2, loRaDevice.PreferredWindow);
        }

        [Fact]
        public async Task When_PreferredWindow_Is_Not_Define_In_Twin_Should_Have_Window1_As_Preferred()
        {
            var twin = LoRaDeviceTwin.Create(AbpDesiredTwinProperties, AbpReportedTwinProperties);

            this.loRaDeviceClient.Setup(x => x.GetTwinAsync(CancellationToken.None))
                .ReturnsAsync(twin);

            await using var loRaDevice = CreateDefaultDevice();
            await loRaDevice.InitializeAsync(Configuration);
            Assert.Equal(ReceiveWindow1, loRaDevice.PreferredWindow);
        }

        [Fact]
        public async Task When_CN470JoinChannel_Is_In_Twin_Should_Have_JoinChannel_Set()
        {
            var twin = LoRaDeviceTwin.Create(AbpDesiredTwinProperties, AbpReportedTwinProperties);
            twin.Properties.Desired["CN470JoinChannel"] = 10;
            twin.Properties.Reported["CN470JoinChannel"] = 2;

            this.loRaDeviceClient.Setup(x => x.GetTwinAsync(CancellationToken.None))
                .ReturnsAsync(twin);

            await using var loRaDevice = CreateDefaultDevice();
            await loRaDevice.InitializeAsync(Configuration);
            Assert.Equal(2, loRaDevice.ReportedCN470JoinChannel); // check that reported property is prioritized
        }

        [Fact]
        public async Task New_LoRaDevice_Should_Have_C2D_Enabled()
        {
            await using var loRaDevice = CreateDefaultDevice();
            Assert.True(loRaDevice.DownlinkEnabled);
        }

        [Fact]
        public async Task New_LoRaDevice_Should_Have_PreferredWindow_As_1()
        {
            await using var loRaDevice = CreateDefaultDevice();
            Assert.Equal(ReceiveWindow1, loRaDevice.PreferredWindow);
        }

        [Fact]
        public async Task After_3_Resubmits_Should_Not_Be_Valid_To_Resend_Ack()
        {
            await using var target = CreateDefaultDevice();

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
        public async Task When_ResetFcnt_In_New_Instance_Should_Have_HasFrameCountChanges_False()
        {
            await using var target = CreateDefaultDevice();

            // Setting from 0 to 0 should not trigger changes
            target.ResetFcnt();
            Assert.False(target.HasFrameCountChanges);
            Assert.Equal(0U, target.LastSavedFCntDown);
            Assert.Equal(0U, target.LastSavedFCntUp);
        }

        [Fact]
        public async Task When_Updating_LastUpdate_Is_Updated()
        {
            var twin = LoRaDeviceTwin.Create(OtaaDesiredTwinProperties);

            this.loRaDeviceClient.Setup(x => x.GetTwinAsync(CancellationToken.None))
                .ReturnsAsync(twin);

            await using var loRaDevice = CreateDefaultDevice();
            var lastUpdate = loRaDevice.LastUpdate = DateTime.UtcNow - TimeSpan.FromDays(1);
            await loRaDevice.InitializeAsync(Configuration);
            Assert.True(loRaDevice.LastUpdate > lastUpdate);
        }

        [Fact]
        public async Task When_Update_Fails_LastUpdate_Is_Not_Changed()
        {
            this.loRaDeviceClient.Setup(x => x.GetTwinAsync(CancellationToken.None))
                .ThrowsAsync(new IotHubException());

            await using var loRaDevice = CreateDefaultDevice();
            var lastUpdate = loRaDevice.LastUpdate = DateTime.UtcNow - TimeSpan.FromDays(1);
            await Assert.ThrowsAsync<LoRaProcessingException>(async () => await loRaDevice.InitializeAsync(Configuration));
            Assert.Equal(lastUpdate, loRaDevice.LastUpdate);
        }

        [Fact]
        public async Task When_ResetFcnt_In_Device_With_Pending_Changes_Should_Have_HasFrameCountChanges_True()
        {
            var devAddr = new DevAddr(0x1231);

            // Non zero fcnt up
            await using var target = CreateDefaultDevice();
            target.SetFcntUp(1);
            target.AcceptFrameCountChanges();
            target.ResetFcnt();
            Assert.Equal(0U, target.FCntUp);
            Assert.Equal(0U, target.FCntDown);
            Assert.True(target.HasFrameCountChanges);

            // Non zero fcnt down
            await using var secondConnectionManager = new SingleDeviceConnectionManager(this.loRaDeviceClient.Object);
            await using var secondTarget = new LoRaDevice(devAddr, new DevEui(0x12312), secondConnectionManager);
            secondTarget.SetFcntDown(1);
            secondTarget.AcceptFrameCountChanges();
            secondTarget.ResetFcnt();
            Assert.Equal(0U, secondTarget.FCntUp);
            Assert.Equal(0U, secondTarget.FCntDown);
            Assert.True(secondTarget.HasFrameCountChanges);

            // Non zero fcnt down and up
            await using var thirdConnectionManager = new SingleDeviceConnectionManager(this.loRaDeviceClient.Object);
            await using var thirdTarget = new LoRaDevice(devAddr, new DevEui(0x12312), thirdConnectionManager);
            thirdTarget.SetFcntDown(1);
            thirdTarget.SetFcntDown(2);
            thirdTarget.AcceptFrameCountChanges();
            thirdTarget.ResetFcnt();
            Assert.Equal(0U, thirdTarget.FCntUp);
            Assert.Equal(0U, thirdTarget.FCntDown);
            Assert.True(thirdTarget.HasFrameCountChanges);
        }

        [Fact]
        public async Task When_ResetFcnt_In_NonZero_FcntUp_Or_FcntDown_Should_Have_HasFrameCountChanges_True()
        {
            var devAddr = new DevAddr(0x1231);

            // Non zero fcnt up
            await using var target = CreateDefaultDevice();
            target.SetFcntUp(1);
            target.AcceptFrameCountChanges();
            target.ResetFcnt();
            Assert.Equal(0U, target.FCntUp);
            Assert.Equal(0U, target.FCntDown);
            Assert.True(target.HasFrameCountChanges);

            // Non zero fcnt down
            await using var secondConnectionManager = new SingleDeviceConnectionManager(this.loRaDeviceClient.Object);
            await using var secondTarget = new LoRaDevice(devAddr, new DevEui(0x12312), secondConnectionManager);
            secondTarget.SetFcntDown(1);
            secondTarget.AcceptFrameCountChanges();
            secondTarget.ResetFcnt();
            Assert.Equal(0U, secondTarget.FCntUp);
            Assert.Equal(0U, secondTarget.FCntDown);
            Assert.Equal(0U, secondTarget.LastSavedFCntUp);
            Assert.Equal(1U, secondTarget.LastSavedFCntDown);
            Assert.True(secondTarget.HasFrameCountChanges);

            // Non zero fcnt down and up
            await using var thirdConnectionManager = new SingleDeviceConnectionManager(this.loRaDeviceClient.Object);
            await using var thirdTarget = new LoRaDevice(devAddr, new DevEui(0x12312), thirdConnectionManager);
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
            var twin = LoRaDeviceTwin.Create(AbpDesiredTwinProperties, AbpReportedTwinProperties);
            twin.Properties.Reported["FCntDown"] = 10;
            twin.Properties.Reported["FCntUp"] = 20;

            this.loRaDeviceClient.Setup(x => x.GetTwinAsync(CancellationToken.None))
                .ReturnsAsync(twin);

            await using var loRaDevice = CreateDefaultDevice();

            await loRaDevice.InitializeAsync(Configuration);
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
            var twin = LoRaDeviceTwin.Create(
                AbpDesiredTwinProperties,
                AbpReportedTwinProperties with
                {
                    FCntDown = 10,
                    FCntUp = 20,
                    PreferredGatewayId = "gateway1",
                });

            twin.Properties.Reported["Region"] = regionValue;

            this.loRaDeviceClient.Setup(x => x.GetTwinAsync(CancellationToken.None))
                .ReturnsAsync(twin);

            await using var loRaDevice = CreateDefaultDevice();
            await loRaDevice.InitializeAsync(Configuration);

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
            const string gatewayId = "mygateway";
            var twin = LoRaDeviceTwin.Create(AbpDesiredTwinProperties with { GatewayId = gatewayId }, AbpReportedTwinProperties);
            twin.Properties.Desired["KeepAliveTimeout"] = keepAliveTimeoutValue;

            this.loRaDeviceClient.Setup(x => x.GetTwinAsync(CancellationToken.None))
                .ReturnsAsync(twin);

            await using var loRaDevice = CreateDefaultDevice();
            await loRaDevice.InitializeAsync(Configuration);
            Assert.Equal(expectedKeepAliveTimeout, loRaDevice.KeepAliveTimeout);
        }

        [Fact]
        public async Task When_Initialized_With_Class_C_And_Custom_RX2DR_Should_Have_Correct_Properties()
        {
            var twin = LoRaDeviceTwin.Create(
                OtaaDesiredTwinProperties,
                OtaaReportedTwinProperties with
                {
                    FCntDown = 9,
                    FCntUp = 100,
                    NetId = new NetId(1231),
                    Region = LoRaRegionType.US915
                });

            twin.Properties.Desired["RX2DataRate"] = "10";
            twin.Properties.Desired["ClassType"] = "C";
            twin.Properties.Reported["RX2DataRate"] = 10;

            this.loRaDeviceClient.Setup(x => x.GetTwinAsync(CancellationToken.None))
                .ReturnsAsync(twin);

            await using var loRaDevice = CreateDefaultDevice();
            await loRaDevice.InitializeAsync(Configuration);
            Assert.Equal(LoRaDeviceClassType.C, loRaDevice.ClassType);
            Assert.Equal(OtaaDesiredTwinProperties.GatewayId, loRaDevice.GatewayID);
            Assert.Equal(9u, loRaDevice.FCntDown);
            Assert.Equal(100u, loRaDevice.FCntUp);
            Assert.Equal(DR10, loRaDevice.ReportedRX2DataRate.Value);
            Assert.Equal(DR10, loRaDevice.DesiredRX2DataRate.Value);
            Assert.Equal(OtaaReportedTwinProperties.AppSessionKey, loRaDevice.AppSKey);
            Assert.Equal(OtaaReportedTwinProperties.NetworkSessionKey, loRaDevice.NwkSKey);
            Assert.Equal(LoRaRegionType.US915, loRaDevice.LoRaRegion);
            Assert.False(loRaDevice.IsABP);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task When_Updating_Dwell_Time_Settings_Should_Update(bool acceptChanges)
        {
            // arrange
            var dwellTimeSetting = new DwellTimeSetting(true, false, 3);
            await using var loRaDevice = CreateDefaultDevice();

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
            await using var loRaDevice = CreateDefaultDevice();
            TwinCollection actualReportedProperties = null;
            this.loRaDeviceClient.Setup(x => x.UpdateReportedPropertiesAsync(It.IsAny<TwinCollection>(), It.IsAny<CancellationToken>()))
                                 .Callback((TwinCollection t, CancellationToken _) => actualReportedProperties = t)
                                 .ReturnsAsync(true);

            // act
            loRaDevice.UpdateDwellTimeSetting(dwellTimeSetting, acceptChanges);
            await loRaDevice.SaveChangesAsync();

            // assert
            Assert.Equal(dwellTimeSetting, loRaDevice.ReportedDwellTimeSetting);
            if (acceptChanges)
            {
                Assert.Null(actualReportedProperties);
                this.loRaDeviceClient.Verify(c => c.UpdateReportedPropertiesAsync(It.IsAny<TwinCollection>(), It.IsAny<CancellationToken>()), Times.Never);
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
            await using var loRaDevice = CreateDefaultDevice();
            var dwellTimeSetting = new DwellTimeSetting(true, false, 4);
            var twin = LoRaDeviceTwin.Create(OtaaDesiredTwinProperties);
            twin.Properties.Reported["TxParam"] = JsonSerializer.Serialize(dwellTimeSetting);
            this.loRaDeviceClient.Setup(x => x.GetTwinAsync(CancellationToken.None))
                                 .ReturnsAsync(twin);

            // act
            _ = await loRaDevice.InitializeAsync(Configuration);

            // assert
            Assert.Equal(dwellTimeSetting, loRaDevice.ReportedDwellTimeSetting);
        }

#pragma warning disable CA2000 // Dispose objects before losing scope
        private LoRaDevice CreateDefaultDevice(ILoRaDeviceClientConnectionManager connectionManager = null) =>
            new(new DevAddr(0xffffffff), new DevEui(0), connectionManager ?? new SingleDeviceConnectionManager(this.loRaDeviceClient.Object));
#pragma warning restore CA2000 // Dispose objects before losing scope

        public class FrameCounterInitTests
        {
            private readonly Mock<ILoRaDeviceClient> loRaDeviceClient;

            public FrameCounterInitTests()
            {
                this.loRaDeviceClient = new Mock<ILoRaDeviceClient>();
            }

            [Fact]
            public async Task If_No_Reset_FcntUpDown_Initialized()
            {
                await using var device = CreateDefault();

                const uint fcntUp = 10;
                const uint fcntDown = 2;

                var twin = LoRaDeviceTwin.Create(reportedProperties: new LoRaReportedTwinProperties
                {
                    FCntUp = fcntUp,
                    FCntDown = fcntDown,
                });

                device.ExecuteInitializeFrameCounters(twin);
                AssertFcntUp(fcntUp, device);
                AssertFcntDown(fcntDown, device);

                this.loRaDeviceClient.Verify(x => x.UpdateReportedPropertiesAsync(It.IsAny<TwinCollection>(), It.IsAny<CancellationToken>()), Times.Never);
            }

            [Theory]
            [InlineData(1, 1, 10, 10, 0,  0,  true)]   // fcnt start set, but wasn't reported - expect to be set to the start counter and saved
            [InlineData(1, 1, 10, 10, 10, 10, false)]  // all up to date - no update - expect set to last reported
            [InlineData(2, 1, 10, 10, 10, 10, true)]   // reset counter higher - expect to set to start counter and saved
            [InlineData(2, 3, 10, 10, 10, 10, false)]  // reset counter smaller - expect set to last reported
            public async Task When_Start_Specified_Initialized_Correctly(uint fcntResetDesired, uint fcntResetReported, uint startDesiredUp, uint startDesiredDown, uint startReportedUp, uint startReportedDown, bool expectStart)
            {
                await using var device = CreateDefault();

                const uint fcntUp = 10;
                const uint fcntDown = 2;

                var twin = LoRaDeviceTwin.Create(
                    new LoRaDesiredTwinProperties
                    {
                        FCntUpStart = startDesiredUp,
                        FCntDownStart = startDesiredDown,
                        FCntResetCounter = fcntResetDesired
                    },
                    new LoRaReportedTwinProperties
                    {
                        FCntUpStart = startReportedUp,
                        FCntDownStart = startReportedDown,
                        FCntUp = fcntUp,
                        FCntDown = fcntDown,
                        FCntResetCounter = fcntResetReported
                    });

                device.ExecuteInitializeFrameCounters(twin);

                this.loRaDeviceClient.Verify(x => x.UpdateReportedPropertiesAsync(It.IsAny<TwinCollection>(), It.IsAny<CancellationToken>()), expectStart ? Times.Once : Times.Never);

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
            public async Task When_Reset_Specified_Initialized_Correctly(uint startDesiredUp, uint startDesiredDown, uint startReportedUp, uint startReportedDown, bool expectStart)
            {
                await using var device = CreateDefault();

                const uint fcntUp = 10;
                const uint fcntDown = 2;
                var twin = LoRaDeviceTwin.Create(
                    new LoRaDesiredTwinProperties
                    {
                        FCntUpStart = startDesiredUp,
                        FCntDownStart = startDesiredDown,
                    },
                    new LoRaReportedTwinProperties
                    {
                        FCntUpStart = startReportedUp,
                        FCntDownStart = startReportedDown,
                        FCntUp = fcntUp,
                        FCntDown = fcntDown
                    });

                device.ExecuteInitializeFrameCounters(twin);

                this.loRaDeviceClient.Verify(x => x.UpdateReportedPropertiesAsync(It.IsAny<TwinCollection>(), It.IsAny<CancellationToken>()), expectStart ? Times.Once : Times.Never);

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
                private readonly ILogger<TwinPropertiesReader> logger = NullLogger<TwinPropertiesReader>.Instance;

                public LoRaDeviceTest(ILoRaDeviceClient deviceClient)
#pragma warning disable CA2000 // Dispose objects before losing scope - ownership is transferred
                    : base (new DevAddr(0xffffffff), new DevEui(0), new SingleDeviceConnectionManager(deviceClient))
#pragma warning restore CA2000 // Dispose objects before losing scope
                {

                }

                public void ExecuteInitializeFrameCounters(Twin twin)
                    => InitializeFrameCounters(new TwinCollectionReader(twin.Properties.Desired, this.logger),
                                               new TwinCollectionReader(twin.Properties.Reported, this.logger));
            }
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        public async Task BeginDeviceClientConnectionActivity_Delegates_To_Connection_Manager_When_Device_Has_KeepAliveTimeout(int timeoutSeconds)
        {
            // arrange
            var connectionManagerMock = new Mock<ILoRaDeviceClientConnectionManager>();
            await using var target = CreateDefaultDevice(connectionManagerMock.Object);
            target.KeepAliveTimeout = timeoutSeconds;

            // act
            target.BeginDeviceClientConnectionActivity();

            // assert
            connectionManagerMock.Verify(x => x.BeginDeviceClientConnectionActivity(target),
                                         Times.Exactly(timeoutSeconds > 0 ? 1 : 0));
        }
    }
}
