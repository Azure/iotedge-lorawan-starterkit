using LoRaWan.NetworkServer.V2;
using Microsoft.Azure.Devices.Shared;
using Moq;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace LoRaWan.NetworkServer.Test
{
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
    }
}
