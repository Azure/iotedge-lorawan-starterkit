// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

namespace LoRaWan.Tests.Unit.NetworkServer.Logger
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using LoRaWan.NetworkServer;
    using LoRaWan.NetworkServer.Logger;
    using LoRaWan.Tests.Common;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;
    using Moq;
    using Xunit;

    public class IoTHubLoggerTests
    {
        [Theory]
        [InlineData(LogLevel.Debug, LogLevel.Information, 1)]
        [InlineData(LogLevel.Debug, LogLevel.Debug, 1)]
        [InlineData(LogLevel.Debug, LogLevel.Trace, 0)]
        public async Task If_Log_Level_Is_Higher_Log_Is_Written(LogLevel configuredLevel, LogLevel entryLevel, int sendMessageCount)
        {
            // arrange
            using var testableLogger = SetupProviderAndLogger(new LoRaLoggerConfiguration { LogLevel = configuredLevel, UseScopes = false });

            // act
            testableLogger.Value.Object.Log(entryLevel, "foo");

            // assert
            await VerifySendMessageCountAsync(testableLogger.Value, sendMessageCount);
        }

        [Theory]
        [InlineData(0, 0, 1)]
        [InlineData(0, 1, 1)]
        [InlineData(1, 1, 1)]
        [InlineData(1, 2, 0)]
        public async Task When_EventId_Changes_Output_Is_Written_When_Matching_Or_Omitted(int configuredEventId, int loggedEventId, int sendMessageCount)
        {
            // arrange
            using var testableLogger = SetupProviderAndLogger(new LoRaLoggerConfiguration { LogLevel = LogLevel.Trace, EventId = configuredEventId, UseScopes = false });

            // act
            testableLogger.Value.Object.LogInformation(loggedEventId, "Test");

            // assert
            await VerifySendMessageCountAsync(testableLogger.Value, sendMessageCount);
        }

        [Fact]
        public async Task When_Structured_Parameters_Are_Passed_They_Are_Replaced()
        {
            // arrange
            using var testableLogger = SetupProviderAndLogger();

            // act
            testableLogger.Value.Object.LogInformation("{Id}", 1);

            // assert
            await VerifyMessageAsync(testableLogger.Value, "1");
        }

        [Fact]
        public async Task Scope_Is_Set_In_Message_When_Activated()
        {
            // arrange
            using var testableLogger = SetupProviderAndLogger(new LoRaLoggerConfiguration { LogLevel = LogLevel.Trace });
            const string devAddr = "12345678ADDR";
            const string message = "foo";

            // act
            using var scope = testableLogger.Value.Object.BeginDeviceAddressScope(devAddr);
            testableLogger.Value.Object.LogInformation(message);

            // assert
            await VerifyMessageAsync(testableLogger.Value, devAddr + " " + message);
        }

        [Fact]
        public async Task Scope_Is_Not_Set_In_Message_When_Deactivated()
        {
            // arrange
            using var testableLogger = SetupProviderAndLogger(new LoRaLoggerConfiguration { LogLevel = LogLevel.Trace, UseScopes = false });
            const string devAddr = "12345678ADDR";
            const string message = "foo";

            // act
            using var scope = testableLogger.Value.Object.BeginDeviceAddressScope(devAddr);
            testableLogger.Value.Object.LogInformation(message);

            // assert
            await VerifyMessageAsync(testableLogger.Value, message);
        }

        [Fact]
        public async Task Error_During_Module_Client_Initialization_Disables_Logger()
        {
            // arrange
            using var testableLogger = SetupProviderAndLogger(new Lazy<Task<ModuleClient>>(() => throw new FormatException()));
            var logger = testableLogger.Value.Object;
            var logLevel = LogLevel.Information;
            Assert.True(logger.IsEnabled(logLevel));

            // act
            logger.Log(logLevel, "foo");

            // assert
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            while (logger.IsEnabled(logLevel))
                await Task.Delay(TimeSpan.FromMilliseconds(50), cts.Token);
            Assert.False(logger.IsEnabled(logLevel));
        }

        [Fact]
        public async Task Traces_Iot_Hub_Send_Events()
        {
            // arrange
            var tracing = new Mock<ITracing>();
            const LogLevel logLevel = LogLevel.Information;
            using var testableLogger = SetupProviderAndLogger(new LoRaLoggerConfiguration { LogLevel = logLevel }, tracing: tracing.Object);

            // act
            testableLogger.Value.Object.Log(logLevel, "foo");

            // assert
            await tracing.RetryVerifyAsync(t => t.TrackIotHubDependency("SDK SendEvent", "log"), Times.Once);
        }

        [Fact]
        public void Does_Not_Trace_If_Log_Level_Disabled()
        {
            // arrange
            var tracing = new Mock<ITracing>();
            using var testableLogger = SetupProviderAndLogger(new LoRaLoggerConfiguration { LogLevel = LogLevel.Error }, tracing: tracing.Object);

            // act
            testableLogger.Value.Object.Log(LogLevel.Information, "foo");

            // assert
            tracing.Verify(t => t.TrackIotHubDependency(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        private static DisposableValue<Mock<IotHubLogger>> SetupProviderAndLogger(Lazy<Task<ModuleClient>>? moduleClientFactory) =>
             SetupProviderAndLogger(new LoRaLoggerConfiguration { LogLevel = LogLevel.Trace, UseScopes = false }, moduleClientFactory);

        private static DisposableValue<Mock<IotHubLogger>> SetupProviderAndLogger() => SetupProviderAndLogger(null);

        private static DisposableValue<Mock<IotHubLogger>> SetupProviderAndLogger(LoRaLoggerConfiguration configuration,
                                                                                  Lazy<Task<ModuleClient>>? moduleClientFactory = null,
                                                                                  ITracing? tracing = null)
        {
            var optionsMonitor = new Mock<IOptionsMonitor<LoRaLoggerConfiguration>>();
            optionsMonitor.Setup(om => om.CurrentValue).Returns(configuration);
            optionsMonitor.Setup(om => om.OnChange(It.IsAny<Action<LoRaLoggerConfiguration, string>>())).Returns(NoopDisposable.Instance);
            var mcf = moduleClientFactory ?? new Lazy<Task<ModuleClient>>(Task.FromResult((ModuleClient)null!));
            tracing ??= new NoopTracing();
            var provider = new IotHubLoggerProvider(optionsMonitor.Object, mcf, tracing);
            return new DisposableValue<Mock<IotHubLogger>>(new Mock<IotHubLogger>(provider, mcf, tracing), provider);
        }

        private static async Task VerifyMessageAsync(Mock<IotHubLogger> logger, string message)
        {
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed (false positive, verification only)
            await logger.RetryVerifyAsync(x => x.SendAsync(It.IsAny<ModuleClient>(), message), Times.Once);
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
        }

        private static async Task VerifySendMessageCountAsync(Mock<IotHubLogger> logger, int sendMessageCount)
        {
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed (false positive, verification only)
            await logger.RetryVerifyAsync(x => x.SendAsync(It.IsAny<ModuleClient>(), It.IsAny<string>()), Times.Exactly(sendMessageCount));
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
        }
    }
}
