// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Common
{
    using System;
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

        public string DeviceKeyFormatMultiGW { get; set; }

        public bool CreateDevices { get; set; } = true;

        public LogValidationAssertLevel NetworkServerModuleLogAssertLevel { get; set; } = LogValidationAssertLevel.Warning;

        public LogValidationAssertLevel IoTHubAssertLevel { get; set; } = LogValidationAssertLevel.Warning;

        public LoraRegion LoraRegion { get; set; } = LoraRegion.EU;

        // Gets/sets if network server is using TCP for logging
        public bool TcpLog { get; set; }

        // Gets/sets network server TCP log port
        public int TcpLogPort { get; set; } = 6000;

        // Gets/sets gateway NetId
        public uint NetId { get; set; } = 1;

        // Gets/sets the network server module identifier
        public string NetworkServerModuleID { get; set; } = "LoRaWanNetworkSrvModule";

        public string FunctionAppCode { get; set; }

        public Uri FunctionAppBaseUrl { get; set; }

        public int NumberOfGateways { get; set; } = 2;

        // Gets/sets the TXPower value to use in tests
        public short TxPower { get; set; } = 14;

        public bool RunningInCI { get; set; }

        public string RemoteConcentratorConnection { get; set; } //i.e. pi@raspberrypi

        public string RadioDev { get; set; } // i.e. "/dev/ttyACM0"

        public string DefaultBasicStationEui { get; set; } //i.e. "ABC111FFFEDEF000"

        // The path of where the station binary is located on local pc
        public string BasicStationExecutablePath { get; set; } //i.e. "C:\\folder\\station"

        // The path where the SSH Private Key is located (needed for remotely copying needed files and/or executing commands)
        public string SshPrivateKeyPath { get; set; } //i.e. "~/.ssh/mykey" (on WSL, it's the path in the WSL environment)

        public string SharedLnsEndpoint { get; set; } //i.e. "wss://hostname:5001"

        public string SharedCupsEndpoint { get; set; } //i.e. "https://hostname:5002"

        public string CupsBasicStationEui { get; set; } //i.e. "ABC111FFFEDEF000" to be used for CUPS tests

        public string ClientThumbprint { get; set; } //i.e. 4a0639c9c67221919fdb9618fa6fa0680259eaf2 (SHA1 thumbprint of cups.crt)
    }
}
