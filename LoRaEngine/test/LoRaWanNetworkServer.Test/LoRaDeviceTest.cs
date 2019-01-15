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

        [Fact]
        public async Task When_Initialized_New_OTAA_Device_Should_Have_All_Properties()
        {
            var twin = TestUtils.CreateTwin(
                desired: new Dictionary<string, object>
                {
                    {"AppEUI", "ABC0200000000009" },
                    {"AppKey", "ABC02000000000000000000000000009"},
                    {"GatewayID", "mygateway"},
                    {"SensorDecoder", "DecoderValueSensor"},
                    {"$version", 1},
                },
                reported: new Dictionary<string, object>
                {
                    {"$version", 1},
                }
            );

            this.loRaDeviceClient.Setup(x => x.GetTwinAsync())
                .ReturnsAsync(twin);

            var loRaDevice = new LoRaDevice("", "ABC0200000000009", this.loRaDeviceClient.Object);
            await loRaDevice.InitializeAsync();
            Assert.Equal("ABC0200000000009", loRaDevice.AppEUI);
            Assert.Equal("ABC02000000000000000000000000009", loRaDevice.AppKey);
            Assert.Equal("mygateway", loRaDevice.GatewayID);
            Assert.Equal("DecoderValueSensor", loRaDevice.SensorDecoder);
            Assert.Empty(loRaDevice.AppSKey ?? "");
            Assert.Empty(loRaDevice.NwkSKey ?? "");
            Assert.Empty(loRaDevice.DevAddr ?? "");
            Assert.Empty(loRaDevice.DevNonce ?? "");
            Assert.Empty(loRaDevice.NetID ?? "");
            Assert.False(loRaDevice.IsABP);
            Assert.False(loRaDevice.IsOurDevice);
        }


        [Fact]
        public async Task When_Initialized_Joined_OTAA_Device_Should_Have_All_Properties()
        {
            var twin = TestUtils.CreateTwin(
                desired: new Dictionary<string, object>
                {
                    {"AppEUI", "ABC0200000000009" },
                    {"AppKey", "ABC02000000000000000000000000009"},
                    {"GatewayID", "mygateway"},
                    {"SensorDecoder", "DecoderValueSensor"},
                    {"$version", 1},
                },
                reported: new Dictionary<string, object>
                {
                    {"$version", 1},
                    {"NwkSKey", "ABC02000000000000000000000000009ABC02000000000000000000000000009" },
                    {"AppSKey", "ABCD2000000000000000000000000009ABC02000000000000000000000000009"},
                    {"DevNonce", "0123"},
                    {"DevAddr", "0000AABB"},
                }
            );

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
                    {"NwkSKey", "ABC02000000000000000000000000009ABC02000000000000000000000000009" },
                    {"AppSKey", "ABCD2000000000000000000000000009ABC02000000000000000000000000009"},
                    {"DevAddr", "0000AABB"},
                    {"GatewayID", "mygateway"},
                    {"SensorDecoder", "DecoderValueSensor"},
                    {"$version", 1},
                },
                reported: new Dictionary<string, object>
                {
                    {"$version", 1},
                    {"NwkSKey", "ABC02000000000000000000000000009ABC02000000000000000000000000009" },
                    {"AppSKey", "ABCD2000000000000000000000000009ABC02000000000000000000000000009"},
                    {"DevAddr", "0000AABB"},
                }
            );

            this.loRaDeviceClient.Setup(x => x.GetTwinAsync())
                .ReturnsAsync(twin);

            var loRaDevice = new LoRaDevice("00000001", "ABC0200000000009", this.loRaDeviceClient.Object);
            await loRaDevice.InitializeAsync();
            Assert.Empty(loRaDevice.AppEUI ?? "");
            Assert.Empty(loRaDevice.AppKey ?? "");
            Assert.Equal("mygateway", loRaDevice.GatewayID);
            Assert.Equal("DecoderValueSensor", loRaDevice.SensorDecoder);
            Assert.True(loRaDevice.IsABP);
            Assert.True(loRaDevice.IsOurDevice);
            Assert.Equal("ABC02000000000000000000000000009ABC02000000000000000000000000009", loRaDevice.NwkSKey);
            Assert.Equal("ABCD2000000000000000000000000009ABC02000000000000000000000000009", loRaDevice.AppSKey);
            Assert.Empty(loRaDevice.DevNonce ?? "");
            Assert.Equal("0000AABB", loRaDevice.DevAddr);
        }
    }
}
