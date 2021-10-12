// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWanTest
{
    using LoRaWan;
    using Xunit;

    public class MacHeaderTest
    {
        readonly MacHeader confirmedDataDown = new(128);
        readonly MacHeader unconfirmedDataUp = new(64);

        [Fact]
        public void Equals_Returns_True_When_Value_Equals()
        {
            var other = new MacHeader(128);
            Assert.True(this.confirmedDataDown.Equals(other));
        }

        [Fact]
        public void Equals_Returns_False_When_Value_Is_Different()
        {
            var other = this.unconfirmedDataUp;
            Assert.False(this.confirmedDataDown.Equals(other));
        }

        [Fact]
        public void Equals_Returns_False_When_Other_Type_Is_Different()
        {
            Assert.False(this.confirmedDataDown.Equals(new object()));
        }

        [Fact]
        public void Equals_Returns_False_When_Other_Type_Is_Null()
        {
            Assert.False(this.confirmedDataDown.Equals(null));
        }

        [Fact]
        public void Op_Equality_Returns_True_When_Values_Equal()
        {
            var other = new MacHeader(128);
            Assert.True(this.confirmedDataDown == other);
        }

        [Fact]
        public void Op_Equality_Returns_False_When_Values_Differ()
        {
            Assert.False(this.confirmedDataDown == this.unconfirmedDataUp);
        }

        [Fact]
        public void Op_Inequality_Returns_False_When_Values_Equal()
        {
            var other = new MacHeader(128);
            Assert.False(this.confirmedDataDown != other);
        }

        [Fact]
        public void Op_Inequality_Returns_True_When_Values_Differ()
        {
            Assert.True(this.confirmedDataDown != this.unconfirmedDataUp);
        }

        [Fact]
        public void ToString_Returns_Hex_String()
        {
            Assert.Equal("80", this.confirmedDataDown.ToString());
        }

        [Theory]
        [InlineData(  0, MacMessageType.JoinRequest        , 0)]
        [InlineData( 31, MacMessageType.JoinRequest        , 3)]
        [InlineData( 32, MacMessageType.JoinAccept         , 0)]
        [InlineData( 63, MacMessageType.JoinAccept         , 3)]
        [InlineData( 64, MacMessageType.UnconfirmedDataUp  , 0)]
        [InlineData( 95, MacMessageType.UnconfirmedDataUp  , 3)]
        [InlineData( 96, MacMessageType.UnconfirmedDataDown, 0)]
        [InlineData(127, MacMessageType.UnconfirmedDataDown, 3)]
        [InlineData(128, MacMessageType.ConfirmedDataUp    , 0)]
        [InlineData(159, MacMessageType.ConfirmedDataUp    , 3)]
        [InlineData(160, MacMessageType.ConfirmedDataDown  , 0)]
        [InlineData(191, MacMessageType.ConfirmedDataDown  , 3)]
        [InlineData(192, MacMessageType.RejoinRequest      , 0)]
        [InlineData(223, MacMessageType.RejoinRequest      , 3)]
        [InlineData(224, MacMessageType.Proprietary        , 0)]
        [InlineData(255, MacMessageType.Proprietary        , 3)]
        public void MacHeader_Returns_ProperMessageType_And_Major(byte value, MacMessageType macMessageType, int major)
        {
            // arrange
            var mhdr = new MacHeader(value);

            // assert
            Assert.Equal(macMessageType, mhdr.MessageType);
            Assert.Equal(major, mhdr.Major);
        }

        [Fact]
        public void Write_Writes_Byte_And_Returns_Updated_Span()
        {
            var bytes = new byte[4];
            var remainingBytes = unconfirmedDataUp.Write(bytes);
            remainingBytes.Fill(0xff);
            Assert.Equal(3, remainingBytes.Length);
            Assert.Equal(new byte[] { 0x40, 0xff, 0xff, 0xff }, bytes);
        }
    }
}
