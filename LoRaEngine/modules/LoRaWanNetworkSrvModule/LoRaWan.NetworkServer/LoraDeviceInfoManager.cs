//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using LoRaTools;
using LoRaTools.Utils;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace LoRaWan.NetworkServer
{

    public class LoraDeviceInfoManager
    {
        public string FacadeServerUrl;
        public string FacadeAuthCode;
        private readonly NetworkServerConfiguration configuration;

        public LoraDeviceInfoManager(NetworkServerConfiguration configuration)
        {
            this.configuration = configuration;
        }

        public async Task<ushort> NextFCntDown(string DevEUI, ushort FCntDown, ushort FCntUp, string GatewayId)
        {
            Logger.Log(DevEUI, $"syncing FCntDown for multigateway", Logger.LoggingLevel.Info);
            var client = GetHttpClient();
            var url = $"{FacadeServerUrl}NextFCntDown?code={FacadeAuthCode}&DevEUI={DevEUI}&FCntDown={FCntDown}&FCntUp={FCntUp}&GatewayId={GatewayId}";
            HttpResponseMessage response = await client.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {

                Logger.Log(DevEUI, $"error calling the NextFCntDown function, check the function log", Logger.LoggingLevel.Error);
                return 0;

            }

            string fcntDownString = await response.Content.ReadAsStringAsync();

            //todo ronnie check for fcnt above ushort
            ushort newFCntDown = ushort.Parse(fcntDownString);

            return newFCntDown;

        }

        public async Task<bool> ABPFcntCacheReset(string DevEUI)
        {
            Logger.Log(DevEUI, $"ABP FCnt cache reset for multigateway", Logger.LoggingLevel.Info);
            var client = GetHttpClient();
            var url = $"{FacadeServerUrl}NextFCntDown?code={FacadeAuthCode}&DevEUI={DevEUI}&ABPFcntCacheReset=true";
            HttpResponseMessage response = await client.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {

                Logger.Log(DevEUI, $"error calling the NextFCntDown function, check the function log", Logger.LoggingLevel.Error);
                return false;

            }



            return true;

        }

        public async Task<LoraDeviceInfo> GetLoraDeviceInfoAsync(string DevAddr, string GatewayId)
        {
            var client = GetHttpClient();
            var url = $"{FacadeServerUrl}GetDevice?code={FacadeAuthCode}&DevAddr={DevAddr}&GatewayId={GatewayId}";
            HttpResponseMessage response = await client.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {

                Logger.Log(DevAddr, $"error calling façade api: {response.ReasonPhrase} check the azure function log", Logger.LoggingLevel.Error);
                return null;
            }

            var result = await response.Content.ReadAsStringAsync();
            //TODO enable multi devices with the same devaddr

            List<IoTHubDeviceInfo> iotHubDeviceInfos = ((List<IoTHubDeviceInfo>)JsonConvert.DeserializeObject(result, typeof(List<IoTHubDeviceInfo>)));

            LoraDeviceInfo loraDeviceInfo = new LoraDeviceInfo();
            loraDeviceInfo.DevAddr = DevAddr;


            //we did not find a device with this devaddr so we assume is not ours
            if (iotHubDeviceInfos.Count == 0)
            {
                loraDeviceInfo.IsOurDevice = false;
            }
            else
            {


                IoTHubDeviceInfo iotHubDeviceInfo = iotHubDeviceInfos[0];

                loraDeviceInfo.DevEUI = iotHubDeviceInfo.DevEUI;
                loraDeviceInfo.PrimaryKey = iotHubDeviceInfo.PrimaryKey;


                loraDeviceInfo.HubSender = new IoTHubConnector(iotHubDeviceInfo.DevEUI, iotHubDeviceInfo.PrimaryKey, this.configuration);




                var twin = await loraDeviceInfo.HubSender.GetTwinAsync();

                if (twin != null)
                {
                    //ABP Case
                    if (twin.Properties.Desired.Contains("AppSKey"))
                    {

                        loraDeviceInfo.AppSKey = twin.Properties.Desired["AppSKey"];
                        loraDeviceInfo.NwkSKey = twin.Properties.Desired["NwkSKey"];
                        loraDeviceInfo.DevAddr = twin.Properties.Desired["DevAddr"];


                    }
                    //OTAA Case
                    else if (twin.Properties.Reported.Contains("AppSKey"))
                    {
                        loraDeviceInfo.AppSKey = twin.Properties.Reported["AppSKey"];
                        loraDeviceInfo.NwkSKey = twin.Properties.Reported["NwkSKey"];
                        loraDeviceInfo.DevAddr = twin.Properties.Reported["DevAddr"];
                        loraDeviceInfo.DevNonce = twin.Properties.Reported["DevNonce"];

                        //todo check if appkey and appeui is needed in the flow
                        loraDeviceInfo.AppEUI = twin.Properties.Desired["AppEUI"];
                        loraDeviceInfo.AppKey = twin.Properties.Desired["AppKey"];


                    }
                    else
                    {
                        Logger.Log(loraDeviceInfo.DevEUI, $"AppSKey not present neither in Desired or in Reported properties", Logger.LoggingLevel.Error);
                    }

                    if (twin.Properties.Desired.Contains("GatewayID"))
                        loraDeviceInfo.GatewayID = twin.Properties.Desired["GatewayID"];
                    if (twin.Properties.Desired.Contains("SensorDecoder"))
                        loraDeviceInfo.SensorDecoder = twin.Properties.Desired["SensorDecoder"];


                    loraDeviceInfo.IsOurDevice = true;


                    if (twin.Properties.Reported.Contains("FCntUp"))
                        loraDeviceInfo.FCntUp = twin.Properties.Reported["FCntUp"];
                    if (twin.Properties.Reported.Contains("FCntDown"))
                        loraDeviceInfo.FCntDown = twin.Properties.Reported["FCntDown"];


                    Logger.Log(loraDeviceInfo.DevEUI, $"done getting twins", Logger.LoggingLevel.Info);
                }
            }

            return loraDeviceInfo;
        }

        /// <summary>
        /// Code Performing the OTAA
        /// </summary>
        /// <param name="GatewayID"></param>
        /// <param name="DevEUI"></param>
        /// <param name="AppEUI"></param>
        /// <param name="DevNonce"></param>
        /// <returns></returns>
        public async Task<LoraDeviceInfo> PerformOTAAAsync(string GatewayID, string DevEUI, string AppEUI, string DevNonce, LoraDeviceInfo joinLoraDeviceInfo)
        {

            string AppSKey;
            string NwkSKey;
            string DevAddr;
            string AppNonce;
            IoTHubDeviceInfo iotHubDeviceInfo;

            if (DevEUI == null || AppEUI == null || DevNonce == null)
            {
                string errorMsg = "Missing devEUI/AppEUI/DevNonce in the OTAARequest";
                //log.Error(errorMsg);
                Logger.Log(DevEUI, errorMsg, Logger.LoggingLevel.Error);
                return null;
            }


            if (joinLoraDeviceInfo == null)
            {
                joinLoraDeviceInfo = new LoraDeviceInfo();
            }

            joinLoraDeviceInfo.DevEUI = DevEUI;


            ////we don't have the key to access iot hub query the registry
            //if (joinLoraDeviceInfo.PrimaryKey == null)
            //{

            Logger.Log(DevEUI, $"querying the registry for device key", Logger.LoggingLevel.Info);
            var client = GetHttpClient();
            var url = String.Concat($"{FacadeServerUrl}GetDevice?", $"{FacadeAuthCode}"=="" ? "" : $"code={FacadeAuthCode}&", $"DevEUI={DevEUI}&DevNonce={DevNonce}&GatewayId={GatewayID}");
   
            HttpResponseMessage response = await client.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                if (response.StatusCode == System.Net.HttpStatusCode.BadRequest)
                {
                    var badReqResult = await response.Content.ReadAsStringAsync();

                    if (!String.IsNullOrEmpty(badReqResult) && badReqResult == "UsedDevNonce")
                    {
                        Logger.Log(DevEUI, $"DevNonce already used by this device", Logger.LoggingLevel.Info);
                        return null;
                    }
                }


                Logger.Log(DevEUI, $"error calling façade api: {response.ReasonPhrase} check the azure function log", Logger.LoggingLevel.Error);
                return null;
            }

            var result = await response.Content.ReadAsStringAsync();

            List<IoTHubDeviceInfo> iotHubDeviceInfos = ((List<IoTHubDeviceInfo>)JsonConvert.DeserializeObject(result, typeof(List<IoTHubDeviceInfo>)));
            if (iotHubDeviceInfos.Count == 0)
            {
                joinLoraDeviceInfo.IsJoinValid = false;
                joinLoraDeviceInfo.IsOurDevice = false;
                return joinLoraDeviceInfo;
            }
            else
            {
                iotHubDeviceInfo = iotHubDeviceInfos[0];
                joinLoraDeviceInfo.PrimaryKey = iotHubDeviceInfo.PrimaryKey;
            }
            //}


            joinLoraDeviceInfo.HubSender = new IoTHubConnector(joinLoraDeviceInfo.DevEUI, joinLoraDeviceInfo.PrimaryKey, this.configuration);


            //we don't have yet the twin data so we need to get it 
            if (joinLoraDeviceInfo.AppKey == null || joinLoraDeviceInfo.AppEUI == null)
            {

                Logger.Log(DevEUI, $"getting twins for OTAA for device", Logger.LoggingLevel.Info);

                var twin = await joinLoraDeviceInfo.HubSender.GetTwinAsync();
                if (twin != null)
                {
                    joinLoraDeviceInfo.IsOurDevice = true;

                    if (!twin.Properties.Desired.Contains("AppEUI"))
                    {
                        string errorMsg = $"missing AppEUI for OTAA for device";
                        Logger.Log(DevEUI, errorMsg, Logger.LoggingLevel.Error);
                        return null;
                    }
                    else
                    {
                        joinLoraDeviceInfo.AppEUI = twin.Properties.Desired["AppEUI"].Value;
                    }

                    //Make sure that there is the AppKey if not we cannot do the OTAA
                    if (!twin.Properties.Desired.Contains("AppKey"))
                    {
                        string errorMsg = $"missing AppKey for OTAA for device";
                        Logger.Log(DevEUI, errorMsg, Logger.LoggingLevel.Error);
                        return null;
                    }
                    else
                    {
                        joinLoraDeviceInfo.AppKey = twin.Properties.Desired["AppKey"].Value;
                    }

                    //Make sure that is a new request and not a replay
                    if (twin.Properties.Reported.Contains("DevNonce"))
                    {
                        joinLoraDeviceInfo.DevNonce = twin.Properties.Reported["DevNonce"];
                    }

                    if (twin.Properties.Desired.Contains("GatewayID"))
                    {
                        joinLoraDeviceInfo.GatewayID = twin.Properties.Desired["GatewayID"].Value;
                    }

                    if (twin.Properties.Desired.Contains("SensorDecoder"))
                    {
                        joinLoraDeviceInfo.SensorDecoder = twin.Properties.Desired["SensorDecoder"].Value;
                    }

                    Logger.Log(DevEUI, $"done getting twins for OTAA device", Logger.LoggingLevel.Info);

                }
                else
                {
                    Logger.Log(DevEUI, $"failed getting twins for OTAA device", Logger.LoggingLevel.Error);
                    return null;
                }


            }
            else
            {
                Logger.Log(DevEUI, $"using cached twins for OTAA device", Logger.LoggingLevel.Info);
            }

            //We add it to the cache so the next join has already the data, important for offline
            Cache.AddToCache(DevEUI, joinLoraDeviceInfo);

            //Make sure that there is the AppEUI and it matches if not we cannot do the OTAA
            if (joinLoraDeviceInfo.AppEUI != AppEUI)
            {
                string errorMsg = $"AppEUI for OTAA does not match for device";
                Logger.Log(DevEUI, errorMsg, Logger.LoggingLevel.Error);
                return null;
            }

            //Make sure that is a new request and not a replay         
            if (!String.IsNullOrEmpty(joinLoraDeviceInfo.DevNonce) && joinLoraDeviceInfo.DevNonce == DevNonce)
            {

                string errorMsg = $"DevNonce already used by this device";
                Logger.Log(DevEUI, errorMsg, Logger.LoggingLevel.Info);
                joinLoraDeviceInfo.IsJoinValid = false;
                return joinLoraDeviceInfo;
            }


            //Check that the device is joining through the linked gateway and not another

            if (!String.IsNullOrEmpty(joinLoraDeviceInfo.GatewayID) && joinLoraDeviceInfo.GatewayID.ToUpper() != GatewayID.ToUpper())
            {
                string errorMsg = $"not the right gateway device-gateway:{joinLoraDeviceInfo.GatewayID} current-gateway:{GatewayID}";
                Logger.Log(DevEUI,errorMsg, Logger.LoggingLevel.Info);
                joinLoraDeviceInfo.IsJoinValid = false;
                return joinLoraDeviceInfo;
            }

            byte[] netId = new byte[3] { 0, 0, 1 };
            AppNonce = OTAAKeysGenerator.getAppNonce();
            AppSKey = OTAAKeysGenerator.calculateKey(new byte[1] { 0x02 }, ConversionHelper.StringToByteArray(AppNonce), netId, ConversionHelper.StringToByteArray(DevNonce), ConversionHelper.StringToByteArray(joinLoraDeviceInfo.AppKey));
            NwkSKey = OTAAKeysGenerator.calculateKey(new byte[1] { 0x01 }, ConversionHelper.StringToByteArray(AppNonce), netId, ConversionHelper.StringToByteArray(DevNonce), ConversionHelper.StringToByteArray(joinLoraDeviceInfo.AppKey)); ;
            DevAddr = OTAAKeysGenerator.getDevAddr(netId);
            joinLoraDeviceInfo.DevAddr = DevAddr;
            joinLoraDeviceInfo.NwkSKey = NwkSKey;
            joinLoraDeviceInfo.AppSKey = AppSKey;
            joinLoraDeviceInfo.AppNonce = AppNonce;
            joinLoraDeviceInfo.DevNonce = DevNonce;
            joinLoraDeviceInfo.NetId = BitConverter.ToString(netId).Replace("-", ""); ;
            //Accept the JOIN Request and the futher messages
            joinLoraDeviceInfo.IsJoinValid = true;

            return joinLoraDeviceInfo;
        }

        HttpClient GetHttpClient()
        {
            HttpClient httpClient;

            if (!string.IsNullOrEmpty(this.configuration.HttpsProxy))
            {
                var webProxy = new WebProxy(
                    new Uri(this.configuration.HttpsProxy),
                    BypassOnLocal: false);

                var proxyHttpClientHandler = new HttpClientHandler
                {
                    Proxy = webProxy,
                    UseProxy = true,
                };

                httpClient = new HttpClient(proxyHttpClientHandler);
            }
            else
            {
                httpClient = new HttpClient();
            }

            return httpClient;
        }
    }
}