// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Unit
{
    using System.Collections;
    using global::LoRaTools;
    using Xunit;

    public class ClassBTests
    {

        


        [Fact]
        public void CheckWorks()
        {
            var payload = new BitArray(new bool[] { false, true, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, true, false, true, false, true, true, false });
            var result = CRC16.Compute(payload);
            var expectedResult = new BitArray(new bool[] { false, false, true, false, false, true, true, true, true, false, false, true, true, true, true, false });
            Assert.Equal(expectedResult, result);
        }
    }
}
