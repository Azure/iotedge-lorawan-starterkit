// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

namespace LoRaWan.Tests.Unit.Logger
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using global::Logger;
    using LoRaWan.Tests.Common;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Extensions.Logging;
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
            using var testableLogger = SetupProviderAndLogger(new IotHubLoggerConfiguration(configuredLevel, default, false));

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
            using var testableLogger = SetupProviderAndLogger(new IotHubLoggerConfiguration(LogLevel.Trace, configuredEventId, false));

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
            using var testableLogger = SetupProviderAndLogger(new IotHubLoggerConfiguration(LogLevel.Trace, default, UseScopes: true));
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
            using var testableLogger = SetupProviderAndLogger(new IotHubLoggerConfiguration(LogLevel.Trace, default, UseScopes: false));
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

        private static DisposableValue<Mock<IotHubLogger>> SetupProviderAndLogger(Lazy<Task<ModuleClient>>? moduleClientFactory) =>
             SetupProviderAndLogger(new IotHubLoggerConfiguration(LogLevel.Trace, default, false), moduleClientFactory);

        private static DisposableValue<Mock<IotHubLogger>> SetupProviderAndLogger() => SetupProviderAndLogger(null);

        private static DisposableValue<Mock<IotHubLogger>> SetupProviderAndLogger(IotHubLoggerConfiguration configuration, Lazy<Task<ModuleClient>>? moduleClientFactory = null)
        {
            var mcf = moduleClientFactory ?? new Lazy<Task<ModuleClient>>(Task.FromResult((ModuleClient)null!));
            var provider = new IotHubLoggerProvider(configuration, mcf);
            return new DisposableValue<Mock<IotHubLogger>>(new Mock<IotHubLogger>(provider, mcf), provider);
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
