//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using PacketManager;

namespace LoRaWan.NetworkServer
{
    public class IoTHubDeviceInfo
    {
        public string DevAddr;
        public string DevEUI;
        public string PrimaryKey;
    }
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
            var url = $"{FacadeServerUrl}GetDevice?code={FacadeAuthCode}&devAddr={DevAddr}";
            HttpResponseMessage response = await client.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                Logger.Log(DevAddr, $"error calling façade api: {response.ReasonPhrase} check the azure function log", Logger.LoggingLevel.Error);
                return null;
            }

            var result = response.Content.ReadAsStringAsync().Result;
            //TODO enable multi devices with the same devaddr
            IoTHubDeviceInfo iotHubDeviceInfo = ((List<IoTHubDeviceInfo>)JsonConvert.DeserializeObject(result, typeof(List<IoTHubDeviceInfo>)))[0];
            LoraDeviceInfo loraDeviceInfo = new LoraDeviceInfo();
            loraDeviceInfo.HubSender = new IoTHubSender(iotHubDeviceInfo.DevEUI, iotHubDeviceInfo.PrimaryKey);

            var twin = await loraDeviceInfo.HubSender.GetTwinAsync();
            loraDeviceInfo.DevAddr = DevAddr;
            loraDeviceInfo.DevEUI = twin.DeviceId;
            //ABP Case
            if (twin.Properties.Desired.Contains("AppSKey"))
            {
                loraDeviceInfo.AppSKey = twin.Properties.Desired["AppSKey"].Value;
                loraDeviceInfo.NwkSKey = twin.Properties.Desired["NwkSKey"].Value;
            }
            //OTAA Case
            else if (twin.Properties.Reported.Contains("AppSKey"))
            {
                loraDeviceInfo.AppSKey = twin.Properties.Reported["AppSKey"].Value;
                loraDeviceInfo.NwkSKey = twin.Properties.Reported["NwkSKey"].Value;
                loraDeviceInfo.AppEUI = twin.Properties.Desired["AppEUI"].Value;
                //todo check if appkey and appeui is needed in the flow
                loraDeviceInfo.AppKey = twin.Properties.Desired["AppKey"].Value;
                loraDeviceInfo.DevNonce = twin.Properties.Reported["DevNonce"].Value;
            }
            else
            {
                Logger.Log(loraDeviceInfo.DevEUI, $"AppSKey not present neither in Desired or in Reported properties", Logger.LoggingLevel.Error);
            }

            if (twin.Properties.Desired.Contains("GatewayID"))
                loraDeviceInfo.GatewayID = twin.Properties.Desired["GatewayID"].Value;
            if (twin.Properties.Desired.Contains("SensorDecoder"))
                loraDeviceInfo.SensorDecoder = twin.Properties.Desired["SensorDecoder"].Value;

            loraDeviceInfo.IsOurDevice = true;
            if (twin.Properties.Reported.Contains("FCntUp"))
                loraDeviceInfo.FCntUp = twin.Properties.Reported["FCntUp"];
            if (twin.Properties.Reported.Contains("FCntDown"))
            {
                loraDeviceInfo.FCntDown = twin.Properties.Reported["FCntDown"];
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
        public static async Task<LoraDeviceInfo> PerformOTAAAsync(string GatewayID, string DevEUI, string AppEUI, string DevNonce)
        {
            string AppKey;
            string AppSKey;
            string NwkSKey;
            string DevAddr;
            string AppNonce;
            //todo fix this 
            bool returnAppSKey = true;

            if (DevEUI == null || AppEUI == null || DevNonce == null)
            {
                string errorMsg = "Missing devEUI/AppEUI/DevNonce in the OTAARequest";
                //log.Error(errorMsg);
                Logger.Log(DevEUI, errorMsg, Logger.LoggingLevel.Error);
                return null;
            }

            var client = new HttpClient();
            var url = $"{FacadeServerUrl}GetDevice?code={FacadeAuthCode}&devEUI={DevEUI}";
            HttpResponseMessage response = await client.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                Logger.Log(DevEUI, $"error calling façade api: {response.ReasonPhrase} check the azure function log", Logger.LoggingLevel.Error);
                return null;
            }

            var result = response.Content.ReadAsStringAsync().Result;

            IoTHubDeviceInfo iotHubDeviceInfo = ((List<IoTHubDeviceInfo>)JsonConvert.DeserializeObject(result, typeof(List<IoTHubDeviceInfo>)))[0];
            LoraDeviceInfo loraDeviceInfo = new LoraDeviceInfo();
            loraDeviceInfo.DevEUI = DevEUI;
            loraDeviceInfo.PrimaryKey = iotHubDeviceInfo.PrimaryKey;

            loraDeviceInfo.HubSender = new IoTHubSender(loraDeviceInfo.DevEUI, loraDeviceInfo.PrimaryKey);

            var twin = await loraDeviceInfo.HubSender.GetTwinAsync();
            if (twin != null)
            {
                loraDeviceInfo.IsOurDevice = true;
                //Make sure that there is the AppEUI and it matches if not we cannot do the OTAA
                if (!twin.Properties.Desired.Contains("AppEUI"))
                {
                    string errorMsg = $"Missing AppEUI for OTAA for device";
                    //log.Error(errorMsg);
                    Logger.Log(DevEUI, errorMsg, Logger.LoggingLevel.Error);
                    return null;
                }
                else
                {
                    if (twin.Properties.Desired["AppEUI"].Value != AppEUI)
                    {
                        string errorMsg = $"AppEUI for OTAA does not match for device";
                        //log.Error(errorMsg);
                        Logger.Log(DevEUI, errorMsg, Logger.LoggingLevel.Error);
                        return null;
                    }
                }

                //Make sure that there is the AppKey if not we cannot do the OTAA
                if (!twin.Properties.Desired.Contains("AppKey"))
                {
                    string errorMsg = $"Missing AppKey for OTAA for device";
                    //log.Error(errorMsg);
                    Logger.Log(DevEUI, errorMsg, Logger.LoggingLevel.Error);
                    return null;
                }
                else
                {
                    AppKey = twin.Properties.Desired["AppKey"].Value;
                }

                //Make sure that is a new request and not a replay
                if (twin.Properties.Reported.Contains("DevNonce"))
                {
                    if (twin.Properties.Reported["DevNonce"] == DevNonce)
                    {
                        //TODO check if logic is correct here. 
                        string errorMsg = $"DevNonce already used for device";
                        Logger.Log(DevEUI, errorMsg, Logger.LoggingLevel.Info);
                        loraDeviceInfo.DevAddr = DevNonce;
                        loraDeviceInfo.IsJoinValid = false;
                        return loraDeviceInfo;
                    }
                }
                //Check that the device is joining through the linked gateway and not another
                if (twin.Properties.Desired.Contains("GatewayID"))
                {
                    if (!String.IsNullOrEmpty(twin.Properties.Desired["GatewayID"].Value) && twin.Properties.Desired["GatewayID"].Value.ToUpper() != GatewayID.ToUpper())
                    {
                        string errorMsg = $"Not the right gateway device-gateway:{twin.Properties.Desired["GatewayID"].Value} current-gateway:{GatewayID}";
                        Logger.Log(errorMsg, Logger.LoggingLevel.Info);
                        loraDeviceInfo.DevAddr = DevNonce;
                        if (twin.Properties.Desired.Contains("GatewayID"))
                            loraDeviceInfo.GatewayID = twin.Properties.Desired["GatewayID"].Value;
                        loraDeviceInfo.IsJoinValid = false;
                        return loraDeviceInfo;
                    }
                }
                byte[] netId = new byte[3] { 0, 0, 1 };
                AppNonce = OTAAKeysGenerator.getAppNonce();
                AppSKey = OTAAKeysGenerator.calculateKey(new byte[1] { 0x02 }, OTAAKeysGenerator.StringToByteArray(AppNonce), netId, OTAAKeysGenerator.StringToByteArray(DevNonce), OTAAKeysGenerator.StringToByteArray(AppKey));
                NwkSKey = OTAAKeysGenerator.calculateKey(new byte[1] { 0x01 }, OTAAKeysGenerator.StringToByteArray(AppNonce), netId, OTAAKeysGenerator.StringToByteArray(DevNonce), OTAAKeysGenerator.StringToByteArray(AppKey)); ;
                DevAddr = OTAAKeysGenerator.getDevAddr(netId);
                loraDeviceInfo.DevAddr = DevAddr;
                loraDeviceInfo.AppKey = AppKey;
                loraDeviceInfo.NwkSKey = NwkSKey;
                loraDeviceInfo.AppSKey = AppSKey;
                loraDeviceInfo.AppNonce = AppNonce;
                loraDeviceInfo.AppEUI = AppEUI;
                loraDeviceInfo.NetId = BitConverter.ToString(netId).Replace("-", ""); ;
                //Accept the JOIN Request and the futher messages
                loraDeviceInfo.IsJoinValid = true;
                if (twin.Properties.Desired.Contains("GatewayID"))
                    loraDeviceInfo.GatewayID = twin.Properties.Desired["GatewayID"].Value;
                if (twin.Properties.Desired.Contains("SensorDecoder"))
                    loraDeviceInfo.SensorDecoder = twin.Properties.Desired["SensorDecoder"].Value;
            }
            else
            {
                loraDeviceInfo.IsOurDevice = false;
            }


            return loraDeviceInfo;
        }
    }




}