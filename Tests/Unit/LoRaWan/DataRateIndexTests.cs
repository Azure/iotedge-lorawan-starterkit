// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Unit
{
    using System;
    using System.Linq;
    using LoRaWan;
    using LoRaWan.Tests.Common;
    using Xunit;
    using MoreEnumerable = MoreLinq.MoreEnumerable;

    public class DataRateIndexTests
    {
        public static readonly TheoryData<int, DataRateIndex> MemberValueData =
            TheoryDataFactory.From(MoreEnumerable.Sequence(0, 15).Zip(Enum.GetValues<DataRateIndex>()));

        [Theory]
        [MemberData(nameof(MemberValueData))]
        public void MemberValue(int expected, DataRateIndex input)
        {
            Assert.Equal(expected, (int)input);
        }
    }
}
