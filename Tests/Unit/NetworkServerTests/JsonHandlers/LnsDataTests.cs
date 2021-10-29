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
    }
}
