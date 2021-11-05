// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Unit.LoggerTests
{
    using System;
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

            const string devEUI = "12345678";
            const string message = "test";
            using var scope = logger.BeginDeviceScope(devEUI);
            logger.LogInformation(message);

            var expected = useScopes ? string.Concat(devEUI, " ", message) : message;
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

            const string devEUI = "12345678";
            const string message = devEUI + " test";
            using var scope = logger.BeginDeviceScope(devEUI);
            logger.LogInformation(message);

            moqInfo.Verify(x => x.Invoke(It.Is<string>(x => x == message)), Times.Once);
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

            options.Update(new LoRaConsoleLoggerConfiguration { LogLevel = LogLevel.Critical });

            Assert.Equal(LogLevel.Critical, provider.LogLevel);
        }
    }

    public abstract class LoRaConsoleLoggerTestBase
    {
        internal static TestLoRaConsoleLoggerOptionsMonitor CreateLoggerConfigMonitor(LoRaConsoleLoggerConfiguration config = null)
        {
            config ??= new LoRaConsoleLoggerConfiguration() { LogLevel = LogLevel.Trace };
            return new TestLoRaConsoleLoggerOptionsMonitor(config);
        }
    }

    internal class TestLoRaConsoleLoggerOptionsMonitor : IOptionsMonitor<LoRaConsoleLoggerConfiguration>
    {
        public TestLoRaConsoleLoggerOptionsMonitor(LoRaConsoleLoggerConfiguration config)
        {
            CurrentValue = config;
        }

        private Action<LoRaConsoleLoggerConfiguration, string> listener;
        public LoRaConsoleLoggerConfiguration CurrentValue { get; private set; }
        public LoRaConsoleLoggerConfiguration Get(string name)
        {
            return CurrentValue;
        }

        public void Update(LoRaConsoleLoggerConfiguration config)
        {
            CurrentValue = config;
            this.listener?.Invoke(config, null);
        }

        public IDisposable OnChange(Action<LoRaConsoleLoggerConfiguration, string> listener)
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
