// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Unit.NetworkServerTests.JsonHandlers
{
    using System.Text.Json;
    using LoRaWan.NetworkServer.BasicsStation;
    using LoRaWan.NetworkServer.BasicsStation.JsonHandlers;
    using NetworkServer;
    using Xunit;

    public class LnsDataTests
    {
        [Theory]
        [InlineData(@"{ ""msgtype"": ""router_config"" }", LnsMessageType.RouterConfig)]
        [InlineData(@"{ ""msgtype"": ""dnmsg"" }", LnsMessageType.DownlinkMessage)]
        [InlineData(@"{ ""msgtype"": ""dntxed"" }", LnsMessageType.TransmitConfirmation)]
        [InlineData(@"{ ""msgtype"": ""jreq"" }", LnsMessageType.JoinRequest)]
        [InlineData(@"{ ""msgtype"": ""updf"" }", LnsMessageType.UplinkDataFrame)]
        [InlineData(@"{ ""msgtype"": ""version"" }", LnsMessageType.Version)]
        [InlineData(@"{ ""onePropBefore"": { ""value"": 123 }, ""msgtype"": ""version"" }", LnsMessageType.Version)]
        [InlineData(@"{ ""msgtype"": ""version"", ""onePropAfter"": { ""value"": 123 } }", LnsMessageType.Version)]
        internal void ReadMessageType_Succeeds(string json, LnsMessageType expectedMessageType)
        {
            var messageType = LnsData.MessageTypeReader.Read(json);
            Assert.Equal(expectedMessageType, messageType);
        }


        [Theory]
        [InlineData(@"{ ""msgtype"": ""NOTrouter_config"" }")]
        [InlineData(@"{ ""msgtype"": ""NOTdnmsg"" }")]
        [InlineData(@"{ ""msgtype"": ""NOTdntxed"" }")]
        [InlineData(@"{ ""msgtype"": ""NOTjreq"" }")]
        [InlineData(@"{ ""msgtype"": ""NOTupdf"" }")]
        [InlineData(@"{ ""msgtype"": ""NOTversion"" }")]
        [InlineData(@"{ ""onePropBefore"": { ""value"": 123 }, ""msgtype"": ""NOTversion"" }")]
        [InlineData(@"{ ""msgtype"": ""NOTversion"" }, ""onePropAfter"": { ""value"": 123 }")]
        internal void ReadMessageType_Fails(string json)
        {
            Assert.Throws<JsonException>(() => _ = LnsData.MessageTypeReader.Read(json));
        }

        [Theory]
        [InlineData(@"{""DR"": 4, ""Freq"": 868100000, ""upinfo"": {""rctx"": 0,""xtime"": 40250921680313459,""gpstime"": 0,""fts"": -1,""rssi"": -60,""snr"": 9,""rxtime"": 1635347491.917289}}")]
        [InlineData(@"{""upinfo"": {""rctx"": 0,""xtime"": 40250921680313459,""gpstime"": 0,""rssi"": -60,""snr"": 9}, ""DR"": 4, ""Freq"": 868100000}")]
        internal void RadioMetadataReader_Succeeds(string json)
        {
            var radioMetadata = LnsData.RadioMetadataReader.Read(json);
            Assert.Equal(new DataRate(4), radioMetadata.DataRate);
            Assert.Equal(new Hertz(868100000), radioMetadata.Frequency);
            Assert.Equal((ulong)40250921680313459, radioMetadata.Xtime);
            Assert.Equal((uint)0, radioMetadata.AntennaPreference);
            Assert.Equal((uint)0, radioMetadata.GpsTime);
            Assert.Equal(-60, radioMetadata.ReceivedSignalStrengthIndication);
            Assert.Equal(9, radioMetadata.SignalNoiseRatio);
        }

        [Theory]
        [InlineData(@"{""DR"": -1, ""Freq"": 868100000, ""upinfo"": {""rctx"": 0,""xtime"": 40250921680313459,""gpstime"": 0,""rssi"": -60,""snr"": 9}}")]
        [InlineData(@"{""DR"": 4, ""Freq"": -868100000, ""upinfo"": {""rctx"": 0,""xtime"": 40250921680313459,""gpstime"": 0,""rssi"": -60,""snr"": 9}}")]
        [InlineData(@"{""DR"": 4, ""Freq"": 868100000, ""upinfo"": {""rctx"": -1,""xtime"": 40250921680313459,""gpstime"": 0,""rssi"": -60,""snr"": 9}}")]
        [InlineData(@"{""DR"": 4, ""Freq"": 868100000, ""upinfo"": {""rctx"": 0,""xtime"": -40250921680313459,""gpstime"": 0,""rssi"": -60,""snr"": 9}}")]
        [InlineData(@"{""DR"": 4, ""Freq"": 868100000, ""upinfo"": {""rctx"": 0,""xtime"": 40250921680313459,""gpstime"": -1,""rssi"": -60,""snr"": 9}}")]
        [InlineData(@"{""DR"": 4, ""Freq"": 868100000, ""upinfo"": {""rctx"": 0,""xtime"": 40250921680313459,""gpstime"": -1,""rssi"": -60,""snr"": ""9""}}")]
        [InlineData(@"{""DR"": 4, ""Freq"": 868100000, ""upinfo"": {""rctx"": 0,""xtime"": 40250921680313459,""gpstime"": 0,""rssi"": ""-60"",""snr"": 9}}")]
        internal void RadioMetadataReader_Fails(string json)
        {
            Assert.Throws<JsonException>(() => _ = LnsData.RadioMetadataReader.Read(json));
        }

        [Fact]
        internal void UpstreamDataframeReader_Succeeds()
        {
            var json = @"{ ""msgtype"": ""updf"", ""MHdr"": 128, ""DevAddr"": 58772467, ""FCtrl"": 0, ""FCnt"": 164, ""FOpts"": """",
                           ""FPort"": 8, ""FRMPayload"": ""5ABBBA"", ""MIC"": -1943282916 }";
            var updf = LnsData.UpstreamDataFrameReader.Read(json);
            Assert.Equal(new DevAddr(58772467), updf.DevAddr);
            Assert.Equal(string.Empty, updf.FOpts);
            Assert.Equal(new FramePort(8), updf.FPort);
            Assert.Equal(new FrameControl(0), updf.FrameControl);
            Assert.Equal(164, updf.FrameCounter);
            Assert.Equal("5ABBBA", updf.FRMPayload);
            Assert.Equal(new MacHeader(128), updf.MHdr);
            Assert.Equal(new Mic(unchecked((uint)-1943282916)), updf.Mic);
        }

        [Theory]
        [InlineData(@"{ ""msgtype"": ""upAf"", ""MHdr"": 128, ""DevAddr"": 58772467, ""FCtrl"": 0, ""FCnt"": 164, ""FOpts"": """", ""FPort"": 8, ""FRMPayload"": ""5ABBBA"", ""MIC"": -1943282916 }")]
        [InlineData(@"{ ""msgtype"": ""updf"", ""MHdr"": 300, ""DevAddr"": 58772467, ""FCtrl"": 0, ""FCnt"": 164, ""FOpts"": """", ""FPort"": 8, ""FRMPayload"": ""5ABBBA"", ""MIC"": -1943282916 }")]
        [InlineData(@"{ ""msgtype"": ""updf"", ""MHdr"": 128, ""DevAddr"": -58772467, ""FCtrl"": 0, ""FCnt"": 164, ""FOpts"": """", ""FPort"": 8, ""FRMPayload"": ""5ABBBA"", ""MIC"": -1943282916 }")]
        [InlineData(@"{ ""msgtype"": ""updf"", ""MHdr"": 128, ""DevAddr"": 58772467, ""FCtrl"": 300, ""FCnt"": 164, ""FOpts"": """", ""FPort"": 8, ""FRMPayload"": ""5ABBBA"", ""MIC"": -1943282916 }")]
        [InlineData(@"{ ""msgtype"": ""updf"", ""MHdr"": 128, ""DevAddr"": 58772467, ""FCtrl"": 0, ""FCnt"": -164, ""FOpts"": """", ""FPort"": 8, ""FRMPayload"": ""5ABBBA"", ""MIC"": -1943282916 }")]
        [InlineData(@"{ ""msgtype"": ""updf"", ""MHdr"": 128, ""DevAddr"": 58772467, ""FCtrl"": 0, ""FCnt"": 164, ""FOpts"": 5, ""FPort"": 8, ""FRMPayload"": ""5ABBBA"", ""MIC"": -1943282916 }")]
        [InlineData(@"{ ""msgtype"": ""updf"", ""MHdr"": 128, ""DevAddr"": 58772467, ""FCtrl"": 0, ""FCnt"": 164, ""FOpts"": """", ""FPort"": 300, ""FRMPayload"": ""5ABBBA"", ""MIC"": -1943282916 }")]
        [InlineData(@"{ ""msgtype"": ""updf"", ""MHdr"": 128, ""DevAddr"": 58772467, ""FCtrl"": 0, ""FCnt"": 164, ""FOpts"": """", ""FPort"": 8, ""FRMPayload"": 5, ""MIC"": -1943282916 }")]
        [InlineData(@"{ ""msgtype"": ""updf"", ""MHdr"": 128, ""DevAddr"": 58772467, ""FCtrl"": 0, ""FCnt"": 164, ""FOpts"": """", ""FPort"": 8, ""FRMPayload"": ""5ABBBA"", ""MIC"": 5.0 }")]
        internal void UpstreamDataframeReader_Fails(string json)
        {
            Assert.Throws<JsonException>(() => _ = LnsData.UpstreamDataFrameReader.Read(json));
        }

        [Fact]
        internal void JoinRequestFrameReader_Succeeds()
        {
            var json = @"{""msgtype"":""jreq"",""MHdr"":0,""JoinEui"":""47-62-78-C8-E5-D2-C4-B5"",""DevEui"":""85-27-C1-DF-EE-A4-16-9E"",
                          ""DevNonce"":41675,""MIC"":1528855177}";
            var jreq = LnsData.JoinRequestFrameReader.Read(json);
            Assert.Equal(new MacHeader(0), jreq.MHdr);
            Assert.Equal(new JoinEui(5143806528655115445), jreq.JoinEui);
            Assert.Equal(new DevEui(9594850698661729950), jreq.DevEui);
            Assert.Equal(new DevNonce(41675), jreq.DevNonce);
            Assert.Equal(new Mic(1528855177), jreq.Mic);
        }

        [Theory]
        [InlineData(@"{""msgtype"":""jrAq"",""MHdr"":0,""JoinEui"":""47-62-78-C8-E5-D2-C4-B5"",""DevEui"":""85-27-C1-DF-EE-A4-16-9E"",""DevNonce"":41675,""MIC"":1528855177}")]
        [InlineData(@"{""msgtype"":""jreq"",""MHdr"":300,""JoinEui"":""47-62-78-C8-E5-D2-C4-B5"",""DevEui"":""85-27-C1-DF-EE-A4-16-9E"",""DevNonce"":41675,""MIC"":1528855177}")]
        [InlineData(@"{""msgtype"":""jreq"",""MHdr"":0,""JoinEui"":""476278C8E5D2C4B5"",""DevEui"":""85-27-C1-DF-EE-A4-16-9E"",""DevNonce"":41675,""MIC"":1528855177}")]
        [InlineData(@"{""msgtype"":""jreq"",""MHdr"":0,""JoinEui"":""47-62-78-C8-E5-D2-C4-B5"",""DevEui"":""8527C1DFEEA4169E"",""DevNonce"":41675,""MIC"":1528855177}")]
        [InlineData(@"{""msgtype"":""jreq"",""MHdr"":0,""JoinEui"":""47-62-78-C8-E5-D2-C4-B5"",""DevEui"":""85-27-C1-DF-EE-A4-16-9E"",""DevNonce"":-41675,""MIC"":1528855177}")]
        [InlineData(@"{""msgtype"":""jreq"",""MHdr"":0,""JoinEui"":""47-62-78-C8-E5-D2-C4-B5"",""DevEui"":""85-27-C1-DF-EE-A4-16-9E"",""DevNonce"":41675,""MIC"":""-1528855177""}")]
        internal void JoinRequestFrameReader_Fails(string json)
        {
            Assert.Throws<JsonException>(() => _ = LnsData.JoinRequestFrameReader.Read(json));
        }
    }
}
