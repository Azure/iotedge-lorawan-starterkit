using System;
using Newtonsoft.Json;
using Microsoft.Azure.Devices;
using Microsoft.Azure.Devices.Shared;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.Auth;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Azure.Devices.Common;

namespace LoRaWANDevice
{
    public partial class Generator
    {

        public Switches Configuration;
        private ConfigFile config;
        private bool ValidConfig = false;

        public Generator()
        {
            Configuration = new Switches();
        }

        public int LoadValidateConfigFile(string filepath)
        {
            ValidConfig = false;
            // Deserialise JSON file for processing
            try
            {
                using (StreamReader file = File.OpenText(filepath))
                {
                    JsonSerializer serializer = new JsonSerializer();
                    config = (ConfigFile)serializer.Deserialize(file, typeof(ConfigFile));
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.Write("JSON Configuration File Failure:");
                System.Diagnostics.Debug.WriteLine(ex.Message);
                return 1;
            }

            // ToDo: Validate loaded config file here
            // Review the config file to determine it matches one of the 3 use cases defined

            ValidConfig = true;
            return 0;
        }


        public async Task Provision()
        {
            if (!ValidConfig)
            {
                return;
            }

            var deviceToAdd = new ExportImportDevice(); ;
            ExportImportDevice.PropertyContainer con = new ExportImportDevice.PropertyContainer();

            Node x = new Node();
            x.Class = config.NodeClass;
            x.GatewayId = config.GatewayId;
            x.SensorDecoder = config.SensorDecoder;
            deviceToAdd.Id = CredentialGenerator.genEUI();

            // ToDo: Check for devEui file provided and consume
            // x.DevEuiFile = file path
            // Does it exist?
            // Repeat node creation for each devEui provided
            // Number of devEui may or may not match requested node numbers
            x.DevEui = deviceToAdd.Id;

            x.AppEui = CredentialGenerator.genEUI();
            x.AppKey = CredentialGenerator.genKey();

            // ToDo: Parameterise netId in config file
            byte[] netId = new byte[3] { 0, 0, 1 };

            byte[] NwkSKeyType = new byte[1] { 0x01 };
            byte[] AppSKeyType = new byte[1] { 0x02 };

            var serializedDevices = new List<string>();

            for (int i = 0; i < config.NumberOfNodes; i++)
            {

                deviceToAdd.Id = CredentialGenerator.genEUI();

                deviceToAdd.Authentication = new AuthenticationMechanism()
                {
                    SymmetricKey = new SymmetricKey()
                    {
                        PrimaryKey = CryptoKeyGenerator.GenerateKey(32),
                        SecondaryKey = CryptoKeyGenerator.GenerateKey(32)
                    }
                };

                deviceToAdd.ImportMode = ImportMode.Create;

                if (config.ActivationMethod == NodeActivationMethod.ABP)
                {
                    byte[] devNonce = CredentialGenerator.StringToByteArray(CredentialGenerator.genNonce());
                    byte[] appNonce = CredentialGenerator.StringToByteArray(CredentialGenerator.genNonce());

                    x.NwkSKey = CredentialGenerator.genKey(NwkSKeyType, appNonce, netId, devNonce, CredentialGenerator.StringToByteArray(x.AppKey));
                    x.AppSkey = CredentialGenerator.genKey(AppSKeyType, appNonce, netId, devNonce, CredentialGenerator.StringToByteArray(x.AppKey));
                    x.DeviceAddr = CredentialGenerator.genDevAddr(netId);

                    string json = JsonConvert.SerializeObject(x);
                    con.DesiredProperties = new TwinCollection(json, "{}");
                    deviceToAdd.Properties = con;

                    serializedDevices.Add(JsonConvert.SerializeObject(deviceToAdd));
                }

                else if (config.ActivationMethod == NodeActivationMethod.OTAA)
                {

                    string json = JsonConvert.SerializeObject(x);
                    con.DesiredProperties = new TwinCollection(json, "{}");
                    deviceToAdd.Properties = con;

                    serializedDevices.Add(JsonConvert.SerializeObject(deviceToAdd));
                }
            }

            int result = await ApplyDeviceList(serializedDevices, config);

        }

        private async Task<int> ApplyDeviceList( List<string> serializedDevices, ConfigFile config)
        {
            // Now put serializedDevices into blob storage and set up rest.
            // Write the list to the blob
            var sb = new StringBuilder();
            serializedDevices.ForEach(serializedDevice => sb.AppendLine(serializedDevice));

            StorageCredentials storageCredentials = new StorageCredentials(config.BlobStorageAccountName, config.BlobStorageKeyValue);

            CloudStorageAccount storageAccount = new CloudStorageAccount(storageCredentials, true);

            CloudBlobClient blobClient = storageAccount.CreateCloudBlobClient();


            CloudBlobContainer provisionContainer = blobClient.GetContainerReference("provision");
            provisionContainer.CreateIfNotExists();

            CloudBlobContainer backupContainer = blobClient.GetContainerReference("backup");
            backupContainer.CreateIfNotExists();

            CloudBlockBlob blob = provisionContainer.GetBlockBlobReference("devices.txt");


            // This crashes code: Issue with await/async pattern ??
            //await blob.DeleteIfExistsAsync();

            var b = blob.DeleteIfExistsAsync().Result;

            using (CloudBlobStream stream = await blob.OpenWriteAsync())
            {
                byte[] bytes = Encoding.UTF8.GetBytes(sb.ToString());
                for (var i = 0; i < bytes.Length; i += 500)
                {
                    int length = Math.Min(bytes.Length - i, 500);
                    await stream.WriteAsync(bytes, i, length);
                }

            }

            string inputContainerSasUri = GetContainerSasUri(provisionContainer);
            string outputContainerSasUri = GetContainerSasUri(backupContainer);

            RegistryManager registryManager = RegistryManager.CreateFromConnectionString(config.IoTHubConnectionString);

            try
            {
                JobProperties importJob = registryManager.ImportDevicesAsync(inputContainerSasUri, outputContainerSasUri).Result;

                // Wait until job is finished
                while (true)
                {
                    importJob = registryManager.GetJobAsync(importJob.JobId).Result;
                    if (importJob.Status == JobStatus.Completed ||
                        importJob.Status == JobStatus.Failed ||
                        importJob.Status == JobStatus.Cancelled)
                    {
                        // Job has finished executing
                        break;
                    }

                    Task.Delay(TimeSpan.FromSeconds(5)).Wait();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex.Message);
                return 1;
            }
            return 0;
        }

        public void MakeConfigFile(string filepath)
        {
            ConfigFile config = new ConfigFile();
            JsonSerializer serializer = new JsonSerializer();
            serializer.NullValueHandling = NullValueHandling.Ignore;
            using (StreamWriter sw = new StreamWriter(filepath))
            using (JsonWriter writer = new JsonTextWriter(sw))
            {
                // Fails to write anything in the file
                serializer.Serialize(writer, config);
            }
        }

        public bool ABPNodeCreate(byte[] devAddr, string DevEui, string AppSKey, string NwkSKey, string GatewayId, string SensorDecoder)
        {
            if(SensorDecoder==null || GatewayId == null)
            {
                return false;
            }

            Node newNode = new Node();

            if (DevEui == "" || DevEui == null)
            {
                // Generate DevEui
            }
            if (AppSKey == "" || AppSKey == null)
            {

            }
            if(NwkSKey == "" || NwkSKey == null)
            {

            }

            // Validate Node

            // Send Node to IoT Hub

            return true;

        }

        public bool OTAANodeCreate(string DevEui, string AppEui, string AppKey)
        {
            Node newNode = new Node();

            if(DevEui == "" || DevEui==null)
            {
                // Generate DevEui
            }

            if(AppEui == "" || AppEui ==null)
            {
                // Generate AppEui
            }

            if(AppKey == "" || AppKey == null)
            {
                // Generate AppKey
            }

            // Validate Node

            // Send Node to IoT Hub

            return true;
        }

        static string GetContainerSasUri(CloudBlobContainer container)
        {
            // Set the expiry time and permissions for the container.
            // In this case no start time is specified, so the
            // shared access signature becomes valid immediately.
            var sasConstraints = new SharedAccessBlobPolicy();
            sasConstraints.SharedAccessExpiryTime = DateTime.UtcNow.AddHours(10);
            sasConstraints.Permissions =
              SharedAccessBlobPermissions.Write |
              SharedAccessBlobPermissions.Read |
              SharedAccessBlobPermissions.Create |
              SharedAccessBlobPermissions.Delete;

            string sasContainerToken = container.GetSharedAccessSignature(sasConstraints);

            return container.Uri + sasContainerToken;

        }

    }

}
