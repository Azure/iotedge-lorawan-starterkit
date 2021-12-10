// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

namespace LoRaWan.Tests.Unit
{
    using Xunit;

    public class MetricTests
    {
        [Fact]
        public void Mega()
        {
            const double x = 1.23;
            var m = Metric.Mega(x);
            var result = m.Value;

            Assert.Equal(x, result);
        }
    }
}
