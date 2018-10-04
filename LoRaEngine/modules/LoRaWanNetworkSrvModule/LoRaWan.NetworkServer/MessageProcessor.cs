//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//
using LoRaTools;
using LoRaTools.Regions;
using Microsoft.Azure.Devices.Client;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PacketManager;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;


namespace LoRaWan.NetworkServer
{
    public class MessageProcessor : IDisposable
    {


        private DateTime startTimeProcessing;

        private static string GatewayID;


        private List<byte[]> fOptsPending = new List<byte[]>();

        public async Task processMessage(byte[] message)
        {
            startTimeProcessing = DateTime.UtcNow;

            //gate the edge device id for checking if the device is linked to a specific gateway
            if (string.IsNullOrEmpty(GatewayID))
            {
                GatewayID = Environment.GetEnvironmentVariable("IOTEDGE_DEVICEID");
            }

            LoRaMessage loraMessage = new LoRaMessage(message);



            byte[] udpMsgForPktForwarder = new Byte[0];

            if (!loraMessage.IsLoRaMessage)
            {
                udpMsgForPktForwarder = ProcessNonLoraMessage(loraMessage);
            }
            else
            {
                if (RegionFactory.CurrentRegion == null)
                    RegionFactory.Create(((UplinkPktFwdMessage)loraMessage.LoraMetadata.FullPayload).rxpk[0]);
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
            await UdpServer.UdpSendMessage(udpMsgForPktForwarder);
        }

        private byte[] ProcessNonLoraMessage(LoRaMessage loraMessage)
        {
            byte[] udpMsgForPktForwarder = new byte[0];
            if (loraMessage.PhysicalPayload.identifier == PhysicalIdentifier.PULL_DATA)
            {
                PhysicalPayload pullAck = new PhysicalPayload(loraMessage.PhysicalPayload.token, PhysicalIdentifier.PULL_ACK, null);
                udpMsgForPktForwarder = pullAck.GetMessage();
            }

            return udpMsgForPktForwarder;
        }
        private async Task<byte[]> ProcessLoraMessage(LoRaMessage loraMessage)
        {
            bool validFrameCounter = false;
            byte[] udpMsgForPktForwarder = new byte[0];
            string devAddr = BitConverter.ToString(loraMessage.PayloadMessage.DevAddr).Replace("-", "");
            Message c2dMsg = null;
            Cache.TryGetValue(devAddr, out LoraDeviceInfo loraDeviceInfo);
            if (loraDeviceInfo == null)
            {
                loraDeviceInfo = await LoraDeviceInfoManager.GetLoraDeviceInfoAsync(devAddr);
                Logger.Log(loraDeviceInfo.DevEUI, $"processing message, device not in cache", Logger.LoggingLevel.Info);
                Cache.AddToCache(devAddr, loraDeviceInfo);
            }
            else
            {
                Logger.Log(loraDeviceInfo.DevEUI, $"processing message, device in cache", Logger.LoggingLevel.Info);
            }

            if (loraDeviceInfo != null && loraDeviceInfo.IsOurDevice)
            {
                //either there is no gateway linked to the device or the gateway is the one that the code is running
                if (String.IsNullOrEmpty(loraDeviceInfo.GatewayID) || loraDeviceInfo.GatewayID.ToUpper() == GatewayID.ToUpper())
                {
                    if (loraMessage.CheckMic(loraDeviceInfo.NwkSKey))
                    {
                        if (loraDeviceInfo.HubSender == null)
                        {
                            loraDeviceInfo.HubSender = new IoTHubSender(loraDeviceInfo.DevEUI, loraDeviceInfo.PrimaryKey);
                        }
                        UInt16 fcntup = BitConverter.ToUInt16(((LoRaPayloadStandardData)loraMessage.PayloadMessage).Fcnt, 0);
                        byte[] linkCheckCmdResponse = null;

                        //check if the frame counter is valid: either is above the server one or is an ABP device resetting the counter (relaxed seqno checking)
                        if (fcntup > loraDeviceInfo.FCntUp || (fcntup == 1 && String.IsNullOrEmpty(loraDeviceInfo.AppEUI)))
                        {
                            //save the reset fcnt for ABP (relaxed seqno checking)
                            if (fcntup == 1 && String.IsNullOrEmpty(loraDeviceInfo.AppEUI))
                                _ = loraDeviceInfo.HubSender.UpdateFcntAsync(fcntup, 0, true);

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

                            Rxpk rxPk = ((UplinkPktFwdMessage)loraMessage.LoraMetadata.FullPayload).rxpk[0];
                            dynamic fullPayload = JObject.FromObject(rxPk);
                            string jsonDataPayload = "";
                            uint fportUp = (uint)((LoRaPayloadStandardData)loraMessage.PayloadMessage).Fport[0];
                            fullPayload.port = fportUp;
                            fullPayload.fcnt = fcntup;

                            if (String.IsNullOrEmpty(loraDeviceInfo.SensorDecoder))
                            {
                                jsonDataPayload = Convert.ToBase64String(decryptedMessage);
                                fullPayload.data = jsonDataPayload;
                            }
                            else
                            {
                                Logger.Log(loraDeviceInfo.DevEUI, $"decoding with: {loraDeviceInfo.SensorDecoder} port: {fportUp}", Logger.LoggingLevel.Info);
                                jsonDataPayload = LoraDecoders.DecodeMessage(decryptedMessage, fportUp, loraDeviceInfo.SensorDecoder);
                                fullPayload.data = JObject.Parse(jsonDataPayload);
                            }

                            fullPayload.eui = loraDeviceInfo.DevEUI;
                            fullPayload.gatewayid = GatewayID;
                            //Edge timestamp
                            fullPayload.edgets = (long)((startTimeProcessing - new DateTime(1970, 1, 1)).TotalMilliseconds);

                            List<KeyValuePair<String, String>> messageProperties = new List<KeyValuePair<String, String>>();

                            //Parsing MacCommands and add them as property of the message to be sent to the IoT Hub.
                            var macCommand = ((LoRaPayloadStandardData)loraMessage.PayloadMessage).GetMacCommands();
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
                            Logger.Log(loraDeviceInfo.DevEUI, $"sent message '{jsonDataPayload}' to hub", Logger.LoggingLevel.Info);

                            loraDeviceInfo.FCntUp = fcntup;
                        }
                        else
                        {
                            validFrameCounter = false;
                            Logger.Log(loraDeviceInfo.DevEUI, $"invalid frame counter, msg: {fcntup} server: {loraDeviceInfo.FCntUp}", Logger.LoggingLevel.Info);
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
                            //check if there is another message
                            var secondC2dMsg = await loraDeviceInfo.HubSender.ReceiveAsync(TimeSpan.FromMilliseconds(20));
                            if (secondC2dMsg != null)
                            {
                                //put it back to the queue for the next pickup
                                _ = loraDeviceInfo.HubSender.AbandonAsync(secondC2dMsg);
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
                                //increase the fcnt down and save it to iot hub twins
                                loraDeviceInfo.FCntDown++;
                                Logger.Log(loraDeviceInfo.DevEUI, $"down frame counter: {loraDeviceInfo.FCntDown}", Logger.LoggingLevel.Info);
                                //Saving both fcnts to twins
                                _ = loraDeviceInfo.HubSender.UpdateFcntAsync(loraDeviceInfo.FCntUp, loraDeviceInfo.FCntDown);
                                //todo need implementation of current configuation to implement this as this depends on RX1DROffset
                                //var _datr = ((UplinkPktFwdMessage)loraMessage.LoraMetadata.FullPayload).rxpk[0].datr;
                                var _datr = RegionFactory.CurrentRegion.GetDownstreamDR(((UplinkPktFwdMessage)loraMessage.LoraMetadata.FullPayload).rxpk[0]);
                                //todo should discuss about the logic in case of multi channel gateway.
                                uint _rfch = ((UplinkPktFwdMessage)loraMessage.LoraMetadata.FullPayload).rxpk[0].rfch;
                                //todo should discuss about the logic in case of multi channel gateway
                                //in c
                                double _freq = RegionFactory.CurrentRegion.GetDownstreamChannel(((UplinkPktFwdMessage)loraMessage.LoraMetadata.FullPayload).rxpk[0]);
                                uint txDelay = 0;
                                //if we are already longer than 900 mssecond move to the 2 second window
                                //uncomment the following line to force second windows usage TODO change this to a proper expression?
                                //Thread.Sleep(901);
                                if ((DateTime.UtcNow - startTimeProcessing) > TimeSpan.FromMilliseconds(RegionFactory.CurrentRegion.receive_delay1 * 1000 - 100))
                                {
                                    if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("RX2_DATR")))
                                    {
                                        Logger.Log(loraDeviceInfo.DevEUI, $"using standard second receive windows", Logger.LoggingLevel.Info);

                                        _freq = RegionFactory.CurrentRegion.RX2DefaultReceiveWindows.frequency;
                                        _datr = RegionFactory.CurrentRegion.DRtoConfiguration[RegionFactory.CurrentRegion.RX2DefaultReceiveWindows.dr].configuration;


                                    }
                                    //if specific twins are set, specify second channel to be as specified
                                    else
                                    {

                                        _freq = double.Parse(Environment.GetEnvironmentVariable("RX2_FREQ"));
                                        _datr = Environment.GetEnvironmentVariable("RX2_DATR");
                                        Logger.Log(loraDeviceInfo.DevEUI, $"using custom DR second receive windows freq : {_freq}, datr:{_datr}", Logger.LoggingLevel.Info);

                                    }

                                    txDelay = 1000000;
                                }


                                long _tmst = ((UplinkPktFwdMessage)loraMessage.LoraMetadata.FullPayload).rxpk[0].tmst + txDelay;
                                Byte[] devAddrCorrect = new byte[4];
                                Array.Copy(loraMessage.PayloadMessage.DevAddr, devAddrCorrect, 4);
                                Array.Reverse(devAddrCorrect);

                                //check if the c2d message has  a mac command
                                byte[] macbytes = null;
                                if (c2dMsg != null)
                                {
                                    var macCmd = c2dMsg.Properties.Where(o => o.Key == "CidType");

                                    if (macCmd.Count() != 0)
                                    {

                                        MacCommandHolder macCommandHolder = new MacCommandHolder(Convert.ToByte(macCmd.First().Value));
                                        macbytes = macCommandHolder.macCommand[0].ToBytes();
                                    }
                                }
                                if (macbytes != null && linkCheckCmdResponse != null)
                                    macbytes = macbytes.Concat(linkCheckCmdResponse).ToArray();
                                //adding the FoptsLength
                                LoRaPayloadStandardData ackLoRaMessage = new LoRaPayloadStandardData(StringToByteArray("A0"),
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
                                LoRaMessage ackMessage = new LoRaMessage(ackLoRaMessage, LoRaMessageType.ConfirmedDataDown, rndToken, _datr, 0, _freq, _tmst);
                                udpMsgForPktForwarder = ackMessage.PhysicalPayload.GetMessage();
                                linkCheckCmdResponse = null;
                                //confirm the message to iot hub only if we are in time for a delivery
                                if (c2dMsg != null)
                                {
                                    _ = loraDeviceInfo.HubSender.CompleteAsync(c2dMsg);
                                    Logger.Log(loraDeviceInfo.DevEUI, $"complete the c2d msg to IoT Hub", Logger.LoggingLevel.Info);
                                }

                            }
                            else
                            {
                                PhysicalPayload pushAck = new PhysicalPayload(loraMessage.PhysicalPayload.token, PhysicalIdentifier.PUSH_ACK, null);
                                udpMsgForPktForwarder = pushAck.GetMessage();
                                _ = loraDeviceInfo.HubSender.UpdateFcntAsync(loraDeviceInfo.FCntUp, null);
                                //put back the c2d message to the queue for the next round
                                _ = loraDeviceInfo.HubSender.AbandonAsync(c2dMsg);
                                Logger.Log(loraDeviceInfo.DevEUI, $"too late for down message, sending only ACK to gateway", Logger.LoggingLevel.Info);
                            }


                        }
                        //No ack requested and no c2d message we send the udp ack only to the gateway
                        else if (loraMessage.LoRaMessageType == LoRaMessageType.UnconfirmedDataUp && c2dMsg == null)
                        {

                            PhysicalPayload pushAck = new PhysicalPayload(loraMessage.PhysicalPayload.token, PhysicalIdentifier.PUSH_ACK, null);
                            udpMsgForPktForwarder = pushAck.GetMessage();

                            //if ABP and 1 we reset the counter (loose frame counter) with force, if not we update normally
                            if (fcntup == 1 && String.IsNullOrEmpty(loraDeviceInfo.AppEUI))
                                _ = loraDeviceInfo.HubSender.UpdateFcntAsync(fcntup, null, true);
                            else if (validFrameCounter)
                                _ = loraDeviceInfo.HubSender.UpdateFcntAsync(loraDeviceInfo.FCntUp, null);

                        }
                        if (linkCheckCmdResponse != null)
                        {
                            Byte[] devAddrCorrect = new byte[4];
                            Array.Copy(loraMessage.PayloadMessage.DevAddr, devAddrCorrect, 4);
                            byte[] fctrl2 = new byte[1] { 32 };

                            Array.Reverse(devAddrCorrect);
                            LoRaPayloadStandardData macReply = new LoRaPayloadStandardData(StringToByteArray("A0"),
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

                            var _datr = RegionFactory.CurrentRegion.GetDownstreamDR(((UplinkPktFwdMessage)loraMessage.LoraMetadata.FullPayload).rxpk[0]);
                            //todo should discuss about the logic in case of multi channel gateway.
                            uint _rfch = ((UplinkPktFwdMessage)loraMessage.LoraMetadata.FullPayload).rxpk[0].rfch;
                            double _freq = RegionFactory.CurrentRegion.GetDownstreamChannel(((UplinkPktFwdMessage)loraMessage.LoraMetadata.FullPayload).rxpk[0]);
                            long txDelay = 1000000;
                            long _tmst = ((UplinkPktFwdMessage)loraMessage.LoraMetadata.FullPayload).rxpk[0].tmst + txDelay;

                            //todo ronnie should check the device twin preference if using confirmed or unconfirmed down
                            LoRaMessage ackMessage = new LoRaMessage(macReply, LoRaMessageType.UnconfirmedDataDown, rndToken, _datr, 0, _freq, _tmst);
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
                Logger.Log(devAddr, $"device with devAddr {devAddr} is not our device, ignore message", Logger.LoggingLevel.Info);
            }

            Logger.Log(loraDeviceInfo.DevEUI, $"processing time: {DateTime.UtcNow - startTimeProcessing}", Logger.LoggingLevel.Info);



            return udpMsgForPktForwarder;
        }



        private async Task<byte[]> ProcessJoinRequest(LoRaMessage loraMessage)
        {
            byte[] udpMsgForPktForwarder = new Byte[0];
            LoraDeviceInfo joinLoraDeviceInfo;
            var joinReq = (LoRaPayloadJoinRequest)loraMessage.PayloadMessage;
            Array.Reverse(joinReq.DevEUI);
            Array.Reverse(joinReq.AppEUI);


            string devEui = BitConverter.ToString(joinReq.DevEUI).Replace("-", "");
            string devNonce = BitConverter.ToString(joinReq.DevNonce).Replace("-", "");

            Logger.Log(devEui, $"join request received", Logger.LoggingLevel.Info);

            //checking if this devnonce was already processed or the deveui was already refused
            Cache.TryGetValue(devEui, out joinLoraDeviceInfo);

            //we have a join request in the cache
            if (joinLoraDeviceInfo != null)
            {
                //it is not our device so ingore the join
                if (!joinLoraDeviceInfo.IsOurDevice)
                {
                    Logger.Log(devEui, $"join request refused the device is not ours", Logger.LoggingLevel.Info);
                    return null;
                }
                //is our device but the join was not valid
                else if (!joinLoraDeviceInfo.IsJoinValid)
                {
                    //if the devNonce is equal to the current it is a potential replay attck
                    if (joinLoraDeviceInfo.DevNonce == devNonce)
                    {
                        Logger.Log(devEui, $"join request refused devNonce already used", Logger.LoggingLevel.Info);
                        return null;
                    }

                    //Check if the device is trying to join through the wrong gateway
                    if (!String.IsNullOrEmpty(joinLoraDeviceInfo.GatewayID) && joinLoraDeviceInfo.GatewayID.ToUpper() != GatewayID.ToUpper())
                    {
                        Logger.Log(devEui, $"trying to join not through its linked gateway, ignoring join request", Logger.LoggingLevel.Info);
                        return null;
                    }
                }
            }

            joinLoraDeviceInfo = await LoraDeviceInfoManager.PerformOTAAAsync(GatewayID, devEui, BitConverter.ToString(joinReq.AppEUI).Replace("-", ""), devNonce);


            if (joinLoraDeviceInfo != null && joinLoraDeviceInfo.IsJoinValid)
            {
                byte[] appNonce = StringToByteArray(joinLoraDeviceInfo.AppNonce);
                byte[] netId = StringToByteArray(joinLoraDeviceInfo.NetId);
                byte[] devAddr = StringToByteArray(joinLoraDeviceInfo.DevAddr);
                string appKey = joinLoraDeviceInfo.AppKey;
                Array.Reverse(netId);
                Array.Reverse(appNonce);
                LoRaPayloadJoinAccept loRaPayloadJoinAccept = new LoRaPayloadJoinAccept(
                    //NETID 0 / 1 is default test 
                    BitConverter.ToString(netId).Replace("-", ""),
                    //todo add app key management
                    appKey,
                    //todo add device address management
                    devAddr,
                    appNonce
                    );
                var _datr = RegionFactory.CurrentRegion.GetDownstreamDR(((UplinkPktFwdMessage)loraMessage.LoraMetadata.FullPayload).rxpk[0]);
                uint _rfch = ((UplinkPktFwdMessage)loraMessage.LoraMetadata.FullPayload).rxpk[0].rfch;
                double _freq = RegionFactory.CurrentRegion.GetDownstreamChannel(((UplinkPktFwdMessage)loraMessage.LoraMetadata.FullPayload).rxpk[0]);
                //set tmst for the normal case
                long _tmst = ((UplinkPktFwdMessage)loraMessage.LoraMetadata.FullPayload).rxpk[0].tmst + RegionFactory.CurrentRegion.join_accept_delay1 * 1000000;

                //uncomment to force second windows usage
                //Thread.Sleep(4600-(int)(DateTime.Now - startTimeProcessing).TotalMilliseconds);
                //in this case it's too late, we need to break
                if ((DateTime.UtcNow - startTimeProcessing) > TimeSpan.FromMilliseconds(RegionFactory.CurrentRegion.join_accept_delay2 * 1000))
                {
                    Logger.Log(devEui, $"processing of the join request took too long, sending no message", Logger.LoggingLevel.Info);
                    var physicalResponse = new PhysicalPayload(loraMessage.PhysicalPayload.token, PhysicalIdentifier.PULL_RESP, null);
                    return physicalResponse.GetMessage();
                }
                //in this case the second join windows must be used
                else if ((DateTime.UtcNow - startTimeProcessing) > TimeSpan.FromMilliseconds(RegionFactory.CurrentRegion.join_accept_delay2 * 1000 - 500))
                {
                    Logger.Log(devEui, $"processing of the join request took too long, using second join accept receive window", Logger.LoggingLevel.Info);
                    _tmst = ((UplinkPktFwdMessage)loraMessage.LoraMetadata.FullPayload).rxpk[0].tmst + RegionFactory.CurrentRegion.join_accept_delay2 * 1000000;
                    if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("RX2_DATR")))
                    {
                        Logger.Log(devEui, $"using standard second receive windows for join request", Logger.LoggingLevel.Info);
                        //using EU fix DR for RX2
                        _freq = RegionFactory.CurrentRegion.RX2DefaultReceiveWindows.frequency;
                        _datr = RegionFactory.CurrentRegion.DRtoConfiguration[RegionFactory.CurrentRegion.RX2DefaultReceiveWindows.dr].configuration;
                    }
                    //if specific twins are set, specify second channel to be as specified
                    else
                    {
                        Logger.Log(devEui, $"using custom DR second receive windows for join request", Logger.LoggingLevel.Info);
                        _freq = double.Parse(Environment.GetEnvironmentVariable("RX2_FREQ"));
                        _datr = Environment.GetEnvironmentVariable("RX2_DATR");
                    }
                }
                LoRaMessage joinAcceptMessage = new LoRaMessage(loRaPayloadJoinAccept, LoRaMessageType.JoinAccept, loraMessage.PhysicalPayload.token, _datr, 0, _freq, _tmst);
                udpMsgForPktForwarder = joinAcceptMessage.PhysicalPayload.GetMessage();
                //join request resets the frame counters
                joinLoraDeviceInfo.FCntUp = 0;
                joinLoraDeviceInfo.FCntDown = 0;
                //update reported properties and frame Counter
                await joinLoraDeviceInfo.HubSender.UpdateReportedPropertiesOTAAasync(joinLoraDeviceInfo);
                //add to cache for processing normal messages. This awoids one additional call to the server.
                Cache.AddToCache(joinLoraDeviceInfo.DevAddr, joinLoraDeviceInfo);
                Logger.Log(devEui, $"join accept sent", Logger.LoggingLevel.Info);
            }
            else
            {
                Logger.Log(devEui, $"join request refused", Logger.LoggingLevel.Info);

            }
            
            //add to cache to avoid replay attack, btw server side does the check too.
            //TODO Ronnie Check
            //Cache.AddToCache(devEui, joinLoraDeviceInfo);

            return udpMsgForPktForwarder;
        }

        private byte[] StringToByteArray(string hex)
        {

            return Enumerable.Range(0, hex.Length)
                             .Where(x => x % 2 == 0)
                             .Select(x => Convert.ToByte(hex.Substring(x, 2), 16))
                             .ToArray();
        }

        public void Dispose()
        {

        }
    }
}
