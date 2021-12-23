// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Unit.NetworkServer
{
    using System;
    using System.Threading.Tasks;
    using LoRaWan.NetworkServer;
    using LoRaWan.Tests.Common;
    using Microsoft.Azure.Devices.Shared;
    using Moq;
    using Xunit;

    public sealed class SingleGatewayFrameCounterUpdateStrategyTest : IDisposable
    {
        private readonly Mock<ILoRaDeviceClient> deviceClient;
        private readonly ILoRaDeviceClientConnectionManager connectionManager;
        private readonly LoRaDevice device;

        public SingleGatewayFrameCounterUpdateStrategyTest()
        {
            this.deviceClient = new Mock<ILoRaDeviceClient>();
            this.connectionManager = new SingleDeviceConnectionManager(this.deviceClient.Object);
            this.device = new LoRaDevice(new DevAddr(1), "2", connectionManager);
        }

        public void Dispose()
        {
            this.device.Dispose();
            this.connectionManager.Dispose();
        }

        [Theory]
        [InlineData(0, 0)]
        [InlineData(10, 0)]
        [InlineData(0, 10)]
        [InlineData(22, 22)]
        public async Task When_Device_Has_No_Changes_To_Fcnt_Should_Not_Save_Changes(uint fcntDown, uint fcntUp)
        {
            var target = new SingleGatewayFrameCounterUpdateStrategy();

            this.device.SetFcntDown(fcntDown);
            this.device.SetFcntUp(fcntUp);
            this.device.AcceptFrameCountChanges();

            await target.SaveChangesAsync(this.device);

            this.deviceClient.VerifyAll();
        }

        [Theory]
        [InlineData(0)]
        [InlineData(9)]
        public async Task When_Device_Has_Up_To_9_Changes_In_Fcnt_Up_Should_Not_Save_Changes(uint startFcntUp)
        {
            var target = new SingleGatewayFrameCounterUpdateStrategy();

            this.device.SetFcntUp(startFcntUp);
            this.device.AcceptFrameCountChanges();

            for (uint i = 1; i <= 9; ++i)
            {
                this.device.SetFcntUp(i);
                await target.SaveChangesAsync(this.device);
            }

            this.deviceClient.VerifyAll();
        }

        [Theory]
        [InlineData(0)]
        [InlineData(9)]
        public async Task When_Device_Has_Up_To_9_Changes_In_Fcnt_Down_Should_Not_Save_Changes(uint startFcntDown)
        {
            var target = new SingleGatewayFrameCounterUpdateStrategy();

            this.device.SetFcntDown(startFcntDown);
            this.device.AcceptFrameCountChanges();

            for (var i = 1; i <= 9; ++i)
            {
                await target.NextFcntDown(this.device, 10);
                await target.SaveChangesAsync(this.device);
            }

            this.deviceClient.VerifyAll();
        }

        [Theory]
        [InlineData(10)]
        [InlineData(30)]
        [InlineData(1000)]
        public async Task When_Device_FcntUp_Change_Is_10_Or_More_Should_Save_Changes(uint fcntUp)
        {
            var target = new SingleGatewayFrameCounterUpdateStrategy();

            this.deviceClient.Setup(x => x.UpdateReportedPropertiesAsync(It.IsNotNull<TwinCollection>()))
                .ReturnsAsync(true)
                .Callback<TwinCollection>(t =>
                {
                    Assert.Equal(fcntUp, (uint)t[TwinProperty.FCntUp]);
                    Assert.Equal(0U, (uint)t[TwinProperty.FCntDown]);
                });

            this.device.SetFcntUp(fcntUp);
            await target.SaveChangesAsync(this.device);

            this.deviceClient.VerifyAll();
        }

        [Theory]
        [InlineData(0, 0)]
        [InlineData(9, 9)]
        [InlineData(9, 30)]
        [InlineData(10, 30)]
        public async Task When_Device_FcntDown_Change_Is_10_Or_More_Should_Save_Changes(uint startingFcntDown, uint startingFcntUp)
        {
            var target = new SingleGatewayFrameCounterUpdateStrategy();

            this.deviceClient.Setup(x => x.UpdateReportedPropertiesAsync(It.IsNotNull<TwinCollection>()))
                .ReturnsAsync(true)
                .Callback<TwinCollection>(t =>
                {
                    Assert.Equal(startingFcntDown + 10, (uint)t[TwinProperty.FCntDown]);
                    Assert.Equal(startingFcntUp, (uint)t[TwinProperty.FCntUp]);
                });

            this.device.SetFcntUp(startingFcntUp);
            this.device.SetFcntDown(startingFcntDown);
            this.device.AcceptFrameCountChanges();

            for (var i = 1; i <= 15; i++)
            {
                await target.NextFcntDown(this.device, startingFcntUp + 1);
                await target.SaveChangesAsync(this.device);
            }

            this.deviceClient.VerifyAll();
        }

        [Fact]
        public async Task When_Device_With_0_As_Fcnt_Is_Loaded_Reset_Should_Not_Save_Reported_Properties()
        {
            var target = new SingleGatewayFrameCounterUpdateStrategy();

            await target.ResetAsync(this.device, 1, string.Empty);
            Assert.False(this.device.HasFrameCountChanges);
            this.deviceClient.VerifyAll();
        }
    }
}
