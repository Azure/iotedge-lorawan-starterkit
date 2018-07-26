using Microsoft.Azure.Devices;
using Microsoft.Azure.Devices.Client;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace LoRaWan.NetworkServer
{
    public class LoraDeviceInfoManager
    {
        public static string FacadeServerUrl;
        public static string FacadeAuthCode;

        public LoraDeviceInfoManager()
        {

        }

        public static async Task<LoraDeviceInfo> GetLoraDeviceInfoAsync(string DevAddr)
        {
            var client = new HttpClient();

            var url = $"{FacadeServerUrl}GetNwkSKeyAppSKey?code={FacadeAuthCode}&devAddr={DevAddr}";

            HttpResponseMessage response = await client.GetAsync(url);
            var result = response.Content.ReadAsStringAsync().Result;

            LoraDeviceInfo loraDeviceInfo = (LoraDeviceInfo)JsonConvert.DeserializeObject(result, typeof(LoraDeviceInfo));

            return loraDeviceInfo;
        }

        public static async Task<LoraDeviceInfo> PerformOTAAAsync(string DevEUI, string AppEUI, string DevNonce)
        {
            var client = new HttpClient();         

            var url = $"{FacadeServerUrl}PerformOTAA?code={FacadeAuthCode}&DevEUI={DevEUI}&DevNonce={DevNonce}&AppEUI={AppEUI}";

            HttpResponseMessage response = await client.GetAsync(url);
            var result = response.Content.ReadAsStringAsync().Result;

            LoraDeviceInfo loraDeviceInfo = (LoraDeviceInfo)JsonConvert.DeserializeObject(result, typeof(LoraDeviceInfo));

            return loraDeviceInfo;
        }


    }

}