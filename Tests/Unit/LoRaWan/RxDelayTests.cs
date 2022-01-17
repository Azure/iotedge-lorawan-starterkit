// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Unit
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using LoRaWan.Tests.Common;
    using Xunit;
    using static LoRaWan.RxDelay;
    using MoreEnumerable = MoreLinq.MoreEnumerable;

    public class RxDelayTests
    {
        private static readonly IReadOnlyList<RxDelay> RxDelays = Enum.GetValues<RxDelay>();

        [Fact]
        public void Defines_Values_From_0_To_15()
        {
            Assert.Equal(MoreEnumerable.Sequence(0, 15), from v in RxDelays select (int)v);
        }

        public static readonly TheoryData<int, RxDelay> RxDelaySecondsData =
            TheoryDataFactory.From(from rxd in RxDelays
                                   select (rxd is RxDelay0 or RxDelay1 ? 1 : (int)rxd, rxd));

        [Theory]
        [MemberData(nameof(RxDelaySecondsData))]
        public void ToSeconds(int expectedSeconds, RxDelay delay)
        {
            Assert.Equal(expectedSeconds, delay.ToSeconds());
        }

        [Theory]
        [MemberData(nameof(RxDelaySecondsData))]
        public void ToTimeSpan(int expectedSeconds, RxDelay delay)
        {
            Assert.Equal(TimeSpan.FromSeconds(expectedSeconds), delay.ToTimeSpan());
        }

        public static readonly TheoryData<RxDelay, RxDelay> RxDelayIncData =
            TheoryDataFactory.From(from rxd in RxDelays
                                   select ((RxDelay)((int)rxd + 1), rxd));

        [Theory]
        [MemberData(nameof(RxDelayIncData))]
        public void Inc(RxDelay expected, RxDelay input)
        {
            Assert.Equal(expected, input.Inc());
        }

        [Fact]
        public void Inc_Does_Not_Throw_On_Invalid_Result()
        {
            var ex = Record.Exception(() => RxDelays[^1].Inc());
            Assert.Null(ex);
        }

        [Fact]
        public void ToSeconds_Throws_When_Input_Is_Invalid()
        {
            var input = RxDelays[^1].Inc();

            var ex = Assert.Throws<ArgumentException>(() => input.ToSeconds());
            Assert.Equal("delay", ex.ParamName);
        }

        [Fact]
        public void ToTimeSpan_Throws_When_Input_Is_Invalid()
        {
            var input = RxDelays[^1].Inc();

            var ex = Assert.Throws<ArgumentException>(() => input.ToSeconds());
            Assert.Equal("delay", ex.ParamName);
        }
    }
}
