namespace LoRaWanTest
{
    using System;
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
        [InlineData((byte)0, MacMessageType.JoinRequest, 0)]
        [InlineData((byte)31, MacMessageType.JoinRequest, 3)]
        [InlineData((byte)32, MacMessageType.JoinAccept, 0)]
        [InlineData((byte)63, MacMessageType.JoinAccept, 3)]
        [InlineData((byte)64, MacMessageType.UnconfirmedDataUp, 0)]
        [InlineData((byte)95, MacMessageType.UnconfirmedDataUp, 3)]
        [InlineData((byte)96, MacMessageType.UnconfirmedDataDown, 0)]
        [InlineData((byte)127, MacMessageType.UnconfirmedDataDown, 3)]
        [InlineData((byte)128, MacMessageType.ConfirmedDataUp, 0)]
        [InlineData((byte)159, MacMessageType.ConfirmedDataUp, 3)]
        [InlineData((byte)160, MacMessageType.ConfirmedDataDown, 0)]
        [InlineData((byte)191, MacMessageType.ConfirmedDataDown, 3)]
        [InlineData((byte)192, MacMessageType.RejoinRequest, 0)]
        [InlineData((byte)223, MacMessageType.RejoinRequest, 3)]
        [InlineData((byte)224, MacMessageType.Proprietary, 0)]
        [InlineData((byte)255, MacMessageType.Proprietary, 3)]
        public void MacHeader_Returns_ProperMessageType_And_Major(byte value, MacMessageType macMessageType, int major)
        {
            // arrange
            var mhdr = new MacHeader(value);

            // assert
            Assert.Equal(macMessageType, mhdr.MessageType);
            Assert.Equal(major, mhdr.Major);
        }

        [Fact]
        public void Write_Succeeds_And_Returns_SlicedSpan()
        {
            var rentedMemoryLength = 4;
            Span<byte> memorySpan = stackalloc byte[rentedMemoryLength];
            var newSpan = unconfirmedDataUp.Write(memorySpan);
            Assert.Equal(rentedMemoryLength - 1, newSpan.Length);
        }
    }
}
