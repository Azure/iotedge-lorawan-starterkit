// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan
{
    using System;
    using System.Net;
    using System.Net.Sockets;
    using System.Text;
    using System.Threading;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Extensions.Logging;

    public class Logger
    {
        // Interval where we try to estabilish connection to udp logger
        const int RETRY_UDP_LOG_CONNECTION_INTERVAL_IN_MS = 1000 * 10;

        public static LogLevel LoggerLevel => (LogLevel)configuration.LogLevel;

        static LoggerConfiguration configuration = new LoggerConfiguration();
        static volatile UdpClient udpClient;
        static IPEndPoint udpEndpoint;
        static volatile bool isInitializeUdpLoggerRunning = false;
        private static Timer retryUdpLogInitializationTimer;

        public static void Init(LoggerConfiguration loggerConfiguration)
        {
            configuration = loggerConfiguration;

            if (configuration.LogToUdp)
            {
                InitializeUdpLogger(isRetry: false);

                // If udp client was not created set a timer to retry every x seconds
                // The listening container might not have started yet (i.e. AzureDevOpsAgent)
                if (udpClient == null)
                {
                    // retry in 10 seconds
                    retryUdpLogInitializationTimer = new Timer(
                        (state) =>
                        {
                            InitializeUdpLogger(isRetry: true);
                        },
                        null,
                        RETRY_UDP_LOG_CONNECTION_INTERVAL_IN_MS,
                        RETRY_UDP_LOG_CONNECTION_INTERVAL_IN_MS);
                }
            }
        }

        public static void LogAlways(string message)
        {
            LogAlways(null, message);
        }

        public static void LogAlways(string deviceId, string message)
        {
            Log(null, message, LogLevel.Critical);
        }

        public static void Log(string message, LogLevel logLevel)
        {
            Log(null, message, logLevel);
        }

        /// <summary>
        /// Use this if you want to serialize an object to JSON and
        /// append it to the message. The serialization will only take place
        /// if the logLevel is larger or equal to the configured level
        /// </summary>
        /// <param name="deviceId">DeviceEUI</param>
        /// <param name="message">The message that should be prepended to the serialized string</param>
        /// <param name="toJson">The object to serialize</param>
        /// <param name="logLevel">The desired <see cref="LogLevel"/></param>
        public static void Log(string deviceId, string message, object toJson, LogLevel logLevel)
        {
            if (logLevel >= configuration.LogLevel)
            {
                var serializedObj = Newtonsoft.Json.JsonConvert.SerializeObject(toJson);
                Log(deviceId, string.Concat(message, serializedObj), logLevel);
            }
        }

        public static void Log(string deviceId, string message, LogLevel logLevel)
        {
            if (logLevel >= configuration.LogLevel)
            {
                var msg = message;

                if (!string.IsNullOrEmpty(deviceId))
                    msg = $"{deviceId}: {message}";

                if (configuration.LogToHub && configuration.ModuleClient != null)
                {
                    configuration.ModuleClient.SendEventAsync(new Message(UTF8Encoding.ASCII.GetBytes(msg)));
                }

                if (configuration.LogToConsole)
                    LogToConsole(msg, logLevel);

                if (udpClient != null)
                    LogToUdp(msg);
            }
        }

        static void LogToConsole(string message, LogLevel logLevel = LogLevel.Information)
        {
            if (logLevel == LogLevel.Error)
            {
                Console.Error.WriteLine(string.Concat(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"), " ", message));
            }
            else
            {
                Console.WriteLine(string.Concat(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"), " ", message));
            }
        }

        static void LogToUdp(string message)
        {
            try
            {
                if (!string.IsNullOrEmpty(configuration.GatewayId))
                {
                    message = string.Concat($"[{configuration.GatewayId}] ", message);
                }

                var messageInBytes = Encoding.UTF8.GetBytes(message);
                udpClient.Send(messageInBytes, messageInBytes.Length, udpEndpoint);
            }
            catch (Exception ex)
            {
                LogToConsole(string.Concat(" Error logging to UDP: ", ex.ToString()), LogLevel.Error);
            }
        }

        // Initialize Udp Logger
        // Might be called from a timer while it does not work
        // Need to make this retries because NetworkServer might become alive
        // before listening container is started
        static void InitializeUdpLogger(bool isRetry = false)
        {
            if (isInitializeUdpLoggerRunning)
            {
                LogToConsole("Retrying to connect to UDP log server skipped, one already running");
                return;
            }

            if (isRetry)
                LogToConsole("Retrying to connect to UDP log server");

            try
            {
                if (string.IsNullOrEmpty(configuration.LogToUdpAddress))
                {
                    udpEndpoint = new IPEndPoint(IPAddress.Broadcast, configuration.LogToUdpPort);
                }
                else
                {
                    if (IPAddress.TryParse(configuration.LogToUdpAddress, out var parsedIpAddress))
                    {
                        udpEndpoint = new IPEndPoint(parsedIpAddress, configuration.LogToUdpPort);
                    }
                    else
                    {
                        try
                        {
                            // try to parse the address as dns
                            var addresses = Dns.GetHostAddresses(configuration.LogToUdpAddress);
                            if (addresses == null || addresses.Length == 0)
                            {
                                LogToConsole($"Could not resolve ip address from '{configuration.LogToUdpAddress}'", LogLevel.Error);
                            }
                            else
                            {
                                udpEndpoint = new IPEndPoint(addresses[0], configuration.LogToUdpPort);
                            }
                        }
                        catch (Exception ex)
                        {
                            LogToConsole($"Could not resolve ip address from '{configuration.LogToUdpAddress}'. {ex.Message}", LogLevel.Error);
                        }
                    }
                }

                if (udpEndpoint != null)
                {
                    udpClient = new UdpClient
                    {
                        ExclusiveAddressUse = false
                    };

                    LogToConsole(string.Concat("Logging to Udp: ", udpEndpoint.ToString()));

                    if (retryUdpLogInitializationTimer != null)
                    {
                        retryUdpLogInitializationTimer.Dispose();
                        retryUdpLogInitializationTimer = null;
                    }
                }
            }
            catch (Exception ex)
            {
                LogToConsole(string.Concat("Error starting UDP logging: ", ex.ToString()), LogLevel.Error);
            }
            finally
            {
                isInitializeUdpLoggerRunning = false;
            }
        }
    }
}
