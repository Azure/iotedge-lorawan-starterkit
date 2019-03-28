// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using Microsoft.Extensions.Configuration;

    public class TestConfiguration
    {
        public static TestConfiguration GetConfiguration()
        {
            var result = new TestConfiguration();

            new ConfigurationBuilder()
                .AddJsonFile("appsettings.json", optional: true)
                .AddJsonFile("appsettings.local.json", optional: true)
                .AddEnvironmentVariables()
                .Build()
                .GetSection("testConfiguration")
                .Bind(result);

            return result;
        }

        public string IoTHubEventHubConnectionString { get; set; }

        public string IoTHubConnectionString { get; set; }

        public int EnsureHasEventDelayBetweenReadsInSeconds { get; set; } = 15;

        public int EnsureHasEventMaximumTries { get; set; } = 5;

        public string IoTHubEventHubConsumerGroup { get; set; } = "$Default";

        public string LeafDeviceSerialPort { get; set; } = "/dev/ttyACM";

        public string LeafDeviceGatewayID { get; set; }

        // IP of the LoRaWanNetworkSrvModule
        public string NetworkServerIP { get; set; }

        // Device prefix to be used
        public string DevicePrefix { get; set; }

        // Device key format. Must be at maximum 16 character long
        public string DeviceKeyFormat { get; set; }

        public bool CreateDevices { get; set; } = true;

        public LogValidationAssertLevel NetworkServerModuleLogAssertLevel { get; set; } = LogValidationAssertLevel.Warning;

        public LogValidationAssertLevel IoTHubAssertLevel { get; set; } = LogValidationAssertLevel.Warning;

        public LoraRegion LoraRegion { get; set; } = LoraRegion.EU;

        // Gets/sets if network server is using udp for logging
        public bool UdpLog { get; set; }

        // Gets/sets network server udp log port
        public int UdpLogPort { get; set; } = 6000;

        // Gets/sets gateway NetId
        public uint NetId { get; set; } = 1;

        // Gets/sets the network server module identifier
        public string NetworkServerModuleID { get; set; } = "LoRaWanNetworkSrvModule";

        public string FunctionAppCode { get; set; }

        public string FunctionAppBaseUrl { get; set; }
    }
}
