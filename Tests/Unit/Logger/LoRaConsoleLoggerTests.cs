// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Unit.Logger
{
    using System;
    using global::Logger;
    using LoRaWan;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;
    using Moq;
    using Xunit;

    public class LoRaConsoleLoggerTests : LoRaConsoleLoggerTestBase
    {
        [Theory]
        [InlineData(LogLevel.Debug, LogLevel.Information, true)]
        [InlineData(LogLevel.Debug, LogLevel.Debug, true)]
        [InlineData(LogLevel.Debug, LogLevel.Trace, false)]
        public void If_Log_Level_Is_Higher_Log_Is_Written(LogLevel configuredLevel, LogLevel entryLevel, bool shouldLog)
        {
            var options = CreateLoggerConfigMonitor();
            options.CurrentValue.LogLevel = configuredLevel;

            var moqInfo = new Mock<Action<string>>();
            var provider = new Mock<LoRaConsoleLoggerProvider>(options);
            var logger = new TestLoRaConsoleLogger(moqInfo.Object, null, provider.Object);
            logger.Log(entryLevel, "test");
            moqInfo.Verify(x => x.Invoke(It.IsAny<string>()), shouldLog ? Times.Once : Times.Never);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void When_Scope_Settings_Change_DevEUI_Is_Handled_In_The_Message(bool useScopes)
        {
            var options = CreateLoggerConfigMonitor();
            options.CurrentValue.UseScopes = useScopes;

            var moqInfo = new Mock<Action<string>>();
            var provider = new Mock<LoRaConsoleLoggerProvider>(options);

            var logger = new TestLoRaConsoleLogger(moqInfo.Object, null, provider.Object);

            var devEUI = new DevEui(0x12345678);
            const string message = "test";
            using var scope = logger.BeginDeviceScope(devEUI);
            logger.LogInformation(message);

            var expected = useScopes ? devEUI + " " + message : message;
            moqInfo.Verify(x => x.Invoke(It.Is<string>(x => x == expected)), Times.Once);
        }

        [Fact]
        public void When_Message_Already_Prefixed_Scope_Should_Not_Be_Added()
        {
            var options = CreateLoggerConfigMonitor();
            options.CurrentValue.UseScopes = true;

            var moqInfo = new Mock<Action<string>>();
            var provider = new Mock<LoRaConsoleLoggerProvider>(options);

            var logger = new TestLoRaConsoleLogger(moqInfo.Object, null, provider.Object);

            var devEUI = new DevEui(0x12345678);
            var message = devEUI + " test";
            using var scope = logger.BeginDeviceScope(devEUI);
            logger.LogInformation(message);

            moqInfo.Verify(x => x.Invoke(It.Is<string>(x => x == message)), Times.Once);
        }

        [Theory]
        [InlineData(0x0123_4567_89ab_cdefU, null, null, "0123456789ABCDEF foo")]
        [InlineData(null, "ADDR", null, "ADDR foo")]
        [InlineData(null, null, 1, "0000000000000001 foo")]
        [InlineData(0x0123_4567_89ab_cdefU, null, 1, "0123456789ABCDEF foo")]
        [InlineData(0x0123_4567_89ab_cdefU, "ADDR", null, "0123456789ABCDEF foo")]
        [InlineData(null, "ADDR", 1, "ADDR foo")]
        public void When_Multiple_Scopes_Set_DevEUI_Preferred_Over_DevAddr_Preferred_Over_StationEUI(ulong? devEuiScope, string devAddrScope, int? stationEuiScope, string expected)
        {
            // arrange
            var options = CreateLoggerConfigMonitor();
            var moqInfo = new Mock<Action<string>>();
            var provider = new Mock<LoRaConsoleLoggerProvider>(options);
            var logger = new TestLoRaConsoleLogger(moqInfo.Object, null, provider.Object);

            // act
            using var euiScope = devEuiScope is { } someDevEuiScope ? logger.BeginDeviceScope(new DevEui(someDevEuiScope)) : default;
            using var addrScope = devAddrScope is null ? default : logger.BeginDeviceAddressScope(devAddrScope);
            using var statScope = stationEuiScope is { } s ? logger.BeginEuiScope(new StationEui((ulong)s)) : default;
            logger.LogInformation("foo");

            // assert
            moqInfo.Verify(x => x.Invoke(expected), Times.Once);
        }

        [Fact]
        public void DevAddr_Scope_Set_In_Message()
        {
            // arrange
            var options = CreateLoggerConfigMonitor();
            var moqInfo = new Mock<Action<string>>();
            var provider = new Mock<LoRaConsoleLoggerProvider>(options);
            var logger = new TestLoRaConsoleLogger(moqInfo.Object, null, provider.Object);
            const string devAddr = "12345678ADDR";
            const string message = "foo";

            // act
            using var scope = logger.BeginDeviceAddressScope(devAddr);
            logger.LogInformation(message);

            // assert
            var expected = string.Concat(devAddr, " ", message);
            moqInfo.Verify(x => x.Invoke(expected), Times.Once);
        }

        [Theory]
        [InlineData(0, 0, true)]
        [InlineData(0, 1, true)]
        [InlineData(1, 1, true)]
        [InlineData(1, 2, false)]
        public void When_EventId_Changes_Output_Is_Written_When_Matching_Or_Omitted(int configuredEventId, int loggedEventId, bool expectsOutput)
        {
            var options = CreateLoggerConfigMonitor();
            options.CurrentValue.EventId = configuredEventId;

            var moqInfo = new Mock<Action<string>>();
            var provider = new Mock<LoRaConsoleLoggerProvider>(options);

            var logger = new TestLoRaConsoleLogger(moqInfo.Object, null, provider.Object);

            logger.LogInformation(loggedEventId, "Test");

            moqInfo.Verify(x => x.Invoke(It.IsAny<string>()), expectsOutput ? Times.Once : Times.Never);
        }

        [Fact]
        public void When_Structured_Parameters_Are_Passed_They_Are_Replaced()
        {
            var options = CreateLoggerConfigMonitor();
            var moqInfo = new Mock<Action<string>>();
            var provider = new Mock<LoRaConsoleLoggerProvider>(options);

            var logger = new TestLoRaConsoleLogger(moqInfo.Object, null, provider.Object);

            logger.LogInformation("{Id}", 1);

            moqInfo.Verify(x => x.Invoke(It.Is<string>(x => x == "1")), Times.Once);
        }

        [Fact]
        public void When_Error_Is_Logged_Target_Is_StdErr()
        {
            var options = CreateLoggerConfigMonitor();

            var moqErr = new Mock<Action<string>>();
            var provider = new Mock<LoRaConsoleLoggerProvider>(options);

            var logger = new TestLoRaConsoleLogger(null, moqErr.Object, provider.Object);

            logger.LogError("Test");

            moqErr.Verify(x => x.Invoke(It.IsAny<string>()), Times.Once);
        }
    }

    public class LoRaConsoleLoggerProviderTests : LoRaConsoleLoggerTestBase
    {
        [Fact]
        public void When_Using_Provider_LoRaConsoleLogger_Is_Created()
        {
            using var provider = new LoRaConsoleLoggerProvider(CreateLoggerConfigMonitor());
            var logger = provider.CreateLogger("Test");
            Assert.IsType<LoRaConsoleLogger>(logger);
        }

        [Fact]
        public void When_Using_Requesting_Same_Logger_Category_Same_Logger_Is_Used()
        {
            const string category = "Test";
            using var provider = new LoRaConsoleLoggerProvider(CreateLoggerConfigMonitor());
            var logger = provider.CreateLogger(category);
            Assert.Equal(logger, provider.CreateLogger(category));
        }

        [Fact]
        public void When_Configuration_Changes_Change_Is_Populated()
        {
            var options = CreateLoggerConfigMonitor();
            options.CurrentValue.LogLevel = LogLevel.Debug;
            using var provider = new LoRaConsoleLoggerProvider(options);

            Assert.Equal(LogLevel.Debug, provider.LogLevel);

            options.Update(new LoRaLoggerConfiguration { LogLevel = LogLevel.Critical });

            Assert.Equal(LogLevel.Critical, provider.LogLevel);
        }
    }

    public abstract class LoRaConsoleLoggerTestBase
    {
        internal static TestLoRaConsoleLoggerOptionsMonitor CreateLoggerConfigMonitor(LoRaLoggerConfiguration config = null)
        {
            config ??= new LoRaLoggerConfiguration() { LogLevel = LogLevel.Trace };
            return new TestLoRaConsoleLoggerOptionsMonitor(config);
        }
    }

    internal class TestLoRaConsoleLoggerOptionsMonitor : IOptionsMonitor<LoRaLoggerConfiguration>
    {
        public TestLoRaConsoleLoggerOptionsMonitor(LoRaLoggerConfiguration config)
        {
            CurrentValue = config;
        }

        private Action<LoRaLoggerConfiguration, string> listener;
        public LoRaLoggerConfiguration CurrentValue { get; private set; }
        public LoRaLoggerConfiguration Get(string name)
        {
            return CurrentValue;
        }

        public void Update(LoRaLoggerConfiguration config)
        {
            CurrentValue = config;
            this.listener?.Invoke(config, null);
        }

        public IDisposable OnChange(Action<LoRaLoggerConfiguration, string> listener)
        {
            if (this.listener != null) throw new InvalidOperationException("Only one listener is supported");
            this.listener = listener;
            return new NopDisposable();
        }

        private class NopDisposable : IDisposable
        {
            public void Dispose()
            {
            }
        }
    }

    internal class TestLoRaConsoleLogger : LoRaConsoleLogger
    {
        private readonly Action<string> onWrite;
        private readonly Action<string> onWriteError;

        public TestLoRaConsoleLogger(Action<string> onWrite, Action<string> onWriteError, LoRaConsoleLoggerProvider provider)
            : base(provider)
        {
            this.onWrite = onWrite;
            this.onWriteError = onWriteError;
        }

        protected override void ConsoleWrite(string message)
        {
            this.onWrite(message);
        }

        protected override void ConsoleWriteError(string message)
        {
            this.onWriteError(message);
        }
    }
}
