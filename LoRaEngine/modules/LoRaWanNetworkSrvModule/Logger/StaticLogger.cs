// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan
{
    using Microsoft.Extensions.Logging;

    public static class StaticLogger
    {
        public static LogLevel LoggerLevel => sink.LogLevel;

        private static readonly ILogSink sink = new ConsoleLogSink(LogLevel.Error);

        public static void Log(string message, LogLevel logLevel) =>
            sink?.Log(message, logLevel);

        public static void Log(string deviceId, string message, LogLevel logLevel) =>
            sink?.Log(deviceId, message, logLevel);
    }
}
