// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.SimulatedTest
{
    using LoRaWan.Tests.Shared;
    using Xunit;

    public class IntegrationTestBaseSim : IntegrationTestBase, IClassFixture<IntegrationTestFixtureSim>
    {
        protected IntegrationTestFixtureSim TestFixtureSim
        {
            get { return (IntegrationTestFixtureSim)TestFixture; }
        }

        public IntegrationTestBaseSim(IntegrationTestFixtureSim testFixture)
            : base(testFixture)
        {
        }
    }
}
