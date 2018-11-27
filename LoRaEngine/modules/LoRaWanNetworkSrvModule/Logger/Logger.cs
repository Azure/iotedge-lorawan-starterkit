using Microsoft.Azure.Devices.Client;
using System;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace LoRaWan
{ 
    public class Logger
    {
        public enum LoggingLevel : int { Always=0, Full, Info, Error };

        static LoggerConfiguration configuration = new LoggerConfiguration();
        static UdpClient udpClient;
        static IPEndPoint udpEndpoint;

        public static void Init(LoggerConfiguration loggerConfiguration)
        {
            configuration = loggerConfiguration;

            if (configuration.LogToUdp)
            {
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
                    }

                    if (udpEndpoint == null)
                    {
                        LogToConsole($"Logging to Udp failed. Could not resolve ip address from '{configuration.LogToUdpAddress}'");
                    }
                    else
                    {                    
                        udpClient = new UdpClient();
                        udpClient.ExclusiveAddressUse = false;

                        LogToConsole(string.Concat("Logging to Udp: ", udpEndpoint.ToString()));
                    }
                }
                catch (Exception ex)
                {
                    LogToConsole(string.Concat("Error starting UDP logging: ", ex.ToString()));
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
                string msg = "";

                if (string.IsNullOrEmpty(deviceId))
                    msg = message;
                else
                    msg = $"{deviceId}: { message}";

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
        
        static void LogToUdp(string message)
        {            
            try
            {
                var messageInBytes = Encoding.UTF8.GetBytes(message);
                udpClient.Send(messageInBytes, messageInBytes.Length, udpEndpoint);
            }
            catch (Exception ex)
            {
                Console.WriteLine(string.Concat(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")," Error logging to UDP: ", ex.ToString()));
            }
        }

        static void LogToConsole(string message)
        {
            Console.WriteLine(String.Concat(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")," ", message));
        }
    }
}


