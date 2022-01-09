// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Simulation
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using LoRaWan.Tests.Common;

    public class IntegrationTestFixtureSim : IntegrationTestFixtureBase
    {
        // Device1001_Simulated_ABP: used for ABP simulator
        public TestDeviceInfo Device1001_Simulated_ABP { get; private set; }

        // Device1002_Simulated_OTAA: used for simulator
        public TestDeviceInfo Device1002_Simulated_OTAA { get; private set; }

        //// Device1003_Simulated_HttpBasedDecoder: used for simulator http based decoding test
        //public TestDeviceInfo Device1003_Simulated_HttpBasedDecoder { get; private set; }

        private readonly List<TestDeviceInfo> deviceRange1000_ABP = new List<TestDeviceInfo>();

        public IReadOnlyCollection<TestDeviceInfo> DeviceRange1000_ABP => this.deviceRange1000_ABP;

        //private readonly List<TestDeviceInfo> deviceRange2000_1000_ABP = new List<TestDeviceInfo>();

        //public IReadOnlyCollection<TestDeviceInfo> DeviceRange2000_1000_ABP => this.deviceRange2000_1000_ABP;

        //private readonly List<TestDeviceInfo> deviceRange3000_10_OTAA = new List<TestDeviceInfo>();

        //public IReadOnlyCollection<TestDeviceInfo> DeviceRange3000_10_OTAA => this.deviceRange3000_10_OTAA;

        public override void SetupTestDevices()
        {
            var gatewayID = Environment.GetEnvironmentVariable("IOTEDGE_DEVICEID") ?? Configuration.LeafDeviceGatewayID;

            // Simulated devices start at 1000

            // Device1001_Simulated_ABP: used for ABP simulator
            Device1001_Simulated_ABP = new TestDeviceInfo()
            {
                DeviceID = "0000000000001001",
                GatewayID = gatewayID,
                SensorDecoder = "DecoderValueSensor",
                IsIoTHubDevice = true,
                AppSKey = GetAppSessionKey(1001),
                NwkSKey = GetNetworkSessionKey(1001),
                DevAddr = "00001001",
            };

            // Device1002_Simulated_OTAA: used for simulator
            Device1002_Simulated_OTAA = new TestDeviceInfo()
            {
                DeviceID = "0000000000001002",
                AppEUI = "0000000000001002",
                AppKey = GetAppKey(1002),
                GatewayID = gatewayID,
                IsIoTHubDevice = true,
                SensorDecoder = "DecoderValueSensor",
            };

            for (var deviceID = 1100; deviceID <= 1110; deviceID++)
            {
                this.deviceRange1000_ABP.Add(
                    new TestDeviceInfo
                    {
                        DeviceID = deviceID.ToString("0000000000000000", CultureInfo.InvariantCulture),
                        AppEUI = deviceID.ToString("0000000000000000", CultureInfo.InvariantCulture),
                        AppKey = GetAppKey(deviceID),
                        GatewayID = gatewayID,
                        IsIoTHubDevice = true,
                        SensorDecoder = "DecoderValueSensor",
                        AppSKey = GetAppSessionKey(deviceID),
                        NwkSKey = GetNetworkSessionKey(deviceID),
                        DevAddr = deviceID.ToString("00000000", CultureInfo.InvariantCulture),
                    });
            }

          /* Comment ununused device to avoid create unessecary devices.
          // Device1003_Simulated_HttpBasedDecoder: used for simulator http based decoding test
          Device1003_Simulated_HttpBasedDecoder = new TestDeviceInfo
          {
              DeviceID = "0000000000001003",
              AppEUI = "0000000000001003",
              AppKey = GetAppKey(1003),
              GatewayID = gatewayID,
              IsIoTHubDevice = true,
              SensorDecoder = "http://localhost:8888/api/DecoderValueSensor",
          };

          // Range of 1000 ABP devices from 2000 to 2999: Used for load testing
          for (var deviceID = 2000; deviceID <= 2999; deviceID++)
          {
              this.deviceRange2000_1000_ABP.Add(
                  new TestDeviceInfo
                  {
                      DeviceID = deviceID.ToString("0000000000000000", CultureInfo.InvariantCulture),
                      GatewayID = gatewayID,
                      IsIoTHubDevice = true,
                      SensorDecoder = "DecoderValueSensor",
                      KeepAliveTimeout = 0,
                      AppSKey = GetAppSessionKey(deviceID),
                      NwkSKey = GetNetworkSessionKey(deviceID),
                      DevAddr = deviceID.ToString("00000000", CultureInfo.InvariantCulture),
                  });
          }

          // Range of 10 OTAA devices from 3000 to 3009: Used for load testing
          for (var deviceID = 3000; deviceID <= 3009; deviceID++)
          {
              this.deviceRange3000_10_OTAA.Add(
                  new TestDeviceInfo
                  {
                      DeviceID = deviceID.ToString("0000000000000000", CultureInfo.InvariantCulture),
                      AppEUI = deviceID.ToString("0000000000000000", CultureInfo.InvariantCulture),
                      AppKey = GetAppKey(deviceID),
                      GatewayID = gatewayID,
                      IsIoTHubDevice = true,
                      SensorDecoder = "DecoderValueSensor",
                  });
          }
          */
        }

        private AppSessionKey GetAppSessionKey(int value) => AppSessionKey.Parse(GetKeyString(value));
        private NetworkSessionKey GetNetworkSessionKey(int value) => NetworkSessionKey.Parse(GetKeyString(value));
        private AppKey GetAppKey(int value) => AppKey.Parse(GetKeyString(value));

        private string GetKeyString(int value) => GetKey32(value);
    }
}
