// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Unit.NetworkServer.BasicsStation.JsonHandlers
{
    using System;
    using System.Text.Json;
    using Jacob;
    using LoRaWan.NetworkServer.BasicsStation;
    using LoRaWan.NetworkServer.BasicsStation.JsonHandlers;
    using LoRaWan.Tests.Common;
    using Xunit;
    using static LoRaWan.DataRateIndex;

    public class LnsDataTests
    {
        [Theory]
        [InlineData(/*lang=json*/ @"{ 'msgtype': 'router_config' }", LnsMessageType.RouterConfig)]
        [InlineData(/*lang=json*/ @"{ 'msgtype': 'dnmsg' }", LnsMessageType.DownlinkMessage)]
        [InlineData(/*lang=json*/ @"{ 'msgtype': 'dntxed' }", LnsMessageType.TransmitConfirmation)]
        [InlineData(/*lang=json*/ @"{ 'msgtype': 'jreq' }", LnsMessageType.JoinRequest)]
        [InlineData(/*lang=json*/ @"{ 'msgtype': 'updf' }", LnsMessageType.UplinkDataFrame)]
        [InlineData(/*lang=json*/ @"{ 'msgtype': 'propdf' }", LnsMessageType.ProprietaryDataFrame)]
        [InlineData(/*lang=json*/ @"{ 'msgtype': 'dnsched' }", LnsMessageType.MulticastSchedule)]
        [InlineData(/*lang=json*/ @"{ 'msgtype': 'timesync' }", LnsMessageType.TimeSync)]
        [InlineData(/*lang=json*/ @"{ 'msgtype': 'runcmd' }", LnsMessageType.RunCommand)]
        [InlineData(/*lang=json*/ @"{ 'msgtype': 'rmtsh' }", LnsMessageType.RemoteShell)]
        [InlineData(/*lang=json*/ @"{ 'msgtype': 'version' }", LnsMessageType.Version)]
        [InlineData(/*lang=json*/ @"{ 'onePropBefore': { 'value': 123 }, 'msgtype': 'version' }", LnsMessageType.Version)]
        [InlineData(/*lang=json*/ @"{ 'msgtype': 'version', 'onePropAfter': { 'value': 123 } }", LnsMessageType.Version)]
        internal void ReadMessageType_Succeeds(string json, LnsMessageType expectedMessageType)
        {
            var messageType = LnsData.MessageTypeReader.Read(JsonUtil.Strictify(json));
            Assert.Equal(expectedMessageType, messageType);
        }


        [Theory]
        [InlineData(/*lang=json*/ @"{ 'msgtype': 'NOTrouter_config' }")]
        [InlineData(/*lang=json*/ @"{ 'msgtype': 'NOTdnmsg' }")]
        [InlineData(/*lang=json*/ @"{ 'msgtype': 'NOTdntxed' }")]
        [InlineData(/*lang=json*/ @"{ 'msgtype': 'NOTjreq' }")]
        [InlineData(/*lang=json*/ @"{ 'msgtype': 'NOTupdf' }")]
        [InlineData(/*lang=json*/ @"{ 'msgtype': 'NOTversion' }")]
        [InlineData(/*lang=json*/ @"{ 'onePropBefore': { 'value': 123 }, 'msgtype': 'NOTversion' }")]
        [InlineData(/*lang=json*/ @"{ 'msgtype': 'NOTversion', 'onePropAfter': { 'value': 123 }}")]
        internal void ReadMessageType_Fails(string json)
        {
            Assert.Throws<FormatException>(() => _ = LnsData.MessageTypeReader.Read(JsonUtil.Strictify(json)));
        }

        public static readonly TheoryData<FramePort?, int, string>
            UpstreamDataframeReader_Succeeds_Data =
                TheoryDataFactory.From((FramePorts.App8, 8, "5ABBBA"),
                                       ((FramePort?)null, -1, ""));

        [Theory]
        [MemberData(nameof(UpstreamDataframeReader_Succeeds_Data))]
        internal void UpstreamDataframeReader_Succeeds(FramePort? expectedPort, int port, string payload)
        {
            var json = @"{
                msgtype: 'updf', MHdr: 128, DevAddr: 58772467, FCtrl: 0, FCnt: 164, FOpts: '',
                FPort: " + JsonSerializer.Serialize(port) + @",
                FRMPayload: " + JsonSerializer.Serialize(payload) + @",
                MIC: -1943282916, DR: 4, Freq: 868100000,
                upinfo: {
                    rctx: 0, xtime: 40250921680313459, gpstime: 0, fts: -1, rssi: -60,
                    snr: 9, rxtime: 1635347491.917289
                }
            }";

            var updf = LnsData.UpstreamDataFrameReader.Read(JsonUtil.Strictify(json));
            Assert.Equal(new DevAddr(58772467), updf.DevAddr);
            Assert.Equal(string.Empty, updf.Options);
            Assert.Equal(expectedPort, updf.Port);
            Assert.Equal(FrameControlFlags.None, updf.FrameControlFlags);
            Assert.Equal(164, updf.Counter);
            Assert.Equal(payload, updf.Payload);
            Assert.Equal(new MacHeader(128), updf.MacHeader);
            Assert.Equal(new Mic(unchecked(-1943282916)), updf.Mic);
            Assert.Equal(DR4, updf.RadioMetadata.DataRate);
            Assert.Equal(new Hertz(868100000), updf.RadioMetadata.Frequency);
            Assert.Equal((ulong)40250921680313459, updf.RadioMetadata.UpInfo.Xtime);
            Assert.Equal((uint)0, updf.RadioMetadata.UpInfo.AntennaPreference);
            Assert.Equal((uint)0, updf.RadioMetadata.UpInfo.GpsTime);
            Assert.Equal(-60, updf.RadioMetadata.UpInfo.ReceivedSignalStrengthIndication);
            Assert.Equal(9, updf.RadioMetadata.UpInfo.SignalNoiseRatio);
        }

        [Theory]
        [InlineData("Invalid JSON value; expecting a JSON number compatible with Byte. See token \"Number\" at offset 25.",
                    /*lang=json*/
                                  @"{ 'msgtype': 'updf', 'MHdr': 300, 'DevAddr': 58772467, 'FCtrl': 0, 'FCnt': 164, 'FOpts': '', 'FPort': 8, 'FRMPayload': '5ABBBA', 'MIC': -1943282916,
                        'DR': 4, 'Freq': 868100000, 'upinfo': {'rctx': 0,'xtime': 40250921680313459,'gpstime': 0,'fts': -1,'rssi': -60,'snr': 9,'rxtime': 1635347491.917289} }")]
        [InlineData(@"Invalid JSON value; expecting a JSON number compatible with UInt32. See token ""Number"" at offset 39.",
                    /*lang=json*/
                                  @"{ 'msgtype': 'updf', 'MHdr': 128, 'DevAddr': -58772467, 'FCtrl': 0, 'FCnt': 164, 'FOpts': '', 'FPort': 8, 'FRMPayload': '5ABBBA', 'MIC': -1943282916,
                        'DR': 4, 'Freq': 868100000, 'upinfo': {'rctx': 0,'xtime': 40250921680313459,'gpstime': 0,'fts': -1,'rssi': -60,'snr': 9,'rxtime': 1635347491.917289} }")]
        [InlineData(@"Invalid JSON value; expecting a JSON number compatible with Byte. See token ""Number"" at offset 56.",
                    /*lang=json*/
                                  @"{ 'msgtype': 'updf', 'MHdr': 128, 'DevAddr': 58772467, 'FCtrl': 300, 'FCnt': 164, 'FOpts': '', 'FPort': 8, 'FRMPayload': '5ABBBA', 'MIC': -1943282916,
                        'DR': 4, 'Freq': 868100000, 'upinfo': {'rctx': 0,'xtime': 40250921680313459,'gpstime': 0,'fts': -1,'rssi': -60,'snr': 9,'rxtime': 1635347491.917289} }")]
        [InlineData(@"Invalid JSON value; expecting a JSON number compatible with UInt16. See token ""Number"" at offset 65.",
                    /*lang=json*/
                                  @"{ 'msgtype': 'updf', 'MHdr': 128, 'DevAddr': 58772467, 'FCtrl': 0, 'FCnt': -164, 'FOpts': '', 'FPort': 8, 'FRMPayload': '5ABBBA', 'MIC': -1943282916,
                        'DR': 4, 'Freq': 868100000, 'upinfo': {'rctx': 0,'xtime': 40250921680313459,'gpstime': 0,'fts': -1,'rssi': -60,'snr': 9,'rxtime': 1635347491.917289} }")]
        [InlineData(@"Invalid JSON value where a JSON string was expected. See token ""Number"" at offset 77.",
                    /*lang=json*/
                                  @"{ 'msgtype': 'updf', 'MHdr': 128, 'DevAddr': 58772467, 'FCtrl': 0, 'FCnt': 164, 'FOpts': 5, 'FPort': 8, 'FRMPayload': '5ABBBA', 'MIC': -1943282916,
                        'DR': 4, 'Freq': 868100000, 'upinfo': {'rctx': 0,'xtime': 40250921680313459,'gpstime': 0,'fts': -1,'rssi': -60,'snr': 9,'rxtime': 1635347491.917289} }")]
        [InlineData(@"Invalid FPort in JSON, which must be either -1 or 0..255. See token ""Number"" at offset 88.",
                    /*lang=json*/
                                  @"{ 'msgtype': 'updf', 'MHdr': 128, 'DevAddr': 58772467, 'FCtrl': 0, 'FCnt': 164, 'FOpts': '', 'FPort': 300, 'FRMPayload': '5ABBBA', 'MIC': -1943282916,
                        'DR': 4, 'Freq': 868100000, 'upinfo': {'rctx': 0,'xtime': 40250921680313459,'gpstime': 0,'fts': -1,'rssi': -60,'snr': 9,'rxtime': 1635347491.917289} }")]
        [InlineData(@"Invalid JSON value where a JSON string was expected. See token ""Number"" at offset 103.",
                    /*lang=json*/
                                  @"{ 'msgtype': 'updf', 'MHdr': 128, 'DevAddr': 58772467, 'FCtrl': 0, 'FCnt': 164, 'FOpts': '', 'FPort': 8, 'FRMPayload': 5, 'MIC': -1943282916,
                        'DR': 4, 'Freq': 868100000, 'upinfo': {'rctx': 0,'xtime': 40250921680313459,'gpstime': 0,'fts': -1,'rssi': -60,'snr': 9,'rxtime': 1635347491.917289} }")]
        [InlineData(@"Invalid JSON value; expecting a JSON number compatible with Int32. See token ""Number"" at offset 118.",
                    /*lang=json*/
                                  @"{ 'msgtype': 'updf', 'MHdr': 128, 'DevAddr': 58772467, 'FCtrl': 0, 'FCnt': 164, 'FOpts': '', 'FPort': 8, 'FRMPayload': '5ABBBA', 'MIC': 5.0,
                        'DR': 4, 'Freq': 868100000, 'upinfo': {'rctx': 0,'xtime': 40250921680313459,'gpstime': 0,'fts': -1,'rssi': -60,'snr': 9,'rxtime': 1635347491.917289} }")]
        [InlineData(@"Invalid FPort in JSON, which must be either -1 or 0..255. See token ""Number"" at offset 88.",
                    /*lang=json*/
                                  @"{ 'msgtype': 'updf', 'MHdr': 128, 'DevAddr': 58772467, 'FCtrl': 0, 'FCnt': 164, 'FOpts': '', 'FPort': -2, 'FRMPayload': '5ABBBA', 'MIC': 5,
                        'DR': 4, 'Freq': 868100000, 'upinfo': {'rctx': 0,'xtime': 40250921680313459,'gpstime': 0,'fts': -1,'rssi': -60,'snr': 9,'rxtime': 1635347491.917289} }")]
        [InlineData(@"Invalid JSON value where a JSON string was expected. See token ""Null"" at offset 104.",
                    /*lang=json*/
                                  @"{ 'msgtype': 'updf', 'MHdr': 128, 'DevAddr': 58772467, 'FCtrl': 0, 'FCnt': 164, 'FOpts': '', 'FPort': -1, 'FRMPayload': null, 'MIC': 5,
                        'DR': 4, 'Freq': 868100000, 'upinfo': {'rctx': 0,'xtime': 40250921680313459,'gpstime': 0,'fts': -1,'rssi': -60,'snr': 9,'rxtime': 1635347491.917289} }")]
        [InlineData(@"""FRMPayload"" without an ""FPort"" is forbidden.",
                    /*lang=json*/
                                  @"{ 'msgtype': 'updf', 'MHdr': 128, 'DevAddr': 58772467, 'FCtrl': 0, 'FCnt': 164, 'FOpts': '', 'FPort': -1, 'FRMPayload': '5ABBBA', 'MIC': 5,
                        'DR': 4, 'Freq': 868100000, 'upinfo': {'rctx': 0,'xtime': 40250921680313459,'gpstime': 0,'fts': -1,'rssi': -60,'snr': 9,'rxtime': 1635347491.917289} }")]
        internal void UpstreamDataframeReader_Fails(string expectedError, string json)
        {
            var ex = Assert.Throws<JsonException>(() => _ = LnsData.UpstreamDataFrameReader.Read(JsonUtil.Strictify(json)));
            Assert.Equal(expectedError, ex.Message);
        }

        [Fact]
        internal void JoinRequestFrameReader_Succeeds()
        {
            var json = /*lang=json*/ @"{'msgtype':'jreq','MHdr':0,'JoinEui':'47-62-78-C8-E5-D2-C4-B5','DevEui':'85-27-C1-DF-EE-A4-16-9E',
                          'DevNonce':41675,'MIC':1528855177, 'DR': 4, 'Freq': 868100000,
                          'upinfo': {'rctx': 0,'xtime': 40250921680313459,'gpstime': 0,'fts': -1,'rssi': -60,'snr': 9,'rxtime': 1635347491.917289}}";
            var jreq = LnsData.JoinRequestFrameReader.Read(JsonUtil.Strictify(json));
            Assert.Equal(new MacHeader(0), jreq.MacHeader);
            Assert.Equal(new JoinEui(5143806528655115445), jreq.JoinEui);
            Assert.Equal(new DevEui(9594850698661729950), jreq.DevEui);
            Assert.Equal(new DevNonce(41675), jreq.DevNonce);
            Assert.Equal(new Mic(1528855177), jreq.Mic);
            Assert.Equal(DR4, jreq.RadioMetadata.DataRate);
            Assert.Equal(new Hertz(868100000), jreq.RadioMetadata.Frequency);
            Assert.Equal((ulong)40250921680313459, jreq.RadioMetadata.UpInfo.Xtime);
            Assert.Equal((uint)0, jreq.RadioMetadata.UpInfo.AntennaPreference);
            Assert.Equal((uint)0, jreq.RadioMetadata.UpInfo.GpsTime);
            Assert.Equal(-60, jreq.RadioMetadata.UpInfo.ReceivedSignalStrengthIndication);
            Assert.Equal(9, jreq.RadioMetadata.UpInfo.SignalNoiseRatio);
        }

        [Theory]
        [InlineData(/*lang=json*/ @"{'msgtype':'jreq','MHdr':300,'JoinEui':'47-62-78-C8-E5-D2-C4-B5','DevEui':'85-27-C1-DF-EE-A4-16-9E','DevNonce':41675,'MIC':1528855177,
                        'DR': 4, 'Freq': 868100000, 'upinfo': {'rctx': 0,'xtime': 40250921680313459,'gpstime': 0,'fts': -1,'rssi': -60,'snr': 9,'rxtime': 1635347491.917289}}")]
        [InlineData(/*lang=json*/ @"{'msgtype':'jreq','MHdr':0,'JoinEui':'476278C8E5D2C4B5','DevEui':'85-27-C1-DF-EE-A4-16-9E','DevNonce':41675,'MIC':1528855177,
                        'DR': 4, 'Freq': 868100000, 'upinfo': {'rctx': 0,'xtime': 40250921680313459,'gpstime': 0,'fts': -1,'rssi': -60,'snr': 9,'rxtime': 1635347491.917289}}")]
        [InlineData(/*lang=json*/ @"{'msgtype':'jreq','MHdr':0,'JoinEui':'47-62-78-C8-E5-D2-C4-B5','DevEui':'8527C1DFEEA4169E','DevNonce':41675,'MIC':1528855177,
                        'DR': 4, 'Freq': 868100000, 'upinfo': {'rctx': 0,'xtime': 40250921680313459,'gpstime': 0,'fts': -1,'rssi': -60,'snr': 9,'rxtime': 1635347491.917289}}")]
        [InlineData(/*lang=json*/ @"{'msgtype':'jreq','MHdr':0,'JoinEui':'47-62-78-C8-E5-D2-C4-B5','DevEui':'85-27-C1-DF-EE-A4-16-9E','DevNonce':-41675,'MIC':1528855177,
                        'DR': 4, 'Freq': 868100000, 'upinfo': {'rctx': 0,'xtime': 40250921680313459,'gpstime': 0,'fts': -1,'rssi': -60,'snr': 9,'rxtime': 1635347491.917289}}")]
        [InlineData(/*lang=json*/ @"{'msgtype':'jreq','MHdr':0,'JoinEui':'47-62-78-C8-E5-D2-C4-B5','DevEui':'85-27-C1-DF-EE-A4-16-9E','DevNonce':41675,'MIC':'-1528855177',
                        'DR': 4, 'Freq': 868100000, 'upinfo': {'rctx': 0,'xtime': 40250921680313459,'gpstime': 0,'fts': -1,'rssi': -60,'snr': 9,'rxtime': 1635347491.917289}}")]
        internal void JoinRequestFrameReader_Fails(string json)
        {
            _ = Assert.Throws<JsonException>(() => _ = LnsData.JoinRequestFrameReader.Read(JsonUtil.Strictify(json)));
        }

        [Theory]
        [InlineData(/*lang=json*/ @"{'DR': 0}", DR0)]
        [InlineData(/*lang=json*/ @"{'DR': 1}", DR1)]
        [InlineData(/*lang=json*/ @"{'DR': 2}", DR2)]
        [InlineData(/*lang=json*/ @"{'DR': 3}", DR3)]
        [InlineData(/*lang=json*/ @"{'DR': 4}", DR4)]
        [InlineData(/*lang=json*/ @"{'DR': 5}", DR5)]
        [InlineData(/*lang=json*/ @"{'DR': 6}", DR6)]
        [InlineData(/*lang=json*/ @"{'DR': 7}", DR7)]
        [InlineData(/*lang=json*/ @"{'DR': 8}", DR8)]
        [InlineData(/*lang=json*/ @"{'DR': 9}", DR9)]
        [InlineData(/*lang=json*/ @"{'DR': 10}", DR10)]
        [InlineData(/*lang=json*/ @"{'DR': 11}", DR11)]
        [InlineData(/*lang=json*/ @"{'DR': 12}", DR12)]
        [InlineData(/*lang=json*/ @"{'DR': 13}", DR13)]
        [InlineData(/*lang=json*/ @"{'DR': 14}", DR14)]
        [InlineData(/*lang=json*/ @"{'DR': 15}", DR15)]
        internal void RadioMetadata_DataRateProperty_CanReturnProperDataRate(string json, DataRateIndex expectedDataRate)
        {
            var actualDataRate = JsonReader.Object(LnsData.RadioMetadataProperties.DataRate).Read(JsonUtil.Strictify(json));
            Assert.Equal(expectedDataRate, actualDataRate);
        }


        [Theory]
        [InlineData(/*lang=json*/ @"{'DR': -2}")]
        [InlineData(/*lang=json*/ @"{'DR': '0'}")]
        [InlineData(/*lang=json*/ @"{'DR': 301}")]
        internal void RadioMetadata_DataRateProperty_Fails(string json)
        {
            Assert.Throws<JsonException>(() => _ = JsonReader.Object(LnsData.RadioMetadataProperties.DataRate).Read(JsonUtil.Strictify(json)));
        }

        [Theory]
        [InlineData(/*lang=json*/ @"{'Freq': 860000000}", 860000000uL)]
        [InlineData(/*lang=json*/ @"{'Freq': 861000000}", 861000000uL)]
        [InlineData(/*lang=json*/ @"{'Freq': 862000000}", 862000000uL)]
        [InlineData(/*lang=json*/ @"{'Freq': 863000000}", 863000000uL)]
        [InlineData(/*lang=json*/ @"{'Freq': 864000000}", 864000000uL)]
        [InlineData(/*lang=json*/ @"{'Freq': 865000000}", 865000000uL)]
        [InlineData(/*lang=json*/ @"{'Freq': 866000000}", 866000000uL)]
        [InlineData(/*lang=json*/ @"{'Freq': 867000000}", 867000000uL)]
        internal void RadioMetadata_FreqProperty_CanReturnProperFreq(string json, ulong expectedFreq)
        {
            var actualFreq = JsonReader.Object(LnsData.RadioMetadataProperties.Freq).Read(JsonUtil.Strictify(json));
            Assert.Equal(new Hertz(expectedFreq), actualFreq);
        }

        [Theory]
        [InlineData(@"{}")]
        [InlineData(/*lang=json*/ @"{'Freq': -861000000}")]
        [InlineData(/*lang=json*/ @"{'Freq': '862000000'}")]
        internal void RadioMetadata_FreqProperty_Fails(string json)
        {
            Assert.Throws<JsonException>(() => _ = JsonReader.Object(LnsData.RadioMetadataProperties.Freq).Read(JsonUtil.Strictify(json)));
        }
    }
}
