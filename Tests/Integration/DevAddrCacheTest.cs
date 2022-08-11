// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Integration
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Security.Cryptography;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using LoraKeysManagerFacade;
    using LoRaTools;
    using LoRaTools.IoTHubImpl;
    using LoRaWan.Tests.Common;
    using Microsoft.Azure.Devices.Shared;
    using Microsoft.Extensions.Logging.Abstractions;
    using Moq;
    using Newtonsoft.Json;
    using StackExchange.Redis;
    using Xunit;
    using Xunit.Abstractions;

    [Collection(RedisFixture.CollectionName)]
#pragma warning disable xUnit1033 // False positive: Test classes decorated with 'Xunit.IClassFixture<TFixture>' or 'Xunit.ICollectionFixture<TFixture>' should add a constructor argument of type TFixture
    public class DevAddrCacheTest : FunctionTestBase, IClassFixture<RedisFixture>
#pragma warning restore xUnit1033 // False positive: Test classes decorated with 'Xunit.IClassFixture<TFixture>' or 'Xunit.ICollectionFixture<TFixture>' should add a constructor argument of type TFixture
    {
        private const string FullUpdateKey = "fullUpdateKey";
        private const string GlobalDevAddrUpdateKey = "globalUpdateKey";
        private const string CacheKeyPrefix = "devAddrTable:";

        private const string PrimaryKey = "ABCDEFGH1234567890";

        private readonly ILoRaDeviceCacheStore cache;
        private readonly ITestOutputHelper testOutputHelper;

        public DevAddrCacheTest(RedisFixture redis, ITestOutputHelper testOutputHelper)
        {
            if (redis is null) throw new ArgumentNullException(nameof(redis));
            this.cache = new LoRaDeviceCacheRedisStore(redis.Database);
            this.testOutputHelper = testOutputHelper;
        }

        private static Mock<IDeviceRegistryManager> InitRegistryManager(List<DevAddrCacheInfo> deviceIds)
        {
            var currentDevAddrContext = new List<DevAddrCacheInfo>();
            var currentDevices = deviceIds;
            var mockRegistryManager = new Mock<IDeviceRegistryManager>(MockBehavior.Strict);
            var hasMoreShouldReturn = true;

            var primaryKey = Convert.ToBase64String(Encoding.UTF8.GetBytes(PrimaryKey));
            mockRegistryManager
                .Setup(x => x.GetDevicePrimaryKeyAsync(It.IsAny<string>()))
                .ReturnsAsync((string _) => primaryKey);

            mockRegistryManager
                .Setup(x => x.GetTwinAsync(It.IsNotNull<string>(), It.IsAny<CancellationToken?>()))
                .ReturnsAsync((string deviceId, CancellationToken _) => new IoTHubLoRaDeviceTwin(new Twin(deviceId)));

            var numberOfDevices = deviceIds.Count;

            // mock Page Result
            var mockPageResult = new Mock<IRegistryPageResult<ILoRaDeviceTwin>>();

            // we only want to run hasmoreresult once
            mockPageResult
                .Setup(x => x.HasMoreResults)
                .Returns(() =>
                {
                    if (hasMoreShouldReturn)
                    {
                        hasMoreShouldReturn = false;
                        return true;
                    }

                    return false;
                });

            mockPageResult
                .Setup(x => x.GetNextPageAsync())
                .ReturnsAsync(() =>
                {
                    var devAddressesToConsider = currentDevAddrContext;
                    var twins = new List<ILoRaDeviceTwin>();
                    foreach (var devaddrItem in devAddressesToConsider)
                    {
                        var deviceTwin = new Twin
                        {
                            DeviceId = devaddrItem.DevEUI.Value.ToString(),
                            Properties = new TwinProperties()
                            {
                                Desired = new TwinCollection($"{{\"{TwinPropertiesConstants.DevAddr}\": \"{devaddrItem.DevAddr}\", \"{TwinPropertiesConstants.GatewayID}\": \"{devaddrItem.GatewayId}\"}}", $"{{\"$lastUpdated\": \"{devaddrItem.LastUpdatedTwins.ToString(Constants.RoundTripDateTimeStringFormat)}\"}}"),
                                Reported = new TwinCollection($"{{}}", $"{{\"$lastUpdated\": \"0001-01-01T00:00:00Z\"}}"),
                            }
                        };

                        twins.Add(new IoTHubLoRaDeviceTwin(deviceTwin));
                    }

                    return twins;
                });

            mockRegistryManager
                .Setup(x => x.FindLoRaDeviceByDevAddr(It.IsAny<DevAddr>()))
                .Returns((DevAddr someDevAddr) =>
                {
                    hasMoreShouldReturn = true;
                    currentDevAddrContext = currentDevices.Where(v => v.DevAddr == someDevAddr).ToList();
                    return mockPageResult.Object;
                });

            mockRegistryManager
                .Setup(x => x.GetAllLoRaDevices())
                .Returns(() =>
                {
                    hasMoreShouldReturn = true;
                    currentDevAddrContext = currentDevices;
                    return mockPageResult.Object;
                });

            mockRegistryManager
                .Setup(x => x.GetLastUpdatedLoRaDevices(It.IsAny<DateTime>()))
                .Returns((DateTime lastDeltaUpdate) =>
                {
                    currentDevAddrContext = currentDevices.Where(d => d.LastUpdatedTwins >= lastDeltaUpdate).ToList();
                    // reset device count in case HasMoreResult is called more than once
                    hasMoreShouldReturn = true;
                    return mockPageResult.Object;
                });
            return mockRegistryManager;
        }

        private static void InitCache(ILoRaDeviceCacheStore cache, List<DevAddrCacheInfo> deviceIds)
        {
            var loradevaddrcache = new LoRaDevAddrCache(cache, null, null);
            foreach (var device in deviceIds)
            {
                loradevaddrcache.StoreInfo(device);
            }
        }

        /// <summary>
        /// Ensure that the Locks get released if an exception pop.
        /// </summary>
        [Theory]
        [InlineData(null)]
        [InlineData(FullUpdateKey)]
        public async Task When_PerformNeededSyncs_Fails_Should_Release_Lock(string lockToTake)
        {
            var devAddrcache = new LoRaDevAddrCache(this.cache, null, null);
            await LockDevAddrHelper.PrepareLocksForTests(this.cache, lockToTake == null ? null : new[] { lockToTake });
            var managerInput = new List<DevAddrCacheInfo> { new DevAddrCacheInfo() { DevEUI = TestEui.GenerateDevEui(), DevAddr = CreateDevAddr() } };
            var registryManagerMock = InitRegistryManager(managerInput);
            registryManagerMock.Setup(x => x.GetLastUpdatedLoRaDevices(It.IsAny<DateTime>())).Throws(new RedisException(string.Empty));
            registryManagerMock.Setup(x => x.GetAllLoRaDevices()).Throws(new RedisException(string.Empty));
            await devAddrcache.PerformNeededSyncs(registryManagerMock.Object);

            // When doing a full update, the FullUpdateKey lock should be reset to 1min, the GlobalDevAddrUpdateKey should be gone
            // When doing a partial update, the GlobalDevAddrUpdateKey should be gone
            switch (lockToTake)
            {
                case FullUpdateKey:
                    Assert.Null(await this.cache.GetObjectTTL(GlobalDevAddrUpdateKey));
                    break;
                case null:
                    var nextFullUpdate = await this.cache.GetObjectTTL(FullUpdateKey);
                    Assert.True(nextFullUpdate <= TimeSpan.FromMinutes(1));
                    Assert.Null(await this.cache.GetObjectTTL(GlobalDevAddrUpdateKey));
                    break;
                default: throw new InvalidOperationException("invalid test case");
            }
        }

        [Fact]
        // This test simulate a new call from an unknow device. It checks that :
        // The server correctly query iot hub
        // Server saves answer in the Cache for future usage
        public async Task When_DevAddr_Is_Not_In_Cache_Query_Iot_Hub_And_Save_In_Cache()
        {
            var gatewayId = NewUniqueEUI64();
            var dateTime = DateTime.UtcNow;
            var managerInput = new List<DevAddrCacheInfo>();

            for (var i = 0; i < 2; i++)
            {
                managerInput.Add(new DevAddrCacheInfo()
                {
                    DevEUI = TestEui.GenerateDevEui(),
                    DevAddr = CreateDevAddr()
                });
            }

            var devAddrJoining = managerInput[0].DevAddr;
            var registryManagerMock = InitRegistryManager(managerInput);

            var items = new List<IoTHubDeviceInfo>();

            // In this test we want no updates running
            // initialize locks for test to run correctly
            var lockToTake = new string[2] { FullUpdateKey, GlobalDevAddrUpdateKey };
            await LockDevAddrHelper.PrepareLocksForTests(this.cache, lockToTake);

            var deviceGetter = SetupDeviceGetter(registryManagerMock.Object);
            items = await deviceGetter.GetDeviceList(null, gatewayId, new DevNonce(0xABCD), devAddrJoining);

            Assert.Single(items);
            // If a cache miss it should save it in the redisCache
            var devAddrcache = new LoRaDevAddrCache(this.cache, null, null);
            var queryResult = this.cache.GetHashObject(string.Concat(CacheKeyPrefix, devAddrJoining));
            Assert.Single(queryResult);
            var resultObject = JsonConvert.DeserializeObject<DevAddrCacheInfo>(queryResult[0].Value);
            Assert.Equal(managerInput[0].DevAddr, resultObject.DevAddr);
            Assert.Equal(managerInput[0].GatewayId ?? string.Empty, resultObject.GatewayId);
            Assert.Equal(managerInput[0].DevEUI, resultObject.DevEUI);

            registryManagerMock.Verify(x => x.FindLoRaDeviceByDevAddr(It.IsAny<DevAddr>()), Times.Once);
            registryManagerMock.Verify(x => x.GetAllLoRaDevices(), Times.Never);
            registryManagerMock.Verify(x => x.GetLastUpdatedLoRaDevices(It.IsAny<DateTime>()), Times.Never);
            registryManagerMock.Verify(x => x.GetDevicePrimaryKeyAsync(It.IsAny<string>()), Times.Once);
        }

        [Fact]
        // This test simulate a call received by multiple server. It ensures IoT Hub is only queried once.
        public async Task Multi_Gateway_When_DevAddr_Is_Not_In_Cache_Query_Iot_Hub_Only_Once_And_Save_In_Cache()
        {
            var gateway1 = NewUniqueEUI64();
            var gateway2 = NewUniqueEUI64();
            var dateTime = DateTime.UtcNow;
            var managerInput = new List<DevAddrCacheInfo>();

            for (var i = 0; i < 2; i++)
            {
                managerInput.Add(new DevAddrCacheInfo()
                {
                    DevEUI = TestEui.GenerateDevEui(),
                    DevAddr = CreateDevAddr()
                });
            }

            var devAddrJoining = managerInput[0].DevAddr;
            var registryManagerMock = InitRegistryManager(managerInput);

            // In this test we want no updates running
            // initialize locks for test to run correctly
            var lockToTake = new string[2] { FullUpdateKey, GlobalDevAddrUpdateKey };
            await LockDevAddrHelper.PrepareLocksForTests(this.cache, lockToTake);

            var deviceGetter = SetupDeviceGetter(registryManagerMock.Object);
            // Simulate three queries
            var tasks =
                from gw in new[] { gateway1, gateway2 }
                select Enumerable.Repeat(gw, 2) into gws // repeat each gateway twice
                from gw in gws
                select deviceGetter.GetDeviceList(null, gw, new DevNonce(0xABCD), devAddrJoining);

            await Task.WhenAll(tasks);

            // If a cache miss it should save it in the redisCache
            var devAddrcache = new LoRaDevAddrCache(this.cache, null, null);
            var queryResult = this.cache.GetHashObject(string.Concat(CacheKeyPrefix, devAddrJoining));
            Assert.Single(queryResult);
            var resultObject = JsonConvert.DeserializeObject<DevAddrCacheInfo>(queryResult[0].Value);
            Assert.Equal(managerInput[0].DevAddr, resultObject.DevAddr);
            Assert.Equal(managerInput[0].GatewayId ?? string.Empty, resultObject.GatewayId);
            Assert.Equal(managerInput[0].DevEUI, resultObject.DevEUI);

            registryManagerMock.Verify(x => x.FindLoRaDeviceByDevAddr(It.IsAny<DevAddr>()), Times.Once);
            registryManagerMock.Verify(x => x.GetAllLoRaDevices(), Times.Never);
            registryManagerMock.Verify(x => x.GetLastUpdatedLoRaDevices(It.IsAny<DateTime>()), Times.Never);
            registryManagerMock.Verify(x => x.GetDevicePrimaryKeyAsync(It.IsAny<string>()), Times.Once);
        }

        [Fact]
        // This test ensure that if a device is in cache without a key, it get the keys from iot hub and saave it
        public async Task When_DevAddr_Is_In_Cache_Without_Key_Should_Not_Query_Iot_Hub_For_Twin_But_Should_Get_Key_And_Update()
        {
            var gatewayId = NewUniqueEUI64();
            var dateTime = DateTime.UtcNow;
            var managerInput = new List<DevAddrCacheInfo>();
            for (var i = 0; i < 2; i++)
            {
                managerInput.Add(new DevAddrCacheInfo()
                {
                    DevEUI = TestEui.GenerateDevEui(),
                    DevAddr = CreateDevAddr(),
                    GatewayId = gatewayId,
                    LastUpdatedTwins = dateTime
                });
            }

            var devAddrJoining = managerInput[0].DevAddr;
            InitCache(this.cache, managerInput);
            var registryManagerMock = InitRegistryManager(managerInput);
            var items = new List<IoTHubDeviceInfo>();

            // In this test we want no updates running
            // initialize locks for test to run correctly
            var lockToTake = new string[2] { FullUpdateKey, GlobalDevAddrUpdateKey };
            await LockDevAddrHelper.PrepareLocksForTests(this.cache, lockToTake);

            var deviceGetter = SetupDeviceGetter(registryManagerMock.Object);
            items = await deviceGetter.GetDeviceList(null, gatewayId, new DevNonce(0xABCD), devAddrJoining);

            Assert.Single(items);
            var queryResult = this.cache.GetHashObject(string.Concat(CacheKeyPrefix, devAddrJoining));
            Assert.Single(queryResult);
            // The key should have been saved
            var resultObject = JsonConvert.DeserializeObject<DevAddrCacheInfo>(queryResult[0].Value);
            Assert.NotNull(resultObject.PrimaryKey);

            // Iot hub should never have been called.
            registryManagerMock.Verify(x => x.GetLastUpdatedLoRaDevices(It.IsAny<DateTime>()), Times.Never, "IoT Hub should not have been called, as the device was present in the cache.");
            registryManagerMock.Verify(x => x.GetAllLoRaDevices(), Times.Never, "IoT Hub should not have been called, as the device was present in the cache.");
            registryManagerMock.Verify(x => x.GetTwinAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never, "IoT Hub should not have been called, as the device was present in the cache.");
            // Should query for the key as key is missing
            registryManagerMock.Verify(x => x.GetDevicePrimaryKeyAsync(It.IsAny<string>()), Times.Once);
        }

        [Fact]
        // This test ensure that if a device is in cache without a key, it get the keys from iot hub and save it
        public async Task Multi_Gateway_When_DevAddr_Is_In_Cache_Without_Key_Should_Not_Query_Iot_Hub_For_Twin_But_Should_Get_Key_And_Update()
        {
            var gatewayId = NewUniqueEUI64();
            var dateTime = DateTime.UtcNow;
            var managerInput = new List<DevAddrCacheInfo>();
            for (var i = 0; i < 2; i++)
            {
                managerInput.Add(new DevAddrCacheInfo()
                {
                    DevEUI = TestEui.GenerateDevEui(),
                    DevAddr = CreateDevAddr(),
                    GatewayId = gatewayId,
                    LastUpdatedTwins = dateTime
                });
            }

            var devAddrJoining = managerInput[0].DevAddr;
            InitCache(this.cache, managerInput);
            var registryManagerMock = InitRegistryManager(managerInput);

            // In this test we want no updates running
            // initialize locks for test to run correctly
            var lockToTake = new string[2] { FullUpdateKey, GlobalDevAddrUpdateKey };
            await LockDevAddrHelper.PrepareLocksForTests(this.cache, lockToTake);

            var deviceGetter = SetupDeviceGetter(registryManagerMock.Object);
            var tasks =
                from gw in Enumerable.Repeat(gatewayId, 3)
                select deviceGetter.GetDeviceList(null, gw, new DevNonce(0xABCD), devAddrJoining);

            await Task.WhenAll(tasks);
            // Iot hub should never have been called.
            registryManagerMock.Verify(x => x.GetLastUpdatedLoRaDevices(It.IsAny<DateTime>()), Times.Never, "IoT Hub should not have been called, as the device was present in the cache.");
            registryManagerMock.Verify(x => x.GetAllLoRaDevices(), Times.Never, "IoT Hub should not have been called, as the device was present in the cache.");
            registryManagerMock.Verify(x => x.GetTwinAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never, "IoT Hub should not have been called, as the device was present in the cache.");
            // Should query for the key as key is missing
            registryManagerMock.Verify(x => x.GetDevicePrimaryKeyAsync(It.IsAny<string>()), Times.Once);
            var queryResult = this.cache.GetHashObject(string.Concat(CacheKeyPrefix, devAddrJoining));
            Assert.Single(queryResult);
            // The key should have been saved
            var resultObject = JsonConvert.DeserializeObject<DevAddrCacheInfo>(queryResult[0].Value);
            Assert.NotNull(resultObject.PrimaryKey);
        }

        [Fact]
        // This test ensure that if the device has the key within the cache, it should not make any query to iot hub
        public async Task When_DevAddr_Is_In_Cache_With_Key_Should_Not_Query_Iot_Hub_For_Twin_At_All()
        {
            var gatewayId = NewUniqueEUI64();
            var dateTime = DateTime.UtcNow;
            var primaryKey = Convert.ToBase64String(Encoding.UTF8.GetBytes(PrimaryKey));
            var managerInput = new List<DevAddrCacheInfo>();
            for (var i = 0; i < 2; i++)
            {
                managerInput.Add(new DevAddrCacheInfo()
                {
                    DevEUI = TestEui.GenerateDevEui(),
                    DevAddr = CreateDevAddr(),
                    GatewayId = gatewayId,
                    PrimaryKey = primaryKey,
                    LastUpdatedTwins = dateTime
                });
            }

            var devAddrJoining = managerInput[0].DevAddr;
            InitCache(this.cache, managerInput);
            var registryManagerMock = InitRegistryManager(managerInput);

            var items = new List<IoTHubDeviceInfo>();
            // In this test we want no updates running
            // initialize locks for test to run correctly
            var lockToTake = new string[2] { FullUpdateKey, GlobalDevAddrUpdateKey };
            await LockDevAddrHelper.PrepareLocksForTests(this.cache, lockToTake);

            var deviceGetter = SetupDeviceGetter(registryManagerMock.Object);
            items = await deviceGetter.GetDeviceList(null, gatewayId, new DevNonce(0xABCD), devAddrJoining);

            Assert.Single(items);
            // Iot hub should never have been called.
            registryManagerMock.Verify(x => x.GetLastUpdatedLoRaDevices(It.IsAny<DateTime>()), Times.Never, "IoT Hub should not have been called, as the device was present in the cache.");
            registryManagerMock.Verify(x => x.GetAllLoRaDevices(), Times.Never, "IoT Hub should not have been called, as the device was present in the cache.");
            registryManagerMock.Verify(x => x.GetTwinAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never, "IoT Hub should not have been called, as the device was present in the cache.");
            // Should not query for the key as key is there
            registryManagerMock.Verify(x => x.GetDevicePrimaryKeyAsync(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        // This test ensure that if the device has the key within the cache, it should not make any query to iot hub
        public async Task When_Device_Is_Not_Ours_Save_In_Cache_And_Dont_Query_Hub_Again()
        {
            var gatewayId = NewUniqueEUI64();
            var dateTime = DateTime.UtcNow;
            // In this test we want no updates running
            // initialize locks for test to run correctly
            var lockToTake = new string[2] { FullUpdateKey, GlobalDevAddrUpdateKey };
            await LockDevAddrHelper.PrepareLocksForTests(this.cache, lockToTake);

            var items = new List<IoTHubDeviceInfo>();
            var primaryKey = Convert.ToBase64String(Encoding.UTF8.GetBytes(PrimaryKey));
            var managerInput = new List<DevAddrCacheInfo>();
            for (var i = 0; i < 2; i++)
            {
                managerInput.Add(new DevAddrCacheInfo()
                {
                    DevEUI = TestEui.GenerateDevEui(),
                    DevAddr = CreateDevAddr(),
                    GatewayId = gatewayId,
                    PrimaryKey = primaryKey,
                    LastUpdatedTwins = dateTime
                });
            }

            var devAddrJoining = CreateDevAddr();
            InitCache(this.cache, managerInput);
            var registryManagerMock = InitRegistryManager(managerInput);

            var deviceGetter = new DeviceGetter(registryManagerMock.Object, this.cache, NullLogger<DeviceGetter>.Instance);
            items = await deviceGetter.GetDeviceList(null, gatewayId, new DevNonce(0xABCD), devAddrJoining);

            Assert.Empty(items);
            var queryResult = this.cache.GetHashObject(string.Concat(CacheKeyPrefix, devAddrJoining));
            Assert.Single(queryResult);
            var resultObject = JsonConvert.DeserializeObject<DevAddrCacheInfo>(queryResult[0].Value);
            Assert.Null(resultObject.DevEUI);
            Assert.Null(resultObject.PrimaryKey);
            Assert.Null(resultObject.GatewayId);
            var query2Result = this.cache.GetHashObject(string.Concat(CacheKeyPrefix, devAddrJoining));
            Assert.Single(query2Result);

            // Iot hub should never have been called.
            registryManagerMock.Verify(x => x.GetLastUpdatedLoRaDevices(It.IsAny<DateTime>()), Times.Never, "IoT Hub should not have been called, as the device was present in the cache.");
            registryManagerMock.Verify(x => x.GetAllLoRaDevices(), Times.Never, "IoT Hub should not have been called, as the device was present in the cache.");
            registryManagerMock.Verify(x => x.GetTwinAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never, "IoT Hub should not have been called, as the device was present in the cache.");
            // Should not query for the key as key is there
            registryManagerMock.Verify(x => x.GetDevicePrimaryKeyAsync(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        // Check that the server perform a full reload if the locking key for full reload is not present
        public async Task When_FullUpdateKey_Is_Not_there_Should_Perform_Full_Reload()
        {
            var gatewayId = NewUniqueEUI64();
            var dateTime = DateTime.UtcNow;
            var primaryKey = Convert.ToBase64String(Encoding.UTF8.GetBytes(PrimaryKey));
            var managerInput = new List<DevAddrCacheInfo>();
            for (var i = 0; i < 5; i++)
            {
                managerInput.Add(new DevAddrCacheInfo()
                {
                    DevEUI = TestEui.GenerateDevEui(),
                    DevAddr = CreateDevAddr(),
                    GatewayId = gatewayId,
                });
            }

            var devAddrJoining = managerInput[0].DevAddr;
            // The cache start as empty
            var registryManagerMock = InitRegistryManager(managerInput);

            // initialize locks for test to run correctly
            await LockDevAddrHelper.PrepareLocksForTests(this.cache);

            var items = new List<IoTHubDeviceInfo>();

            var deviceGetter = SetupDeviceGetter(registryManagerMock.Object);
            items = await deviceGetter.GetDeviceList(null, gatewayId, new DevNonce(0xABCD), devAddrJoining);

            Assert.Single(items);
            registryManagerMock.Verify(x => x.GetAllLoRaDevices(), Times.Once);
            registryManagerMock.Verify(x => x.GetLastUpdatedLoRaDevices(It.IsAny<DateTime>()), Times.Never);
            registryManagerMock.Verify(x => x.GetTwinAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
            // We expect to query for the key once (the device with an active connection)
            registryManagerMock.Verify(x => x.GetDevicePrimaryKeyAsync(It.IsAny<string>()), Times.Once);

            // we expect the devices are saved
            for (var i = 1; i < 5; i++)
            {
                var queryResult = this.cache.GetHashObject(string.Concat(CacheKeyPrefix, managerInput[i].DevAddr));
                Assert.Single(queryResult);
                var resultObject = JsonConvert.DeserializeObject<DevAddrCacheInfo>(queryResult[0].Value);
                Assert.Equal(managerInput[i].GatewayId, resultObject.GatewayId);
                Assert.Equal(managerInput[i].DevEUI, resultObject.DevEUI);
            }
        }

        [Fact]
        // Trigger delta update correctly to see if it performs correctly on an empty cache
        public async Task Delta_Update_Perform_Correctly_On_Empty_Cache()
        {
            var gatewayId = NewUniqueEUI64();
            var dateTime = DateTime.UtcNow;

            var managerInput = new List<DevAddrCacheInfo>();
            for (var i = 0; i < 5; i++)
            {
                managerInput.Add(new DevAddrCacheInfo()
                {
                    DevEUI = TestEui.GenerateDevEui(),
                    DevAddr = CreateDevAddr(),
                    GatewayId = gatewayId,
                    LastUpdatedTwins = dateTime.AddMinutes((float)-i * 40) // on empty cache, only updates from last hour are processed, therefore out of 5 device only 2 will be added with this computation
                });
            }

            var devAddrJoining = managerInput[0].DevAddr;
            // The cache start as empty
            var registryManagerMock = InitRegistryManager(managerInput);

            // initialize locks for test to run correctly
            var locksToTake = new string[1] { FullUpdateKey };
            await LockDevAddrHelper.PrepareLocksForTests(this.cache, locksToTake);

            var devAddrCache = new LoRaDevAddrCache(this.cache, null, gatewayId);
            await devAddrCache.PerformNeededSyncs(registryManagerMock.Object);

            while (!string.IsNullOrEmpty(this.cache.StringGet(GlobalDevAddrUpdateKey)))
            {
                await Task.Delay(100);
            }

            var foundItem = 0;
            // we expect the devices are saved
            for (var i = 0; i < 5; i++)
            {
                var queryResult = this.cache.GetHashObject(string.Concat(CacheKeyPrefix, managerInput[i].DevAddr));
                if (queryResult.Length > 0)
                {
                    foundItem++;
                    Assert.Single(queryResult);
                    var resultObject = JsonConvert.DeserializeObject<DevAddrCacheInfo>(queryResult[0].Value);
                    Assert.Equal(managerInput[i].GatewayId, resultObject.GatewayId);
                    Assert.Equal(managerInput[i].DevEUI, resultObject.DevEUI);
                }
            }

            // Only two items should be updated by the delta updates
            Assert.Equal(2, foundItem);

            registryManagerMock.Verify(x => x.GetAllLoRaDevices(), Times.Never);
            registryManagerMock.Verify(x => x.GetLastUpdatedLoRaDevices(It.IsAny<DateTime>()), Times.Once);
            // Iot hub should never have been called.
            registryManagerMock.Verify(x => x.GetTwinAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
            // We expect to query for the key once (the device with an active connection)
            registryManagerMock.Verify(x => x.GetDevicePrimaryKeyAsync(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        // This test perform a delta update and we check the following
        // primary key present in the cache is still here after a delta up
        // Items with save Devaddr are correctly saved (one old from cache, one from iot hub)
        // Gateway Id is correctly updated in old cache information.
        // Primary Key are kept as UpdateTime is similar
        public async Task Delta_Update_Perform_Correctly_On_Non_Empty_Cache_And_Keep_Old_Values()
        {
            var oldGatewayId = NewUniqueEUI64();
            var newGatewayId = NewUniqueEUI64();
            var dateTime = DateTime.UtcNow;
            var primaryKey = Convert.ToBase64String(Encoding.UTF8.GetBytes(PrimaryKey));
            var managerInput = new List<DevAddrCacheInfo>();

            var adressForDuplicateDevAddr = CreateDevAddr();
            for (var i = 0; i < 5; i++)
            {
                managerInput.Add(new DevAddrCacheInfo()
                {
                    DevEUI = TestEui.GenerateDevEui(),
                    DevAddr = CreateDevAddr(),
                    GatewayId = newGatewayId,
                    LastUpdatedTwins = dateTime
                });
            }

            managerInput.Add(new DevAddrCacheInfo()
            {
                DevEUI = TestEui.GenerateDevEui(),
                DevAddr = adressForDuplicateDevAddr,
                GatewayId = newGatewayId,
                LastUpdatedTwins = dateTime
            });

            var devAddrJoining = managerInput[0].DevAddr;
            // The cache start as empty
            var registryManagerMock = InitRegistryManager(managerInput);

            // Set up the cache with expectation.
            var cacheInput = new List<DevAddrCacheInfo>();
            for (var i = 0; i < 5; i++)
            {
                cacheInput.Add(new DevAddrCacheInfo()
                {
                    DevEUI = managerInput[i].DevEUI,
                    DevAddr = managerInput[i].DevAddr,
                    GatewayId = oldGatewayId,
                    LastUpdatedTwins = dateTime
                });
            }

            cacheInput[2].PrimaryKey = primaryKey;
            cacheInput[3].PrimaryKey = primaryKey;

            var devEui = TestEui.GenerateDevEui();
            cacheInput.Add(new DevAddrCacheInfo()
            {
                DevEUI = devEui,
                DevAddr = adressForDuplicateDevAddr,
                GatewayId = oldGatewayId,
                PrimaryKey = primaryKey,
                LastUpdatedTwins = dateTime
            });
            InitCache(this.cache, cacheInput);

            // initialize locks for test to run correctly
            var locksToTake = new string[1] { FullUpdateKey };
            await LockDevAddrHelper.PrepareLocksForTests(this.cache, locksToTake);

            var devAddrCache = new LoRaDevAddrCache(this.cache, null, newGatewayId);
            await devAddrCache.PerformNeededSyncs(registryManagerMock.Object);

            // we expect the devices are saved
            for (var i = 0; i < managerInput.Count; i++)
            {
                if (managerInput[i].DevAddr != adressForDuplicateDevAddr)
                {
                    var queryResult = this.cache.GetHashObject(string.Concat(CacheKeyPrefix, managerInput[i].DevAddr));
                    Assert.Single(queryResult);
                    var resultObject = JsonConvert.DeserializeObject<DevAddrCacheInfo>(queryResult[0].Value);
                    Assert.Equal(managerInput[i].GatewayId, resultObject.GatewayId);
                    Assert.Equal(managerInput[i].DevEUI, resultObject.DevEUI);
                    Assert.Equal(cacheInput[i].PrimaryKey, resultObject.PrimaryKey);
                }
            }

            // let's check the devices with a double EUI
            var query2Result = this.cache.GetHashObject(string.Concat(CacheKeyPrefix, adressForDuplicateDevAddr));
            Assert.Equal(2, query2Result.Length);
            for (var i = 0; i < 2; i++)
            {
                var resultObject = JsonConvert.DeserializeObject<DevAddrCacheInfo>(query2Result[0].Value);
                if (resultObject.DevEUI == devEui)
                {
                    Assert.Equal(oldGatewayId, resultObject.GatewayId);
                    Assert.Equal(primaryKey, resultObject.PrimaryKey);
                }
                else
                {
                    Assert.Equal(newGatewayId, resultObject.GatewayId);
                    Assert.True(string.IsNullOrEmpty(resultObject.PrimaryKey));
                }
            }

            registryManagerMock.Verify(x => x.GetAllLoRaDevices(), Times.Never);
            registryManagerMock.Verify(x => x.GetLastUpdatedLoRaDevices(It.IsAny<DateTime>()), Times.Once);

            // Iot hub should never have been called.
            registryManagerMock.Verify(x => x.GetTwinAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
            // We expect to query for the key once (the device with an active connection)
            registryManagerMock.Verify(x => x.GetDevicePrimaryKeyAsync(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        // This test perform a delta update and we check the following
        // primary key present in the cache is still here after a delta up
        // Items with save Devaddr are correctly saved (one old from cache, one from iot hub)
        // Gateway Id is correctly updated in old cache information.
        // Primary Key are dropped as updatetime is defferent
        public async Task Delta_Update_Perform_Correctly_On_Non_Empty_Cache_And_Keep_Old_Values_Except_Primary_Key()
        {
            var oldGatewayId = NewUniqueEUI64();
            var newGatewayId = NewUniqueEUI64();
            var dateTime = DateTime.UtcNow.AddMinutes(-10);
            var updateDateTime = DateTime.UtcNow;

            var primaryKey = Convert.ToBase64String(Encoding.UTF8.GetBytes(PrimaryKey));
            var managerInput = new List<DevAddrCacheInfo>();

            for (var i = 0; i < 5; i++)
            {
                managerInput.Add(new DevAddrCacheInfo()
                {
                    DevEUI = TestEui.GenerateDevEui(),
                    DevAddr = CreateDevAddr(),
                    GatewayId = newGatewayId,
                    LastUpdatedTwins = updateDateTime
                });
            }

            var registryManagerMock = InitRegistryManager(managerInput);

            // Set up the cache with expectation.
            var cacheInput = new List<DevAddrCacheInfo>();
            for (var i = 0; i < 5; i++)
            {
                cacheInput.Add(new DevAddrCacheInfo()
                {
                    DevEUI = managerInput[i].DevEUI,
                    DevAddr = managerInput[i].DevAddr,
                    LastUpdatedTwins = dateTime,
                    PrimaryKey = primaryKey,
                    GatewayId = oldGatewayId
                });
            }

            InitCache(this.cache, cacheInput);
            // initialize locks for test to run correctly
            var locksToTake = new string[1] { FullUpdateKey };
            await LockDevAddrHelper.PrepareLocksForTests(this.cache, locksToTake);

            var devAddrCache = new LoRaDevAddrCache(this.cache, null, newGatewayId);
            await devAddrCache.PerformNeededSyncs(registryManagerMock.Object);

            // we expect the devices are saved
            for (var i = 0; i < managerInput.Count; i++)
            {
                var queryResult = this.cache.GetHashObject(string.Concat(CacheKeyPrefix, managerInput[i].DevAddr));
                Assert.Single(queryResult);
                var resultObject = JsonConvert.DeserializeObject<DevAddrCacheInfo>(queryResult[0].Value);
                Assert.Equal(managerInput[i].GatewayId, resultObject.GatewayId);
                Assert.Equal(managerInput[i].DevEUI, resultObject.DevEUI);
                // as the object changed the keys should not be saved
                Assert.Equal(string.Empty, resultObject.PrimaryKey);
            }

            registryManagerMock.Verify(x => x.GetAllLoRaDevices(), Times.Never);
            registryManagerMock.Verify(x => x.GetLastUpdatedLoRaDevices(It.IsAny<DateTime>()), Times.Once);

            // Iot hub should never have been called.
            registryManagerMock.Verify(x => x.GetTwinAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
            // We expect to query for the key once (the device with an active connection)
            registryManagerMock.Verify(x => x.GetDevicePrimaryKeyAsync(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        // This test perform a full update and we check the following
        // primary key present in the cache is still here after a fullupdate
        // Items with same Devaddr are correctly saved (one old from cache, one from iot hub)
        // Old cache items sharing a devaddr not in the new update are correctly removed
        // Items with a devAddr not in the update are correctly still in cache
        // Gateway Id is correctly updated in old cache information.
        // Primary Key are kept as UpdateTime is similar
        public async Task Full_Update_Perform_Correctly_On_Non_Empty_Cache_And_Keep_Old_Values()
        {
            var oldGatewayId = NewUniqueEUI64();
            var newGatewayId = NewUniqueEUI64();
            var dateTime = DateTime.UtcNow;
            var primaryKey = Convert.ToBase64String(Encoding.UTF8.GetBytes(PrimaryKey));
            var newValues = new List<DevAddrCacheInfo>();

            var adressForDuplicateDevAddr = CreateDevAddr();
            for (var i = 0; i < 5; i++)
            {
                newValues.Add(new DevAddrCacheInfo()
                {
                    DevEUI = TestEui.GenerateDevEui(),
                    DevAddr = CreateDevAddr(),
                    GatewayId = newGatewayId,
                    LastUpdatedTwins = dateTime
                });
            }

            newValues.Add(new DevAddrCacheInfo()
            {
                DevEUI = TestEui.GenerateDevEui(),
                DevAddr = adressForDuplicateDevAddr,
                GatewayId = newGatewayId,
                LastUpdatedTwins = dateTime
            });

            var devAddrJoining = newValues[0].DevAddr;
            // The cache start as empty
            var registryManagerMock = InitRegistryManager(newValues);

            // Set up the cache with expectation.
            var cacheInput = new List<DevAddrCacheInfo>();
            for (var i = 0; i < 5; i++)
            {
                cacheInput.Add(new DevAddrCacheInfo()
                {
                    DevEUI = newValues[i].DevEUI,
                    DevAddr = newValues[i].DevAddr,
                    GatewayId = oldGatewayId,
                    LastUpdatedTwins = dateTime
                });
            }

            cacheInput[2].PrimaryKey = primaryKey;
            cacheInput[3].PrimaryKey = primaryKey;

            // this is a device that will be overwritten by the update as it share a devaddr with an updated device
            var devEuiDoubleItem = TestEui.GenerateDevEui();

            cacheInput.Add(new DevAddrCacheInfo()
            {
                DevEUI = devEuiDoubleItem,
                DevAddr = adressForDuplicateDevAddr,
                GatewayId = oldGatewayId,
                PrimaryKey = primaryKey,
                LastUpdatedTwins = dateTime
            });

            InitCache(this.cache, cacheInput);

            // initialize locks for test to run correctly
            await LockDevAddrHelper.PrepareLocksForTests(this.cache);

            var devAddrCache = new LoRaDevAddrCache(this.cache, null, newGatewayId);
            await devAddrCache.PerformNeededSyncs(registryManagerMock.Object);

            // we expect the devices are saved, the double device id should not be there anymore
            for (var i = 0; i < newValues.Count; i++)
            {
                var queryResult = this.cache.GetHashObject(string.Concat(CacheKeyPrefix, newValues[i].DevAddr));
                Assert.Single(queryResult);
                var result2Object = JsonConvert.DeserializeObject<DevAddrCacheInfo>(queryResult[0].Value);
                Assert.Equal(newGatewayId, result2Object.GatewayId);
                Assert.Equal(newValues[i].DevEUI, result2Object.DevEUI);
                if (newValues[i].DevEUI == devEuiDoubleItem)
                {
                    Assert.Equal(cacheInput[i].PrimaryKey, result2Object.PrimaryKey);
                }
            }

            registryManagerMock.Verify(x => x.GetLastUpdatedLoRaDevices(It.IsAny<DateTime>()), Times.Never);
            registryManagerMock.Verify(x => x.GetAllLoRaDevices(), Times.Once);

            // Iot hub should never have been called.
            registryManagerMock.Verify(x => x.GetTwinAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
            // We expect to query for the key once (the device with an active connection)
            registryManagerMock.Verify(x => x.GetDevicePrimaryKeyAsync(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        // This test perform a full update and we check the following
        // primary key present in the cache is still here after a fullupdate
        // Items with same Devaddr are correctly saved (one old from cache, one from iot hub)
        // Old cache items sharing a devaddr not in the new update are correctly removed
        // Items with a devAddr not in the update are correctly still in cache
        // Gateway Id is correctly updated in old cache information.
        // Primary Key are not kept as UpdateTime is not similar
        public async Task Full_Update_Perform_Correctly_On_Non_Empty_Cache_And_Keep_Old_Values_Except_Primary_Keys()
        {
            var oldGatewayId = NewUniqueEUI64();
            var newGatewayId = NewUniqueEUI64();
            var dateTime = DateTime.UtcNow;
            var updateDateTime = DateTime.UtcNow.AddMinutes(3);
            var primaryKey = Convert.ToBase64String(Encoding.UTF8.GetBytes(PrimaryKey));
            var newValues = new List<DevAddrCacheInfo>();

            for (var i = 0; i < 5; i++)
            {
                newValues.Add(new DevAddrCacheInfo()
                {
                    DevEUI = TestEui.GenerateDevEui(),
                    DevAddr = CreateDevAddr(),
                    GatewayId = newGatewayId,
                    LastUpdatedTwins = updateDateTime
                });
            }

            // The cache start as empty
            var registryManagerMock = InitRegistryManager(newValues);

            // Set up the cache with expectation.
            var cacheInput = new List<DevAddrCacheInfo>();
            for (var i = 0; i < 5; i++)
            {
                cacheInput.Add(new DevAddrCacheInfo()
                {
                    DevEUI = newValues[i].DevEUI,
                    DevAddr = newValues[i].DevAddr,
                    GatewayId = oldGatewayId,
                    LastUpdatedTwins = dateTime,
                    PrimaryKey = primaryKey
                });
            }

            InitCache(this.cache, cacheInput);

            // initialize locks for test to run correctly
            await LockDevAddrHelper.PrepareLocksForTests(this.cache);

            var devAddrCache = new LoRaDevAddrCache(this.cache, null, newGatewayId);
            await devAddrCache.PerformNeededSyncs(registryManagerMock.Object);

            // we expect the devices are saved, the double device id should not be there anymore
            for (var i = 0; i < newValues.Count; i++)
            {
                var queryResult = this.cache.GetHashObject(string.Concat(CacheKeyPrefix, newValues[i].DevAddr));
                Assert.Single(queryResult);
                var result2Object = JsonConvert.DeserializeObject<DevAddrCacheInfo>(queryResult[0].Value);
                Assert.Equal(newGatewayId, result2Object.GatewayId);
                Assert.Equal(newValues[i].DevEUI, result2Object.DevEUI);
                Assert.Equal(string.Empty, result2Object.PrimaryKey);
            }

            registryManagerMock.Verify(x => x.GetLastUpdatedLoRaDevices(It.IsAny<DateTime>()), Times.Never);
            registryManagerMock.Verify(x => x.GetAllLoRaDevices(), Times.Once);

            // Iot hub should never have been called.
            registryManagerMock.Verify(x => x.GetTwinAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
            // We expect to query for the key once (the device with an active connection)
            registryManagerMock.Verify(x => x.GetDevicePrimaryKeyAsync(It.IsAny<string>()), Times.Never);
        }

        private static DevAddr CreateDevAddr() => new DevAddr((uint)RandomNumberGenerator.GetInt32(int.MaxValue));

        private DeviceGetter SetupDeviceGetter(IDeviceRegistryManager registryManager) =>
            new DeviceGetter(registryManager, this.cache, new TestOutputLogger<DeviceGetter>(this.testOutputHelper));
    }
}
