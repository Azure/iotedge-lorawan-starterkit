//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//
using LoRaTools;
using LoRaTools.LoRaMessage;
using LoRaTools.LoRaPhysical;
using LoRaTools.Regions;
using LoRaTools.Utils;
using Microsoft.Azure.Devices.Client;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static LoRaTools.LoRaMessage.LoRaPayloadData;

namespace LoRaWan.NetworkServer
{
    public class MessageProcessor : IDisposable
    {
        // Defines Cloud to device message property containing fport value
        const string FPORT_MSG_PROPERTY_KEY = "fport";

        // Fport value reserved for mac commands
        const byte LORA_FPORT_RESERVED_MAC_MSG = 0;

        // Starting Fport value reserved for future applications
        const byte LORA_FPORT_RESERVED_FUTURE_START = 224;

        private DateTime startTimeProcessing;
        private List<byte[]> fOptsPending = new List<byte[]>();
        private readonly NetworkServerConfiguration configuration;
        private readonly LoraDeviceInfoManager loraDeviceInfoManager;

        public MessageProcessor(NetworkServerConfiguration configuration, LoraDeviceInfoManager loraDeviceInfoManager)
        {
            this.configuration = configuration;
            this.loraDeviceInfoManager = loraDeviceInfoManager;
        }
        public async Task<byte[]> ProcessMessageAsync(byte[] message)
        {
            startTimeProcessing = DateTime.UtcNow;
            LoRaMessageWrapper loraMessage = new LoRaMessageWrapper(message);
            if (loraMessage.IsLoRaMessage)
            {
                if (RegionFactory.CurrentRegion == null)
                    RegionFactory.Create(loraMessage.PktFwdPayload.GetPktFwdMessage().Rxpks[0]);
                //join message
                if (loraMessage.LoRaMessageType == LoRaMessageType.JoinRequest)
                {
                    return await ProcessJoinRequest(loraMessage);
                }
                //normal message
                else if (loraMessage.LoRaMessageType == LoRaMessageType.UnconfirmedDataUp || loraMessage.LoRaMessageType == LoRaMessageType.ConfirmedDataUp)
                {
                    return await ProcessLoraMessage(loraMessage);
                }
            }
            return null;
        }

        interface ILoRaDeviceRegistry
        {
            // Going to search devices in
            // 1. Cache
            // 2. If offline -> local storage (future functionality, reverse lookup)
            // 3. If online -> call function (return all devices with matching devaddr)
            // 3.1 Validate [mic, gatewayid]

            // In order to handle a scenario where the network server is restarted and the fcntDown was not yet saved (we save every 10)
            // If device does not have gatewayid this will be handled by the service facade function (NextFCntDown)
            // 4. if (loraDeviceInfo.IsABP() && loraDeviceInfo.GatewayID != null && loraDeviceInfo was not read from cache)  device.FcntDown += 10;


            Task<LoraDeviceInfo> GetDeviceForPayload(Rxpk rxpk);
        }

        ILoRaDeviceRegistry deviceRegistry;

        class PhysicalPayload
        {
            public Rxpk[] GetRxpks();
        }

        class Rxpk
        {
            public LoRaPayload LoRaPayload { get;  }

        }

        class LoRaPayload
        {
            public string DevAddr { get; }
            public UInt16 NetId { get; }
            public UInt32 FcntUp { get; }

            public bool IsConfirmed();

            public string GetDecryptedPayload(string AppSKey);
        }            

