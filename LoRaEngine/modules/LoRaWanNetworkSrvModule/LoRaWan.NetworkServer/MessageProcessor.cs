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
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static LoRaTools.LoRaMessage.LoRaPayloadData;

namespace LoRaWan.NetworkServer
{
    public class MessageProcessor : IDisposable
    {
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
            byte[] udpMsgForPktForwarder = new Byte[0];
            if (!loraMessage.IsLoRaMessage)
            {
                udpMsgForPktForwarder = ProcessNonLoraMessage(loraMessage);
            }
            else
            {
                if (RegionFactory.CurrentRegion == null)
                    RegionFactory.Create(loraMessage.PktFwdPayload.GetPktFwdMessage().Rxpks[0]);
                //join message
                if (loraMessage.LoRaMessageType == LoRaMessageType.JoinRequest)
                {
                    udpMsgForPktForwarder = await ProcessJoinRequest(loraMessage);
                }
                //normal message
                else if (loraMessage.LoRaMessageType == LoRaMessageType.UnconfirmedDataUp || loraMessage.LoRaMessageType == LoRaMessageType.ConfirmedDataUp)
                {
                    udpMsgForPktForwarder = await ProcessLoraMessage(loraMessage);
                }
            }

            //send reply to pktforwarder
            return udpMsgForPktForwarder;

        }

