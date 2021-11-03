// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Unit.NetworkServerTests.BasicsStation
{
    using System;
    using System.ComponentModel.DataAnnotations;
    using LoRaWan.NetworkServer.BasicsStation;
    using Xunit;

    public class LnsMessageTypeExtensionsTests
    {
        [Theory]
        [InlineData("version", LnsMessageType.Version)]
        [InlineData("router_config", LnsMessageType.RouterConfig)]
        [InlineData("jreq", LnsMessageType.JoinRequest)]
        [InlineData("updf", LnsMessageType.UplinkDataFrame)]
        [InlineData("dntxed", LnsMessageType.TransmitConfirmation)]
        [InlineData("dnmsg", LnsMessageType.DownlinkMessage)]
        internal void TryParseLnsMessageType_Succeeds(string input, LnsMessageType expectedType)
        {
            Assert.True(LnsMessageTypeExtensions.TryParseLnsMessageType(input, out var actualType));
            Assert.Equal(expectedType, actualType);
        }

        [Theory]
        [InlineData("NOTversion")]
        [InlineData("NOTrouter_config")]
        [InlineData("NOTjreq")]
        [InlineData("NOTupdf")]
        [InlineData("NOTdntxed")]
        [InlineData("NOTdnmsg")]
        internal void TryParseLnsMessageType_Fails(string input)
        {
            Assert.False(LnsMessageTypeExtensions.TryParseLnsMessageType(input, out var actualType));
            Assert.Null(actualType);
        }

        [Theory]
        [InlineData(LnsMessageType.Version, "version")]
        [InlineData(LnsMessageType.RouterConfig, "router_config")]
        [InlineData(LnsMessageType.JoinRequest, "jreq")]
        [InlineData(LnsMessageType.UplinkDataFrame, "updf")]
        [InlineData(LnsMessageType.TransmitConfirmation, "dntxed")]
        [InlineData(LnsMessageType.DownlinkMessage, "dnmsg")]
        internal void ToBasicStationString_Succeeds(LnsMessageType type, string expectedString)
        {
            Assert.Equal(expectedString, type.ToBasicStationString());
        }

        [Theory]
        [InlineData(null)]
        [InlineData(LnsMessageType.JoinRequest)]
        internal void ParseAndValidate_Suceeds(LnsMessageType? lnsMessageType)
        {
            Assert.Equal(LnsMessageType.JoinRequest, LnsMessageTypeExtensions.ParseAndValidate("jreq", lnsMessageType));
        }

        [Fact]
        internal void ParseAndValidate_Throws_OnNonSuccessfulValidation()
        {
            Assert.Throws<ValidationException>(() => _ = LnsMessageTypeExtensions.ParseAndValidate("jreq", LnsMessageType.UplinkDataFrame));
        }

        [Fact]
        internal void ParseAndValidate_Throws_OnNonValidMessageType()
        {
            Assert.Throws<FormatException>(() => _ = LnsMessageTypeExtensions.ParseAndValidate("jreqNotValid", LnsMessageType.UplinkDataFrame));
        }
    }
}
