// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tools.CLI.Options
{
    using System;
    using CommandLine;

    [Verb("add-gateway", HelpText = "Add a new gateway device to IoT Hub.")]
    public class AddGatewayOption
    {
        internal const int DefaultSpiSpeed = 8;
        internal const int DefaultSpiDev = 0;
        internal const string DefaultAzureIotEdgeVersion = "1.4";
        internal const string DefaultLnsHostAddress = "ws://mylns:5000";
        internal const string DefaultNetwork = "quickstartnetwork";

        [Option(
            "reset-pin",
            Required = true,
            HelpText = "Pin"
            )]
        public int ResetPin { get; set; }

        [Option(
            "spi-speed",
            Default = DefaultSpiSpeed,
            Required = false,
            HelpText = "SPI speed"
            )]
        public int SpiSpeed { get; set; }

        [Option(
            "spi-dev",
            Required = false,
            Default = DefaultSpiDev,
            HelpText = "SPI dev"
            )]
        public int SpiDev { get; set; }

        [Option(
            "device-id",
            Required = true,
            HelpText = "Gateway device identifier"
            )]
        public string DeviceId { get; set; }

        [Option(
            "api-url",
            Required = true,
            HelpText = "API URL to LoRa Function"
            )]
        public Uri ApiURL { get; set; }


        [Option(
            "api-key",
            Required = true,
            HelpText = "API authorization key"
        )]
        public string ApiAuthCode { get; set; }

        [Option(
            "lns-host-address",
            Required = false,
            Default = DefaultLnsHostAddress,
            HelpText = "LNS host address"
            )]
        public string TwinHostAddress { get; set; }

        [Option(
            "network",
            Required = false,
            Default = DefaultNetwork,
            HelpText = "Network identifier for LNS Discovery purposes"
            )]
        public string Network { get; set; }

        [Option(
            "azure-iot-edge-version",
            Default = DefaultAzureIotEdgeVersion,
            Required = false,
            HelpText = "Azure IoT Edge version"
        )]
        public string AzureIotEdgeVersion { get; set; }

        [Option(
            "monitoring",
            Default = false,
            Required = false,
            HelpText = "Indicates if IoT Edge monitoring is enabled"
        )]
        public bool? MonitoringEnabled { get; set; }


        [Option(
            "iothub-resource-id",
            Required = false,
            HelpText = "Indicates the IoT Hub resource id for monitoring purposes. Format is '/subscriptions/<subscription id>/resourceGroups/<resource group name>/providers/Microsoft.Devices/IoTHubs/<iot hub name>'"
        )]
        public string IoTHubResourceId { get; set; }

        [Option(
            "log-analytics-workspace-id",
            Required = false,
            HelpText = "Indicates the Log Analytics Workspace identifier where monitoring events will be sent."
        )]
        public string LogAnalyticsWorkspaceId { get; set; }

        [Option(
            "log-analytics-shared-key",
            Required = false,
            HelpText = "Indicates the Log Analytics shared key used to authenticate."
        )]
        public string LogAnalyticsSharedKey { get; set; }

        [Option(
            "lora-version",
            Required = true,
            HelpText = "LoRaWAN Starter Kit version"
        )]
        public string LoRaVersion { get; set; }
    }
}