        Task<Txpk> ProcessLoraMessage2(Rxpk rxpk)
        {
            var loraPayload = rxpk.LoRaPayload;
            var devAddr = loraPayload.DevAddr;
            var netId = loraPayload.NetId;
            if (!ValidateNetId(netId))
            {
                Log("Invalid netid");
                return null;
            }


            // Find device that matches:
            // - devAddr
            // - mic check (requires: loraDeviceInfo.NwkSKey or loraDeviceInfo.AppKey, rxpk.LoraPayload.Mic)
            // - gateway id
            var loraDeviceInfo = deviceRegistry.GetDeviceForPayloadAsync(loraPayload);
            if (loraDeviceInfo == null)
                return null;

            // In order to handle a scenario where the network server is restarted and the fcntDown was not yet saved (we save every 10)
            // If device does not have gatewayid this will be handled by the service facade function (NextFCntDown)
            // here or at the deviceRegistry, what is better?
            if (loraDeviceInfo.IsABP() && loraDeviceInfo.GatewayID != null && loraDeviceInfo was not read from cache)  
                loraDeviceInfo.FcntDown += 10;


            //reply attack or confirmed reply

            // Confirmed resubmit means: A confirmed message was received previously but we did not answer in time
            // Device will send it again and we just need to return an ack (but also check for C2D to send it over)
            var isConfirmedResubmit = false;
            if (loraPayload.FcntUp <= loraDeviceInfo.FcntUp)
            {
                // Future: Keep track of how many times we acked the confirmed message (4+ times we skip)
                //if it is confirmed most probably we did not ack in time before or device lost the ack packet so we should continue but not send the msg to iothub 
                if (loraPayload.IsConfirmed() && loraPayload.FcntUp == loraDeviceInfo.FcntUp)
                {
                    isConfirmedResubmit = true;
                }
                else
                {
                    return null;
                }
            }


            var fcntDown = 0;

            // Leaf devices that restart lose the counter. In relax mode we accept the incoming frame counter
            // ABP device does not reset the Fcnt so in relax mode we should reset for 0 (LMIC based) or 1
            if (loraDeviceInfo.IsABP() && loraDeviceInfo.IsABPRelaxedFrameCounter() && loraDeviceInfo.FcntUp > 0 && loraPayload.FcntUp <= 1)
            {
                // known problem when device restarts, starts fcnt from zero
                loraDeviceInfo.SetFcntUp(0);
                loraDeviceInfo.SetFcntDown(0);

                _ = SaveFcnt(loraDeviceInfo, force: true);

                if (loraDeviceInfo.GatewayID == null)
                    await ABPFcntCacheReset(loraDeviceInfo);
            }

            // If it is confirmed it require us to update the frame counter down
            // Multiple gateways: in redis, otherwise in device twin
            if (loraPayload.IsConfirmed())
                fcntDown = NextFcntDown(loraDeviceInfo);


            var validFcntUp = loraPayload.FcntUp > loraDeviceInfo.FnctUp;
            if (validFcntUp)
            {
                // if it is an upward acknowledgement from the device it does not have a payload
                // This is confirmation from leaf device that he received a C2D confirmed
                if (!loraPayload.IsUpwardAcknowledgement())
                {
                    var decryptedPayload = loraPayload.GetDecryptedPayload(loraDeviceInfo.AppSKey);
                    var payload = DecodePayload(loraDeviceInfo, decryptedPayload);
                }                


                // What do we need to send an UpAck to IoT Hub?
                // What is the content of the message
                // TODO Future: No wait if it is an unconfirmed message
                await = SendD2CAsync();
            }

            //we check if we have time to futher progress or not
            //C2D checks are quite expensive so if we are really late we just stop here            
            var timePassed = (now - start);
            var timeToSecondWindow = (timePassed - GetReceiveDelay1(RegionFactory.CurrentRegion, loraDeviceInfo.ReceiveDelay1) + GetReceiveDelay2(RegionFactory.CurrentRegion, loraDeviceInfo.ReceiveDelay2));
            if (timeToSecondWindow < TIME_TO_PACKAGE_AND_SEND_MSG)
                return null;

            if (loraPayload.IsConfirmed() && timeToSecondWindow <= MINIMUM_EXPECTED_TIME_TO_CHECK_C2D_MSG) // around 200ms
            {
                _ = SaveFcnt(loraDeviceInfo, force: false);
                return new Txpk(rxpk, ReceiveWindow.Second);
            }

            // ReceiveAsync has a longer timeout
            // But we wait less that the timeout (available time before 2nd window)
            // if message is received after timeout, keep it in loraDeviceInfo and return the next call
            timePassed = (now - start);
            timeToSecondWindow = (timePassed - loraDeviceInfo.ReceiveDelay1 + loraDeviceInfo.ReceiveDelay2);
            var c2dMsg = await CheckC2D(waitTime: timeToSecondWindow - MINIMUM_EXPECTED_TIME_TO_CHECK_C2D_MSG);
            if (c2dMsg != null && !ValidateCloudToDeviceMessage(loraDeviceInfo, c2dMsg))
            {
                // complete message and set to null
            }

            var payloadToDevice = new LoRaPayloadData(loraPayload.IsConfirmed() ? LoRaMessageType.ConfirmedDataDown : LoRaMessageType.UnconfirmedDataDown);

            if (c2dMsg != null)
            {
                // The message coming from the device was not confirmed, therefore we did not computed the frame count down
                // Now we need to increment because there is a C2D message to be sent
                if (!loraPayload.IsConfirmed())
                    fcntDown = NextFcntDown(loraDeviceInfo);

                timePassed = (now - start);
                timeToSecondWindow = (timePassed - loraDeviceInfo.ReceiveDelay1 + loraDeviceInfo.ReceiveDelay2);
                if (timeToSecondWindow > TIME_TO_PACKAGE_AND_SEND_MSG)
                {
                    var additionalMsg = await CheckC2D(waitTime: timeToSecondWindow - MINIMUM_EXPECTED_TIME_TO_CHECK_C2D_MSG);
                    if (additionalMsg != null)
                    {
                        payloadToDevice.FPending = true;
                        _ = additionalMsg.AbandonAsync();
                    }
                }

                // prepare message to device
                payloadToDevice.SetData(c2dMsg.Body, loraDeviceInfo.DevAddr, loraDeviceInfo.AppSKey);
                payloadToDevice.FportDown = (byte)(c2dMsg.Properties["fport"]);
                if (c2dMsg.Properties["confirmed"] == "true")
                    payloadToDevice.SetConfirmed();
                
            }

            if (!rxpk.IsConfirmed() && c2dMsg == null)
            {
                await SaveFnct(loraDeviceInfo, force: false);
                return null;
            }

            // We did it in the LoRaPayloadData constructor
            // we got here:
            // a) was a confirm request
            // b) we have a c2d message
            //if (rxpk.IsConfirmed())
            //    txpk.SetAsAcknoledgement();


            timePassed = (now - start);

            ReceiveWindow downReceiveWindow = ReceiveWindow.FirstWindow;
            if (!loraDeviceInfo.AlwaysUseSecondWindow && timePassed < loraDeviceInfo.ReceiveDelay1)
                downReceiveWindow = ReceiveWindow.FirstWindow;
            else if (timePassed < loraDeviceInfo.ReceiveDelay1 + loraDeviceInfo.ReceiveDelay2)
                downReceiveWindow = ReceiveWindow.SecondWindow;
            else
            {
                // TODO: verify if we should call Abandon message
                return null;
            }

            _ = CompleteC2D();
            _ = SaveFcnt(loraDeviceInfo, force: false);

            return Txpk.Create(downReceiveWindow, payloadToDevice, loraDeviceInfo.NwkSKey);
            
            return txpk;
 
        }

        int NextFcntDown(LoraDeviceInfo device)
        {
            // make it thread safe
            if (GatewayID == DeviceGatewayID)
                device.FcntDown++
           else
                device.FcntDown = AzureFunctionNextFcntDown(DevEUI)

           return device.FcntDown;
        }

