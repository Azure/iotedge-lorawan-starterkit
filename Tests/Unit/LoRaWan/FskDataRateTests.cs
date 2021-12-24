// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Unit
{
    using LoRaWan;
    using Xunit;

    public class FskDataRateTests
    {
        private readonly FskDataRate subject = FskDataRate.Fsk50000;

        [Fact]
        public void Fsk50000_Has_Correct_Modulation_Kind()
        {
            Assert.Equal(ModulationKind.Fsk, this.subject.ModulationKind);
        }

        [Fact]
        public void Fsk50000_Has_Correct_Bit_Rate()
        {
            Assert.Equal(50_000, this.subject.BitRateInKiloBitsPerSecond);
        }
    }
}
