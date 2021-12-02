// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

namespace LoRaWan.Tests.Unit.Logger
{
    using System;
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
            var testableLogger = SetupProviderAndLogger(new IotHubLoggerConfiguration(configuredLevel, default, false));

            // act
            testableLogger.Object.Log(entryLevel, "foo");

            // assert
            await VerifySendMessageCountAsync(testableLogger, sendMessageCount);
        }

        [Theory]
        [InlineData(0, 0, 1)]
        [InlineData(0, 1, 1)]
        [InlineData(1, 1, 1)]
        [InlineData(1, 2, 0)]
        public async Task When_EventId_Changes_Output_Is_Written_When_Matching_Or_Omitted(int configuredEventId, int loggedEventId, int sendMessageCount)
        {
            // arrange
            var testableLogger = SetupProviderAndLogger(new IotHubLoggerConfiguration(LogLevel.Trace, configuredEventId, false));

            // act
            testableLogger.Object.LogInformation(loggedEventId, "Test");

            // assert
            await VerifySendMessageCountAsync(testableLogger, sendMessageCount);
        }

        [Fact]
        public async Task When_Structured_Parameters_Are_Passed_They_Are_Replaced()
        {
            // arrange
            var testableLogger = SetupProviderAndLogger();

            // act
            testableLogger.Object.LogInformation("{Id}", 1);

            // assert
            await VerifyMessageAsync(testableLogger, "1");
        }

        [Fact]
        public async void Scope_Is_Set_In_Message_When_Activated()
        {
            // arrange
            var testableLogger = SetupProviderAndLogger(new IotHubLoggerConfiguration(LogLevel.Trace, default, UseScopes: true));
            const string devAddr = "12345678ADDR";
            const string message = "foo";

            // act
            using var scope = testableLogger.Object.BeginDeviceAddressScope(devAddr);
            testableLogger.Object.LogInformation(message);

            // assert
            await VerifyMessageAsync(testableLogger, devAddr + " " + message);
        }

        [Fact]
        public async void Scope_Is_Not_Set_In_Message_When_Deactivated()
        {
            // arrange
            var testableLogger = SetupProviderAndLogger(new IotHubLoggerConfiguration(LogLevel.Trace, default, UseScopes: false));
            const string devAddr = "12345678ADDR";
            const string message = "foo";

            // act
            using var scope = testableLogger.Object.BeginDeviceAddressScope(devAddr);
            testableLogger.Object.LogInformation(message);

            // assert
            await VerifyMessageAsync(testableLogger, message);
        }

        private static Mock<IotHubLogger> SetupProviderAndLogger() =>
            SetupProviderAndLogger(new IotHubLoggerConfiguration(LogLevel.Trace, default, false));

        private static Mock<IotHubLogger> SetupProviderAndLogger(IotHubLoggerConfiguration configuration)
        {
            var provider = new IotHubLoggerProvider(configuration, new Lazy<Task<ModuleClient>>((Task<ModuleClient>)null!));
            return new Mock<IotHubLogger>(provider, null);
        }

        private static async Task VerifyMessageAsync(Mock<IotHubLogger> logger, string message)
        {
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed (false positive, verification only)
            await logger.RetryVerifyAsync(x => x.SendAsync(message), Times.Once);
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
        }

        private static async Task VerifySendMessageCountAsync(Mock<IotHubLogger> logger, int sendMessageCount)
        {
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed (false positive, verification only)
            await logger.RetryVerifyAsync(x => x.SendAsync(It.IsAny<string>()), Times.Exactly(sendMessageCount));
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
        }
    }
}
