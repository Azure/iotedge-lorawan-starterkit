// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

namespace LoRaWan.NetworkServer
{
    using Microsoft.ApplicationInsights.Channel;
    using Microsoft.ApplicationInsights.Extensibility;

    internal sealed class TelemetryInitializer : ITelemetryInitializer
    {
        private const string RoleName = "NetworkServer";
        private readonly string gatewayId;

        public TelemetryInitializer(NetworkServerConfiguration networkServerConfiguration) =>
            this.gatewayId = networkServerConfiguration.GatewayID;

        public void Initialize(ITelemetry telemetry)
        {
            telemetry.Context.Cloud.RoleName = RoleName;
            telemetry.Context.Cloud.RoleInstance = this.gatewayId;
        }
    }
}
