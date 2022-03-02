// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Unit.NetworkServer
{
    using LoRaWan.Tests.Common;
    using Xunit;
    using Xunit.Abstractions;

    public class ProcessingDelayTest : MessageProcessorTestBase
    {
        public ProcessingDelayTest(ITestOutputHelper testOutputHelper) :
            base(testOutputHelper)
        { }

        [Theory]
        [InlineData(null, true)]
        [InlineData(400, true)]
        [InlineData(1500, true)]
        [InlineData(0, false)]
        [InlineData(-100, false)]
        [InlineData(-1000, false)]
        public void IsProcessingDelayEnbled(int? processingDelay, bool processingDelayEnabled)
        {
            if (processingDelay is { } delay)
                ServerConfiguration.ProcessingDelayInMilliseconds = delay;

            Assert.Equal(processingDelayEnabled, RequestHandlerImplementation.IsProcessingDelayEnabled());
        }
    }
}
