// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Unit.NetworkServer.BasicsStation.JsonHandlers
{
    using System;
    using System.Text.Json;
    using LoRaWan.NetworkServer;
    using LoRaWan.NetworkServer.BasicsStation;
    using LoRaWan.NetworkServer.BasicsStation.JsonHandlers;
    using LoRaWan.Tests.Common;
    using Xunit;
    using static LoRaWan.DataRateIndex;

    public class LnsDataTests
    {
        [Theory]
        [InlineData(@"{ 'msgtype': 'router_config' }", LnsMessageType.RouterConfig)]
        [InlineData(@"{ 'msgtype': 'dnmsg' }", LnsMessageType.DownlinkMessage)]
        [InlineData(@"{ 'msgtype': 'dntxed' }", LnsMessageType.TransmitConfirmation)]
        [InlineData(@"{ 'msgtype': 'jreq' }", LnsMessageType.JoinRequest)]
        [InlineData(@"{ 'msgtype': 'updf' }", LnsMessageType.UplinkDataFrame)]
        [InlineData(@"{ 'msgtype': 'propdf' }", LnsMessageType.ProprietaryDataFrame)]
        [InlineData(@"{ 'msgtype': 'dnsched' }", LnsMessageType.MulticastSchedule)]
        [InlineData(@"{ 'msgtype': 'timesync' }", LnsMessageType.TimeSync)]
        [InlineData(@"{ 'msgtype': 'runcmd' }", LnsMessageType.RunCommand)]
        [InlineData(@"{ 'msgtype': 'rmtsh' }", LnsMessageType.RemoteShell)]
        [InlineData(@"{ 'msgtype': 'version' }", LnsMessageType.Version)]
        [InlineData(@"{ 'onePropBefore': { 'value': 123 }, 'msgtype': 'version' }", LnsMessageType.Version)]
        [InlineData(@"{ 'msgtype': 'version', 'onePropAfter': { 'value': 123 } }", LnsMessageType.Version)]
        internal void ReadMessageType_Succeeds(string json, LnsMessageType expectedMessageType)
        {
            var messageType = LnsData.MessageTypeReader.Read(JsonUtil.Strictify(json));
            Assert.Equal(expectedMessageType, messageType);
        }


        [Theory]
        [InlineData(@"{ 'msgtype': 'NOTrouter_config' }")]
        [InlineData(@"{ 'msgtype': 'NOTdnmsg' }")]
        [InlineData(@"{ 'msgtype': 'NOTdntxed' }")]
        [InlineData(@"{ 'msgtype': 'NOTjreq' }")]
        [InlineData(@"{ 'msgtype': 'NOTupdf' }")]
        [InlineData(@"{ 'msgtype': 'NOTversion' }")]
        [InlineData(@"{ 'onePropBefore': { 'value': 123 }, 'msgtype': 'NOTversion' }")]
        [InlineData(@"{ 'msgtype': 'NOTversion', 'onePropAfter': { 'value': 123 }}")]
        internal void ReadMessageType_Fails(string json)
        {
            Assert.Throws<FormatException>(() => _ = LnsData.MessageTypeReader.Read(JsonUtil.Strictify(json)));
        }

        [Fact]
        internal void UpstreamDataframeReader_Succeeds()
        {
            var json = @"{ 'msgtype': 'updf', 'MHdr': 128, 'DevAddr': 58772467, 'FCtrl': 0, 'FCnt': 164, 'FOpts': '',
                           'FPort': 8, 'FRMPayload': '5ABBBA', 'MIC': -1943282916, 'DR': 4, 'Freq': 868100000,
                           'upinfo': {'rctx': 0,'xtime': 40250921680313459,'gpstime': 0,'fts': -1,'rssi': -60,
                           'snr': 9,'rxtime': 1635347491.917289}}";
            var updf = LnsData.UpstreamDataFrameReader.Read(JsonUtil.Strictify(json));
            Assert.Equal(new DevAddr(58772467), updf.DevAddr);
            Assert.Equal(string.Empty, updf.Options);
            Assert.Equal(new FramePort(8), updf.Port);
            Assert.Equal(FrameControlFlags.None, updf.FrameControlFlags);
            Assert.Equal(164, updf.Counter);
            Assert.Equal("5ABBBA", updf.Payload);
            Assert.Equal(new MacHeader(128), updf.MacHeader);
            Assert.Equal(new Mic(unchecked((uint)-1943282916)), updf.Mic);
            Assert.Equal(DR4, updf.RadioMetadata.DataRate);
            Assert.Equal(new Hertz(868100000), updf.RadioMetadata.Frequency);
            Assert.Equal((ulong)40250921680313459, updf.RadioMetadata.UpInfo.Xtime);
            Assert.Equal((uint)0, updf.RadioMetadata.UpInfo.AntennaPreference);
            Assert.Equal((uint)0, updf.RadioMetadata.UpInfo.GpsTime);
            Assert.Equal(-60, updf.RadioMetadata.UpInfo.ReceivedSignalStrengthIndication);
            Assert.Equal(9, updf.RadioMetadata.UpInfo.SignalNoiseRatio);
        }

        [Theory]
        [InlineData(@"{ 'msgtype': 'updf', 'MHdr': 300, 'DevAddr': 58772467, 'FCtrl': 0, 'FCnt': 164, 'FOpts': '', 'FPort': 8, 'FRMPayload': '5ABBBA', 'MIC': -1943282916,
                        'DR': 4, 'Freq': 868100000, 'upinfo': {'rctx': 0,'xtime': 40250921680313459,'gpstime': 0,'fts': -1,'rssi': -60,'snr': 9,'rxtime': 1635347491.917289} }")]
        [InlineData(@"{ 'msgtype': 'updf', 'MHdr': 128, 'DevAddr': -58772467, 'FCtrl': 0, 'FCnt': 164, 'FOpts': '', 'FPort': 8, 'FRMPayload': '5ABBBA', 'MIC': -1943282916,
                        'DR': 4, 'Freq': 868100000, 'upinfo': {'rctx': 0,'xtime': 40250921680313459,'gpstime': 0,'fts': -1,'rssi': -60,'snr': 9,'rxtime': 1635347491.917289} }")]
        [InlineData(@"{ 'msgtype': 'updf', 'MHdr': 128, 'DevAddr': 58772467, 'FCtrl': 300, 'FCnt': 164, 'FOpts': '', 'FPort': 8, 'FRMPayload': '5ABBBA', 'MIC': -1943282916,
                        'DR': 4, 'Freq': 868100000, 'upinfo': {'rctx': 0,'xtime': 40250921680313459,'gpstime': 0,'fts': -1,'rssi': -60,'snr': 9,'rxtime': 1635347491.917289} }")]
        [InlineData(@"{ 'msgtype': 'updf', 'MHdr': 128, 'DevAddr': 58772467, 'FCtrl': 0, 'FCnt': -164, 'FOpts': '', 'FPort': 8, 'FRMPayload': '5ABBBA', 'MIC': -1943282916,
                        'DR': 4, 'Freq': 868100000, 'upinfo': {'rctx': 0,'xtime': 40250921680313459,'gpstime': 0,'fts': -1,'rssi': -60,'snr': 9,'rxtime': 1635347491.917289} }")]
        [InlineData(@"{ 'msgtype': 'updf', 'MHdr': 128, 'DevAddr': 58772467, 'FCtrl': 0, 'FCnt': 164, 'FOpts': 5, 'FPort': 8, 'FRMPayload': '5ABBBA', 'MIC': -1943282916,
                        'DR': 4, 'Freq': 868100000, 'upinfo': {'rctx': 0,'xtime': 40250921680313459,'gpstime': 0,'fts': -1,'rssi': -60,'snr': 9,'rxtime': 1635347491.917289} }")]
        [InlineData(@"{ 'msgtype': 'updf', 'MHdr': 128, 'DevAddr': 58772467, 'FCtrl': 0, 'FCnt': 164, 'FOpts': '', 'FPort': 300, 'FRMPayload': '5ABBBA', 'MIC': -1943282916,
                        'DR': 4, 'Freq': 868100000, 'upinfo': {'rctx': 0,'xtime': 40250921680313459,'gpstime': 0,'fts': -1,'rssi': -60,'snr': 9,'rxtime': 1635347491.917289} }")]
        [InlineData(@"{ 'msgtype': 'updf', 'MHdr': 128, 'DevAddr': 58772467, 'FCtrl': 0, 'FCnt': 164, 'FOpts': '', 'FPort': 8, 'FRMPayload': 5, 'MIC': -1943282916,
                        'DR': 4, 'Freq': 868100000, 'upinfo': {'rctx': 0,'xtime': 40250921680313459,'gpstime': 0,'fts': -1,'rssi': -60,'snr': 9,'rxtime': 1635347491.917289} }")]
        [InlineData(@"{ 'msgtype': 'updf', 'MHdr': 128, 'DevAddr': 58772467, 'FCtrl': 0, 'FCnt': 164, 'FOpts': '', 'FPort': 8, 'FRMPayload': '5ABBBA', 'MIC': 5.0,
                        'DR': 4, 'Freq': 868100000, 'upinfo': {'rctx': 0,'xtime': 40250921680313459,'gpstime': 0,'fts': -1,'rssi': -60,'snr': 9,'rxtime': 1635347491.917289} }")]
        internal void UpstreamDataframeReader_Fails(string json)
        {
            _ = Assert.Throws<JsonException>(() => _ = LnsData.UpstreamDataFrameReader.Read(JsonUtil.Strictify(json)));
        }

        [Fact]
        internal void JoinRequestFrameReader_Succeeds()
        {
            var json = @"{'msgtype':'jreq','MHdr':0,'JoinEui':'47-62-78-C8-E5-D2-C4-B5','DevEui':'85-27-C1-DF-EE-A4-16-9E',
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
        [InlineData(@"{'msgtype':'jreq','MHdr':300,'JoinEui':'47-62-78-C8-E5-D2-C4-B5','DevEui':'85-27-C1-DF-EE-A4-16-9E','DevNonce':41675,'MIC':1528855177,
                        'DR': 4, 'Freq': 868100000, 'upinfo': {'rctx': 0,'xtime': 40250921680313459,'gpstime': 0,'fts': -1,'rssi': -60,'snr': 9,'rxtime': 1635347491.917289}}")]
        [InlineData(@"{'msgtype':'jreq','MHdr':0,'JoinEui':'476278C8E5D2C4B5','DevEui':'85-27-C1-DF-EE-A4-16-9E','DevNonce':41675,'MIC':1528855177,
                        'DR': 4, 'Freq': 868100000, 'upinfo': {'rctx': 0,'xtime': 40250921680313459,'gpstime': 0,'fts': -1,'rssi': -60,'snr': 9,'rxtime': 1635347491.917289}}")]
        [InlineData(@"{'msgtype':'jreq','MHdr':0,'JoinEui':'47-62-78-C8-E5-D2-C4-B5','DevEui':'8527C1DFEEA4169E','DevNonce':41675,'MIC':1528855177,
                        'DR': 4, 'Freq': 868100000, 'upinfo': {'rctx': 0,'xtime': 40250921680313459,'gpstime': 0,'fts': -1,'rssi': -60,'snr': 9,'rxtime': 1635347491.917289}}")]
        [InlineData(@"{'msgtype':'jreq','MHdr':0,'JoinEui':'47-62-78-C8-E5-D2-C4-B5','DevEui':'85-27-C1-DF-EE-A4-16-9E','DevNonce':-41675,'MIC':1528855177,
                        'DR': 4, 'Freq': 868100000, 'upinfo': {'rctx': 0,'xtime': 40250921680313459,'gpstime': 0,'fts': -1,'rssi': -60,'snr': 9,'rxtime': 1635347491.917289}}")]
        [InlineData(@"{'msgtype':'jreq','MHdr':0,'JoinEui':'47-62-78-C8-E5-D2-C4-B5','DevEui':'85-27-C1-DF-EE-A4-16-9E','DevNonce':41675,'MIC':'-1528855177',
                        'DR': 4, 'Freq': 868100000, 'upinfo': {'rctx': 0,'xtime': 40250921680313459,'gpstime': 0,'fts': -1,'rssi': -60,'snr': 9,'rxtime': 1635347491.917289}}")]
        internal void JoinRequestFrameReader_Fails(string json)
        {
            _ = Assert.Throws<JsonException>(() => _ = LnsData.JoinRequestFrameReader.Read(JsonUtil.Strictify(json)));
        }

        [Theory]
        [InlineData(@"{'DR': 0}", DR0)]
        [InlineData(@"{'DR': 1}", DR1)]
        [InlineData(@"{'DR': 2}", DR2)]
        [InlineData(@"{'DR': 3}", DR3)]
        [InlineData(@"{'DR': 4}", DR4)]
        [InlineData(@"{'DR': 5}", DR5)]
        [InlineData(@"{'DR': 6}", DR6)]
        [InlineData(@"{'DR': 7}", DR7)]
        [InlineData(@"{'DR': 8}", DR8)]
        [InlineData(@"{'DR': 9}", DR9)]
        [InlineData(@"{'DR': 10}", DR10)]
        [InlineData(@"{'DR': 11}", DR11)]
        [InlineData(@"{'DR': 12}", DR12)]
        [InlineData(@"{'DR': 13}", DR13)]
        [InlineData(@"{'DR': 14}", DR14)]
        [InlineData(@"{'DR': 15}", DR15)]
        internal void RadioMetadata_DataRateProperty_CanReturnProperDataRate(string json, DataRateIndex expectedDataRate)
        {
            var actualDataRate = JsonReader.Object(LnsData.RadioMetadataProperties.DataRate).Read(JsonUtil.Strictify(json));
            Assert.Equal(expectedDataRate, actualDataRate);
        }


        [Theory]
        [InlineData(@"{'DR': -2}")]
        [InlineData(@"{'DR': '0'}")]
        [InlineData(@"{'DR': 301}")]
        internal void RadioMetadata_DataRateProperty_Fails(string json)
        {
            Assert.Throws<JsonException>(() => _ = JsonReader.Object(LnsData.RadioMetadataProperties.DataRate).Read(JsonUtil.Strictify(json)));
        }

        [Theory]
        [InlineData(@"{'Freq': 860000000}", 860000000uL)]
        [InlineData(@"{'Freq': 861000000}", 861000000uL)]
        [InlineData(@"{'Freq': 862000000}", 862000000uL)]
        [InlineData(@"{'Freq': 863000000}", 863000000uL)]
        [InlineData(@"{'Freq': 864000000}", 864000000uL)]
        [InlineData(@"{'Freq': 865000000}", 865000000uL)]
        [InlineData(@"{'Freq': 866000000}", 866000000uL)]
        [InlineData(@"{'Freq': 867000000}", 867000000uL)]
        internal void RadioMetadata_FreqProperty_CanReturnProperFreq(string json, ulong expectedFreq)
        {
            var actualFreq = JsonReader.Object(LnsData.RadioMetadataProperties.Freq).Read(JsonUtil.Strictify(json));
            Assert.Equal(new Hertz(expectedFreq), actualFreq);
        }

        [Theory]
        [InlineData(@"{}")]
        [InlineData(@"{'Freq': -861000000}")]
        [InlineData(@"{'Freq': '862000000'}")]
        internal void RadioMetadata_FreqProperty_Fails(string json)
        {
            Assert.Throws<JsonException>(() => _ = JsonReader.Object(LnsData.RadioMetadataProperties.Freq).Read(JsonUtil.Strictify(json)));
        }
    }
}
