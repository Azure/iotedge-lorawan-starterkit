// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Unit.NetworkServer
{
    using System;
    using System.Globalization;
    using LoRaWan.NetworkServer;
    using LoRaWan.Tests.Common;
    using Xunit;

    public class LoRaDeviceTelemetryTest
    {
        [Theory]
        [InlineData(1, FramePorts.App1)]
        [InlineData(2, FramePorts.App10)]
        [InlineData(100, FramePorts.App2)]
        public void When_Creating_Should_Copy_Values_From_Rxpk_And_Payload(uint fcnt, FramePort fport)
        {
            var simulatedDevice = new SimulatedDevice(TestDeviceInfo.CreateABPDevice(1));
            var payload = simulatedDevice.CreateUnconfirmedDataUpMessage("1", fcnt: fcnt, fport: fport);
            var decodedValue = new { value = 1 };

            using var loRaRequest = WaitableLoRaRequest.CreateWaitableRequest(payload);
            var target = new LoRaDeviceTelemetry(loRaRequest, payload, decodedValue, payload.GetDecryptedPayload(simulatedDevice.AppSKey));
            Assert.Equal(checked((uint)loRaRequest.RadioMetadata.DataRate), target.Chan);
            Assert.Equal(Convert.ToBase64String(payload.GetDecryptedPayload(simulatedDevice.AppSKey)), target.Rawdata);
            Assert.Equal(decodedValue, target.Data);
            Assert.Equal(TestUtils.TestRegion.GetDatarateFromIndex(loRaRequest.RadioMetadata.DataRate).ToString(), target.Datr);
            Assert.Equal(loRaRequest.RadioMetadata.Frequency.InMega, target.Freq);
            Assert.Equal(loRaRequest.RadioMetadata.UpInfo.SignalNoiseRatio, target.Lsnr);
            Assert.Equal(ModulationKind.LoRa.ToString(), target.Modu);
            Assert.Equal(loRaRequest.RadioMetadata.UpInfo.AntennaPreference, target.Rfch);
            Assert.Equal(loRaRequest.RadioMetadata.UpInfo.ReceivedSignalStrengthIndication, target.Rssi);
            Assert.Equal(loRaRequest.RadioMetadata.UpInfo.Xtime.ToString(CultureInfo.InvariantCulture), target.Time);
            Assert.Equal(unchecked((uint)loRaRequest.RadioMetadata.UpInfo.Xtime), target.Tmms);
            Assert.Equal(loRaRequest.RadioMetadata.UpInfo.GpsTime, target.Tmst);
            Assert.Equal(payload.GetFcnt(), target.Fcnt);
            Assert.Equal(payload.Fport, target.Port);
        }
    }
}
