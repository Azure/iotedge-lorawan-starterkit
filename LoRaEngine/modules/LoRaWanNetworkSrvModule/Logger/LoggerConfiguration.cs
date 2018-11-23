using Microsoft.Azure.Devices.Client;

namespace LoRaWan
{
    // Defines the logger configuration
    public class LoggerConfiguration
    {
        // Gets/sets the module client
         public ModuleClient ModuleClient { get; set; }

        // Gets/sets if logging to console is enabled
        // Default: true
        public bool LogToConsole { get;  set; } = true;

        // Gets/sets the logging level
        // Default: 0 (Always logging)
        public int LogLevel { get;  set; } = 0;

        // Gets/sets if logging to IoT Hub is enabled
        // Default: false
        public bool LogToHub { get;  set; }

        // Gets/sets if logging to udp is enabled (used for integration tests mainly)
        // Default: false
        public bool LogToUdp { get; set; }

        // Gets/sets udp address to send log
        public string LogToUdpAddress { get; set; }

        // Gets/sets udp port to send logs
        public int LogToUdpPort { get; set; } = 6000;
    }

}