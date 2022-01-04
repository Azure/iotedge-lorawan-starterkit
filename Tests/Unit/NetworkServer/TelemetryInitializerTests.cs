// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Unit.NetworkServer
{
    using LoRaWan.NetworkServer;
    using Microsoft.ApplicationInsights.Channel;
    using Microsoft.ApplicationInsights.DataContracts;
    using Moq;
    using Xunit;

    public class TelemetryInitializerTests
    {
        private readonly Mock<ITelemetry> telemetry;

        public TelemetryInitializerTests()
        {
            this.telemetry = new Mock<ITelemetry>();
            var telemetryContext = new TelemetryContext();
            _ = this.telemetry.SetupGet(t => t.Context).Returns(telemetryContext);
        }

        [Fact]
        public void Initialize_RoleName()
        {
            // arrange
            var subject = new TelemetryInitializer(new NetworkServerConfiguration());

            // act
            subject.Initialize(this.telemetry.Object);

            // assert
            Assert.Equal("NetworkServer", this.telemetry.Object.Context.Cloud.RoleName);
        }

        [Fact]
        public void Initialize_RoleInstance()
        {
            // arrange
            const string gatewayId = "foo";
            var subject = new TelemetryInitializer(new NetworkServerConfiguration { GatewayID = gatewayId });

            // act
            subject.Initialize(this.telemetry.Object);

            // assert
            Assert.Equal(gatewayId, this.telemetry.Object.Context.Cloud.RoleInstance);
        }
    }
}
