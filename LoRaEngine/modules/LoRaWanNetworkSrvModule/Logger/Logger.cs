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
                        udpEndpoint = new IPEndPoint(IPAddress.Parse(configuration.LogToUdpAddress), configuration.LogToUdpPort);
                    }
                    
                    udpClient = new UdpClient();
                    udpClient.ExclusiveAddressUse = false;

                    Console.WriteLine(string.Concat(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")," Logging to Udp: ", udpEndpoint.ToString()));
                }
                catch (Exception ex)
                {
                    Console.WriteLine(string.Concat(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")," Error starting UDP logging: ", ex.ToString()));
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
                    Console.WriteLine(String.Concat(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")," ", msg));

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
    }
}


