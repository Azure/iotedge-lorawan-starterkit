// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Unit.NetworkServer
{
    using System;
    using global::LoRaTools.Regions;
    using LoRaWan.NetworkServer;
    using LoRaWan.NetworkServer.BasicsStation;
    using LoRaWan.Tests.Common;
    using Xunit;

    public class LoRaDeviceTelemetryTest
    {
        [Theory]
        [InlineData(1, (FramePort)1)]
        [InlineData(2, (FramePort)10)]
        [InlineData(100, (FramePort)2)]
        public void When_Creating_Should_Copy_Values_From_Rxpk_And_Payload(uint fcnt, FramePort fport)
        {
            var simulatedDevice = new SimulatedDevice(TestDeviceInfo.CreateABPDevice(1));
            var payload = simulatedDevice.CreateUnconfirmedDataUpMessage("1", fcnt: fcnt, fport: fport);
            var decodedValue = new { value = 1 };

            var rxpk = new BasicStationToRxpk(TestUtils.GenerateTestRadioMetadata(), RegionManager.EU868);

            var target = new LoRaDeviceTelemetry(rxpk, payload, decodedValue, payload.GetDecryptedPayload(simulatedDevice.AppSKey));
            Assert.Equal(rxpk.Chan, target.Chan);
            Assert.Equal(rxpk.Codr, target.Codr);
            Assert.Equal(Convert.ToBase64String(payload.GetDecryptedPayload(simulatedDevice.AppSKey)), target.Rawdata);
            Assert.Equal(decodedValue, target.Data);
            Assert.Equal(rxpk.Datr, target.Datr);
            Assert.Equal(rxpk.Freq, target.Freq);
            Assert.Equal(rxpk.Lsnr, target.Lsnr);
            Assert.Equal(rxpk.Modu, target.Modu);
            Assert.Equal(rxpk.Rfch, target.Rfch);
            Assert.Equal(rxpk.Rssi, target.Rssi);
            Assert.Equal(rxpk.Size, target.Size);
            Assert.Equal(rxpk.Stat, target.Stat);
            Assert.Equal(rxpk.Time, target.Time);
            Assert.Equal(rxpk.Tmms, target.Tmms);
            Assert.Equal(rxpk.Tmst, target.Tmst);
            Assert.Equal(payload.GetFcnt(), target.Fcnt);
            Assert.Equal(payload.Fport, target.Port);
        }
    }
}
