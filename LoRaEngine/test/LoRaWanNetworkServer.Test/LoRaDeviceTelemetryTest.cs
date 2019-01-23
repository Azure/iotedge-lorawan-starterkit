//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//
using LoRaWan.NetworkServer;
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
            Assert.Equal(rxpk.chan, target.Chan);
            Assert.Equal(rxpk.codr, target.Codr);
            Assert.Equal(rxpk.data, target.Rawdata);
            Assert.Equal(decodedValue, target.Data);
            Assert.Equal(rxpk.datr, target.Datr);
            Assert.Equal(rxpk.freq, target.Freq);
            Assert.Equal(rxpk.lsnr, target.Lsnr);
            Assert.Equal(rxpk.modu, target.Modu);
            Assert.Equal(rxpk.rfch, target.Rfch);
            Assert.Equal(rxpk.rssi, target.Rssi);
            Assert.Equal(rxpk.size, target.Size);
            Assert.Equal(rxpk.stat, target.Stat);
            Assert.Equal(rxpk.time, target.Time);
            Assert.Equal(rxpk.tmms, target.Tmms);
            Assert.Equal(rxpk.tmst, target.Tmst);      
            Assert.Equal(payload.GetFcnt(), target.Fcnt);
            Assert.Equal(payload.GetFPort(), target.Port); 
            

        }

    }

}