using Microsoft.Azure.Devices;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace LoRaWan.NetworkServer
{
    public class LoaraKeysManager
    {

        private bool registryMode = false;
        private RegistryManager registryManager;
        public LoaraKeysManager(bool registrymode, string connectionString)
        {
            registryMode = registrymode;

            if (registryMode)
                registryManager = RegistryManager.CreateFromConnectionString(connectionString);

        }

        public async Task<LoraKeys> GetKeys(string DevAddr)
        {
            if (registryMode)
                return await GetKeysRegistryMode(DevAddr);
            else
                return null;
        }

        private async Task<LoraKeys> GetKeysRegistryMode(string DevAddr)
        {

            //need to fix sql injection attack
            var query = registryManager.CreateQuery($"SELECT * FROM devices WHERE tags.DevAddr = '{DevAddr}'", 1);

            LoraKeys loraKeys = new LoraKeys();
            loraKeys.DevAddr = DevAddr;


            while (query.HasMoreResults)
            {
                var page = await query.GetNextAsTwinAsync();
                foreach (var twin in page)
                {
                    loraKeys.DeviceEUI = twin.DeviceId;
                    loraKeys.AppSKey = twin.Tags["AppSKey"].Value;
                    loraKeys.NwkSKey = twin.Tags["NwkSKey"].Value;
                    loraKeys.Accept = true;
                }
            }

            return loraKeys;

        }
    }



    public class LoraKeys
    {
        public string DevAddr;
        public string DeviceEUI;
        public string NwkSKey;
        public string AppSKey;
        public bool Accept = false;
    }
}
