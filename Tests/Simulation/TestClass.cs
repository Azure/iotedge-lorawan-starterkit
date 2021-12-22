// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Simulation
{
    using Xunit;

    public class TestClass
    {
        [Fact]
        public void Test()
        {
            var basicsstation = new BasicsStationsSimulator("aabb");
            basicsstation.ConnectAsync(new System.Uri("ws://localhost:5000/router-info"));
        }

    }
}
