// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Unit.NetworkServer
{
    using LoRaWan.NetworkServer;
    using LoRaWan.Tests.Common;
    using Xunit;

    public class LoRaDeviceTelemetryTest
    {
        [Theory]
        [InlineData(1, FramePorts.App1, "1", "MQ==")]
        [InlineData(2, FramePorts.App10, "1", "MQ==")]
        [InlineData(100, FramePorts.App2, "1", "MQ==")]
        [InlineData(100, null, "", "")]
        public void When_Creating_Should_Copy_Values_From_Rxpk_And_Payload(uint fcnt, FramePort? fport, string data, string expectedRawData)
        {
            var simulatedDevice = new SimulatedDevice(TestDeviceInfo.CreateABPDevice(1));
            var payload = simulatedDevice.CreateUnconfirmedDataUpMessage(data, fcnt: fcnt, fport: fport);
            var decodedValue = new { value = 1 };

            using var loRaRequest = WaitableLoRaRequest.CreateWaitableRequest(payload);
            var target = new LoRaDeviceTelemetry(loRaRequest, payload, decodedValue, payload.GetDecryptedPayload(simulatedDevice.AppSKey.Value));
            Assert.Equal(checked((uint)loRaRequest.RadioMetadata.DataRate), target.Chan);
            Assert.Equal(expectedRawData, target.Rawdata);
            Assert.Equal(decodedValue, target.Data);
            Assert.Equal(TestUtils.TestRegion.GetDatarateFromIndex(loRaRequest.RadioMetadata.DataRate).ToString(), target.Datr);
            Assert.Equal(loRaRequest.RadioMetadata.Frequency.InMega, target.Freq);
            Assert.Equal(loRaRequest.RadioMetadata.UpInfo.SignalNoiseRatio, target.Lsnr);
            Assert.Equal(ModulationKind.LoRa.ToString(), target.Modu);
            Assert.Equal(loRaRequest.RadioMetadata.UpInfo.AntennaPreference, target.Rfch);
            Assert.Equal(loRaRequest.RadioMetadata.UpInfo.ReceivedSignalStrengthIndication, target.Rssi);
            Assert.Equal(loRaRequest.RadioMetadata.UpInfo.Xtime, target.Time);
            Assert.Equal(unchecked((uint)loRaRequest.RadioMetadata.UpInfo.Xtime), target.GpsTime);
            Assert.Equal(payload.Fcnt, target.Fcnt);
            Assert.Equal(payload.Fport, target.Port);
        }
    }
}
