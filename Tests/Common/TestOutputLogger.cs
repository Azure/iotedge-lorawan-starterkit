// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

namespace LoRaWan.Tests.Common
{
    using System;
    using LoRaWan.NetworkServer;
    using Microsoft.Extensions.Logging;
    using Xunit.Abstractions;

    /// <summary>
    /// Logger class that offers integration with XUnit's <see cref="ITestOutputHelper"/>.
    /// It forwards log statements directly to <see cref="ITestOutputHelper"/> without taking into account scope information.
    /// </summary>
    public sealed class TestOutputLogger<T> : ILogger<T>
    {
        private const LogLevel TestLogLevel = LogLevel.Debug;

        private readonly ITestOutputHelper testOutputHelper;

        public TestOutputLogger(ITestOutputHelper testOutputHelper) =>
            this.testOutputHelper = testOutputHelper;

        public IDisposable BeginScope<TState>(TState state) => NullDisposable.Instance;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= TestLogLevel;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel)) return;
            var message = formatter(state, exception);
            this.testOutputHelper.WriteLine(message);
        }
    }
}