        SaveFcnt(LoraDeviceInfo loraDeviceInfo, bool force)
        {
            if (loraDeviceInfo.FCntUp % 10 == 0 || force)
            {
                SaveTwin(loraDeviceInfo);
            }
        }

        Task UpdateFcntDown(LoraDeviceInfo loraDevice)
        {
                if (loraDeviceInfo.Gateway == this.Gateway)
                    loraDevice.FCntDown++;

        }

        private async Task<byte[]> ProcessLoraMessage(LoRaMessageWrapper loraMessage)
        {
            bool validFrameCounter = false;
            byte[] udpMsgForPktForwarder = new byte[0];
            string devAddr = ConversionHelper.ByteArrayToString(loraMessage.LoRaPayloadMessage.DevAddr.ToArray());
            Message c2dMsg = null;
            LoraDeviceInfo loraDeviceInfo = null;
            if (Cache.TryGetRequestValue(devAddr, out ConcurrentDictionary<string,LoraDeviceInfo> loraDeviceInfoCacheList))
            {
                loraDeviceInfo = loraDeviceInfoCacheList.Values.FirstOrDefault(x => 
                x.NwkSKey!=null?loraMessage.CheckMic(x.NwkSKey):false);     
            }
            if (loraDeviceInfo == null)
            {
                loraDeviceInfo = await this.loraDeviceInfoManager.GetLoraDeviceInfoAsync(devAddr, this.configuration.GatewayID, loraMessage);
                if (loraDeviceInfo != null&&loraDeviceInfo.DevEUI!=null)
                {
                    if (loraDeviceInfo.DevEUI != null)
                        Logger.Log(loraDeviceInfo.DevEUI, $"processing message, device not in cache", Logger.LoggingLevel.Info);
                        if (loraDeviceInfo.IsOurDevice)
                    {
                        Cache.AddRequestToCache(devAddr, loraDeviceInfo);
                    }
                } 
               
            }
            else
            {
                Logger.Log(devAddr, $"processing message, device in cache", Logger.LoggingLevel.Info);
            }

            if (loraDeviceInfo != null && loraDeviceInfo.IsOurDevice)
            {
                //either there is no gateway linked to the device or the gateway is the one that the code is running
                if (string.IsNullOrEmpty(loraDeviceInfo.GatewayID) || string.Compare(loraDeviceInfo.GatewayID, this.configuration.GatewayID, ignoreCase: true) == 0)
                {
                    if (loraDeviceInfo.HubSender == null)
                    {
                        loraDeviceInfo.HubSender = new IoTHubConnector(loraDeviceInfo.DevEUI, loraDeviceInfo.PrimaryKey, this.configuration);
                    }
                    UInt16 fcntup = BitConverter.ToUInt16(loraMessage.LoRaPayloadMessage.GetLoRaMessage().Fcnt.ToArray(), 0);
                    byte[] linkCheckCmdResponse = null;

                    //check if the frame counter is valid: either is above the server one or is an ABP device resetting the counter (relaxed seqno checking)
                    if (fcntup > loraDeviceInfo.FCntUp || (fcntup == 0 && loraDeviceInfo.FCntUp == 0) || (fcntup == 1 && String.IsNullOrEmpty(loraDeviceInfo.AppEUI)))
                    {
                        //save the reset fcnt for ABP (relaxed seqno checking)
                        if (fcntup == 1 && String.IsNullOrEmpty(loraDeviceInfo.AppEUI))
                        {
                            _ = loraDeviceInfo.HubSender.UpdateFcntAsync(fcntup, 0, true);

                            //if the device is not attached to a gateway we need to reset the abp fcnt server side cache
                            if (String.IsNullOrEmpty(loraDeviceInfo.GatewayID))
                            {
                                bool rit = await this.loraDeviceInfoManager.ABPFcntCacheReset(loraDeviceInfo.DevEUI);
                            }
                        }

                        validFrameCounter = true;
                        Logger.Log(loraDeviceInfo.DevEUI, $"valid frame counter, msg: {fcntup} server: {loraDeviceInfo.FCntUp}", Logger.LoggingLevel.Info);

                        byte[] decryptedMessage = null;
                        try
                        {
                            decryptedMessage = loraMessage.DecryptPayload(loraDeviceInfo.AppSKey);
                        }
                        catch (Exception ex)
                        {
                            Logger.Log(loraDeviceInfo.DevEUI, $"failed to decrypt message: {ex.Message}", Logger.LoggingLevel.Error);
                        }
                        Rxpk rxPk = loraMessage.PktFwdPayload.GetPktFwdMessage().Rxpks[0];
                        dynamic fullPayload = JObject.FromObject(rxPk);
                        string jsonDataPayload = "";
                        uint fportUp = 0;
                        bool isAckFromDevice = false;
                        if (loraMessage.LoRaPayloadMessage.GetLoRaMessage().Fport.Span.Length > 0)
                        {
                            fportUp = (uint)loraMessage.LoRaPayloadMessage.GetLoRaMessage().Fport.Span[0];
                        }
                        else // this is an acknowledgment sent from the device
                        {
                            isAckFromDevice = true;
                            fullPayload.deviceAck = true;
                        }
                        fullPayload.port = fportUp;
                        fullPayload.fcnt = fcntup;

                        // ACK from device or no decoder set in LoRa Device Twin. Simply return decryptedMessage
                        if (isAckFromDevice || String.IsNullOrEmpty(loraDeviceInfo.SensorDecoder))
                        {
                            if (String.IsNullOrEmpty(loraDeviceInfo.SensorDecoder))
                            {
                                Logger.Log(loraDeviceInfo.DevEUI, $"no decoder set in device twin. port: {fportUp}", Logger.LoggingLevel.Full);
                            }

                            jsonDataPayload = Convert.ToBase64String(decryptedMessage);
                            fullPayload.data = jsonDataPayload;
                        }
                        // Decoder is set in LoRa Device Twin. Send decrpytedMessage (payload) and fportUp (fPort) to decoder.
                        else
                        {
                            Logger.Log(loraDeviceInfo.DevEUI, $"decoding with: {loraDeviceInfo.SensorDecoder} port: {fportUp}", Logger.LoggingLevel.Full);
                            fullPayload.data = await LoraDecoders.DecodeMessage(decryptedMessage, fportUp, loraDeviceInfo.SensorDecoder);
                        }

                        fullPayload.eui = loraDeviceInfo.DevEUI;
                        fullPayload.gatewayid = this.configuration.GatewayID;
                        //Edge timestamp
                        fullPayload.edgets = (long)((startTimeProcessing - new DateTime(1970, 1, 1)).TotalMilliseconds);
                        List<KeyValuePair<String, String>> messageProperties = new List<KeyValuePair<String, String>>();

                        //Parsing MacCommands and add them as property of the message to be sent to the IoT Hub.
                        var macCommand = ((LoRaPayloadData)loraMessage.LoRaPayloadMessage).GetMacCommands();
                        if (macCommand.macCommand.Count > 0)
                        {
                            for (int i = 0; i < macCommand.macCommand.Count; i++)
                            {
                                messageProperties.Add(new KeyValuePair<string, string>(macCommand.macCommand[i].Cid.ToString(), value: JsonConvert.SerializeObject(macCommand.macCommand[i], Newtonsoft.Json.Formatting.None)));
                                //in case it is a link check mac, we need to send it downstream.
                                if (macCommand.macCommand[i].Cid == CidEnum.LinkCheckCmd)
                                {
                                    linkCheckCmdResponse = new LinkCheckCmd(rxPk.GetModulationMargin(), 1).ToBytes();
                                }
                            }
                        }
                        string iotHubMsg = fullPayload.ToString(Newtonsoft.Json.Formatting.None);
                        await loraDeviceInfo.HubSender.SendMessageAsync(iotHubMsg, messageProperties);

                        if (isAckFromDevice)
                        {
                            Logger.Log(loraDeviceInfo.DevEUI, $"ack from device sent to hub", Logger.LoggingLevel.Info);
                        }
                        else
                        {
                            var fullPayloadAsString = string.Empty;
                            if (fullPayload.data is JValue jvalue)
                            {
                                fullPayloadAsString = jvalue.ToString();
                            }
                            else if (fullPayload.data is JObject jobject)
                            {
                                fullPayloadAsString = jobject.ToString(Formatting.None);
                            }
                            Logger.Log(loraDeviceInfo.DevEUI, $"message '{fullPayloadAsString}' sent to hub", Logger.LoggingLevel.Info);
                        }
                        loraDeviceInfo.FCntUp = fcntup;
                    }
                    else
                    {
                        validFrameCounter = false;
                        Logger.Log(loraDeviceInfo.DevEUI, $"invalid frame counter, msg: {fcntup} server: {loraDeviceInfo.FCntUp}", Logger.LoggingLevel.Info);
                    }
                    byte[] fctrl = new byte[1] { 0 };

                    //we lock as fast as possible and get the down fcnt for multi gateway support for confirmed message
                    if (loraMessage.LoRaMessageType == LoRaMessageType.ConfirmedDataUp && String.IsNullOrEmpty(loraDeviceInfo.GatewayID))
                    {
                        fctrl[0] = (int)FctrlEnum.Ack;
                        ushort newFCntDown = await this.loraDeviceInfoManager.NextFCntDown(loraDeviceInfo.DevEUI, loraDeviceInfo.FCntDown, fcntup, this.configuration.GatewayID);
                        //ok to send down ack or msg
                        if (newFCntDown > 0)
                        {
                            loraDeviceInfo.FCntDown = newFCntDown;
                        }
                        //another gateway was first with this message we simply drop
                        else
                        {
                            Logger.Log(loraDeviceInfo.DevEUI, $"another gateway has already sent ack or downlink msg", Logger.LoggingLevel.Info);
                            Logger.Log(loraDeviceInfo.DevEUI, $"processing time: {DateTime.UtcNow - startTimeProcessing}", Logger.LoggingLevel.Info);
                            return null;
                        }
                    }
                    //start checking for new c2d message, we do it even if the fcnt is invalid so we support replying to the ConfirmedDataUp
                    //todo ronnie should we wait up to 900 msec?
                    c2dMsg = await loraDeviceInfo.HubSender.ReceiveAsync(TimeSpan.FromMilliseconds(500));
                    if (c2dMsg != null && !ValidateCloudToDeviceMessage(loraDeviceInfo, c2dMsg))
                    {
                        _ = loraDeviceInfo.HubSender.CompleteAsync(c2dMsg);
                        c2dMsg = null;
                    }

                    byte[] bytesC2dMsg = null;
                    byte[] fport = null;

                    //check if we got a c2d message to be added in the ack message and prepare the message
                    if (c2dMsg != null)
                    {
                        ////check if there is another message
                        var secondC2dMsg = await loraDeviceInfo.HubSender.ReceiveAsync(TimeSpan.FromMilliseconds(40));
                        if (secondC2dMsg != null)
                        {
                            //put it back to the queue for the next pickup
                            //todo ronnie check abbandon logic especially in case of mqtt
                            _ = await loraDeviceInfo.HubSender.AbandonAsync(secondC2dMsg);
                            //set the fpending flag so the lora device will call us back for the next message
                            fctrl[0] += (int)FctrlEnum.FpendingOrClassB;
                            Logger.Log(loraDeviceInfo.DevEUI, $"Additional C2D messages waiting, setting FPending to 1", Logger.LoggingLevel.Info);

                        }

                        bytesC2dMsg = c2dMsg.GetBytes();
                        //default fport
                        fport = new byte[1] { 1 };

                        if (bytesC2dMsg != null)
                            Logger.Log(loraDeviceInfo.DevEUI, $"C2D message: {Encoding.UTF8.GetString(bytesC2dMsg)}", Logger.LoggingLevel.Info);

                        //todo ronnie implement a better max payload size by datarate
                        //cut to the max payload of lora for any EU datarate
                        if (bytesC2dMsg.Length > 51)
                            Array.Resize(ref bytesC2dMsg, 51);

                        Array.Reverse(bytesC2dMsg);
                    }
                    //if confirmation or cloud to device msg send down the message
                    if (loraMessage.LoRaMessageType == LoRaMessageType.ConfirmedDataUp || c2dMsg != null)
                    {
                        if (loraMessage.LoRaMessageType == LoRaMessageType.ConfirmedDataUp)
                            fctrl[0] += (int)FctrlEnum.Ack;

                        //check if we are not too late for the second receive windows
                        if ((DateTime.UtcNow - startTimeProcessing) <= TimeSpan.FromMilliseconds(RegionFactory.CurrentRegion.receive_delay2 * 1000 - 100))
                        {
                            //if running in multigateway we need to use redis to sync the down fcnt
                            if (!String.IsNullOrEmpty(loraDeviceInfo.GatewayID))
                            {
                                loraDeviceInfo.FCntDown++;
                            }
                            else if (loraMessage.LoRaMessageType == LoRaMessageType.UnconfirmedDataUp)
                            {
                                ushort newFCntDown = await this.loraDeviceInfoManager.NextFCntDown(loraDeviceInfo.DevEUI, loraDeviceInfo.FCntDown, fcntup, this.configuration.GatewayID);

                                //ok to send down ack or msg
                                if (newFCntDown > 0)
                                {
                                    loraDeviceInfo.FCntDown = newFCntDown;
                                }
                                //another gateway was first with this message we simply drop
                                else
                                {
                                    Logger.Log(loraDeviceInfo.DevEUI, $"another gateway has already sent ack or downlink msg", Logger.LoggingLevel.Info);
                                    Logger.Log(loraDeviceInfo.DevEUI, $"processing time: {DateTime.UtcNow - startTimeProcessing}", Logger.LoggingLevel.Info);
                                    return null;
                                }


                            }
                            Logger.Log(loraDeviceInfo.DevEUI, $"down frame counter: {loraDeviceInfo.FCntDown}", Logger.LoggingLevel.Info);

                            //Saving both fcnts to twins
                            _ = loraDeviceInfo.HubSender.UpdateFcntAsync(loraDeviceInfo.FCntUp, loraDeviceInfo.FCntDown);
                            //todo need implementation of current configuation to implement this as this depends on RX1DROffset
                            //var _datr = ((UplinkPktFwdMessage)loraMessage.LoraMetadata.FullPayload).rxpk[0].datr;
                            var datr = RegionFactory.CurrentRegion.GetDownstreamDR(loraMessage.PktFwdPayload.GetPktFwdMessage().Rxpks[0]);
                            //todo should discuss about the logic in case of multi channel gateway.
                            uint rfch = loraMessage.PktFwdPayload.GetPktFwdMessage().Rxpks[0].rfch;
                            //todo should discuss about the logic in case of multi channel gateway
                            double freq = RegionFactory.CurrentRegion.GetDownstreamChannel(loraMessage.PktFwdPayload.GetPktFwdMessage().Rxpks[0]);
                            //if we are already longer than 900 mssecond move to the 2 second window
                            //uncomment the following line to force second windows usage TODO change this to a proper expression?
                            //Thread.Sleep(901
                            long tmst = loraMessage.PktFwdPayload.GetPktFwdMessage().Rxpks[0].tmst + RegionFactory.CurrentRegion.receive_delay1 * 1000000;

                            if ((DateTime.UtcNow - startTimeProcessing) > TimeSpan.FromMilliseconds(RegionFactory.CurrentRegion.receive_delay1 * 1000 - 100))
                            {

                                if (string.IsNullOrEmpty(configuration.Rx2DataRate))
                                {
                                    Logger.Log(loraDeviceInfo.DevEUI, $"using standard second receive windows", Logger.LoggingLevel.Info);
                                    tmst = loraMessage.PktFwdPayload.GetPktFwdMessage().Rxpks[0].tmst + RegionFactory.CurrentRegion.receive_delay2 * 1000000;
                                    freq = RegionFactory.CurrentRegion.RX2DefaultReceiveWindows.frequency;
                                    datr = RegionFactory.CurrentRegion.DRtoConfiguration[RegionFactory.CurrentRegion.RX2DefaultReceiveWindows.dr].configuration;
                                }
                                //if specific twins are set, specify second channel to be as specified
                                else
                                {
                                    freq = configuration.Rx2DataFrequency;
                                    datr = configuration.Rx2DataRate;
                                    Logger.Log(loraDeviceInfo.DevEUI, $"using custom DR second receive windows freq : {freq}, datr:{datr}", Logger.LoggingLevel.Info);
                                }
                            }
                            Byte[] devAddrCorrect = new byte[4];
                            Array.Copy(loraMessage.LoRaPayloadMessage.DevAddr.ToArray(), devAddrCorrect, 4);
                            Array.Reverse(devAddrCorrect);
                            bool requestForConfirmedResponse = false;
                            byte[] rndToken = new byte[2];
                            Random rnd = new Random();
                            rnd.NextBytes(rndToken);

                            //check if the c2d message has a mac command
                            byte[] macbytes = null;
                            if (c2dMsg != null)
                            {
                                if (c2dMsg.Properties.TryGetValueCaseInsensitive("cidtype", out var cidTypeValue))
                                {
                                    Logger.Log(loraDeviceInfo.DevEUI, $"Cloud to device MAC command received", Logger.LoggingLevel.Info);
                                    MacCommandHolder macCommandHolder = new MacCommandHolder(Convert.ToByte(cidTypeValue));
                                    macbytes = macCommandHolder.macCommand[0].ToBytes();
                                }

                                if (c2dMsg.Properties.TryGetValueCaseInsensitive("confirmed", out var confirmedValue) && confirmedValue.Equals("true", StringComparison.OrdinalIgnoreCase))
                                {
                                    requestForConfirmedResponse = true;
                                    Logger.Log(loraDeviceInfo.DevEUI, $"Cloud to device message requesting a confirmation", Logger.LoggingLevel.Info);

                                }
                                if (c2dMsg.Properties.TryGetValueCaseInsensitive("fport", out var fPortValue))
                                {
                                    int fportint = int.Parse(fPortValue);

                                    fport[0] = BitConverter.GetBytes(fportint)[0];
                                    Logger.Log(loraDeviceInfo.DevEUI, $"Cloud to device message with a Fport of " + fPortValue, Logger.LoggingLevel.Info);

                                }
                                Logger.Log(loraDeviceInfo.DevEUI, String.Format("Sending a downstream message with ID {0}",
                                    ConversionHelper.ByteArrayToString(rndToken)),
                                    Logger.LoggingLevel.Full);
                            }

                            if (requestForConfirmedResponse)
                            {
                                fctrl[0] += (int)FctrlEnum.FpendingOrClassB;
                            }
                            if (macbytes != null && linkCheckCmdResponse != null)
                                macbytes = macbytes.Concat(linkCheckCmdResponse).ToArray();
                            LoRaPayloadData ackLoRaMessage = new LoRaPayloadData(
                                requestForConfirmedResponse ? MType.ConfirmedDataDown : MType.UnconfirmedDataDown,
                                //ConversionHelper.StringToByteArray(requestForConfirmedResponse?"A0":"60"),
                                devAddrCorrect,
                                fctrl,
                                BitConverter.GetBytes(loraDeviceInfo.FCntDown),
                                macbytes,
                                fport,
                                bytesC2dMsg,
                                1);

                            ackLoRaMessage.PerformEncryption(loraDeviceInfo.AppSKey);
                            ackLoRaMessage.SetMic(loraDeviceInfo.NwkSKey);


                            //todo ronnie should check the device twin preference if using confirmed or unconfirmed down
                            LoRaMessageWrapper ackMessage = new LoRaMessageWrapper(ackLoRaMessage, LoRaMessageType.UnconfirmedDataDown, rndToken, datr, 0, freq, tmst);
                            udpMsgForPktForwarder = ackMessage.PhysicalPayload.GetMessage();

                            linkCheckCmdResponse = null;
                            //confirm the message to iot hub only if we are in time for a delivery
                            if (c2dMsg != null)
                            {
                                //todo ronnie check if it is ok to do async so we make it in time to send the message
                                _ = loraDeviceInfo.HubSender.CompleteAsync(c2dMsg);
                                //bool rit = await loraDeviceInfo.HubSender.CompleteAsync(c2dMsg);
                                //if (rit)
                                //    Logger.Log(loraDeviceInfo.DevEUI, $"completed the c2d msg to IoT Hub", Logger.LoggingLevel.Info);
                                //else
                                //{
                                //    //we could not complete the msg so we send only a pushAck
                                //    PhysicalPayload pushAck = new PhysicalPayload(loraMessage.PhysicalPayload.token, PhysicalIdentifier.PUSH_ACK, null);
                                //    udpMsgForPktForwarder = pushAck.GetMessage();
                                //    Logger.Log(loraDeviceInfo.DevEUI, $"could not complete the c2d msg to IoT Hub", Logger.LoggingLevel.Info);

                                //}
                            }

                        }
                        else
                        {

                            //put back the c2d message to the queue for the next round
                            //todo ronnie check abbandon logic especially in case of mqtt
                            if (c2dMsg != null)
                            {
                                _ = await loraDeviceInfo.HubSender.AbandonAsync(c2dMsg);
                            }

                            Logger.Log(loraDeviceInfo.DevEUI, $"too late for down message, sending only ACK to gateway", Logger.LoggingLevel.Info);
                            _ = loraDeviceInfo.HubSender.UpdateFcntAsync(loraDeviceInfo.FCntUp, null);
                        }
                    }
                    //No ack requested and no c2d message we send the udp ack only to the gateway
                    else if (loraMessage.LoRaMessageType == LoRaMessageType.UnconfirmedDataUp && c2dMsg == null)
                    {
                        ////if ABP and 1 we reset the counter (loose frame counter) with force, if not we update normally
                        //if (fcntup == 1 && String.IsNullOrEmpty(loraDeviceInfo.AppEUI))
                        //    _ = loraDeviceInfo.HubSender.UpdateFcntAsync(fcntup, null, true);
                        if (validFrameCounter)
                            _ = loraDeviceInfo.HubSender.UpdateFcntAsync(loraDeviceInfo.FCntUp, null);

                    }

                    //If there is a link check command waiting
                    if (linkCheckCmdResponse != null)
                    {
                        Byte[] devAddrCorrect = new byte[4];
                        Array.Copy(loraMessage.LoRaPayloadMessage.DevAddr.ToArray(), devAddrCorrect, 4);
                        byte[] fctrl2 = new byte[1] { 0 };

                        Array.Reverse(devAddrCorrect);
                        LoRaPayloadData macReply = new LoRaPayloadData(MType.ConfirmedDataDown,
                            devAddrCorrect,
                            fctrl2,
                            BitConverter.GetBytes(loraDeviceInfo.FCntDown),
                            null,
                            new byte[1] { 0 },
                            linkCheckCmdResponse,
                            1);

                        macReply.PerformEncryption(loraDeviceInfo.AppSKey);
                        macReply.SetMic(loraDeviceInfo.NwkSKey);

                        byte[] rndToken = new byte[2];
                        Random rnd = new Random();
                        rnd.NextBytes(rndToken);

                        var datr = RegionFactory.CurrentRegion.GetDownstreamDR(loraMessage.PktFwdPayload.GetPktFwdMessage().Rxpks[0]);
                        //todo should discuss about the logic in case of multi channel gateway.
                        uint rfch = loraMessage.PktFwdPayload.GetPktFwdMessage().Rxpks[0].rfch;
                        double freq = RegionFactory.CurrentRegion.GetDownstreamChannel(loraMessage.PktFwdPayload.GetPktFwdMessage().Rxpks[0]);
                        long tmst = loraMessage.PktFwdPayload.GetPktFwdMessage().Rxpks[0].tmst + RegionFactory.CurrentRegion.receive_delay1 * 1000000;
                        //todo ronnie should check the device twin preference if using confirmed or unconfirmed down
                        LoRaMessageWrapper ackMessage = new LoRaMessageWrapper(macReply, LoRaMessageType.UnconfirmedDataDown, rndToken, datr, 0, freq, tmst);
                        udpMsgForPktForwarder = ackMessage.PhysicalPayload.GetMessage();
                    }



                }
                else
                {
                    Logger.Log(loraDeviceInfo.DevEUI, $"ignore message because is not linked to this GatewayID", Logger.LoggingLevel.Info);
                }
            }
            else
            {
                Logger.Log(devAddr, $"device is not our device, ignore message", Logger.LoggingLevel.Info);
            }




            Logger.Log(loraDeviceInfo?.DevEUI ?? devAddr, $"processing time: {DateTime.UtcNow - startTimeProcessing}", Logger.LoggingLevel.Info);

            return udpMsgForPktForwarder;
        }
       
