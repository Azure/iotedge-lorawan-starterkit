// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

namespace LoRaWan.Tests.Common
{
    using System;
    using System.Collections.Generic;
    using LoRaWan.NetworkServer;
    using Microsoft.Extensions.Logging;

    public sealed class VerifiableLogger<T> : ILogger<T>
    {
        private readonly List<(string, Exception?)> logs = new();

        public IReadOnlyList<(string Message, Exception? Exception)> Logs => this.logs;

        public IDisposable BeginScope<TState>(TState state) => NoopDisposable.Instance;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            this.logs.Add((formatter(state, exception), exception));
    }
}
