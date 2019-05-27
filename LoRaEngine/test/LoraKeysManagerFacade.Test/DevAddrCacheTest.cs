// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoraKeysManagerFacade.Test
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net.Http;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using LoRaWan.Test.Shared;
    using Microsoft.Azure.Devices;
    using Microsoft.Azure.Devices.Shared;
    using Moq;
    using Newtonsoft.Json;
    using Xunit;

    public class DevAddrCacheTest : FunctionTestBase, IClassFixture<RedisFixture>
    {
        private const string FullUpdateKey = "fullUpdateKey";
        private const string GlobalDevAddrUpdateKey = "globalUpdateKey";
        private const string DeltaUpdateKey = "deltaUpdateKey";
        private const string CacheKeyPrefix = "devAddrTable:";

        private const string PrimaryKey = "ABCDEFGH1234567890";

        private readonly ILoRaDeviceCacheStore cache;

        public DevAddrCacheTest(RedisFixture redis)
        {
            this.cache = new LoRaDeviceCacheRedisStore(redis.Database);
        }

        private Mock<RegistryManager> InitRegistryManager(List<DevAddrCacheInfo> deviceIds, int numberOfDeviceDeltaUpdates = 2)
        {
            List<DevAddrCacheInfo> currentDevAddrContext = new List<DevAddrCacheInfo>();
            List<DevAddrCacheInfo> currentDevices = deviceIds;
            var mockRegistryManager = new Mock<RegistryManager>(MockBehavior.Strict);
            bool hasMoreShouldReturn = true;

            var primaryKey = Convert.ToBase64String(Encoding.UTF8.GetBytes(PrimaryKey));
            mockRegistryManager
                .Setup(x => x.GetDeviceAsync(It.IsAny<string>()))
                .ReturnsAsync((string deviceId) => new Device(deviceId) { Authentication = new AuthenticationMechanism() { SymmetricKey = new SymmetricKey() { PrimaryKey = primaryKey } } });

            mockRegistryManager
                .Setup(x => x.GetTwinAsync(It.IsNotNull<string>()))
                .ReturnsAsync((string deviceId) => new Twin(deviceId));

            int numberOfDevices = deviceIds.Count;

            // CacheMiss query
            var cacheMissQueryMock = new Mock<IQuery>(MockBehavior.Strict);

            // we only want to run hasmoreresult once
            cacheMissQueryMock
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

            cacheMissQueryMock
                .Setup(x => x.GetNextAsTwinAsync())
                .ReturnsAsync(() =>
                {
                    var devAddressesToConsider = currentDevAddrContext;
                    List<Twin> twins = new List<Twin>();
                    foreach (var devaddrItem in devAddressesToConsider)
                    {
                        var deviceTwin = new Twin();
                        deviceTwin.DeviceId = devaddrItem.DevEUI;
                        deviceTwin.Properties = new TwinProperties()
                        {
                            Desired = new TwinCollection($"{{\"DevAddr\": \"{devaddrItem.DevAddr}\", \"GatewayId\": \"{devaddrItem.GatewayId}\"}}", $"{{\"$lastUpdated\": \"{devaddrItem.LastUpdatedTwins}\"}}"),
                        };

                        twins.Add(deviceTwin);
                    }
                    return twins;
                });

            mockRegistryManager
                .Setup(x => x.CreateQuery(It.Is<string>(z => z.Contains("SELECT * FROM devices WHERE properties.desired.DevAddr =")), 100))
                .Returns((string query, int pageSize) =>
                {
                    hasMoreShouldReturn = true;
                    currentDevAddrContext = currentDevices.Where(v => v.DevAddr == query.Split('\'')[1]).ToList();
                    return cacheMissQueryMock.Object;
                });

            mockRegistryManager
                .Setup(x => x.CreateQuery(It.Is<string>(z => z.Contains("SELECT * FROM devices WHERE is_defined(properties.desired.AppKey) "))))
                .Returns((string query) =>
                {
                    hasMoreShouldReturn = true;
                    currentDevAddrContext = currentDevices;
                    return cacheMissQueryMock.Object;
                });

            mockRegistryManager
                .Setup(x => x.CreateQuery(It.Is<string>(z => z.Contains("SELECT * FROM c where properties.desired.$metadata.$lastUpdated >="))))
                .Returns((string query) =>
                {
                    currentDevAddrContext = currentDevices.Take(numberOfDeviceDeltaUpdates).ToList();
                    // reset device count in case HasMoreResult is called more than once
                    hasMoreShouldReturn = true;
                    return cacheMissQueryMock.Object;
                });
            return mockRegistryManager;
        }

        private void InitCache(ILoRaDeviceCacheStore cache, List<DevAddrCacheInfo> deviceIds)
        {
            var loradevaddrcache = new LoRaDevAddrCache(cache, null, null);
            foreach (var device in deviceIds)
            {
                loradevaddrcache.StoreInfo(device);
            }
        }

        [Fact]
        // This test simulate a new call from an unknow device. It checks that :
        // The server correctly query iot hub
        // Server saves answer in the Cache for future usage
        public async Task When_DevAddr_Is_Not_In_Cache_Query_Iot_Hub_And_Save_In_Cache()
        {
            string gatewayId = NewUniqueEUI64();
            DateTime dateTime = DateTime.UtcNow;
            List<DevAddrCacheInfo> managerInput = new List<DevAddrCacheInfo>();

            for (int i = 0; i < 2; i++)
            {
                managerInput.Add(new DevAddrCacheInfo()
                {
                    DevEUI = NewUniqueEUI64(),
                    DevAddr = NewUniqueEUI32()
                });
            }

            var devAddrJoining = managerInput[0].DevAddr;
            var registryManagerMock = this.InitRegistryManager(managerInput);

            List<IoTHubDeviceInfo> items = new List<IoTHubDeviceInfo>();

            // In this test we want no updates running
            // initialize locks for test to run correctly
            var lockToTake = new string[2] { FullUpdateKey, DeltaUpdateKey };
            await LockDevAddrHelper.PrepareLocksForTests(this.cache, null, lockToTake);

            var deviceGetter = new DeviceGetter(registryManagerMock.Object, this.cache);
            items = await deviceGetter.GetDeviceList(null, gatewayId, "ABCD", devAddrJoining);

            Assert.Single(items);
            // If a cache miss it should save it in the redisCache
            var devAddrcache = new LoRaDevAddrCache(this.cache, null, null);
            var queryResult = this.cache.GetHashObject(string.Concat(CacheKeyPrefix, devAddrJoining));
            Assert.Single(queryResult);
            var resultObject = JsonConvert.DeserializeObject<DevAddrCacheInfo>(queryResult[0].Value);
            Assert.Equal(managerInput[0].DevAddr, resultObject.DevAddr);
            Assert.Equal(managerInput[0].GatewayId, resultObject.GatewayId);
            Assert.Equal(managerInput[0].DevEUI, resultObject.DevEUI);

            registryManagerMock.Verify(x => x.CreateQuery(It.IsAny<string>(), It.IsAny<int>()), Times.Once);
            registryManagerMock.Verify(x => x.GetTwinAsync(It.IsAny<string>()), Times.Never);
            registryManagerMock.Verify(x => x.GetDeviceAsync(It.IsAny<string>()), Times.Once);
        }

        [Fact]
        // This test simulate a call received by multiple server. It ensures IoT Hub is only queried once.
        public async Task Multi_Gateway_When_DevAddr_Is_Not_In_Cache_Query_Iot_Hub_Only_Once_And_Save_In_Cache()
        {
            string gatewayId = NewUniqueEUI64();
            DateTime dateTime = DateTime.UtcNow;
            List<DevAddrCacheInfo> managerInput = new List<DevAddrCacheInfo>();

            for (int i = 0; i < 2; i++)
            {
                managerInput.Add(new DevAddrCacheInfo()
                {
                    DevEUI = NewUniqueEUI64(),
                    DevAddr = NewUniqueEUI32()
                });
            }

            var devAddrJoining = managerInput[0].DevAddr;
            var registryManagerMock = this.InitRegistryManager(managerInput);

            // In this test we want no updates running
            // initialize locks for test to run correctly
            var lockToTake = new string[2] { FullUpdateKey, DeltaUpdateKey };
            await LockDevAddrHelper.PrepareLocksForTests(this.cache, null, lockToTake);

            var deviceGetter = new DeviceGetter(registryManagerMock.Object, this.cache);
            // Simulate three queries
            var tasks = new Task[3]
              {
                deviceGetter.GetDeviceList(null, gatewayId, "ABCD", devAddrJoining),
                deviceGetter.GetDeviceList(null, gatewayId, "ABCD", devAddrJoining),
                deviceGetter.GetDeviceList(null, gatewayId, "ABCD", devAddrJoining),
              };

            await Task.WhenAll(tasks);

            // If a cache miss it should save it in the redisCache
            var devAddrcache = new LoRaDevAddrCache(this.cache, null, null);
            var queryResult = this.cache.GetHashObject(string.Concat(CacheKeyPrefix, devAddrJoining));
            Assert.Single(queryResult);
            var resultObject = JsonConvert.DeserializeObject<DevAddrCacheInfo>(queryResult[0].Value);
            Assert.Equal(managerInput[0].DevAddr, resultObject.DevAddr);
            Assert.Equal(managerInput[0].GatewayId, resultObject.GatewayId);
            Assert.Equal(managerInput[0].DevEUI, resultObject.DevEUI);

            registryManagerMock.Verify(x => x.CreateQuery(It.IsAny<string>(), It.IsAny<int>()), Times.Once);
            registryManagerMock.Verify(x => x.GetTwinAsync(It.IsAny<string>()), Times.Never);
            registryManagerMock.Verify(x => x.GetDeviceAsync(It.IsAny<string>()), Times.Once);
        }

        [Fact]
        // This test ensure that if a device is in cache without a key, it get the keys from iot hub and saave it
        public async Task When_DevAddr_Is_In_Cache_Without_Key_Should_Not_Query_Iot_Hub_For_Twin_But_Should_Get_Key_And_Update()
        {
            string gatewayId = NewUniqueEUI64();
            DateTime dateTime = DateTime.UtcNow;
            List<DevAddrCacheInfo> managerInput = new List<DevAddrCacheInfo>();
            for (int i = 0; i < 2; i++)
            {
                managerInput.Add(new DevAddrCacheInfo()
                {
                    DevEUI = NewUniqueEUI64(),
                    DevAddr = NewUniqueEUI32(),
                    GatewayId = gatewayId,
                    LastUpdatedTwins = dateTime
                });
            }

            var devAddrJoining = managerInput[0].DevAddr;
            this.InitCache(this.cache, managerInput);
            var registryManagerMock = this.InitRegistryManager(managerInput);
            List<IoTHubDeviceInfo> items = new List<IoTHubDeviceInfo>();

            // In this test we want no updates running
            // initialize locks for test to run correctly
            var lockToTake = new string[2] { FullUpdateKey, DeltaUpdateKey };
            await LockDevAddrHelper.PrepareLocksForTests(this.cache, null, lockToTake);

            var deviceGetter = new DeviceGetter(registryManagerMock.Object, this.cache);
            items = await deviceGetter.GetDeviceList(null, gatewayId, "ABCD", devAddrJoining);

            Assert.Single(items);
            var queryResult = this.cache.GetHashObject(string.Concat(CacheKeyPrefix, devAddrJoining));
            Assert.Single(queryResult);
            // The key should have been saved
            var resultObject = JsonConvert.DeserializeObject<DevAddrCacheInfo>(queryResult[0].Value);
            Assert.NotNull(resultObject.PrimaryKey);

            // Iot hub should never have been called.
            registryManagerMock.Verify(x => x.CreateQuery(It.IsAny<string>()), Times.Never, "IoT Hub should not have been called, as the device was present in the cache.");
            registryManagerMock.Verify(x => x.GetTwinAsync(It.IsAny<string>()), Times.Never, "IoT Hub should not have been called, as the device was present in the cache.");
            // Should query for the key as key is missing
            registryManagerMock.Verify(x => x.GetDeviceAsync(It.IsAny<string>()), Times.Once);
            registryManagerMock.Verify(x => x.CreateQuery(It.IsAny<string>(), It.IsAny<int>()), Times.Never);
        }

        [Fact]
        // This test ensure that if a device is in cache without a key, it get the keys from iot hub and saave it
        public async Task Multi_Gateway_When_DevAddr_Is_In_Cache_Without_Key_Should_Not_Query_Iot_Hub_For_Twin_But_Should_Get_Key_And_Update()
        {
            string gatewayId = NewUniqueEUI64();
            DateTime dateTime = DateTime.UtcNow;
            List<DevAddrCacheInfo> managerInput = new List<DevAddrCacheInfo>();
            for (int i = 0; i < 2; i++)
            {
                managerInput.Add(new DevAddrCacheInfo()
                {
                    DevEUI = NewUniqueEUI64(),
                    DevAddr = NewUniqueEUI32(),
                    GatewayId = gatewayId,
                    LastUpdatedTwins = dateTime
                });
            }

            var devAddrJoining = managerInput[0].DevAddr;
            this.InitCache(this.cache, managerInput);
            var registryManagerMock = this.InitRegistryManager(managerInput);

            // In this test we want no updates running
            // initialize locks for test to run correctly
            var lockToTake = new string[2] { FullUpdateKey, DeltaUpdateKey };
            await LockDevAddrHelper.PrepareLocksForTests(this.cache, null, lockToTake);

            var deviceGetter = new DeviceGetter(registryManagerMock.Object, this.cache);
            var tasks = new Task[3]
            {
                deviceGetter.GetDeviceList(null, gatewayId, "ABCD", devAddrJoining),
                deviceGetter.GetDeviceList(null, gatewayId, "ABCD", devAddrJoining),
                deviceGetter.GetDeviceList(null, gatewayId, "ABCD", devAddrJoining),
            };

            await Task.WhenAll(tasks);
            // Iot hub should never have been called.
            registryManagerMock.Verify(x => x.CreateQuery(It.IsAny<string>()), Times.Never, "IoT Hub should not have been called, as the device was present in the cache.");
            registryManagerMock.Verify(x => x.CreateQuery(It.IsAny<string>(), It.IsAny<int>()), Times.Never, "IoT Hub should not have been called, as the device was present in the cache.");
            registryManagerMock.Verify(x => x.GetTwinAsync(It.IsAny<string>()), Times.Never, "IoT Hub should not have been called, as the device was present in the cache.");
            // Should query for the key as key is missing
            registryManagerMock.Verify(x => x.GetDeviceAsync(It.IsAny<string>()), Times.Once);
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
            string gatewayId = NewUniqueEUI64();
            DateTime dateTime = DateTime.UtcNow;
            var primaryKey = Convert.ToBase64String(Encoding.UTF8.GetBytes(PrimaryKey));
            List<DevAddrCacheInfo> managerInput = new List<DevAddrCacheInfo>();
            for (int i = 0; i < 2; i++)
            {
                managerInput.Add(new DevAddrCacheInfo()
                {
                    DevEUI = NewUniqueEUI64(),
                    DevAddr = NewUniqueEUI32(),
                    GatewayId = gatewayId,
                    PrimaryKey = primaryKey,
                    LastUpdatedTwins = dateTime
                });
            }

            var devAddrJoining = managerInput[0].DevAddr;
            this.InitCache(this.cache, managerInput);
            var registryManagerMock = this.InitRegistryManager(managerInput);

            List<IoTHubDeviceInfo> items = new List<IoTHubDeviceInfo>();
            // In this test we want no updates running
            // initialize locks for test to run correctly
            var lockToTake = new string[2] { FullUpdateKey, DeltaUpdateKey };
            await LockDevAddrHelper.PrepareLocksForTests(this.cache, null, lockToTake);

            var deviceGetter = new DeviceGetter(registryManagerMock.Object, this.cache);
            items = await deviceGetter.GetDeviceList(null, gatewayId, "ABCD", devAddrJoining);

            Assert.Single(items);
            // Iot hub should never have been called.
            registryManagerMock.Verify(x => x.CreateQuery(It.IsAny<string>()), Times.Never, "IoT Hub should not have been called, as the device was present in the cache.");
            registryManagerMock.Verify(x => x.GetTwinAsync(It.IsAny<string>()), Times.Never, "IoT Hub should not have been called, as the device was present in the cache.");
            // Should not query for the key as key is there
            registryManagerMock.Verify(x => x.GetDeviceAsync(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        // This test ensure that if the device has the key within the cache, it should not make any query to iot hub
        public async Task When_Device_Is_Not_Ours_Save_In_Cache_And_Dont_Query_Hub_Again()
        {
            string gatewayId = NewUniqueEUI64();
            DateTime dateTime = DateTime.UtcNow;
            // In this test we want no updates running
            // initialize locks for test to run correctly
            var lockToTake = new string[2] { FullUpdateKey, DeltaUpdateKey };
            await LockDevAddrHelper.PrepareLocksForTests(this.cache, null, lockToTake);

            List<IoTHubDeviceInfo> items = new List<IoTHubDeviceInfo>();
            var primaryKey = Convert.ToBase64String(Encoding.UTF8.GetBytes(PrimaryKey));
            List<DevAddrCacheInfo> managerInput = new List<DevAddrCacheInfo>();
            for (int i = 0; i < 2; i++)
            {
                managerInput.Add(new DevAddrCacheInfo()
                {
                    DevEUI = NewUniqueEUI64(),
                    DevAddr = NewUniqueEUI32(),
                    GatewayId = gatewayId,
                    PrimaryKey = primaryKey,
                    LastUpdatedTwins = dateTime
                });
            }

            var devAddrJoining = NewUniqueEUI32();
            this.InitCache(this.cache, managerInput);
            var registryManagerMock = this.InitRegistryManager(managerInput);

            var deviceGetter = new DeviceGetter(registryManagerMock.Object, this.cache);
            items = await deviceGetter.GetDeviceList(null, gatewayId, "ABCD", devAddrJoining);

            Assert.Empty(items);
            var queryResult = this.cache.GetHashObject(string.Concat(CacheKeyPrefix, devAddrJoining));
            Assert.Single(queryResult);
            var resultObject = JsonConvert.DeserializeObject<DevAddrCacheInfo>(queryResult[0].Value);
            Assert.Equal(resultObject.DevEUI, string.Empty);
            Assert.Null(resultObject.PrimaryKey);
            Assert.Null(resultObject.GatewayId);
            var query2Result = this.cache.GetHashObject(string.Concat(CacheKeyPrefix, devAddrJoining));
            Assert.Single(query2Result);

            // Iot hub should never have been called.
            registryManagerMock.Verify(x => x.CreateQuery(It.IsAny<string>()), Times.Never, "IoT Hub should not have been called, as the device was present in the cache.");
            registryManagerMock.Verify(x => x.GetTwinAsync(It.IsAny<string>()), Times.Never, "IoT Hub should not have been called, as the device was present in the cache.");
            // Should not query for the key as key is there
            registryManagerMock.Verify(x => x.GetDeviceAsync(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        // Check that the server perform a full reload if the locking key for full reload is not present
        public async Task When_FullUpdateKey_Is_Not_there_Should_Perform_Full_Reload()
        {
            string gatewayId = NewUniqueEUI64();
            DateTime dateTime = DateTime.UtcNow;
            var primaryKey = Convert.ToBase64String(Encoding.UTF8.GetBytes(PrimaryKey));
            List<DevAddrCacheInfo> managerInput = new List<DevAddrCacheInfo>();
            for (int i = 0; i < 5; i++)
            {
                managerInput.Add(new DevAddrCacheInfo()
                {
                    DevEUI = NewUniqueEUI64(),
                    DevAddr = NewUniqueEUI32(),
                    GatewayId = gatewayId,
                });
            }

            var devAddrJoining = managerInput[0].DevAddr;
            // The cache start as empty
            var registryManagerMock = this.InitRegistryManager(managerInput);

            // initialize locks for test to run correctly
            string[] neededLocksForTestToRun = new string[2] { FullUpdateKey, GlobalDevAddrUpdateKey };
            await LockDevAddrHelper.PrepareLocksForTests(this.cache, neededLocksForTestToRun, null);

            List<IoTHubDeviceInfo> items = new List<IoTHubDeviceInfo>();

            var deviceGetter = new DeviceGetter(registryManagerMock.Object, this.cache);
            items = await deviceGetter.GetDeviceList(null, gatewayId, "ABCD", devAddrJoining);

            Assert.Single(items);
            // Iot hub should never have been called.
            registryManagerMock.Verify(x => x.CreateQuery(It.IsAny<string>()), Times.Once);
            registryManagerMock.Verify(x => x.GetTwinAsync(It.IsAny<string>()), Times.Never);
            // We expect to query for the key once (the device with an active connection)
            registryManagerMock.Verify(x => x.GetDeviceAsync(It.IsAny<string>()), Times.Once);

            // we expect the devices are saved
            for (int i = 1; i < 5; i++)
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
            string gatewayId = NewUniqueEUI64();
            DateTime dateTime = DateTime.UtcNow;

            List<DevAddrCacheInfo> managerInput = new List<DevAddrCacheInfo>();
            for (int i = 0; i < 5; i++)
            {
                managerInput.Add(new DevAddrCacheInfo()
                {
                    DevEUI = NewUniqueEUI64(),
                    DevAddr = NewUniqueEUI32(),
                    GatewayId = gatewayId,
                    LastUpdatedTwins = dateTime
                });
            }

            var devAddrJoining = managerInput[0].DevAddr;
            // The cache start as empty
            var registryManagerMock = this.InitRegistryManager(managerInput);

            // initialize locks for test to run correctly
            string[] neededLocksForTestToRun = new string[2] { GlobalDevAddrUpdateKey, DeltaUpdateKey };
            var locksGuideTest = new string[1] { FullUpdateKey };
            await LockDevAddrHelper.PrepareLocksForTests(this.cache, neededLocksForTestToRun, locksGuideTest);

            LoRaDevAddrCache devAddrCache = new LoRaDevAddrCache(this.cache, null, gatewayId);
            await devAddrCache.PerformNeededSyncs(registryManagerMock.Object);

            while (!string.IsNullOrEmpty(this.cache.StringGet(GlobalDevAddrUpdateKey)))
            {
                await Task.Delay(100);
            }

            var foundItem = 0;
            // we expect the devices are saved
            for (int i = 0; i < 5; i++)
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

            // Iot hub should never have been called.
            registryManagerMock.Verify(x => x.CreateQuery(It.IsAny<string>()), Times.Once);
            registryManagerMock.Verify(x => x.GetTwinAsync(It.IsAny<string>()), Times.Never);
            // We expect to query for the key once (the device with an active connection)
            registryManagerMock.Verify(x => x.GetDeviceAsync(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        // This test perform a delta update and we check the following
        // primary key present in the cache is still here after a delta up
        // Items with save Devaddr are correctly saved (one old from cache, one from iot hub)
        // Gateway Id is correctly updated in old cache information.
        // Primary Key are kept as UpdateTime is similar
        public async Task Delta_Update_Perform_Correctly_On_Non_Empty_Cache_And_Keep_Old_Values()
        {
            string oldGatewayId = NewUniqueEUI64();
            string newGatewayId = NewUniqueEUI64();
            DateTime dateTime = DateTime.UtcNow;

            var primaryKey = Convert.ToBase64String(Encoding.UTF8.GetBytes(PrimaryKey));
            List<DevAddrCacheInfo> managerInput = new List<DevAddrCacheInfo>();

            var adressForDuplicateDevAddr = NewUniqueEUI32();
            for (int i = 0; i < 5; i++)
            {
                managerInput.Add(new DevAddrCacheInfo()
                {
                    DevEUI = NewUniqueEUI64(),
                    DevAddr = NewUniqueEUI32(),
                    GatewayId = newGatewayId,
                    LastUpdatedTwins = dateTime
                });
            }

            managerInput.Add(new DevAddrCacheInfo()
            {
                DevEUI = NewUniqueEUI64(),
                DevAddr = adressForDuplicateDevAddr,
                GatewayId = newGatewayId,
                LastUpdatedTwins = dateTime
            });

            var devAddrJoining = managerInput[0].DevAddr;
            // The cache start as empty
            var registryManagerMock = this.InitRegistryManager(managerInput, managerInput.Count());

            // Set up the cache with expectation.
            List<DevAddrCacheInfo> cacheInput = new List<DevAddrCacheInfo>();
            for (int i = 0; i < 5; i++)
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

            var devEuiDoubleItem = NewUniqueEUI64();
            cacheInput.Add(new DevAddrCacheInfo()
            {
                DevEUI = devEuiDoubleItem,
                DevAddr = adressForDuplicateDevAddr,
                GatewayId = oldGatewayId,
                PrimaryKey = primaryKey,
                LastUpdatedTwins = dateTime
            });
            this.InitCache(this.cache, cacheInput);

            // initialize locks for test to run correctly
            string[] neededLocksForTestToRun = new string[2] { GlobalDevAddrUpdateKey, DeltaUpdateKey };
            var locksGuideTest = new string[1] { FullUpdateKey };
            await LockDevAddrHelper.PrepareLocksForTests(this.cache, neededLocksForTestToRun, locksGuideTest);

            LoRaDevAddrCache devAddrCache = new LoRaDevAddrCache(this.cache, null, newGatewayId);
            await devAddrCache.PerformNeededSyncs(registryManagerMock.Object);

            // we expect the devices are saved
            for (int i = 0; i < managerInput.Count; i++)
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
            Assert.Equal(2, query2Result.Count());
            for (int i = 0; i < 2; i++)
            {
                var resultObject = JsonConvert.DeserializeObject<DevAddrCacheInfo>(query2Result[0].Value);
                if (resultObject.DevEUI == devEuiDoubleItem)
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

            // Iot hub should never have been called.
            registryManagerMock.Verify(x => x.CreateQuery(It.IsAny<string>()), Times.Once);
            registryManagerMock.Verify(x => x.GetTwinAsync(It.IsAny<string>()), Times.Never);
            // We expect to query for the key once (the device with an active connection)
            registryManagerMock.Verify(x => x.GetDeviceAsync(It.IsAny<string>()), Times.Never);
            registryManagerMock.Verify(x => x.CreateQuery(It.IsAny<string>(), It.IsAny<int>()), Times.Never);
        }

        [Fact]
        // This test perform a delta update and we check the following
        // primary key present in the cache is still here after a delta up
        // Items with save Devaddr are correctly saved (one old from cache, one from iot hub)
        // Gateway Id is correctly updated in old cache information.
        // Primary Key are dropped as updatetime is defferent
        public async Task Delta_Update_Perform_Correctly_On_Non_Empty_Cache_And_Keep_Old_Values_Except_Primary_Key()
        {
            string oldGatewayId = NewUniqueEUI64();
            string newGatewayId = NewUniqueEUI64();
            DateTime dateTime = DateTime.UtcNow;
            DateTime updateDateTime = DateTime.UtcNow.AddMinutes(10);

            var primaryKey = Convert.ToBase64String(Encoding.UTF8.GetBytes(PrimaryKey));
            List<DevAddrCacheInfo> managerInput = new List<DevAddrCacheInfo>();

            var adressForDuplicateDevAddr = NewUniqueEUI32();
            for (int i = 0; i < 5; i++)
            {
                managerInput.Add(new DevAddrCacheInfo()
                {
                    DevEUI = NewUniqueEUI64(),
                    DevAddr = NewUniqueEUI32(),
                    GatewayId = newGatewayId,
                    LastUpdatedTwins = updateDateTime
                });
            }

            var registryManagerMock = this.InitRegistryManager(managerInput, managerInput.Count());

            // Set up the cache with expectation.
            List<DevAddrCacheInfo> cacheInput = new List<DevAddrCacheInfo>();
            for (int i = 0; i < 5; i++)
            {
                cacheInput.Add(new DevAddrCacheInfo()
                {
                    DevEUI = managerInput[i].DevEUI,
                    DevAddr = managerInput[i].DevAddr,
                    LastUpdatedTwins = dateTime,
                    PrimaryKey = primaryKey
                });
            }

            this.InitCache(this.cache, cacheInput);
            // initialize locks for test to run correctly
            string[] neededLocksForTestToRun = new string[2] { GlobalDevAddrUpdateKey, DeltaUpdateKey };
            var locksGuideTest = new string[1] { FullUpdateKey };
            await LockDevAddrHelper.PrepareLocksForTests(this.cache, neededLocksForTestToRun, locksGuideTest);

            LoRaDevAddrCache devAddrCache = new LoRaDevAddrCache(this.cache, null, newGatewayId);
            await devAddrCache.PerformNeededSyncs(registryManagerMock.Object);

            // we expect the devices are saved
            for (int i = 0; i < managerInput.Count; i++)
            {
                var queryResult = this.cache.GetHashObject(string.Concat(CacheKeyPrefix, managerInput[i].DevAddr));
                Assert.Single(queryResult);
                var resultObject = JsonConvert.DeserializeObject<DevAddrCacheInfo>(queryResult[0].Value);
                Assert.Equal(managerInput[i].GatewayId, resultObject.GatewayId);
                Assert.Equal(managerInput[i].DevEUI, resultObject.DevEUI);
                // as the object changed the keys should not be saved
                Assert.Equal(string.Empty, resultObject.PrimaryKey);
            }

            // Iot hub should never have been called.
            registryManagerMock.Verify(x => x.CreateQuery(It.IsAny<string>()), Times.Once);
            registryManagerMock.Verify(x => x.GetTwinAsync(It.IsAny<string>()), Times.Never);
            // We expect to query for the key once (the device with an active connection)
            registryManagerMock.Verify(x => x.GetDeviceAsync(It.IsAny<string>()), Times.Never);
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
            string oldGatewayId = NewUniqueEUI64();
            string newGatewayId = NewUniqueEUI64();
            DateTime dateTime = DateTime.UtcNow;
            var primaryKey = Convert.ToBase64String(Encoding.UTF8.GetBytes(PrimaryKey));
            List<DevAddrCacheInfo> newValues = new List<DevAddrCacheInfo>();

            var adressForDuplicateDevAddr = NewUniqueEUI32();
            for (int i = 0; i < 5; i++)
            {
                newValues.Add(new DevAddrCacheInfo()
                {
                    DevEUI = NewUniqueEUI64(),
                    DevAddr = NewUniqueEUI32(),
                    GatewayId = newGatewayId,
                    LastUpdatedTwins = dateTime
                });
            }

            newValues.Add(new DevAddrCacheInfo()
            {
                DevEUI = NewUniqueEUI64(),
                DevAddr = adressForDuplicateDevAddr,
                GatewayId = newGatewayId,
                LastUpdatedTwins = dateTime
            });

            var devAddrJoining = newValues[0].DevAddr;
            // The cache start as empty
            var registryManagerMock = this.InitRegistryManager(newValues, newValues.Count());

            // Set up the cache with expectation.
            List<DevAddrCacheInfo> cacheInput = new List<DevAddrCacheInfo>();
            for (int i = 0; i < 5; i++)
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
            var devEuiDoubleItem = NewUniqueEUI64();

            cacheInput.Add(new DevAddrCacheInfo()
            {
                DevEUI = devEuiDoubleItem,
                DevAddr = adressForDuplicateDevAddr,
                GatewayId = oldGatewayId,
                PrimaryKey = primaryKey,
                LastUpdatedTwins = dateTime
            });

            this.InitCache(this.cache, cacheInput);

            // initialize locks for test to run correctly
            string[] neededLocksForTestToRun = new string[2] { GlobalDevAddrUpdateKey, FullUpdateKey };
            await LockDevAddrHelper.PrepareLocksForTests(this.cache, neededLocksForTestToRun, null);

            LoRaDevAddrCache devAddrCache = new LoRaDevAddrCache(this.cache, null, newGatewayId);
            await devAddrCache.PerformNeededSyncs(registryManagerMock.Object);

            // we expect the devices are saved, the double device id should not be there anymore
            for (int i = 0; i < newValues.Count; i++)
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

            // Iot hub should never have been called.
            registryManagerMock.Verify(x => x.CreateQuery(It.IsAny<string>()), Times.Once);
            registryManagerMock.Verify(x => x.GetTwinAsync(It.IsAny<string>()), Times.Never);
            // We expect to query for the key once (the device with an active connection)
            registryManagerMock.Verify(x => x.GetDeviceAsync(It.IsAny<string>()), Times.Never);
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
            string oldGatewayId = NewUniqueEUI64();
            string newGatewayId = NewUniqueEUI64();
            DateTime dateTime = DateTime.UtcNow;
            DateTime updateDateTime = DateTime.UtcNow.AddMinutes(3);
            var primaryKey = Convert.ToBase64String(Encoding.UTF8.GetBytes(PrimaryKey));
            List<DevAddrCacheInfo> newValues = new List<DevAddrCacheInfo>();

            var adressForDuplicateDevAddr = NewUniqueEUI32();
            for (int i = 0; i < 5; i++)
            {
                newValues.Add(new DevAddrCacheInfo()
                {
                    DevEUI = NewUniqueEUI64(),
                    DevAddr = NewUniqueEUI32(),
                    GatewayId = newGatewayId,
                    LastUpdatedTwins = updateDateTime
                });
            }

            // The cache start as empty
            var registryManagerMock = this.InitRegistryManager(newValues, newValues.Count());

            // Set up the cache with expectation.
            List<DevAddrCacheInfo> cacheInput = new List<DevAddrCacheInfo>();
            for (int i = 0; i < 5; i++)
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

            this.InitCache(this.cache, cacheInput);

            // initialize locks for test to run correctly
            string[] neededLocksForTestToRun = new string[2] { GlobalDevAddrUpdateKey, FullUpdateKey };
            await LockDevAddrHelper.PrepareLocksForTests(this.cache, neededLocksForTestToRun, null);

            LoRaDevAddrCache devAddrCache = new LoRaDevAddrCache(this.cache, null, newGatewayId);
            await devAddrCache.PerformNeededSyncs(registryManagerMock.Object);

            // we expect the devices are saved, the double device id should not be there anymore
            for (int i = 0; i < newValues.Count; i++)
            {
                var queryResult = this.cache.GetHashObject(string.Concat(CacheKeyPrefix, newValues[i].DevAddr));
                Assert.Single(queryResult);
                var result2Object = JsonConvert.DeserializeObject<DevAddrCacheInfo>(queryResult[0].Value);
                Assert.Equal(newGatewayId, result2Object.GatewayId);
                Assert.Equal(newValues[i].DevEUI, result2Object.DevEUI);
                Assert.Equal(string.Empty, result2Object.PrimaryKey);
            }

            // Iot hub should never have been called.
            registryManagerMock.Verify(x => x.CreateQuery(It.IsAny<string>()), Times.Once);
            registryManagerMock.Verify(x => x.GetTwinAsync(It.IsAny<string>()), Times.Never);
            // We expect to query for the key once (the device with an active connection)
            registryManagerMock.Verify(x => x.GetDeviceAsync(It.IsAny<string>()), Times.Never);
        }
    }
}
