using System;
using System.Collections.Generic;
using System.Text;

namespace LoRaWANDevice
{
    public partial class Generator
    {
        public class Switches
        {
            public bool Backup { get; set; }
            public bool ConfigFile { get; set; }
            public string ConfigFilePath { get; set; }
            public string IoTHubConnectionString { get; set; }
            public string BlobStorageConnectionString { get; set; }
            public NodeActivationMethod ActivationMethod { get; set; }

            public Switches()
            {
                Backup = false;
                ConfigFile = false;
                IoTHubConnectionString = "";
                BlobStorageConnectionString = "";
            }
        }

        public class ConfigFile
        {
            public string IoTHubConnectionString;
            public string BlobStorageAccountName;
            public string BlobStorageKeyValue;
            public NodeActivationMethod ActivationMethod;
            public char NodeClass;
            public string GatewayId;
            public string SensorDecoder;
            public bool Backup;
            public int NumberOfNodes;
            public string DevEUIFile;
            public string AppEUI;
            public string AppKey;
            public string AppSKey;
            public string NwkSKey;


            public ConfigFile()
            {
                IoTHubConnectionString = "<iothub connection string>";
                BlobStorageAccountName = "<blob storage account name>";
                BlobStorageKeyValue = "<blob storage key value>";
                ActivationMethod = NodeActivationMethod.OTAA;
                NodeClass = 'A';
                DevEUIFile = "";
                AppEUI = "";
                AppKey = "";
                AppSKey = "";
                NwkSKey = "";
                GatewayId = "<gateway id>";
                SensorDecoder = "<sensor decoder>";
                NumberOfNodes = 1;
            }
        }
    }
}
