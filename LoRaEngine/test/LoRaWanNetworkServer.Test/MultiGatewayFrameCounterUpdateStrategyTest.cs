﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer.Test
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using System.Threading.Tasks;
    using LoRaWan.NetworkServer;
    using Microsoft.Azure.Devices.Shared;
    using Moq;
    using Xunit;

    public class MultiGatewayFrameCounterUpdateStrategyTest
    {
        private readonly Mock<ILoRaDeviceClient> deviceClient;
        private readonly Mock<LoRaDeviceAPIServiceBase> deviceApi;
        private string gatewayID;

        public MultiGatewayFrameCounterUpdateStrategyTest()
        {
            this.deviceClient = new Mock<ILoRaDeviceClient>(MockBehavior.Strict);
            this.deviceApi = new Mock<LoRaDeviceAPIServiceBase>(MockBehavior.Strict);
            this.gatewayID = "test-gateway";
        }

        [Theory]
        [InlineData(0, 0)]
        [InlineData(10, 0)]
        [InlineData(0, 10)]
        [InlineData(22, 22)]
        public async Task When_Device_Has_No_Changes_To_Fcnt_Should_Not_Save_Changes(uint fcntDown, uint fcntUp)
        {
            var target = new MultiGatewayFrameCounterUpdateStrategy(this.gatewayID, this.deviceApi.Object);

            var device = new LoRaDevice("1", "2", this.deviceClient.Object);
            device.SetFcntDown(fcntDown);
            device.SetFcntUp(fcntUp);
            device.AcceptFrameCountChanges();

            await target.SaveChangesAsync(device);

            this.deviceClient.VerifyAll();
        }

        [Theory]
        [InlineData(0U)]
        [InlineData(9U)]
        public async Task When_Device_Has_Up_To_9_Changes_In_Fcnt_Up_Should_Not_Save_Changes(uint startFcntUp)
        {
            var target = new MultiGatewayFrameCounterUpdateStrategy(this.gatewayID, this.deviceApi.Object);

            var device = new LoRaDevice("1", "2", this.deviceClient.Object);
            device.SetFcntUp(startFcntUp);
            device.AcceptFrameCountChanges();

            for (uint i = 1; i <= 9; ++i)
            {
                device.SetFcntUp(i);
                await target.SaveChangesAsync(device);
            }

            this.deviceApi.VerifyAll();
            this.deviceClient.VerifyAll();
        }

        [Theory]
        [InlineData(0)]
        [InlineData(9)]
        public async Task When_Device_Has_Up_To_9_Changes_In_Fcnt_Down_Should_Not_Save_Changes(uint startFcntDown)
        {
            var target = new MultiGatewayFrameCounterUpdateStrategy(this.gatewayID, this.deviceApi.Object);

            var device = new LoRaDevice("1", "2", this.deviceClient.Object);
            device.SetFcntDown(startFcntDown);
            device.AcceptFrameCountChanges();

            this.deviceApi.Setup(x => x.NextFCntDownAsync(device.DevEUI, It.IsAny<uint>(), It.IsAny<uint>(), this.gatewayID))
                .Returns<string, uint, uint, string>((devEUI, fcntDown, payloadFcnt, gatewayID) =>
                {
                    return Task.FromResult(fcntDown + 1);
                });

            for (var i = 1; i <= 9; ++i)
            {
                await target.NextFcntDown(device, 10);
                await target.SaveChangesAsync(device);
            }

            this.deviceApi.VerifyAll();
            this.deviceClient.VerifyAll();
        }

        [Theory]
        [InlineData(10)]
        [InlineData(30)]
        [InlineData(1000)]
        public async Task When_Device_FcntUp_Change_Is_10_Or_More_Should_Save_Changes(uint fcntUp)
        {
            var target = new MultiGatewayFrameCounterUpdateStrategy(this.gatewayID, this.deviceApi.Object);

            this.deviceClient.Setup(x => x.UpdateReportedPropertiesAsync(It.IsNotNull<TwinCollection>()))
                .ReturnsAsync(true)
                .Callback<TwinCollection>(t =>
                {
                    Assert.Equal(fcntUp, (uint)t[TwinProperty.FCntUp]);
                    Assert.Equal(0U, (uint)t[TwinProperty.FCntDown]);
                });

            var device = new LoRaDevice("1", "2", this.deviceClient.Object);
            device.SetFcntUp(fcntUp);
            await target.SaveChangesAsync(device);

            this.deviceApi.VerifyAll();
            this.deviceClient.VerifyAll();
        }

        [Theory]
        [InlineData(0, 0)]
        [InlineData(9, 9)]
        [InlineData(9, 30)]
        [InlineData(10, 30)]
        public async Task When_Device_FcntDown_Change_Is_10_Or_More_Should_Save_Changes(uint startingFcntDown, uint startingFcntUp)
        {
            var target = new MultiGatewayFrameCounterUpdateStrategy(this.gatewayID, this.deviceApi.Object);

            this.deviceClient.Setup(x => x.UpdateReportedPropertiesAsync(It.IsNotNull<TwinCollection>()))
                .ReturnsAsync(true)
                .Callback<TwinCollection>(t =>
                {
                    Assert.Equal(startingFcntDown + 10, (uint)t[TwinProperty.FCntDown]);
                    Assert.Equal(startingFcntUp, (uint)t[TwinProperty.FCntUp]);
                });

            var device = new LoRaDevice("1", "2", this.deviceClient.Object);
            device.SetFcntUp(startingFcntUp);
            device.SetFcntDown(startingFcntDown);
            device.AcceptFrameCountChanges();

            this.deviceApi.Setup(x => x.NextFCntDownAsync(device.DevEUI, It.IsAny<uint>(), It.IsAny<uint>(), this.gatewayID))
                .Returns<string, uint, uint, string>((devEUI, fcntDown, payloadFcnt, gatewayID) =>
                {
                    return Task.FromResult((uint)(fcntDown + 1));
                });

            for (var i = 1; i <= 15; i++)
            {
                await target.NextFcntDown(device, startingFcntUp + 1);
                await target.SaveChangesAsync(device);
            }

            this.deviceApi.VerifyAll();
            this.deviceClient.VerifyAll();
        }
    }
}
