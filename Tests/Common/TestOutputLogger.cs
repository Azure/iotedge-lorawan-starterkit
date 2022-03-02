// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

namespace LoRaWan.Tests.Common
{
    using System;
    using System.Collections.Concurrent;
    using LoRaTools;
    using Microsoft.Extensions.Logging;
    using Xunit.Abstractions;

    /// <summary>
    /// Logger class that offers integration with XUnit's <see cref="ITestOutputHelper"/>.
    /// It forwards log statements directly to <see cref="ITestOutputHelper"/> without taking into account scope information.
    /// It does not support category names or event IDs.
    /// </summary>
    public class TestOutputLogger : ILogger
    {
        private const LogLevel TestLogLevel = LogLevel.Debug;

        private readonly ITestOutputHelper testOutputHelper;

        public TestOutputLogger(ITestOutputHelper testOutputHelper) =>
            this.testOutputHelper = testOutputHelper;

        public IDisposable BeginScope<TState>(TState state) => NoopDisposable.Instance;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= TestLogLevel;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (formatter is null) throw new ArgumentNullException(nameof(formatter));
            if (!IsEnabled(logLevel)) return;

            var message = formatter(state, exception);

            try
            {
                this.testOutputHelper.WriteLine(message);
            }
            catch (InvalidOperationException)
            {
                // best-effort logging in case testOutputHelper has already been disposed. Fixes:
                // https://github.com/Azure/iotedge-lorawan-starterkit/issues/1554.
            }
        }
    }

    public sealed class TestOutputLogger<T> : TestOutputLogger, ILogger<T>
    {
        public TestOutputLogger(ITestOutputHelper testOutputHelper) : base(testOutputHelper) { }
    }

    public sealed class TestOutputLoggerFactory : ILoggerFactory
    {
        private readonly TestOutputLoggerProvider testOutputLoggerProvider;

        public TestOutputLoggerFactory(ITestOutputHelper testOutputHelper) =>
            this.testOutputLoggerProvider = new TestOutputLoggerProvider(testOutputHelper);

        public void AddProvider(ILoggerProvider provider)
        {
            // Only (and always) supports the TestOutputLoggerProvider.
        }

        public ILogger CreateLogger(string categoryName) =>
            this.testOutputLoggerProvider.CreateLogger(categoryName);

        public void Dispose() => this.testOutputLoggerProvider.Dispose();

        private sealed class TestOutputLoggerProvider : ILoggerProvider
        {
            private readonly ITestOutputHelper testOutputHelper;
            private readonly ConcurrentDictionary<string, TestOutputLogger> loggers = new();

            public TestOutputLoggerProvider(ITestOutputHelper testOutputHelper) =>
                this.testOutputHelper = testOutputHelper;

            public ILogger CreateLogger(string categoryName) =>
                this.loggers.GetOrAdd(categoryName, _ => new TestOutputLogger(this.testOutputHelper));

            public void Dispose() => this.loggers.Clear();
        }
    }
}
