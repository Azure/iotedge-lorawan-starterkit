

using Microsoft.Azure.Devices.Client;
using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace LoRaWan
{

    public class Logger
    {

        public enum LoggingLevel : int { Always = 0, Full, Info, Error };

        private static ModuleClient edgeModuleClient;

        const int PORT = 1234;

        static UdpClient udpClient;

        static bool logToTest = false;

        public static void Init(ModuleClient moduleClient)
        {
            edgeModuleClient = moduleClient;
            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("LOG_TO_TEST")))
            {
                logToTest = true;
                IPEndPoint endPoint = new IPEndPoint(IPAddress.Any, PORT);
                udpClient = new UdpClient(endPoint);
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

            int loggingLevelSetting = (int)LoggingLevel.Error;

            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("LOG_TO_HUB")))
                logToHub = bool.Parse(Environment.GetEnvironmentVariable("LOG_TO_HUB"));


            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("LOG_LEVEL")))
                loggingLevelSetting = int.Parse(Environment.GetEnvironmentVariable("LOG_LEVEL"));

            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("LOG_TO_CONSOLE")))
                logToConsole = bool.Parse(Environment.GetEnvironmentVariable("LOG_TO_CONSOLE"));

            if ((int)loggingLevel >= loggingLevelSetting || loggingLevel == LoggingLevel.Always)
            {

                string msg = "";

                if (string.IsNullOrEmpty(deviceId))
                    msg = message;
                else
                    msg = $"{deviceId}: {message}";

                if (logToHub || logToTest)
                {
                    var msgProp = new Message(UTF8Encoding.ASCII.GetBytes(msg));
                    msgProp.Properties.Add("log", "1");
                    if (edgeModuleClient != null)
                        edgeModuleClient.SendEventAsync(msgProp);
                    if (logToTest)
                    {
                        if (udpClient != null)
                            UdpSendMessage(Encoding.UTF8.GetBytes(msg)).GetAwaiter().GetResult();
                    }
                }
                if (logToConsole)
                    Console.WriteLine(msg);
            }            
        }

        public static async Task UdpSendMessage(byte[] messageToSend)
        {
            if (messageToSend != null && messageToSend.Length != 0)
            {
                await udpClient.SendAsync(messageToSend, messageToSend.Length, IPAddress.Broadcast.ToString(), PORT);
            }
        }
    }
}
