// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

namespace LoRaWan.NetworkServer
{
    using System;
    using Microsoft.ApplicationInsights;
    using Microsoft.ApplicationInsights.DataContracts;

    public interface ITracing
    {
        IDisposable TrackDataMessage();
        IDisposable TrackIotHubDependency(string dependencyName, string data);
    }

    internal sealed class ApplicationInsightsTracing : ITracing
    {
        // Equal to https://github.com/microsoft/ApplicationInsights-dotnet/blob/main/WEB/Src/DependencyCollector/DependencyCollector/Implementation/RemoteDependencyConstants.cs.
        private const string IotHubDependencyTypeName = "Azure IoT Hub";

        private readonly TelemetryClient telemetryClient;
        private readonly string iotHubHostName;

        public ApplicationInsightsTracing(TelemetryClient telemetryClient, NetworkServerConfiguration networkServerConfiguration)
        {
            this.telemetryClient = telemetryClient;
            this.iotHubHostName = networkServerConfiguration.IoTHubHostName;
        }

        public IDisposable TrackDataMessage() => this.telemetryClient.StartOperation<RequestTelemetry>("Data message");

        public IDisposable TrackIotHubDependency(string dependencyName, string data)
        {
            var dependencyTelemetry = new DependencyTelemetry(IotHubDependencyTypeName, this.iotHubHostName, dependencyName, data);
            return this.telemetryClient.StartOperation(dependencyTelemetry);
        }
    }

    internal sealed class NoopTracing : ITracing
    {
        public IDisposable TrackDataMessage() => NoopDisposable.Instance;
        public IDisposable TrackIotHubDependency(string dependencyName, string data) => NoopDisposable.Instance;
    }
}
