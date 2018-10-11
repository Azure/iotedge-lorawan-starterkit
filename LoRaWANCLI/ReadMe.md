# LoRaWAN CLI
OneWeek Oct 2018


<b>Command line switches</b>

/backup, /b   -- Create a backup of existing deployed devices by exporting to blob storage TBD

/configfile, /c 'filename' -- Specified configuration file. Json file

/createconfigfile, /f 'filename' -- Create a template configuration file

/abp  -- Generate ABP device definitions TBD

/otaa  -- Generate OTAA device definitions TBD


<b>Configuration file format:</b>
```
            string IoTHubConnectionString;
            string BlobStorageAccountName;
            string BlobStorageKeyValue;
            NodeActivationMethod ActivationMethod;
            char NodeClass;
            string GatewayId;
            string SensorDecoder;
            bool Backup;
            int NumberOfNodes;
            string DevEUIFile;
            string AppEUI;
            string AppKey;
            string AppSKey;
            string NwkSKey;
```

<b>Default values:</b>
```
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
```
<b>ActivationMethod Enum {ABP, OTAA} (0, 1)</b>


