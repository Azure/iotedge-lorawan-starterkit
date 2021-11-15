// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan
{
    using System;
    using System.Net;
    using System.Net.Sockets;
    using Microsoft.Extensions.Logging;

    public static class Logger
    {
        public static LogLevel LoggerLevel => sink.LogLevel;

        private static ILogSink sink = new ConsoleLogSink(LogLevel.Error);

        public static void Init(LoggerConfiguration configuration,
                                ILogger<TcpLogSink> tcpLogSinkLogger = null)
        {
            if (configuration == null) throw new ArgumentNullException(nameof(configuration));

            var consoleLogSink = configuration.LogToConsole
                               ? new ConsoleLogSink(configuration.LogLevel) : null;

            IPEndPoint endPoint = null;

#pragma warning disable format
            if (configuration is { LogToTcp       : true,
                                   LogToTcpAddress: { Length: > 0 } address,
                                   LogToTcpPort   : var port and > 0 })
#pragma warning restore format
            {
                endPoint = Resolve(address) is { } someAddress
                         ? new IPEndPoint(someAddress, port)
                         : null;
            }

            var tcpLogSink
                = endPoint is { } someEndPoint
#pragma warning disable CA2000 // Dispose objects before losing scope
                ? TcpLogSink.Start(someEndPoint, configuration.LogLevel,
                                   formatter: string.IsNullOrEmpty(configuration.GatewayId)
                                              ? null
                                              : msg => $"[{configuration.GatewayId}] {msg}",
                                   logger: tcpLogSinkLogger)
                : null;
#pragma warning restore CA2000 // Dispose objects before losing scope

            try
            {
                (sink as IDisposable)?.Dispose();
                sink = CompositeLogSink.Choose(consoleLogSink, tcpLogSink);
            }
            catch
            {
                tcpLogSink?.Dispose();
                throw;
            }

            static IPAddress Resolve(string address)
            {
                if (IPAddress.TryParse(address, out var ipAddress))
                {
                    return ipAddress;
                }

                string message;

                try
                {
                    // try to parse the address as dns
                    var addresses = Dns.GetHostAddresses(address);
                    if (addresses.Length > 0)
                        return addresses[0];

                    message = $"Could not resolve IP address of '{address}'";
                }
                catch (SocketException ex)
                {
                    message = $"An error occurred trying to resolve '{address}'. {ex.Message}";
                }
                catch (ArgumentException ex)
                {
                    message = $"'{address}' is an invalid IP address. {ex.Message}";
                }

                Log(message, LogLevel.Error);
                return null;
            }
        }

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
