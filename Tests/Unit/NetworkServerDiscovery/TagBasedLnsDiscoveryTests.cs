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
    using global::LoRaTools;
    using global::LoRaTools.IoTHubImpl;
    using LoRaWan.NetworkServerDiscovery;
    using LoRaWan.Tests.Common;
    using Microsoft.Azure.Devices.Shared;
    using Microsoft.Extensions.Caching.Memory;
    using Microsoft.Extensions.Logging.Abstractions;
    using Moq;
    using Xunit;

    public sealed class TagBasedLnsDiscoveryTests : IDisposable
    {
        private static readonly StationEui StationEui = new StationEui(1);
        private static readonly string[] LnsUris = new[] { "ws://foo:5000/bar/baz", "wss://baz:5001/baz", "ws://baz" };

        private readonly Mock<IDeviceRegistryManager> registryManagerMock;
        private readonly MemoryCache memoryCache;
        private readonly TagBasedLnsDiscovery subject;

        public TagBasedLnsDiscoveryTests()
        {
            this.registryManagerMock = new Mock<IDeviceRegistryManager>();
            this.registryManagerMock.Setup(rm => rm.FindLnsByNetworkId(It.IsAny<string>())).Returns(Mock.Of<IRegistryPageResult<string>>());
            this.memoryCache = new MemoryCache(new MemoryCacheOptions());
            this.subject = new TagBasedLnsDiscovery(memoryCache, this.registryManagerMock.Object, NullLogger<TagBasedLnsDiscovery>.Instance);
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
            _ = await Assert.ThrowsAsync<LoRaProcessingException>(() => this.subject.ResolveLnsAsync(StationEui, CancellationToken.None));
        }

        [Fact]
        public async Task ResolveLnsAsync_Uses_Round_Robin_Distribution()
        {
            // arrange
            const int numberOfRequests = 5;
            const string networkId = "foo";
            SetupLbsTwinResponse(StationEui, networkId);
            SetupIotHubQueryResponse(networkId, LnsUris);

            // act + assert
            for (var i = 0; i < numberOfRequests; ++i)
            {
                var result = await this.subject.ResolveLnsAsync(StationEui, CancellationToken.None);
                Assert.Equal(new Uri(LnsUris[i % LnsUris.Length]), result);
            }
        }

        [Fact]
        public async Task ResolveLnsAsync_Tracks_Last_Returned_Lns_By_Station()
        {
            // arrange
            const string networkId = "foo";
            var firstStation = new StationEui(1);
            var secondStation = new StationEui(2);
            SetupLbsTwinResponse(firstStation, networkId);
            SetupLbsTwinResponse(secondStation, networkId);
            SetupIotHubQueryResponse(networkId, LnsUris);

            // act
            var first = await this.subject.ResolveLnsAsync(firstStation, CancellationToken.None);
            var second = await this.subject.ResolveLnsAsync(secondStation, CancellationToken.None);

            // assert
            Assert.Equal(new Uri(LnsUris[0]), first);
            Assert.Equal(new Uri(LnsUris[0]), second);
        }

        [Fact]
        public async Task ResolveLnsAsync_Caches_Lns_By_Network()
        {
            // arrange
            const string networkId = "foo";
            var firstStation = new StationEui(1);
            var secondStation = new StationEui(2);
            SetupLbsTwinResponse(firstStation, networkId);
            SetupLbsTwinResponse(secondStation, networkId);
            SetupIotHubQueryResponse(networkId, LnsUris);

            // act
            _ = await this.subject.ResolveLnsAsync(firstStation, CancellationToken.None);
            _ = await this.subject.ResolveLnsAsync(secondStation, CancellationToken.None);

            // assert
            this.registryManagerMock.Verify(rm => rm.FindLnsByNetworkId(It.IsAny<string>()), Times.Once);
        }

        [Fact]
        public async Task ResolveLnsAsync_Throws_If_Twin_Not_Found()
        {
            var ex = await Assert.ThrowsAsync<LoRaProcessingException>(() => this.subject.ResolveLnsAsync(StationEui, CancellationToken.None));
            Assert.Equal(LoRaProcessingErrorCode.TwinFetchFailed, ex.ErrorCode);
        }

        public static TheoryData<string?> Erroneous_Host_Address_TheoryData() => TheoryDataFactory.From(new[]
        {
            null, "", "http://mylns:5000", "htt://mylns:5000", "ws:/mylns:5000"
        });

        [Theory]
        [MemberData(nameof(Erroneous_Host_Address_TheoryData))]
        public async Task ResolveLnsAsync_Is_Resilient_Against_Erroneous_Host_Address(string? hostAddress)
        {
            // arrange
            const string networkId = "foo";
            SetupLbsTwinResponse(StationEui, networkId);
            SetupIotHubQueryResponse(networkId, LnsUris.Concat(new[] { hostAddress }).ToList());

            // act
            var result = await this.subject.ResolveLnsAsync(StationEui, CancellationToken.None);

            // assert
            Assert.Contains(result, LnsUris.Select(u => new Uri(u)));
        }

        [Theory]
        [MemberData(nameof(Erroneous_Host_Address_TheoryData))]
        public async Task ResolveLnsAsync_Throws_if_Only_Lns_Is_Misconfigured(string? hostAddress)
        {
            // arrange
            const string networkId = "foo";
            SetupLbsTwinResponse(StationEui, networkId);
            SetupIotHubQueryResponse(networkId, new[] { hostAddress });

            // act + assert
            var ex = await Assert.ThrowsAsync<LoRaProcessingException>(() => this.subject.ResolveLnsAsync(StationEui, CancellationToken.None));
            Assert.Equal(LoRaProcessingErrorCode.LnsDiscoveryFailed, ex.ErrorCode);
        }

        [Fact]
        public async Task ResolveLnsAsync_Caches_Station_Network()
        {
            // arrange
            const string networkId = "foo";
            SetupLbsTwinResponse(StationEui, networkId);
            SetupIotHubQueryResponse(networkId, LnsUris);

            // act
            _ = await this.subject.ResolveLnsAsync(StationEui, CancellationToken.None);
            _ = await this.subject.ResolveLnsAsync(StationEui, CancellationToken.None);

            // assert
            this.registryManagerMock.Verify(rm => rm.GetStationTwinAsync(It.IsAny<StationEui>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        private void SetupLbsTwinResponse(StationEui stationEui, string networkId)
        {
            this.registryManagerMock
                .Setup(rm => rm.GetStationTwinAsync(stationEui, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new IoTHubStationTwin(new Twin { Tags = new TwinCollection(@$"{{""network"":""{networkId}""}}") }));
        }

        private void SetupIotHubQueryResponse(string networkId, IList<string?> hostAddresses)
        {
            var queryMock = new Mock<IRegistryPageResult<string>>();
            var i = 0;
            queryMock.Setup(q => q.HasMoreResults).Returns(() => i++ % 2 == 0);
            queryMock.Setup(q => q.GetNextPageAsync()).ReturnsAsync(from ha in hostAddresses
                                                                      select ha is { } someHa ? JsonSerializer.Serialize(new { hostAddress = ha, deviceId = Guid.NewGuid().ToString() })
                                                                                              : JsonSerializer.Serialize(new { deviceId = Guid.NewGuid().ToString() }));
            this.registryManagerMock
                .Setup(rm => rm.FindLnsByNetworkId(networkId))
                .Returns(queryMock.Object);
        }

        public void Dispose()
        {
            this.memoryCache.Dispose();
            this.subject.Dispose();
        }
    }
}
