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
        [InlineData(  0, MacMessageType.JoinRequest        )]
        [InlineData( 31, MacMessageType.JoinRequest        )]
        [InlineData( 32, MacMessageType.JoinAccept         )]
        [InlineData( 63, MacMessageType.JoinAccept         )]
        [InlineData( 64, MacMessageType.UnconfirmedDataUp  )]
        [InlineData( 95, MacMessageType.UnconfirmedDataUp  )]
        [InlineData( 96, MacMessageType.UnconfirmedDataDown)]
        [InlineData(127, MacMessageType.UnconfirmedDataDown)]
        [InlineData(128, MacMessageType.ConfirmedDataUp    )]
        [InlineData(159, MacMessageType.ConfirmedDataUp    )]
        [InlineData(160, MacMessageType.ConfirmedDataDown  )]
        [InlineData(191, MacMessageType.ConfirmedDataDown  )]
        [InlineData(192, MacMessageType.RejoinRequest      )]
        [InlineData(223, MacMessageType.RejoinRequest      )]
        [InlineData(224, MacMessageType.Proprietary        )]
        [InlineData(255, MacMessageType.Proprietary        )]
        public void Properties_Return_Corresponding_Parts(byte value, MacMessageType macMessageType)
        {
            var subject = new MacHeader(value);
            Assert.Equal(macMessageType, subject.MessageType);
            Assert.Equal(DataMessageVersion.R1, subject.Major);
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
