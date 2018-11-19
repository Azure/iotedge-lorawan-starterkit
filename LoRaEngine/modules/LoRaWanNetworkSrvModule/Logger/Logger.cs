using Microsoft.Azure.Devices.Client;
using System;
using System.Text;

namespace LoRaWan
{
 
    public class Logger
    {

        public enum LoggingLevel : int { Always=0, Full, Info, Error };

        private static ModuleClient edgeModuleClient;
        private static bool logToUdp;
        private static string logUdpServer;

        public static void Init(ModuleClient moduleClient)
        {
            edgeModuleClient = moduleClient;
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

            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("LOG_TO_UDP")))
            {
                if (bool.TryParse(Environment.GetEnvironmentVariable("LOG_TO_UDP"), out var logToUdpEnvValue))
                {
                    logToUdp = logToUdpEnvValue;
                    if (logToUdp)
                    {
                        logUdpServer = Environment.GetEnvironmentVariable("LOG_UDP_SERVER");

                        if (string.IsNullOrEmpty(logUdpServer))
                        {
                            logUdpServer = "127.0.0.1";
                        }
                    }
                }
            } 



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
                    Console.WriteLine(msg);

                if (logToUdp)
                    LogToUDP(msg);
            }
        }

        // Send log to 
        private static void LogToUDP(string msg)
        {
            throw new NotImplementedException();
        }
    }
}


