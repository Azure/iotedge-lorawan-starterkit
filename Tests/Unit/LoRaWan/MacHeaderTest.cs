// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Unit
{
    using LoRaWan;
    using Xunit;

    public class MacHeaderTest
    {
        private readonly MacHeader confirmedDataDown = new(128);
        private readonly MacHeader unconfirmedDataUp = new(64);

        [Theory]
        [InlineData(  0, LoRaMessageType.JoinRequest        , 0)]
        [InlineData( 31, LoRaMessageType.JoinRequest        , 3)]
        [InlineData( 32, LoRaMessageType.JoinAccept         , 0)]
        [InlineData( 63, LoRaMessageType.JoinAccept         , 3)]
        [InlineData( 64, LoRaMessageType.UnconfirmedDataUp  , 0)]
        [InlineData( 95, LoRaMessageType.UnconfirmedDataUp  , 3)]
        [InlineData( 96, LoRaMessageType.UnconfirmedDataDown, 0)]
        [InlineData(127, LoRaMessageType.UnconfirmedDataDown, 3)]
        [InlineData(128, LoRaMessageType.ConfirmedDataUp    , 0)]
        [InlineData(159, LoRaMessageType.ConfirmedDataUp    , 3)]
        [InlineData(160, LoRaMessageType.ConfirmedDataDown  , 0)]
        [InlineData(191, LoRaMessageType.ConfirmedDataDown  , 3)]
        [InlineData(192, LoRaMessageType.RejoinRequest      , 0)]
        [InlineData(223, LoRaMessageType.RejoinRequest      , 3)]
        [InlineData(224, LoRaMessageType.Proprietary        , 0)]
        [InlineData(255, LoRaMessageType.Proprietary        , 3)]
        public void Properties_Return_Corresponding_Parts(byte value, LoRaMessageType macMessageType, int major)
        {
            var subject = new MacHeader(value);
            Assert.Equal(macMessageType, subject.MessageType);
            Assert.Equal(major, subject.Major);
        }

        [Fact]
        public void ToString_Returns_Hex_String()
        {
            Assert.Equal("80", this.confirmedDataDown.ToString());
        }

        [Fact]
        public void Write_Writes_Byte_And_Returns_Updated_Span()
        {
            var bytes = new byte[4];
            var remainingBytes = this.unconfirmedDataUp.Write(bytes);
            remainingBytes.Fill(0xff);
            Assert.Equal(3, remainingBytes.Length);
            Assert.Equal(new byte[] { 0x40, 0xff, 0xff, 0xff }, bytes);
        }
    }
}
