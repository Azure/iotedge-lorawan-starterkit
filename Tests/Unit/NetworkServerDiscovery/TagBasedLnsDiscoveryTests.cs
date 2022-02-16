// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

namespace LoRaWan.Tests.Unit.NetworkServerDiscovery
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using LoRaWan.NetworkServerDiscovery;
    using Microsoft.Azure.Devices;
    using Microsoft.Azure.Devices.Shared;
    using Microsoft.Extensions.Logging.Abstractions;
    using Moq;
    using Xunit;

    public sealed class TagBasedLnsDiscoveryTests
    {
        private static readonly StationEui StationEui = new StationEui(1);
        private static readonly string[] LnsUris = new[] { "ws://foo:5000/bar/baz", "wss://baz:5001/baz", "ws://baz" };

        private readonly Mock<RegistryManager> registryManagerMock;
        private readonly TagBasedLnsDiscovery subject;

        public TagBasedLnsDiscoveryTests()
        {
            this.registryManagerMock = new Mock<RegistryManager>();
            this.registryManagerMock.Setup(rm => rm.CreateQuery(It.IsAny<string>())).Returns(Mock.Of<IQuery>());
            this.subject = new TagBasedLnsDiscovery(this.registryManagerMock.Object, NullLogger<TagBasedLnsDiscovery>.Instance);
        }

        [Fact]
        public async Task ResolveLnsAsync_Resolves_Lns_By_Tag()
        {
            // arrange
            const string networkId = "foo";
            SetupLbsTwinResponse(StationEui, networkId);
            SetupIotHubQueryResponse(networkId, LnsUris);

            // act
            var result = await this.subject.ResolveLnsAsync(StationEui, CancellationToken.None);

            // assert
            Assert.Contains(result, LnsUris.Select(u => new Uri(u)));
        }

        [Fact]
        public async Task ResolveLnsAsync_Throws_If_No_Lns_Matches()
        {
            // arrange
            SetupLbsTwinResponse(StationEui, "firstnetwork");
            SetupIotHubQueryResponse("someothernetwork", LnsUris);

            // act + assert
            var ex = await Assert.ThrowsAsync<LoRaProcessingException>(() => this.subject.ResolveLnsAsync(StationEui, CancellationToken.None));
            Assert.Equal(LoRaProcessingErrorCode.LnsDiscoveryFailed, ex.ErrorCode);
        }

        [Theory]
        [InlineData("asdf123.")]
        [InlineData("'asdf123'")]
        [InlineData("network+")]
        public async Task ResolveLnsAsync_Throws_If_NetworkId_Is_Invalid(string networkId)
        {
            // arrange
            SetupLbsTwinResponse(StationEui, networkId);

            // act + assert
            var ex = await Assert.ThrowsAsync<LoRaProcessingException>(() => this.subject.ResolveLnsAsync(StationEui, CancellationToken.None));
            Assert.Equal(LoRaProcessingErrorCode.InvalidDeviceConfiguration, ex.ErrorCode);
        }

        [Fact]
        public async Task ResolveLnsAsync_Throws_If_Network_Is_Empty()
        {
            SetupLbsTwinResponse(StationEui, string.Empty);
            _ = await Assert.ThrowsAsync<InvalidOperationException>(() => this.subject.ResolveLnsAsync(StationEui, CancellationToken.None));
        }

        private void SetupLbsTwinResponse(StationEui stationEui, string networkId)
        {
            this.registryManagerMock
                .Setup(rm => rm.GetTwinAsync(stationEui.ToString(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Twin { Tags = new TwinCollection(@$"{{""network"":""{networkId}""}}") });
        }

        private void SetupIotHubQueryResponse(string networkId, IList<string> hostAddresses)
        {
            var queryMock = new Mock<IQuery>();
            queryMock.SetupSequence(q => q.HasMoreResults).Returns(true).Returns(false);
            queryMock.Setup(q => q.GetNextAsJsonAsync()).ReturnsAsync(from ha in hostAddresses
                                                                      select JsonSerializer.Serialize(new { hostAddress = ha }));
            this.registryManagerMock
                .Setup(rm => rm.CreateQuery($"SELECT properties.desired.hostAddress FROM devices.modules WHERE tags.network = '{networkId}'"))
                .Returns(queryMock.Object);
        }
    }
}
