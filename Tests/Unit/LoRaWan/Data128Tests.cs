// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Unit
{
    using Xunit;

    public class Data128Tests
    {
        [Theory]
        [InlineData(uint.MinValue, uint.MaxValue)]
        [InlineData(uint.MinValue, uint.MinValue)]
        [InlineData(uint.MaxValue, uint.MaxValue)]
        [InlineData(10, 20)]
        public void Write_Read_Composition_Forms_Identity(uint hi, uint lo)
        {
            // arrange
            var buffer = new byte[Data128.Size];
            var subject = new Data128(hi, lo);

            // act
            _ = subject.Write(buffer);
            var result = Data128.Read(buffer);

            // assert
            Assert.Equal(subject, result);
        }
    }
}
