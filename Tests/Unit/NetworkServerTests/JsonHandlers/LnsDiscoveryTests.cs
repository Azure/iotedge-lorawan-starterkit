// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Unit.NetworkServerTests.JsonHandlers
{
    using System;
    using System.Text.Json;
    using LoRaWan;
    using LoRaWan.NetworkServer;
    using LoRaWan.NetworkServer.BasicsStation.JsonHandlers;
    using Xunit;

    public class LnsDiscoveryTests
    {
        [Theory]
        [InlineData(@"{ ""router"": ""b827:ebff:fee1:e39a"" }")]
        [InlineData(@"{ ""router"": ""b8-27-eb-ff-fe-e1-e3-9a"" }")]
        [InlineData(@"{ ""router"": 13269834311795860378 }")]
        [InlineData(@"{ ""onePropBefore"": { ""value"": 123 }, ""router"": 13269834311795860378 }")]
        [InlineData(@"{ ""router"": 13269834311795860378, ""onePropAfter"": { ""value"": 123 } }")]
        public void ReadQuery(string json)
        {
            var stationEui = LnsDiscovery.QueryReader.Read(json);
            Assert.Equal(new StationEui(0xb827_ebff_fee1_e39aUL), stationEui);
        }

        [Theory]
        [InlineData(@"{ ""nonRouter"": ""wrongValue"" }")]
        [InlineData(@"{}")]
        public void ReadQuery_Throws_OnMissingProperty(string json)
        {
            Assert.Throws<JsonException>(() => _ = LnsDiscovery.QueryReader.Read(json));
        }

        [Theory]
        [InlineData(@"{ ""router"": [""wrongValue""] }")]
        [InlineData(@"{ ""router"": { ""value"": 13269834311795860378 } }")]
        [InlineData(@"{ ""router"": true }")]
        public void ReadQuery_Throws_OnInvalidPropertyType(string json)
        {
            Assert.Throws<JsonException>(() => _ = LnsDiscovery.QueryReader.Read(json));
        }

        private const string ValidMuxs = "0000:00FF:FE00:0000";
        private const string ValidUrlString = "ws://localhost:5000/router-data";

        [Theory]
        [InlineData("b8-27-eb-ff-fe-e1-e3-9a", ValidMuxs, ValidUrlString,
            @"{""router"":""b827:ebff:fee1:e39a"",""muxs"":""0000:00FF:FE00:0000"",""uri"":""ws://localhost:5000/router-data""}")]
        [InlineData("b827:ebff:fee1:e39a", ValidMuxs, ValidUrlString,
            @"{""router"":""b827:ebff:fee1:e39a"",""muxs"":""0000:00FF:FE00:0000"",""uri"":""ws://localhost:5000/router-data""}")]
        public void Write_Succeeds(string stationId6, string muxs, string routerDataEndpoint, string expected)
        {
            var stationEui = stationId6.Contains(':', StringComparison.Ordinal)
                ? Id6.TryParse(stationId6, out var id6) ? new StationEui(id6) : throw new JsonException()
                : Hexadecimal.TryParse(stationId6, out var hhd, '-')
                    ? new StationEui(hhd)
                    : throw new JsonException();

            var computed = Json.Stringify(w =>
                LnsDiscovery.WriteResponse(w, stationEui, muxs, new Uri(routerDataEndpoint)));
            Assert.Equal(expected, computed);
        }

        [Fact]
        public void Write_Fails_BecauseOfNonId6Muxs()
        {
            var muxs = "000000FFFE000000";

            _ = Id6.TryParse("b827:ebff:fee1:e39a", out var stationId6);
            var stationEui = new StationEui(stationId6);

            Assert.Throws<ArgumentException>(() =>
                _ = Json.Write(w => LnsDiscovery.WriteResponse(w, stationEui, muxs, new Uri(ValidUrlString))));
        }

        [Fact]
        public void Write_Fails_BecauseNullUri()
        {
            _ = Id6.TryParse("b827:ebff:fee1:e39a", out var stationId6);
            var stationEui = new StationEui(stationId6);

            Assert.Throws<ArgumentNullException>(() =>
                Json.Write(w => LnsDiscovery.WriteResponse(w, stationEui, ValidMuxs, null)));
        }
    }
}
