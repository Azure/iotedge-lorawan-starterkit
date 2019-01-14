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
using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace LoRaWan.NetworkServer.V2
{
    /// <summary>
    /// Message processor (work in progress)    
    /// </summary>
    /// <remarks>
    /// Refactor of current processor with the following goals in mind
    /// - Easier to understand and extend
    /// - Unit testable
    /// </remarks>
    public class MessageProcessor
    {
        // Defines Cloud to device message property containing fport value
        const string FPORT_MSG_PROPERTY_KEY = "fport";

        // Fport value reserved for mac commands
        const byte LORA_FPORT_RESERVED_MAC_MSG = 0;

        // Starting Fport value reserved for future applications
        const byte LORA_FPORT_RESERVED_FUTURE_START = 224;

        private readonly NetworkServerConfiguration configuration;
        private readonly ILoRaDeviceRegistry deviceRegistry;
        private readonly ILoRaDeviceFrameCounterUpdateStrategyFactory frameCounterUpdateStrategyFactory;
        private readonly ILoRaPayloadDecoder payloadDecoder;
        volatile private Region loraRegion;

        public MessageProcessor(
            NetworkServerConfiguration configuration,
            ILoRaDeviceRegistry deviceRegistry,
            ILoRaDeviceFrameCounterUpdateStrategyFactory frameCounterUpdateStrategyFactory,
            ILoRaPayloadDecoder payloadDecoder)
        {
            this.configuration = configuration;
            this.deviceRegistry = deviceRegistry;
            this.frameCounterUpdateStrategyFactory = frameCounterUpdateStrategyFactory;
            this.payloadDecoder = payloadDecoder;

            // Register frame counter initializer
            // It will take care of seeding ABP devices created here for single gateway scenarios
            this.deviceRegistry.RegisterDeviceInitializer(new FrameCounterLoRaDeviceInitializer(configuration.GatewayID, frameCounterUpdateStrategyFactory));
        }

        
        /// <summary>
        /// Process a raw message
        /// </summary>
        /// <param name="rxpk"></param>
        /// <returns></returns>
        public Task<DownlinkPktFwdMessage> ProcessMessageAsync(Rxpk rxpk) => ProcessMessageAsync(rxpk, DateTime.UtcNow);


        /// <summary>
        /// Process a raw message
        /// </summary>
        /// <param name="rxpk"></param>
        /// <param name="startTimeProcessing"></param>
        /// <returns></returns>
        public async Task<DownlinkPktFwdMessage> ProcessMessageAsync(Rxpk rxpk, DateTime startTimeProcessing)
        {
            if (!LoRaPayload.TryCreateLoRaPayload(rxpk, out LoRaPayload loRaPayload))
            {
                Logger.Log("There was a problem in decoding the Rxpk", Logger.LoggingLevel.Error);
                return null;
            }

            if (this.loraRegion == null)
            {
                if (!RegionFactory.TryResolveRegion(rxpk))
                {
                    // log is generated in Region factory
                    // move here once V2 goes GA
                    return null;
                }

                this.loraRegion = RegionFactory.CurrentRegion;
            }
            
            if (loRaPayload.LoRaMessageType == LoRaMessageType.JoinRequest)
            {
                return await ProcessJoinRequestAsync(rxpk, (LoRaPayloadJoinRequest)loRaPayload, startTimeProcessing);
            }
            else if (loRaPayload.LoRaMessageType == LoRaMessageType.UnconfirmedDataUp || loRaPayload.LoRaMessageType == LoRaMessageType.ConfirmedDataUp)
            {
                return await ProcessLoRaMessageAsync(rxpk, (LoRaPayloadData)loRaPayload, startTimeProcessing);
            }
            
            return null;
        }

        
        // Process LoRa message where the payload is of type LoRaPayloadData
        async Task<DownlinkPktFwdMessage> ProcessLoRaMessageAsync(LoRaTools.LoRaPhysical.Rxpk rxpk, LoRaPayloadData loraPayload, DateTime startTime)
        {
            var devAddr = loraPayload.DevAddr;          

            var timeWatcher = new LoRaOperationTimeWatcher(this.loraRegion, startTime);
            using (var processLogger = new ProcessLogger(timeWatcher, devAddr))
            {
                if (!IsValidNetId(loraPayload.GetNetID()))
                {
                    //Log("Invalid netid");                    
                    return null;
                }

                // Find device that matches:
                // - devAddr
                // - mic check (requires: loraDeviceInfo.NwkSKey or loraDeviceInfo.AppKey, rxpk.LoraPayload.Mic)
                // - gateway id
                var loRaDevice = await deviceRegistry.GetDeviceForPayloadAsync(loraPayload);
                if (loRaDevice == null)
                {
                    Logger.Log(ConversionHelper.ByteArrayToString(devAddr), $"device is not from our network, ignoring message", Logger.LoggingLevel.Info);
                    return null;
                }

                // Add context to logger
                processLogger.SetDevEUI(loRaDevice.DevEUI);

                var isMultiGateway = !string.Equals(loRaDevice.GatewayID, configuration.GatewayID, StringComparison.InvariantCultureIgnoreCase);
                var frameCounterStrategy = isMultiGateway ?
                    frameCounterUpdateStrategyFactory.GetMultiGatewayStrategy() :
                    frameCounterUpdateStrategyFactory.GetSingleGatewayStrategy();

                var payloadFcnt = loraPayload.GetFcnt();
                var requiresConfirmation = loraPayload.IsConfirmed();

                using (new LoRaDeviceFrameCounterSession(loRaDevice, frameCounterStrategy))
                {
                    // Leaf devices that restart lose the counter. In relax mode we accept the incoming frame counter
                    // ABP device does not reset the Fcnt so in relax mode we should reset for 0 (LMIC based) or 1
                    var isRelaxedPayloadFrameCounter = false;
                    if (loRaDevice.IsABP && loRaDevice.IsABPRelaxedFrameCounter && loRaDevice.FCntUp >= 0 && payloadFcnt <= 1)
                    {
                        // known problem when device restarts, starts fcnt from zero
                        _ = frameCounterStrategy.ResetAsync(loRaDevice);
                        isRelaxedPayloadFrameCounter = true;
                    }

                    // Reply attack or confirmed reply
                    // Confirmed resubmit: A confirmed message that was received previously but we did not answer in time
                    // Device will send it again and we just need to return an ack (but also check for C2D to send it over)
                    var isConfirmedResubmit = false;
                    if (!isRelaxedPayloadFrameCounter && payloadFcnt <= loRaDevice.FCntUp)
                    {
                        // Future: Keep track of how many times we acked the confirmed message (4+ times we skip)
                        //if it is confirmed most probably we did not ack in time before or device lost the ack packet so we should continue but not send the msg to iothub 
                        if (requiresConfirmation && payloadFcnt == loRaDevice.FCntUp)
                        {
                            isConfirmedResubmit = true;
                            Logger.Log(loRaDevice.DevEUI, $"resubmit from confirmed message detected, msg: {payloadFcnt} server: {loRaDevice.FCntUp}", Logger.LoggingLevel.Info);
                        }
                        else
                        {
                            Logger.Log(loRaDevice.DevEUI, $"invalid frame counter, message ignored, msg: {payloadFcnt} server: {loRaDevice.FCntUp}", Logger.LoggingLevel.Info);
                            return null;
                        }
                    }

                    var fcntDown = 0;
                    // If it is confirmed it require us to update the frame counter down
                    // Multiple gateways: in redis, otherwise in device twin
                    if (requiresConfirmation)
                    {
                        fcntDown = await frameCounterStrategy.NextFcntDown(loRaDevice);

                        // Failed to update the fcnt down
                        // In multi gateway scenarios it means the another gateway was faster than using, can stop now
                        if (fcntDown <= 0)
                        {
                            return null;
                        }

                        Logger.Log(loRaDevice.DevEUI, $"down frame counter: {loRaDevice.FCntDown}", Logger.LoggingLevel.Info);
                    }


                    if (!isConfirmedResubmit)
                    {
                        var validFcntUp = isRelaxedPayloadFrameCounter || (payloadFcnt > loRaDevice.FCntUp);
                        if (validFcntUp)
                        {
                            Logger.Log(loRaDevice.DevEUI, $"valid frame counter, msg: {payloadFcnt} server: {loRaDevice.FCntUp}", Logger.LoggingLevel.Info);

                            object payloadData = null;
                            // if it is an upward acknowledgement from the device it does not have a payload
                            // This is confirmation from leaf device that he received a C2D confirmed
                            if (!loraPayload.IsUpwardAck())
                            {
                                byte[] decryptedPayloadData = null;
                                try
                                {
                                    decryptedPayloadData = loraPayload.PerformEncryption(loRaDevice.AppSKey);
                                }
                                catch (Exception ex)
                                {
                                    Logger.Log(loRaDevice.DevEUI, $"failed to decrypt message: {ex.Message}", Logger.LoggingLevel.Error);
                                }

                                
                                var fportUp = loraPayload.GetFPort();

                                if (string.IsNullOrEmpty(loRaDevice.SensorDecoder))
                                {
                                    Logger.Log(loRaDevice.DevEUI, $"no decoder set in device twin. port: {fportUp}", Logger.LoggingLevel.Full);
                                    payloadData = Convert.ToBase64String(decryptedPayloadData);
                                }
                                else
                                {
                                    Logger.Log(loRaDevice.DevEUI, $"decoding with: {loRaDevice.SensorDecoder} port: {fportUp}", Logger.LoggingLevel.Full);
                                    payloadData = await payloadDecoder.DecodeMessageAsync(decryptedPayloadData, fportUp, loRaDevice.SensorDecoder);
                                }
                            }


                            // What do we need to send an UpAck to IoT Hub?
                            // What is the content of the message
                            // TODO Future: Don't wait if it is an unconfirmed message
                            await SendDeviceEventAsync(loRaDevice, rxpk, payloadData, loraPayload, timeWatcher);

                            loRaDevice.SetFcntUp(payloadFcnt);
                        }
                        else
                        {
                            Logger.Log(loRaDevice.DevEUI, $"invalid frame counter, msg: {payloadFcnt} server: {loRaDevice.FCntUp}", Logger.LoggingLevel.Info);
                        }
                    }

                    // We check if we have time to futher progress or not
                    // C2D checks are quite expensive so if we are really late we just stop here
                    var timeToSecondWindow = timeWatcher.GetRemainingTimeToReceiveSecondWindow(loRaDevice);
                    if (timeToSecondWindow < LoRaOperationTimeWatcher.ExpectedTimeToPackageAndSendMessage)
                    {
                        return null;
                    }

                    // If it is confirmed and we don't have time to check c2d and send to device we return now
                    if (requiresConfirmation && timeToSecondWindow <= (LoRaOperationTimeWatcher.ExpectedTimeToCheckCloudToDeviceMessagePackageAndSendMessage))
                    {
                        return CreateDownlinkMessage(
                            null,
                            loRaDevice,
                            rxpk,
                            loraPayload,
                            timeWatcher,
                            devAddr,
                            false, // fpending
                            (ushort)fcntDown
                        );
                    }

                    // ReceiveAsync has a longer timeout
                    // But we wait less that the timeout (available time before 2nd window)
                    // if message is received after timeout, keep it in loraDeviceInfo and return the next call
                    var cloudToDeviceReceiveTimeout = timeToSecondWindow - (LoRaOperationTimeWatcher.ExpectedTimeToCheckCloudToDeviceMessagePackageAndSendMessage);
                    var cloudToDeviceMessage = await loRaDevice.ReceiveCloudToDeviceAsync(cloudToDeviceReceiveTimeout);
                    if (cloudToDeviceMessage != null && !ValidateCloudToDeviceMessage(loRaDevice, cloudToDeviceMessage))
                    {
                        _ = loRaDevice.CompleteCloudToDeviceMessageAsync(cloudToDeviceMessage);
                        cloudToDeviceMessage = null;
                    }

                    // Flag indicating if there is another C2D message waiting
                    var fpending = false;
                    if (cloudToDeviceMessage != null)
                    {
                        if (!requiresConfirmation)
                        {
                            // The message coming from the device was not confirmed, therefore we did not computed the frame count down
                            // Now we need to increment because there is a C2D message to be sent
                            fcntDown = await frameCounterStrategy.NextFcntDown(loRaDevice);

                            requiresConfirmation = true;
                        }

                        timeToSecondWindow = timeWatcher.GetRemainingTimeToReceiveSecondWindow(loRaDevice);
                        if (timeToSecondWindow > LoRaOperationTimeWatcher.ExpectedTimeToCheckCloudToDeviceMessagePackageAndSendMessage)
                        {
                            var additionalMsg = await loRaDevice.ReceiveCloudToDeviceAsync(timeToSecondWindow - LoRaOperationTimeWatcher.ExpectedTimeToCheckCloudToDeviceMessagePackageAndSendMessage);
                            if (additionalMsg != null)
                            {
                                fpending = true;
                                _ = loRaDevice.AbandonCloudToDeviceMessageAsync(additionalMsg);
                            }
                        }
                    }


                    // No C2D message and request was not confirmed, return nothing
                    if (!requiresConfirmation)
                    {
                        // TODO: can we let the session save it?
                        //await frameCounterStrategy.SaveChangesAsync(loRaDevice);                    
                        return null;
                    }

                    // We did it in the LoRaPayloadData constructor
                    // we got here:
                    // a) was a confirm request
                    // b) we have a c2d message
                    var confirmDownstream = CreateDownlinkMessage(
                        cloudToDeviceMessage,
                        loRaDevice,
                        rxpk,
                        loraPayload,
                        timeWatcher,
                        devAddr,
                        fpending,
                        (ushort)fcntDown
                    );

                    if (cloudToDeviceMessage != null)
                    {
                        if (confirmDownstream == null)
                        {
                            _ = loRaDevice.AbandonCloudToDeviceMessageAsync(cloudToDeviceMessage);
                        }
                        else
                        {
                            _ = loRaDevice.CompleteCloudToDeviceMessageAsync(cloudToDeviceMessage);
                        }
                    }

                    return confirmDownstream;             
                }
            }
        }

        /// <summary>
        /// Creates downlink message with ack for confirmation or cloud to device message
        /// </summary>
        DownlinkPktFwdMessage CreateDownlinkMessage(
            Message cloudToDeviceMessage, 
            LoRaDevice loraDeviceInfo, 
            Rxpk rxpk,
            LoRaPayloadData loRaPayloadData, 
            LoRaOperationTimeWatcher timeWatcher,
            ReadOnlyMemory<byte> payloadDevAddr,
            bool fpending,
            ushort fcntDown)
        {

            //default fport
            byte fctrl = 0;
            if (loRaPayloadData.LoRaMessageType == LoRaMessageType.ConfirmedDataUp)
            {
                // Confirm receiving message to device                            
                fctrl = (byte)FctrlEnum.Ack;
            }

            byte? fport = null;
            var requiresDeviceAcknowlegement = false;
            byte[] macbytes = null;

            byte[] rndToken = new byte[2];
            Random rnd = new Random();
            rnd.NextBytes(rndToken);

            byte[] frmPayload = null;

            if (cloudToDeviceMessage != null)
            {
                if (cloudToDeviceMessage.Properties.TryGetValueCaseInsensitive("cidtype", out var cidTypeValue))
                {
                    Logger.Log(loraDeviceInfo.DevEUI, $"Cloud to device MAC command received", Logger.LoggingLevel.Info);
                    MacCommandHolder macCommandHolder = new MacCommandHolder(Convert.ToByte(cidTypeValue));
                    macbytes = macCommandHolder.macCommand[0].ToBytes();
                }

                if (cloudToDeviceMessage.Properties.TryGetValueCaseInsensitive("confirmed", out var confirmedValue) && confirmedValue.Equals("true", StringComparison.OrdinalIgnoreCase))
                {
                    requiresDeviceAcknowlegement = true;
                    Logger.Log(loraDeviceInfo.DevEUI, $"Cloud to device message requesting a confirmation", Logger.LoggingLevel.Info);

                }
                if (cloudToDeviceMessage.Properties.TryGetValueCaseInsensitive("fport", out var fPortValue))
                {
                    fport = byte.Parse(fPortValue);
                    Logger.Log(loraDeviceInfo.DevEUI, $"Cloud to device message with a Fport of " + fport.Value, Logger.LoggingLevel.Info);

                }
                Logger.Log(loraDeviceInfo.DevEUI, string.Format("Sending a downstream message with ID {0}",
                    ConversionHelper.ByteArrayToString(rndToken)),
                    Logger.LoggingLevel.Full);


                frmPayload = cloudToDeviceMessage?.GetBytes();
            
                Logger.Log(loraDeviceInfo.DevEUI, $"C2D message: {Encoding.UTF8.GetString(frmPayload)}", Logger.LoggingLevel.Info);
                
                //cut to the max payload of lora for any EU datarate
                if (frmPayload.Length > 51)
                    Array.Resize(ref frmPayload, 51);

                Array.Reverse(frmPayload);
            }
            
            if (fpending)
            {
                fctrl |= (int)FctrlEnum.FpendingOrClassB;
            }
            
            // if (macbytes != null && linkCheckCmdResponse != null)
            //     macbytes = macbytes.Concat(linkCheckCmdResponse).ToArray();


            var reversedDevAddr = new byte[payloadDevAddr.Length];
            var srcDevAddr = payloadDevAddr.Span;
            for (int i=reversedDevAddr.Length-1; i >= 0; --i)
            {
                reversedDevAddr[i] = srcDevAddr[srcDevAddr.Length - (1 + i)];
            }

            var msgType = requiresDeviceAcknowlegement ? LoRaMessageType.ConfirmedDataDown : LoRaMessageType.UnconfirmedDataDown;
            var ackLoRaMessage = new LoRaPayloadData(
                msgType,
                reversedDevAddr,
                new byte[] { fctrl },
                BitConverter.GetBytes(fcntDown),
                macbytes,
                fport.HasValue ? new byte[] { fport.Value } : null,
                frmPayload,
                1);

           

            var receiveWindow = timeWatcher.ResolveReceiveWindowToUse(loraDeviceInfo);
            if (receiveWindow == 0)
                return null;
            
            string datr = null;
            double freq;            
            long tmst;
            if (receiveWindow == 2)
            {
                tmst = rxpk.tmst + loraRegion.receive_delay2 * 1000000;

                if (string.IsNullOrEmpty(configuration.Rx2DataRate))
                {
                    Logger.Log(loraDeviceInfo.DevEUI, $"using standard second receive windows", Logger.LoggingLevel.Info);
                    freq = loraRegion.RX2DefaultReceiveWindows.frequency;
                    datr = loraRegion.DRtoConfiguration[loraRegion.RX2DefaultReceiveWindows.dr].configuration;
                }
                //if specific twins are set, specify second channel to be as specified
                else
                {
                    freq = configuration.Rx2DataFrequency;
                    datr = configuration.Rx2DataRate;
                    Logger.Log(loraDeviceInfo.DevEUI, $"using custom DR second receive windows freq : {freq}, datr:{datr}", Logger.LoggingLevel.Info);
                }
            }
            else
            {
                datr = this.loraRegion.GetDownstreamDR(rxpk);
                freq = this.loraRegion.GetDownstreamChannel(rxpk);
                tmst = rxpk.tmst + loraRegion.receive_delay1 * 1000000;
            }


            //todo: check the device twin preference if using confirmed or unconfirmed down    
            var loRaMessageType = (msgType == LoRaMessageType.ConfirmedDataDown) ? LoRaMessageType.ConfirmedDataDown : LoRaMessageType.UnconfirmedDataDown;
            return ackLoRaMessage.Serialize(rxpk,loraDeviceInfo.AppSKey,loraDeviceInfo.NwkSKey, datr, freq, tmst,loraDeviceInfo.DevEUI);
        }        

        private bool ValidateCloudToDeviceMessage(LoRaDevice loRaDevice, Message cloudToDeviceMsg)
        {
            // ensure fport property has been set
            if (!cloudToDeviceMsg.Properties.TryGetValueCaseInsensitive(FPORT_MSG_PROPERTY_KEY, out var fportValue))
            {
                Logger.Log(loRaDevice.DevEUI, $"missing {FPORT_MSG_PROPERTY_KEY} property in C2D message '{cloudToDeviceMsg.MessageId}'", Logger.LoggingLevel.Error);
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

            Logger.Log(loRaDevice.DevEUI, $"invalid fport '{fportValue}' in C2D message '{cloudToDeviceMsg.MessageId}'", Logger.LoggingLevel.Error);
            return false;
        }

        // Sends device telemetry data to IoT Hub
        private async Task SendDeviceEventAsync(LoRaDevice loRaDevice, Rxpk rxpk, object payloadData, LoRaPayloadData loRaPayloadData, LoRaOperationTimeWatcher timeWatcher)
        {            
            var deviceTelemetry = new LoRaDeviceTelemetry(rxpk);
            deviceTelemetry.DeviceEUI = loRaDevice.DevEUI;
            deviceTelemetry.GatewayID = this.configuration.GatewayID;
            deviceTelemetry.Edgets = (long)((timeWatcher.Start - DateTime.UnixEpoch).TotalMilliseconds);

            if (payloadData != null)
            {
                deviceTelemetry.data = payloadData;
            }

            Dictionary<string, string> eventProperties = null;
            var macCommand = loRaPayloadData.GetMacCommands();
            if (macCommand.macCommand.Count > 0)
            {
                eventProperties = new Dictionary<string, string>();

                for (int i = 0; i < macCommand.macCommand.Count; i++)
                {                    
                    eventProperties[macCommand.macCommand[i].Cid.ToString()] = JsonConvert.SerializeObject(macCommand.macCommand[i], Formatting.None);
                    
                    //in case it is a link check mac, we need to send it downstream.
                    if (macCommand.macCommand[i].Cid == CidEnum.LinkCheckCmd)
                    {
                        //linkCheckCmdResponse = new LinkCheckCmd(rxPk.GetModulationMargin(), 1).ToBytes();
                    }
                }
            }

            await loRaDevice.SendEventAsync(deviceTelemetry, eventProperties);

            var payloadAsRaw = deviceTelemetry.data as string;
            if (payloadAsRaw == null && deviceTelemetry.data != null)
            {
                payloadAsRaw = JsonConvert.SerializeObject(deviceTelemetry.data, Formatting.None);
            }

            Logger.Log(loRaDevice.DevEUI, $"message '{payloadAsRaw}' sent to hub", Logger.LoggingLevel.Info);
        }


        bool IsValidNetId(byte netid)
        {
            return true;
        }


        /// <summary>
        /// Process OTAA join request
        /// </summary>
        async Task<DownlinkPktFwdMessage> ProcessJoinRequestAsync(Rxpk rxpk, LoRaPayloadJoinRequest joinReq,DateTime startTimeProcessing)
        {        
            var timeWatcher = new LoRaOperationTimeWatcher(this.loraRegion, startTimeProcessing);
            using (var processLogger = new ProcessLogger(timeWatcher))
            {

                byte[] udpMsgForPktForwarder = new Byte[0];
                
                joinReq.DevEUI.Span.Reverse();
                joinReq.AppEUI.Span.Reverse();
                var devEUI = LoRaTools.Utils.ConversionHelper.ByteArrayToString(joinReq.DevEUI);
                var appEUI = LoRaTools.Utils.ConversionHelper.ByteArrayToString(joinReq.AppEUI);
                
                // var reversedDevEUICopy = ReverseCopyArray(joinReq.DevEUI);
                // var reversedAppEUICopy = ReverseCopyArray(joinReq.AppEUI);
                // var devEUI = LoRaTools.Utils.ConversionHelper.ByteArrayToString(reversedDevEUICopy);
                // var appEUI = LoRaTools.Utils.ConversionHelper.ByteArrayToString(reversedAppEUICopy);

                // set context to logger
                processLogger.SetDevEUI(devEUI);

                var devNonce = LoRaTools.Utils.ConversionHelper.ByteArrayToString(joinReq.DevNonce);
                Logger.Log(devEUI, $"join request received", Logger.LoggingLevel.Info);

                

                var loRaDevice = await this.deviceRegistry.GetDeviceForJoinRequestAsync(devEUI, appEUI, devNonce);
                if (loRaDevice == null)
                    return null;

                if (string.IsNullOrEmpty(loRaDevice.AppKey))
                {
                    Logger.Log(loRaDevice.DevEUI, "missing AppKey for OTAA for device", Logger.LoggingLevel.Error);
                    return null;
                }

                if (!joinReq.CheckMic(loRaDevice.AppKey))
                {
                    //Logger.Log(devEUI, $"join request MIC invalid", Logger.LoggingLevel.Info);
                    Logger.Log(devEUI, "join refused: invalid MIC", Logger.LoggingLevel.Error);
                    return null;
                }

                if (loRaDevice.AppEUI != appEUI)
                {
                    Logger.Log(devEUI, "join refused: AppEUI for OTAA does not match for device", Logger.LoggingLevel.Error);
                    return null;
                }

                // Make sure that is a new request and not a replay         
                if (!string.IsNullOrEmpty(loRaDevice.DevNonce) && loRaDevice.DevNonce == devNonce)
                {
                    Logger.Log(devEUI, "join refused: DevNonce already used by this device", Logger.LoggingLevel.Info);
                    loRaDevice.IsJoinValid = false;
                    return null;
                }


                //Check that the device is joining through the linked gateway and not another
                if (!string.IsNullOrEmpty(loRaDevice.GatewayID) && !string.Equals(loRaDevice.GatewayID, configuration.GatewayID, StringComparison.InvariantCultureIgnoreCase))
                {
                    Logger.Log(devEUI, $"join refused: trying to join not through its linked gateway, ignoring join request", Logger.LoggingLevel.Info);
                    loRaDevice.IsJoinValid = false;
                    loRaDevice.IsOurDevice = false;
                    return null;
                }

                var netId = new byte[3] { 0, 0, 1 };
                var appNonce = OTAAKeysGenerator.getAppNonce();
                var appNonceBytes = LoRaTools.Utils.ConversionHelper.StringToByteArray(appNonce);
                var appKeyBytes = LoRaTools.Utils.ConversionHelper.StringToByteArray(loRaDevice.AppKey);
                var appSKey = OTAAKeysGenerator.calculateKey(new byte[1] { 0x02 }, appNonceBytes, netId, joinReq.DevNonce, appKeyBytes);
                var nwkSKey = OTAAKeysGenerator.calculateKey(new byte[1] { 0x01 }, appNonceBytes, netId, joinReq.DevNonce, appKeyBytes);
                var devAddr = OTAAKeysGenerator.getDevAddr(netId);

                if (!timeWatcher.InTimeForJoinAccept())
                {
                    // in this case it's too late, we need to break and avoid saving twins
                    Logger.Log(devEUI, $"join refused: processing of the join request took too long, sending no message", Logger.LoggingLevel.Info);
                    return null;
                }

                Logger.Log(loRaDevice.DevEUI, $"saving join properties twins", Logger.LoggingLevel.Full);
                var deviceUpdateSucceeded = await loRaDevice.UpdateAfterJoinAsync(devAddr, nwkSKey, appSKey, appNonce, devNonce, LoRaTools.Utils.ConversionHelper.ByteArrayToString(netId));
                Logger.Log(loRaDevice.DevEUI, $"done saving join properties twins", Logger.LoggingLevel.Full);

                if (!deviceUpdateSucceeded)
                {
                    Logger.Log(devEUI, $"join refused: join request could not save twins", Logger.LoggingLevel.Error);
                    return null;
                }

                var datr = this.loraRegion.GetDownstreamDR(rxpk);
                uint rfch = rxpk.rfch;
                double freq = this.loraRegion.GetDownstreamChannel(rxpk);
                //set tmst for the normal case
                long tmst = rxpk.tmst + this.loraRegion.join_accept_delay1 * 1000000;


                // in this case the second join windows must be used
                var timeToFirstWindow = timeWatcher.GetRemainingTimeToJoinAcceptFirstWindow();
                if (timeToFirstWindow < LoRaOperationTimeWatcher.ExpectedTimeToPackageAndSendMessage)
                {
                    Logger.Log(devEUI, $"processing of the join request took too long, using second join accept receive window", Logger.LoggingLevel.Info);
                    tmst = rxpk.tmst + this.loraRegion.join_accept_delay2 * 1000000;
                    if (string.IsNullOrEmpty(configuration.Rx2DataRate))
                    {
                        Logger.Log(devEUI, $"using standard second receive windows for join request", Logger.LoggingLevel.Info);
                        //using EU fix DR for RX2
                        freq = this.loraRegion.RX2DefaultReceiveWindows.frequency;
                        datr = this.loraRegion.DRtoConfiguration[RegionFactory.CurrentRegion.RX2DefaultReceiveWindows.dr].configuration;
                    }
                    //if specific twins are set, specify second channel to be as specified
                    else
                    {
                        Logger.Log(devEUI, $"using custom  second receive windows for join request", Logger.LoggingLevel.Info);
                        freq = configuration.Rx2DataFrequency;
                        datr = configuration.Rx2DataRate;
                    }
                }

                loRaDevice.IsOurDevice = true;
                this.deviceRegistry.UpdateDeviceAfterJoin(loRaDevice);

                // Build join accept downlink message
                Array.Reverse(netId);
                Array.Reverse(appNonceBytes);



                return CreateJoinAcceptDownlinkMessage(
                    //NETID 0 / 1 is default test 
                    netId: netId,
                    //todo add app key management
                    appKey: loRaDevice.AppKey,
                    //todo add device address management
                    devAddr: devAddr,
                    appNonce: appNonceBytes,
                    datr: datr,
                    freq: freq,
                    tmst: tmst,
                    rxpk: rxpk,
                    devEUI: devEUI
                    );
            }
        }

        /// <summary>
        /// Creates a reversed copy of an byte array
        /// </summary>
        /// <param name="srcArray"></param>
        /// <returns></returns>
        private byte[] ReverseCopyArray(ReadOnlyMemory<byte> srcArray)
        {
            var srcArraySpan = srcArray.Span;
            var res = new byte[srcArray.Length];
            var srcIndex = 0;
            for (var i = res.Length-1; i >= 0; i--, srcIndex++)
                res[i] = srcArraySpan[srcIndex];

            return res;
        }



        /// <summary>
        /// Creates downlink message for join accept
        /// </summary>
        DownlinkPktFwdMessage CreateJoinAcceptDownlinkMessage(
            ReadOnlyMemory<byte> netId,
            string appKey,
            string devAddr,
            ReadOnlyMemory<byte> appNonce,
            string datr,
            double freq,
            long tmst,
            Rxpk rxpk,
            string devEUI
        )
        {
            var loRaPayloadJoinAccept = new LoRaTools.LoRaMessage.LoRaPayloadJoinAccept(
                    //NETID 0 / 1 is default test 
                    LoRaTools.Utils.ConversionHelper.ByteArrayToString(netId),         
                    //todo add device address management
                    ConversionHelper.StringToByteArray(devAddr),
                    appNonce.ToArray(),
                    new byte[] { 0 },
                    new byte[] { 0 },
                    null
                    );

  
            return loRaPayloadJoinAccept.Serialize(rxpk, appKey, datr, freq, tmst, devEUI);

        }
    }
}