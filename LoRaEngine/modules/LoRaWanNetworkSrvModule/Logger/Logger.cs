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

        private static ModuleClient edgeModuleClient;

        static UdpClient udpClient;
        static IPEndPoint udpEndpoint;
        const int DEFAULT_LOG_UDP_PORT = 6000;

        public static void Init(ModuleClient moduleClient)
        {
            edgeModuleClient = moduleClient;

            
            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("LOG_TO_UDP")))
            {
                if (bool.TryParse(Environment.GetEnvironmentVariable("LOG_TO_UDP"), out var logToUDP) && logToUDP)
                {
                    if (!int.TryParse(Environment.GetEnvironmentVariable("LOG_TO_UDP_PORT"), out var logUdpPort))                    
                        logUdpPort = DEFAULT_LOG_UDP_PORT;
                    
                    try
                    {
                        var logUdpAddress = Environment.GetEnvironmentVariable("LOG_TO_UDP_ADDRESS");                    
                        if (string.IsNullOrEmpty(logUdpAddress))
                        {                        
                            udpEndpoint = new IPEndPoint(IPAddress.Broadcast, logUdpPort);
                        }
                        else
                        {
                            udpEndpoint = new IPEndPoint(IPAddress.Parse(logUdpAddress), logUdpPort);
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
        }

        public static void Log(string message, LoggingLevel loggingLevel)
        {
            Log(null, message, loggingLevel);
        }

        public static void Log(string deviceId, string message, LoggingLevel loggingLevel)
        {
            bool logToHub = false;
            bool logToConsole = true;
           

            int loggingLevelSetting = (int)LoggingLevel.Always;

            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("LOG_TO_HUB")))
                logToHub = bool.Parse(Environment.GetEnvironmentVariable("LOG_TO_HUB"));
            
            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("LOG_LEVEL")))
                loggingLevelSetting = int.Parse(Environment.GetEnvironmentVariable("LOG_LEVEL"));

            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("LOG_TO_CONSOLE")))
                logToConsole = bool.Parse(Environment.GetEnvironmentVariable("LOG_TO_CONSOLE"));
    

            if ((int)loggingLevel >= loggingLevelSetting  || loggingLevel == LoggingLevel.Always)
            {
                string msg = "";

                if (string.IsNullOrEmpty(deviceId))
                    msg = message;
                else
                    msg = $"{deviceId}: { message}";

                if (logToHub)
                {
                    if (edgeModuleClient != null)
                        edgeModuleClient.SendEventAsync(new Message(UTF8Encoding.ASCII.GetBytes(msg)));

                }
                if (logToConsole)
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


