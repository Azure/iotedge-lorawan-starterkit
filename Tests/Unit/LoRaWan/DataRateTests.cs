// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Unit
{
    using System;
    using LoRaWan;
    using Xunit;

    public class DataRateTests
    {
        private readonly DataRate subject = new(5);

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
        public void ToString_Returns_Decimal_String()
        {
            Assert.Equal("5", this.subject.ToString());
        }
    }
}
