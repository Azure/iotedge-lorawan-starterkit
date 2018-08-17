//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//
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

           

            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine($"Error calling façade api: {response.ReasonPhrase} check the azure function log");
                return null;
            }

            var result = response.Content.ReadAsStringAsync().Result;

            LoraDeviceInfo loraDeviceInfo = (LoraDeviceInfo)JsonConvert.DeserializeObject(result, typeof(LoraDeviceInfo));

            return loraDeviceInfo;
        }

        public static async Task<LoraDeviceInfo> PerformOTAAAsync(string GatewayID, string DevEUI, string AppEUI, string DevNonce)
        {
            var client = new HttpClient();         

            var url = $"{FacadeServerUrl}PerformOTAA?code={FacadeAuthCode}&GatewayID={GatewayID}&DevEUI={DevEUI}&DevNonce={DevNonce}&AppEUI={AppEUI}";

            HttpResponseMessage response = await client.GetAsync(url);
           

            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine($"Error calling façade api: {response.ReasonPhrase} check the azure function log");
                return null;
            }

            var result = response.Content.ReadAsStringAsync().Result;

            LoraDeviceInfo loraDeviceInfo = (LoraDeviceInfo)JsonConvert.DeserializeObject(result, typeof(LoraDeviceInfo));

            return loraDeviceInfo;
        }


    }

}