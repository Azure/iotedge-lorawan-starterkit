using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Extensions.Configuration;

namespace LoRaWan.IntegrationTest
{
    public class TestConfiguration
    {
        public static TestConfiguration GetConfiguration()
        {
            var result = new TestConfiguration();

            new ConfigurationBuilder()
                .AddJsonFile("appsettings.json", optional: true)
                .AddJsonFile("appsettings.local.json", optional: true)
                .AddEnvironmentVariables()
                .Build()
                .GetSection("testConfiguration")
                .Bind(result);

            return result;
        }



        public string IoTHubEventHubConnectionString { get; set; }
        public string IoTHubConnectionString { get; set; }
        public int EnsureHasEventDelayBetweenReadsInSeconds { get; set; } = 2;
        public int EnsureHasEventMaximumTries { get; set; } = 5;
        public string IoTHubEventHubConsumerGroup { get; set; } = "$Default";

        public string LeafDeviceSerialPort { get; set; } = "/dev/ttyACM";

        public string LeafDeviceId { get; set; }
        public string LeafDeviceAppKey { get; set; }
        public string LeafDeviceAppEui { get; set; }
    }

}
