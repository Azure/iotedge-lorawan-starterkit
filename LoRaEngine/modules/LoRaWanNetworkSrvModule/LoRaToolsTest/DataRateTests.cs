namespace LoRaWanTest
{
    using System;
    using LoRaWan;
    using Xunit;

    public class DataRateTests
    {
        readonly DataRate subject = new(5);
        readonly DataRate other = new(10);

        [Fact]
        public void Initialization_Succeeds_For_Valid_Values()
        {
            for (var dr = 0; dr < 15; dr++)
            {
                var ex = Record.Exception(() => new DataRate(dr));
                Assert.Null(ex);
            }
        }

        [Theory]
        [InlineData(-1)]
        [InlineData(16)]
        public void Initialization_Throws_For_Invalid_Values(int dr)
        {
            var ex = Assert.Throws<ArgumentOutOfRangeException>(() => new DataRate(dr));
            Assert.Equal("value", ex.ParamName);
            Assert.Equal(dr, ex.ActualValue);
        }

        [Fact]
        public void Equals_Returns_True_When_Value_Equals()
        {
            var other = new DataRate(subject.AsInt32);
            Assert.True(this.subject.Equals(other));
        }

        [Fact]
        public void Equals_Returns_False_When_Value_Is_Different()
        {
            var other = this.other;
            Assert.False(this.subject.Equals(other));
        }

        [Fact]
        public void Equals_Returns_False_When_Other_Type_Is_Different()
        {
            Assert.False(this.subject.Equals(new object()));
        }

        [Fact]
        public void Equals_Returns_False_When_Other_Type_Is_Null()
        {
            Assert.False(this.subject.Equals(null));
        }

        [Fact]
        public void Op_Equality_Returns_True_When_Values_Equal()
        {
            var other = new DataRate(subject.AsInt32);
            Assert.True(this.subject == other);
        }

        [Fact]
        public void Op_Equality_Returns_True_When_Values_Differ()
        {
            Assert.False(this.subject == this.other);
        }

        [Fact]
        public void Op_Inequality_Returns_False_When_Values_Equal()
        {
            var other = new DataRate(subject.AsInt32);
            Assert.False(this.subject != other);
        }

        [Fact]
        public void Op_Inequality_Returns_True_When_Values_Differ()
        {
            Assert.True(this.subject != this.other);
        }

        [Fact]
        public void ToString_Returns_Decimal_String()
        {
            Assert.Equal("5", this.subject.ToString());
        }
    }
}
