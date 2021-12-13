// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

namespace LoRaWan.Tests.Unit
{
    using System.Globalization;
    using Xunit;

    public class MegaTests
    {
        private readonly Mega subject = new Mega(1.23);

        [Fact]
        public void Value()
        {
            var result = this.subject.Value;
            Assert.Equal(1.23, result);
        }

        [Fact]
        public void Units()
        {
            var result = this.subject.Units;
            Assert.Equal(1_230_000, result);
        }

        [Fact]
        public void ToString_With_Culture_Returns_Formatted_Number()
        {
            var result = this.subject.ToString(CultureInfo.InvariantCulture);

            Assert.Equal("1.23", result);
        }

        [Fact]
        public void ToString_With_Null_Format_Spec_With_Culture_Returns_Formatted_Number()
        {
            var result = this.subject.ToString(null, CultureInfo.InvariantCulture);

            Assert.Equal("1.23", result);
        }

        [Fact]
        public void ToString_With_Format_Spec_With_Culture_Returns_Formatted_Number()
        {
            var result = this.subject.ToString("0.0000", CultureInfo.InvariantCulture);

            Assert.Equal("1.2300", result);
        }
    }
}
