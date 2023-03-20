// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

namespace LoRaWan.Tests.E2E
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.IO;
    using System.Linq;
    using System.Net.WebSockets;
    using System.Security.Cryptography;
    using System.Text;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using Jacob;
    using LoRaTools;
    using LoRaWan.Tests.Common;
    using Microsoft.AspNetCore.Mvc.Testing;
    using Microsoft.AspNetCore.TestHost;
    using Microsoft.Azure.Devices;
    using Microsoft.Azure.Devices.Common.Exceptions;
    using Microsoft.Azure.Devices.Shared;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Hosting;
    using Xunit;

    public sealed class LnsDiscoveryFixture : IAsyncLifetime
    {
        private const string LnsModuleName = "LoRaWanNetworkSrvModule";
        private const string FirstNetworkName = "network1";
        private const string SecondNetworkName = "network2";

        public sealed record Lns(string DeviceId, string HostAddress, string NetworkId);
        public sealed record Station(StationEui StationEui, string NetworkId);

        private static readonly Lns[] LnsInfo = new[]
        {
            new Lns("discoverylns1", "wss://lns1:5001", FirstNetworkName), new Lns("discoverylns2", "wss://lns2:5001", FirstNetworkName),
            new Lns("discoverylns3", "wss://lns3:5001", SecondNetworkName), new Lns("discoverylns4", "wss://lns4:5001", SecondNetworkName),
        };

        public static readonly ImmutableArray<Station> StationInfo = new[]
        {
            new Station(new StationEui(1213148791), FirstNetworkName),
            new Station(new StationEui(1213148792), FirstNetworkName),
            new Station(new StationEui(1213148793), SecondNetworkName)
        }.ToImmutableArray();

        public static readonly ImmutableArray<string> NetworkIds =
            LnsInfo.Select(l => l.NetworkId)
                   .Distinct()
                   .ToImmutableArray();

        public static readonly ImmutableDictionary<StationEui, ImmutableArray<Lns>> LnsInfoByStation =
            StationInfo.GroupJoin(LnsInfo, station => station.NetworkId, lns => lns.NetworkId, (s, ls) => (s.StationEui, LnsInfo: ls))
                       .ToImmutableDictionary(x => x.StationEui, x => x.LnsInfo.ToImmutableArray());

        private readonly RegistryManager registryManager;

        public LnsDiscoveryFixture()
        {
            this.registryManager = RegistryManager.CreateFromConnectionString(TestConfiguration.GetConfiguration().IoTHubConnectionString);
        }

        public Task CleanupDevicesAsync() =>
            Task.WhenAll(from deviceId in LnsInfo.Select(l => l.DeviceId.ToString())
                                                 .Concat(StationInfo.Select(s => s.StationEui.ToString()))
                         select this.registryManager.RemoveDeviceAsync(deviceId));

        public Task DisposeAsync() => CleanupDevicesAsync();

        public async Task InitializeAsync()
        {
            try
            {
                // Try to cleanup existing devices from previous runs
                await CleanupDevicesAsync();
            }
            catch (DeviceNotFoundException)
            {
                TestLogger.Log("No device to cleanup was found");
            }

            await WaitBetweenRegistryOperations();

            foreach (var lns in LnsInfo)
            {
                await this.registryManager.AddDeviceAsync(new Device(lns.DeviceId) { Capabilities = new DeviceCapabilities { IotEdge = true } });

                var moduleTwin = new Twin()
                {
                    Properties = new TwinProperties { Desired = new TwinCollection(JsonSerializer.Serialize(new { hostAddress = lns.HostAddress })) },
                    Tags = GetNetworkTags(lns.NetworkId)
                };
                await this.registryManager.AddModuleAsync(new Module(lns.DeviceId, LnsModuleName));
                await this.registryManager.UpdateTwinAsync(lns.DeviceId, LnsModuleName, moduleTwin, "*", CancellationToken.None);
            }

            foreach (var station in StationInfo)
            {
                var deviceId = station.StationEui.ToString();
                await this.registryManager.AddDeviceAsync(new Device(deviceId));
                var twin = new Twin(deviceId) { Tags = GetNetworkTags(station.NetworkId) };
                await this.registryManager.UpdateTwinAsync(deviceId, twin, "*", CancellationToken.None);
            }

            await WaitBetweenRegistryOperations();

            static async Task WaitBetweenRegistryOperations() {
                var waitTime = TimeSpan.FromSeconds(60);
                TestLogger.Log($"Waiting for {waitTime.TotalSeconds} seconds.");
                await Task.Delay(waitTime);
            };

            static TwinCollection GetNetworkTags(string networkId) => new TwinCollection(JsonSerializer.Serialize(new { network = networkId }));
        }
    }

    internal sealed class LnsDiscoveryApplication : WebApplicationFactory<Program>
    {
        private const string IotHubConnectionStringConfigurationName = "ConnectionStrings:IotHub";

        protected override IHost CreateHost(IHostBuilder builder)
        {
            var iotHubConnectionString = TestConfiguration.GetConfiguration().IoTHubConnectionString;
            builder.ConfigureAppConfiguration(config => config.AddInMemoryCollection(new Dictionary<string, string>
            {
                [IotHubConnectionStringConfigurationName] = iotHubConnectionString
            }));

            return base.CreateHost(builder);
        }
    }

    [Trait("Category", "SkipWhenLiveUnitTesting")]
    public sealed class LnsDiscoveryTests : IClassFixture<LnsDiscoveryFixture>, IDisposable
    {
        private static readonly IJsonReader<(Uri LnsUri, string Muxs, StationEui StationEui)> RouterInfoResponseReader =
            JsonReader.Object(JsonReader.Property("uri", from u in JsonReader.String()
                                                         select new Uri(u)),
                              JsonReader.Property("muxs", JsonReader.String()),
                              JsonReader.Property("router", from r in JsonReader.String()
                                                            select StationEui.Parse(r)),
                              (uri, muxs, router) => (uri, muxs, router));

        private readonly LnsDiscoveryApplication subject;
        private readonly WebSocketClient webSocketClient;
        private readonly StationEui firstStation = LnsDiscoveryFixture.StationInfo[0].StationEui;
        private readonly StationEui secondStation = LnsDiscoveryFixture.StationInfo[1].StationEui;
        private readonly StationEui thirdStation = LnsDiscoveryFixture.StationInfo[2].StationEui;

        public LnsDiscoveryTests(LnsDiscoveryFixture _)
        {
            this.subject = new LnsDiscoveryApplication();
            this.webSocketClient = this.subject.Server.CreateWebSocketClient();
        }

        [Fact]
        public async Task Discovery_Requests_Should_Be_Distributed_Between_Lns()
        {
            LogTestStart(nameof(Discovery_Requests_Should_Be_Distributed_Between_Lns));

            // arrange
            var station = this.firstStation;
            var lnsInfo = LnsDiscoveryFixture.LnsInfoByStation[station];

            // act + assert
            var responses = new List<Uri>();
            for (var i = 0; i < lnsInfo.Length * 2; ++i)
                responses.Add(await GetLnsAddressAndAssertAsync(station, CancellationToken.None));

            // assert
            AssertLnsResponsesForStation(station, lnsInfo.Concat(lnsInfo).ToList(), responses);
        }

        [Fact]
        public async Task Discovery_Requests_Should_Distinguish_Between_Stations()
        {
            LogTestStart(nameof(Discovery_Requests_Should_Distinguish_Between_Stations));

            // arrange
            var cancellationToken = CancellationToken.None;
            Assert.Equal(LnsDiscoveryFixture.LnsInfoByStation[this.firstStation].AsEnumerable(), LnsDiscoveryFixture.LnsInfoByStation[this.secondStation].AsEnumerable());

            // act
            var firstResult = await GetLnsAddressAndAssertAsync(this.firstStation, cancellationToken);
            var secondResult = await GetLnsAddressAndAssertAsync(this.secondStation, cancellationToken);

            // assert
            Assert.Equal(firstResult.Host, secondResult.Host);
        }

        [Fact]
        public async Task Discovery_Requests_Should_Distinguish_Between_Networks()
        {
            LogTestStart(nameof(Discovery_Requests_Should_Distinguish_Between_Networks));

            // arrange
            var cancellationToken = CancellationToken.None;
            Assert.NotEqual(LnsDiscoveryFixture.LnsInfoByStation[this.firstStation].AsEnumerable(), LnsDiscoveryFixture.LnsInfoByStation[this.thirdStation].AsEnumerable());
            var firstNetworkLnsCount = LnsDiscoveryFixture.LnsInfoByStation[this.firstStation].Length;
            Assert.Equal(firstNetworkLnsCount, LnsDiscoveryFixture.LnsInfoByStation[this.thirdStation].Length);

            // act
            var firstStationResponses = new List<Uri>();
            var thirdStationResponses = new List<Uri>();
            for (var i = 0; i < firstNetworkLnsCount; ++i)
            {
                firstStationResponses.Add(await GetLnsAddressAndAssertAsync(this.firstStation, cancellationToken));
                thirdStationResponses.Add(await GetLnsAddressAndAssertAsync(this.thirdStation, cancellationToken));
            }

            // assert
            AssertLnsResponsesForStation(this.firstStation, LnsDiscoveryFixture.LnsInfoByStation[this.firstStation], firstStationResponses);
            AssertLnsResponsesForStation(this.thirdStation, LnsDiscoveryFixture.LnsInfoByStation[this.thirdStation], thirdStationResponses);
        }

        [Fact]
        public async Task Discovery_Requests_Should_Indicate_Error_Reason_For_Unknown_Station()
        {
            LogTestStart(nameof(Discovery_Requests_Should_Indicate_Error_Reason_For_Unknown_Station));

            var response = await SendSingleMessageAsync(new StationEui((ulong)RandomNumberGenerator.GetInt32(int.MaxValue)), CancellationToken.None);
            Assert.Contains("could not find twin for station", response, StringComparison.OrdinalIgnoreCase);
        }

        private static void AssertLnsResponsesForStation(StationEui station, IReadOnlyCollection<LnsDiscoveryFixture.Lns> expected, IReadOnlyCollection<Uri> actual) =>
            Assert.Equal(expected.Select(l => new Uri(new Uri(l.HostAddress), $"router-data/{station}")).OrderBy(l => l.AbsoluteUri),
                         actual.OrderBy(l => l.AbsoluteUri));

        private async Task<Uri> GetLnsAddressAndAssertAsync(StationEui station, CancellationToken cancellationToken)
        {
            var json = await SendSingleMessageAsync(station, cancellationToken);
            var (lnsUri, muxs, router) = RouterInfoResponseReader.Read(json);
            Assert.NotEmpty(muxs);
            Assert.Equal(router, station);
            return lnsUri;
        }

        private async Task<string> SendSingleMessageAsync(StationEui stationEui, CancellationToken cancellationToken)
        {
            var webSocket = await this.webSocketClient.ConnectAsync(new Uri(this.subject.Server.BaseAddress, "router-info"), cancellationToken);
            await webSocket.SendAsync(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { router = stationEui.AsUInt64 })), WebSocketMessageType.Text, endOfMessage: true, cancellationToken);
            var e = webSocket.ReadTextMessages(cancellationToken);
            var result = !await e.MoveNextAsync() ? throw new InvalidOperationException("No response received.") : e.Current;

            try
            {
                await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Normal closure", cancellationToken);
            }
            catch (IOException)
            {
                // Connection already closed.
            }

            return result;
        }

        private static void LogTestStart(string testName) => TestLogger.Log($"Starting test '{testName}'.");

        public void Dispose() => this.subject.Dispose();
    }
}