        private byte[] ProcessNonLoraMessage(LoRaMessageWrapper loraMessage)
        {
            byte[] udpMsgForPktForwarder = new byte[0];
            if (loraMessage.PhysicalPayload.identifier == PhysicalIdentifier.PULL_DATA)
            {
                PhysicalPayload pullAck = new PhysicalPayload(loraMessage.PhysicalPayload.token, PhysicalIdentifier.PULL_ACK, null);
                udpMsgForPktForwarder = pullAck.GetMessage();
            }

            return udpMsgForPktForwarder;
        }
        private async Task<byte[]> ProcessLoraMessage(LoRaMessageWrapper loraMessage)
        {
            bool validFrameCounter = false;
            byte[] udpMsgForPktForwarder = new byte[0];
            string devAddr = ConversionHelper.ByteArrayToString(loraMessage.LoRaPayloadMessage.DevAddr.ToArray());
            Message c2dMsg = null;
            Cache.TryGetValue(devAddr, out LoraDeviceInfo loraDeviceInfo);

            if (loraDeviceInfo == null || !loraDeviceInfo.IsOurDevice)
            {
                loraDeviceInfo = await this.loraDeviceInfoManager.GetLoraDeviceInfoAsync(devAddr, this.configuration.GatewayID);
                if (loraDeviceInfo.DevEUI != null)
                    Logger.Log(loraDeviceInfo.DevEUI, $"processing message, device not in cache", Logger.LoggingLevel.Info);
                else
                    Logger.Log(devAddr, $"processing message, device not in cache", Logger.LoggingLevel.Info);
                Cache.AddToCache(devAddr, loraDeviceInfo);
            }
            else
            {
                Logger.Log(loraDeviceInfo.DevEUI, $"processing message, device in cache", Logger.LoggingLevel.Info);
            }


            if (loraDeviceInfo != null && loraDeviceInfo.IsOurDevice)
            {
                //either there is no gateway linked to the device or the gateway is the one that the code is running
                if (string.IsNullOrEmpty(loraDeviceInfo.GatewayID) || string.Compare(loraDeviceInfo.GatewayID, this.configuration.GatewayID, ignoreCase: true) == 0)
                {
                    if (loraMessage.CheckMic(loraDeviceInfo.NwkSKey))
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

                            if (isAckFromDevice)
                            {
                                jsonDataPayload = Convert.ToBase64String(decryptedMessage);
                                fullPayload.data = jsonDataPayload;
                            }
                            else
                            {
                                Logger.Log(loraDeviceInfo.DevEUI, $"decoding with: {loraDeviceInfo.SensorDecoder} port: {fportUp}", Logger.LoggingLevel.Info);
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
                                var fullPayloadAsString = fullPayload.data as string;
                                if (fullPayloadAsString == null)
                                {
                                    fullPayloadAsString = ((JObject)fullPayload.data).ToString(Formatting.None);
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


                        //we lock as fast as possible and get the down fcnt for multi gateway support for confirmed message
                        if (loraMessage.LoRaMessageType == LoRaMessageType.ConfirmedDataUp && String.IsNullOrEmpty(loraDeviceInfo.GatewayID))
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
                                PhysicalPayload pushAck = new PhysicalPayload(loraMessage.PhysicalPayload.token, PhysicalIdentifier.PUSH_ACK, null);
                                udpMsgForPktForwarder = pushAck.GetMessage();
                                Logger.Log(loraDeviceInfo.DevEUI, $"another gateway has already sent ack or downlink msg", Logger.LoggingLevel.Info);
                                Logger.Log(loraDeviceInfo.DevEUI, $"processing time: {DateTime.UtcNow - startTimeProcessing}", Logger.LoggingLevel.Info);
                                return udpMsgForPktForwarder;
                            }
                        }
                        //start checking for new c2d message, we do it even if the fcnt is invalid so we support replying to the ConfirmedDataUp
                        //todo ronnie should we wait up to 900 msec?
                        c2dMsg = await loraDeviceInfo.HubSender.ReceiveAsync(TimeSpan.FromMilliseconds(20));
                        byte[] bytesC2dMsg = null;
                        byte[] fport = null;
                        //Todo revamp fctrl
                        byte[] fctrl = new byte[1] { 32 };
                        //check if we got a c2d message to be added in the ack message and prepare the message
                        if (c2dMsg != null)
                        {
                            ////check if there is another message
                            var secondC2dMsg = await loraDeviceInfo.HubSender.ReceiveAsync(TimeSpan.FromMilliseconds(20));
                            if (secondC2dMsg != null)
                            {
                                //put it back to the queue for the next pickup
                                //todo ronnie check abbandon logic especially in case of mqtt
                                _ = await loraDeviceInfo.HubSender.AbandonAsync(secondC2dMsg);
                                //set the fpending flag so the lora device will call us back for the next message
                                fctrl = new byte[1] { 48 };
                            }

                            bytesC2dMsg = c2dMsg.GetBytes();
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
                                        PhysicalPayload pushAck = new PhysicalPayload(loraMessage.PhysicalPayload.token, PhysicalIdentifier.PUSH_ACK, null);
                                        udpMsgForPktForwarder = pushAck.GetMessage();
                                        Logger.Log(loraDeviceInfo.DevEUI, $"another gateway has already sent ack or downlink msg", Logger.LoggingLevel.Info);
                                        Logger.Log(loraDeviceInfo.DevEUI, $"processing time: {DateTime.UtcNow - startTimeProcessing}", Logger.LoggingLevel.Info);
                                        return udpMsgForPktForwarder;
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
                                    fctrl = new byte[1] { 32 };
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

                                //check if the c2d message has a mac command
                                byte[] macbytes = null;
                                if (c2dMsg != null)
                                {
                                    
                                    if (c2dMsg.Properties.Keys.Contains("CidType"))
                                    {
                                            MacCommandHolder macCommandHolder = new MacCommandHolder(Convert.ToByte(c2dMsg.Properties["CidType"]));
                                            macbytes = macCommandHolder.macCommand[0].ToBytes();
                                    }
                                    if (c2dMsg.Properties.Keys.Contains("Confirmed")&&c2dMsg.Properties["Confirmed"] == "true")
                                    {
                                        requestForConfirmedResponse = true;
                                    }
                                    if (c2dMsg.Properties.Keys.Contains("Fport"))
                                    {
                                        fport = BitConverter.GetBytes(int.Parse(c2dMsg.Properties["Fport"]));
                                    }
                                }
                                if(requestForConfirmedResponse )
                                    fctrl[0]+=16 ;
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

                                byte[] rndToken = new byte[2];
                                Random rnd = new Random();
                                rnd.NextBytes(rndToken);
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
                                PhysicalPayload pushAck = new PhysicalPayload(loraMessage.PhysicalPayload.token, PhysicalIdentifier.PUSH_ACK, null);
                                udpMsgForPktForwarder = pushAck.GetMessage();

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

                            PhysicalPayload pushAck = new PhysicalPayload(loraMessage.PhysicalPayload.token, PhysicalIdentifier.PUSH_ACK, null);
                            udpMsgForPktForwarder = pushAck.GetMessage();

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
                            byte[] fctrl2 = new byte[1] { 32 };

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
                        Logger.Log(loraDeviceInfo.DevEUI, $"with devAddr {devAddr} check MIC failed. Device will be ignored from now on", Logger.LoggingLevel.Info);
                        loraDeviceInfo.IsOurDevice = false;
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




            Logger.Log(loraDeviceInfo.DevEUI, $"processing time: {DateTime.UtcNow - startTimeProcessing}", Logger.LoggingLevel.Info);

            if (udpMsgForPktForwarder.Length == 0)
            {
                PhysicalPayload pushAck = new PhysicalPayload(loraMessage.PhysicalPayload.token, PhysicalIdentifier.PUSH_ACK, null);
                udpMsgForPktForwarder = pushAck.GetMessage();
            }
            return udpMsgForPktForwarder;
        }



        private async Task<byte[]> ProcessJoinRequest(LoRaMessageWrapper loraMessage)
        {

            byte[] udpMsgForPktForwarder = new Byte[0];
            LoraDeviceInfo joinLoraDeviceInfo;
            var joinReq = (LoRaPayloadJoinRequest)loraMessage.LoRaPayloadMessage;
            joinReq.DevEUI.Span.Reverse();
            joinReq.AppEUI.Span.Reverse();
            string devEui = ConversionHelper.ByteArrayToString(joinReq.DevEUI.ToArray());
            string devNonce = ConversionHelper.ByteArrayToString(joinReq.DevNonce.ToArray());
            Logger.Log(devEui, $"join request received", Logger.LoggingLevel.Info);
            //checking if this devnonce was already processed or the deveui was already refused
            Cache.TryGetValue(devEui, out joinLoraDeviceInfo);

            //check if join request is valid. 
            //we have a join request in the cache
            if (joinLoraDeviceInfo != null)
            {
                //we query every time in case the device is turned one while not yet in the registry 
                //it is not our device so ingore the join
                //if (!joinLoraDeviceInfo.IsOurDevice)
                //{
                //    Logger.Log(devEui, $"join request refused the device is not ours", Logger.LoggingLevel.Info);
                //    return null;
                //}
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
                    var physicalResponse = new PhysicalPayload(loraMessage.PhysicalPayload.token, PhysicalIdentifier.PUSH_ACK, null);
                    return physicalResponse.GetMessage();
                }
                //join request resets the frame counters
                joinLoraDeviceInfo.FCntUp = 0;
                joinLoraDeviceInfo.FCntDown = 0;
                //in this case it's too late, we need to break and awoid saving twins
                if ((DateTime.UtcNow - startTimeProcessing) > TimeSpan.FromMilliseconds(RegionFactory.CurrentRegion.join_accept_delay2 * 1000))
                {

                    Logger.Log(devEui, $"processing of the join request took too long, sending no message", Logger.LoggingLevel.Info);
                    var physicalResponse = new PhysicalPayload(loraMessage.PhysicalPayload.token, PhysicalIdentifier.PUSH_ACK, null);
                    return physicalResponse.GetMessage();
                }
                //update reported properties and frame Counter
                await joinLoraDeviceInfo.HubSender.UpdateReportedPropertiesOTAAasync(joinLoraDeviceInfo);
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
                 if ((DateTime.UtcNow - startTimeProcessing) > TimeSpan.FromMilliseconds(RegionFactory.CurrentRegion.join_accept_delay1 * 1000 -100 ))
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
                Cache.AddToCache(joinLoraDeviceInfo.DevAddr, joinLoraDeviceInfo);
                Logger.Log(devEui, $"join accept sent", Logger.LoggingLevel.Info);
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
