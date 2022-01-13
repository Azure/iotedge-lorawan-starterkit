// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

namespace LoRaWan.Tests.Unit.Logger
{
    using System;
    using global::Logger;
    using LoRaWan.NetworkServer;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;
    using Moq;
    using Xunit;

    public class LoggerConfigurationMonitorTests
    {
        [Fact]
        public void Constructor_Populates_Configuration()
        {
            // arrange
            var configuration = new LoRaLoggerConfiguration { EventId = default, LogLevel = LogLevel.Debug, UseScopes = true };
            var optionsMonitor = CreateOptionsMonitor(configuration);

            // act
            using var loggerMonitor = new LoggerConfigurationMonitor(optionsMonitor);

            // assert
            Assert.Same(configuration, loggerMonitor.Configuration);
            Assert.NotNull(loggerMonitor.ScopeProvider);
        }

        [Fact]
        public void Monitor_Update_Synchronizes_Configuration()
        {
            // arrange
            var configuration = new LoRaLoggerConfiguration { EventId = default, LogLevel = LogLevel.Debug, UseScopes = true };
            var updatedConfiguration = new LoRaLoggerConfiguration { EventId = default, LogLevel = LogLevel.Information, UseScopes = false };
            var optionsMonitorMock = new Mock<IOptionsMonitor<LoRaLoggerConfiguration>>();
            Action<LoRaLoggerConfiguration, string> actualOnChangeCallback = (a, b) => { };
            optionsMonitorMock.Setup(om => om.CurrentValue).Returns(configuration);
            optionsMonitorMock.Setup(om => om.OnChange(It.IsAny<Action<LoRaLoggerConfiguration, string>>()))
                              .Callback((Action<LoRaLoggerConfiguration, string> c) => actualOnChangeCallback = c)
                              .Returns(NullDisposable.Instance);

            // act
            using var loggerMonitor = new LoggerConfigurationMonitor(optionsMonitorMock.Object);
            actualOnChangeCallback(updatedConfiguration, string.Empty);

            // assert
            Assert.Same(updatedConfiguration, loggerMonitor.Configuration);
            Assert.Null(loggerMonitor.ScopeProvider);
        }

        [Fact]
        public void Disposes_Correctly()
        {
            // arrange
            var onChangeDisposable = new Mock<IDisposable>();
            var optionsMonitor = CreateOptionsMonitor(new LoRaLoggerConfiguration(), onChangeDisposable.Object);

            // act
            using (new LoggerConfigurationMonitor(optionsMonitor))
            {
                // dispose
            }

            // assert
            onChangeDisposable.Verify(oc => oc.Dispose(), Times.Once);
        }

        private static IOptionsMonitor<LoRaLoggerConfiguration> CreateOptionsMonitor(LoRaLoggerConfiguration configuration, IDisposable? onChangeToken = null)
        {
            var optionsMonitorMock = new Mock<IOptionsMonitor<LoRaLoggerConfiguration>>();
            optionsMonitorMock.Setup(om => om.CurrentValue).Returns(configuration);
            optionsMonitorMock.Setup(om => om.OnChange(It.IsAny<Action<LoRaLoggerConfiguration, string>>())).Returns(onChangeToken ?? NullDisposable.Instance);
            return optionsMonitorMock.Object;
        }
    }
}
