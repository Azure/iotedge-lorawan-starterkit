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

    public class Logger
    {
        // Interval where we try to estabilish connection to udp logger
        const int RETRY_UDP_LOG_CONNECTION_INTERVAL_IN_MS = 1000 * 10;

        public enum LoggingLevel : int
        {
            Always = 0,
            Full,
            Info,
            Error
        }

        public static LoggingLevel LoggerLevel => (LoggingLevel)configuration.LogLevel;

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

        public static void Log(string message, LoggingLevel loggingLevel)
        {
            Log(null, message, loggingLevel);
        }

        public static void Log(string deviceId, string message, LoggingLevel loggingLevel)
        {
            if ((int)loggingLevel >= configuration.LogLevel || loggingLevel == LoggingLevel.Always)
            {
                var msg = message;

                if (!string.IsNullOrEmpty(deviceId))
                    msg = $"{deviceId}: {message}";

                if (configuration.LogToHub && configuration.ModuleClient != null)
                {
                    configuration.ModuleClient.SendEventAsync(new Message(UTF8Encoding.ASCII.GetBytes(msg)));
                }

                if (configuration.LogToConsole)
                    LogToConsole(msg);

                if (udpClient != null)
                    LogToUdp(msg);
            }
        }

        static void LogToConsole(string message)
        {
            Console.WriteLine(string.Concat(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"), " ", message));
        }

        static void LogToUdp(string message)
        {
            try
            {
                var messageInBytes = Encoding.UTF8.GetBytes(message);
                udpClient.Send(messageInBytes, messageInBytes.Length, udpEndpoint);
            }
            catch (Exception ex)
            {
                LogToConsole(string.Concat(" Error logging to UDP: ", ex.ToString()));
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
                                LogToConsole($"Could not resolve ip address from '{configuration.LogToUdpAddress}'");
                            }
                            else
                            {
                                udpEndpoint = new IPEndPoint(addresses[0], configuration.LogToUdpPort);
                            }
                        }
                        catch (Exception ex)
                        {
                            LogToConsole($"Could not resolve ip address from '{configuration.LogToUdpAddress}'. {ex.Message}");
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
                LogToConsole(string.Concat("Error starting UDP logging: ", ex.ToString()));
            }
            finally
            {
                isInitializeUdpLoggerRunning = false;
            }
        }
    }
}
