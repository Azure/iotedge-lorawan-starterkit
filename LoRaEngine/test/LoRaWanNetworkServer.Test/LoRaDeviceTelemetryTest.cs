using LoRaWan.NetworkServer.V2;
using LoRaWan.Test.Shared;
using Xunit;

namespace LoRaWan.NetworkServer.Test
{
    public class LoRaDeviceTelemetryTest
    {
        [Theory]
        [InlineData(1, 1)]
        [InlineData(2, 10)]
        [InlineData(100, 2)]
        public void When_Creating_Should_Copy_Values_From_Rxpk_And_Payload(int fcnt, byte fport)
        {
            var simulatedDevice = new SimulatedDevice(TestDeviceInfo.CreateABPDevice(1));
            var payload = simulatedDevice.CreateUnconfirmedDataUpMessage("1", fcnt: fcnt, fport: fport);
            var rxpk = payload.SerializeUplink(simulatedDevice.AppSKey, simulatedDevice.NwkSKey).rxpk[0];
            var decodedValue = new { value=1};

            var target = new LoRaDeviceTelemetry(rxpk, payload,decodedValue);
            Assert.Equal(rxpk.chan, target.chan);
            Assert.Equal(rxpk.codr, target.codr);
            Assert.Equal(rxpk.data, target.rawdata);
            Assert.Equal(decodedValue, target.data);
            Assert.Equal(rxpk.datr, target.datr);
            Assert.Equal(rxpk.freq, target.freq);
            Assert.Equal(rxpk.lsnr, target.lsnr);
            Assert.Equal(rxpk.modu, target.modu);
            Assert.Equal(rxpk.rfch, target.rfch);
            Assert.Equal(rxpk.rssi, target.rssi);
            Assert.Equal(rxpk.size, target.size);
            Assert.Equal(rxpk.stat, target.stat);
            Assert.Equal(rxpk.time, target.time);
            Assert.Equal(rxpk.tmms, target.tmms);
            Assert.Equal(rxpk.tmst, target.tmst);      
            Assert.Equal(payload.GetFcnt(), target.fcnt);
            Assert.Equal(payload.GetFPort(), target.port); 
            

        }

    }

}