        // Validate cloud to device message
        private bool ValidateCloudToDeviceMessage(LoraDeviceInfo loraDeviceInfo, Message c2dMessage)
        {
            // ensure fport property has been set
            if (!c2dMessage.Properties.TryGetValueCaseInsensitive(FPORT_MSG_PROPERTY_KEY, out var fportValue))
            {
                Logger.Log(loraDeviceInfo.DevEUI, $"missing {FPORT_MSG_PROPERTY_KEY} property in C2D message '{c2dMessage.MessageId}'", Logger.LoggingLevel.Error);
                return false;
            }

            if (byte.TryParse(fportValue, out var fport))
            {
                // ensure fport follows LoRa specification
                // 0    => reserved for mac commands
                // 224+ => reserved for future applications 
                if (fport != LORA_FPORT_RESERVED_MAC_MSG && fport < LORA_FPORT_RESERVED_FUTURE_START)
                    return true;
            }

            Logger.Log(loraDeviceInfo.DevEUI, $"invalid fport '{fportValue}' in C2D message '{c2dMessage.MessageId}'", Logger.LoggingLevel.Error);
            return false;
        }


        private async Task<byte[]> ProcessJoinRequest(LoRaMessageWrapper loraMessage)
        {

            byte[] udpMsgForPktForwarder = new Byte[0];
            var joinReq = (LoRaPayloadJoinRequest)loraMessage.LoRaPayloadMessage;
            joinReq.DevEUI.Span.Reverse();
            joinReq.AppEUI.Span.Reverse();
            string devEui = ConversionHelper.ByteArrayToString(joinReq.DevEUI.ToArray());
            string devNonce = ConversionHelper.ByteArrayToString(joinReq.DevNonce.ToArray());
            Logger.Log(devEui, $"join request received", Logger.LoggingLevel.Info);
            //checking if this devnonce was already processed or the deveui was already refused
            //check if join request is valid. 
            //we have a join request in the cache
            if (Cache.TryGetJoinRequestValue(devEui, out LoraDeviceInfo joinLoraDeviceInfo))
            {
           
                //is our device but the join was not valid
                if (!joinLoraDeviceInfo.IsJoinValid)
                {
                    //if the devNonce is equal to the current it is a potential replay attck
                    if (joinLoraDeviceInfo.DevNonce == devNonce)
                    {
                        Logger.Log(devEui, $"join request refused devNonce already used", Logger.LoggingLevel.Info);
                        return null;
                    }

                    //Check if the device is trying to join through the wrong gateway
                    if (!String.IsNullOrEmpty(joinLoraDeviceInfo.GatewayID) && string.Compare(joinLoraDeviceInfo.GatewayID, this.configuration.GatewayID, ignoreCase: true) != 0)
                    {
                        Logger.Log(devEui, $"trying to join not through its linked gateway, ignoring join request", Logger.LoggingLevel.Info);
                        return null;
                    }
                }
            }

            joinLoraDeviceInfo = await this.loraDeviceInfoManager.PerformOTAAAsync(this.configuration.GatewayID, devEui, ConversionHelper.ByteArrayToString(joinReq.AppEUI.ToArray()), devNonce, joinLoraDeviceInfo);


            if (joinLoraDeviceInfo != null && joinLoraDeviceInfo.IsJoinValid)
            {
                if (!loraMessage.LoRaPayloadMessage.CheckMic(joinLoraDeviceInfo.AppKey))
                {
                    Logger.Log(devEui, $"join request MIC invalid", Logger.LoggingLevel.Info);
                }
                //join request resets the frame counters
                joinLoraDeviceInfo.FCntUp = 0;
                joinLoraDeviceInfo.FCntDown = 0;
                //in this case it's too late, we need to break and awoid saving twins
                if ((DateTime.UtcNow - startTimeProcessing) > TimeSpan.FromMilliseconds(RegionFactory.CurrentRegion.join_accept_delay2 * 1000))
                {

                    Logger.Log(devEui, $"processing of the join request took too long, sending no message", Logger.LoggingLevel.Info);
                }
                //update reported properties and frame Counter
                //in case not successfull we interrupt the flow
                if (!await joinLoraDeviceInfo.HubSender.UpdateReportedPropertiesOTAAasync(joinLoraDeviceInfo))
                {
                    Logger.Log(devEui, $"join request could not save twins, join refused", Logger.LoggingLevel.Error);

                    return null;
                }

                byte[] appNonce = ConversionHelper.StringToByteArray(joinLoraDeviceInfo.AppNonce);
                byte[] netId = ConversionHelper.StringToByteArray(joinLoraDeviceInfo.NetId);
                byte[] devAddr = ConversionHelper.StringToByteArray(joinLoraDeviceInfo.DevAddr);
                string appKey = joinLoraDeviceInfo.AppKey;
                Array.Reverse(netId);
                Array.Reverse(appNonce);
                LoRaPayloadJoinAccept loRaPayloadJoinAccept = new LoRaPayloadJoinAccept(
                    //NETID 0 / 1 is default test 
                    ConversionHelper.ByteArrayToString(netId),
                    //todo add app key management
                    appKey,
                    //todo add device address management
                    devAddr,
                    appNonce,
                    new byte[] { 0 },
                    new byte[] { 0 },
                    null
                    );
                var datr = RegionFactory.CurrentRegion.GetDownstreamDR(loraMessage.PktFwdPayload.GetPktFwdMessage().Rxpks[0]);
                uint rfch = loraMessage.PktFwdPayload.GetPktFwdMessage().Rxpks[0].rfch;
                double freq = RegionFactory.CurrentRegion.GetDownstreamChannel(loraMessage.PktFwdPayload.GetPktFwdMessage().Rxpks[0]);
                //set tmst for the normal case
                long tmst = loraMessage.PktFwdPayload.GetPktFwdMessage().Rxpks[0].tmst + RegionFactory.CurrentRegion.join_accept_delay1 * 1000000;

                ////in this case it's too late, we need to break
                //if ((DateTime.UtcNow - startTimeProcessing) > TimeSpan.FromMilliseconds(RegionFactory.CurrentRegion.join_accept_delay2 * 1000))
                //{
                //    Logger.Log(devEui, $"processing of the join request took too long, sending no message", Logger.LoggingLevel.Info);
                //    var physicalResponse = new PhysicalPayload(loraMessage.PhysicalPayload.token, PhysicalIdentifier.PULL_RESP, null);

                //    Logger.Log(devEui, $"processing time: {DateTime.UtcNow - startTimeProcessing}", Logger.LoggingLevel.Info);

                //    return physicalResponse.GetMessage();
                //}
                //in this case the second join windows must be used
                if ((DateTime.UtcNow - startTimeProcessing) > TimeSpan.FromMilliseconds(RegionFactory.CurrentRegion.join_accept_delay1 * 1000 - 100))
                {
                    Logger.Log(devEui, $"processing of the join request took too long, using second join accept receive window", Logger.LoggingLevel.Info);
                    tmst = loraMessage.PktFwdPayload.GetPktFwdMessage().Rxpks[0].tmst + RegionFactory.CurrentRegion.join_accept_delay2 * 1000000;
                    if (string.IsNullOrEmpty(configuration.Rx2DataRate))
                    {
                        Logger.Log(devEui, $"using standard second receive windows for join request", Logger.LoggingLevel.Info);
                        //using EU fix DR for RX2
                        freq = RegionFactory.CurrentRegion.RX2DefaultReceiveWindows.frequency;
                        datr = RegionFactory.CurrentRegion.DRtoConfiguration[RegionFactory.CurrentRegion.RX2DefaultReceiveWindows.dr].configuration;
                    }
                    //if specific twins are set, specify second channel to be as specified
                    else
                    {
                        Logger.Log(devEui, $"using custom  second receive windows for join request", Logger.LoggingLevel.Info);
                        freq = configuration.Rx2DataFrequency;
                        datr = configuration.Rx2DataRate;
                    }
                }
                LoRaMessageWrapper joinAcceptMessage = new LoRaMessageWrapper(loRaPayloadJoinAccept, LoRaMessageType.JoinAccept, loraMessage.PhysicalPayload.token, datr, 0, freq, tmst);

                udpMsgForPktForwarder = joinAcceptMessage.PhysicalPayload.GetMessage();

                //add to cache for processing normal messages. This awoids one additional call to the server.

                Cache.AddRequestToCache(joinLoraDeviceInfo.DevAddr, joinLoraDeviceInfo);

                Logger.Log(devEui, String.Format("join accept sent with ID {0}",
                    ConversionHelper.ByteArrayToString(loraMessage.PhysicalPayload.token)),
                    Logger.LoggingLevel.Full);
            }
            else
            {
                Logger.Log(devEui, $"join request refused", Logger.LoggingLevel.Info);

            }

            Logger.Log(devEui, $"processing time: {DateTime.UtcNow - startTimeProcessing}", Logger.LoggingLevel.Info);


            return udpMsgForPktForwarder;
        }

        public void Dispose()
        {

        }


    }
}
