// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

namespace LoRaWan.Tests.Unit.NetworkServer.BasicsStation
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using LoRaWan.NetworkServer.BasicsStation;
    using Xunit;

    public sealed class LocalLnsDiscoveryTests
    {
        [Fact]
        public async Task ResolveLnsAsync_Returns_Initialization_Uri()
        {
            // arrange
            var url = new Uri("wss://localhost:5001/router-data");
            var subject = new LocalLnsDiscovery(url);

            // act
            var result = await subject.ResolveLnsAsync(new StationEui(1), CancellationToken.None);

            // assert
            Assert.Equal(url, result);
        }
    }
}
