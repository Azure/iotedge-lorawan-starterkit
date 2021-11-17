// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan
{
    using Microsoft.Extensions.Logging;

    public static class StaticLogger
    {
        public static LogLevel LoggerLevel => sink.LogLevel;

        private static readonly ILogSink sink = new ConsoleLogSink(LogLevel.Error);

        public static void LogAlways(string message) =>
            sink?.LogAlways(message);

        public static void Log(string message, LogLevel logLevel) =>
            sink?.Log(message, logLevel);

        /// <summary>
        /// Use this if you want to serialize an object to JSON and
        /// append it to the message. The serialization will only take place
        /// if the logLevel is larger or equal to the configured level.
        /// </summary>
        /// <param name="deviceId">DeviceEUI.</param>
        /// <param name="message">The message that should be prepended to the serialized string.</param>
        /// <param name="toJson">The object to serialize.</param>
        /// <param name="logLevel">The desired <see cref="LogLevel"/>.</param>
        public static void Log(string deviceId, string message, object toJson, LogLevel logLevel) =>
            sink?.Log(deviceId, message, toJson, logLevel);

        public static void Log(string deviceId, string message, LogLevel logLevel) =>
            sink?.Log(deviceId, message, logLevel);
    }
}
