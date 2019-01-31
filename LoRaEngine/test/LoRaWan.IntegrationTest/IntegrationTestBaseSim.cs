using LoRaWan.Test.Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using Xunit;

namespace LoRaWan.IntegrationTest
{
    public class IntegrationTestBaseSim : IntegrationTestBase, IClassFixture<IntegrationTestFixtureSim>
    {
        protected IntegrationTestFixtureSim TestFixtureSim { get { return (IntegrationTestFixtureSim)this.TestFixture; } }

        public IntegrationTestBaseSim(IntegrationTestFixtureSim testFixture)
            : base(testFixture)
        {
        }
    }
}
