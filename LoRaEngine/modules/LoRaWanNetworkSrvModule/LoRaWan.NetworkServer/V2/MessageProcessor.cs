//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using LoRaTools;
using LoRaTools.LoRaMessage;
using LoRaTools.Regions;
using LoRaTools.Utils;
using Microsoft.Azure.Devices.Client;
using Newtonsoft.Json;
using System;
using System.Collections;
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


        public class PhysicalPayload
        {
            public Rxpk[] GetRxpks() => null;
        }

        public class Rxpk
        {
            public LoRaPayload LoRaPayload { get; }

        }

        public class LoRaPayload
        {
            public string DevAddr { get; }
            public UInt16 NetId { get; }
            public UInt32 FcntUp { get; internal set; }

            public virtual bool CheckMic(string nwksKey)
            {
                return true;
            }

            public string GetDecryptedPayload(string appSKey)
            {
                return string.Empty;
            }
        }

        //Not a join message
        public class LoRaPayloadData : LoRaPayload
        {
            IEnumerable GetMacCommands() => null;

            public bool IsConfirmed() => false;

            public bool FPending { get; internal set; }

            internal bool IsUpwardAck()
            {
                throw new NotImplementedException();
            }
        }

        //downJoin
        public class LoRaPayloadJoinAccept : LoRaPayload
        {


        }

        //up join
        public class LoRaPayloadJoinRequest : LoRaPayload
        {


        }

        LoRaTools.LoRaMessage.LoRaPayloadJoinRequest __WorkaroundGetPayloadJoinRequest(LoRaTools.LoRaPhysical.Rxpk rxpk)
        {
            byte[] convertedInputMessage = Convert.FromBase64String(rxpk.data);
            return new LoRaTools.LoRaMessage.LoRaPayloadJoinRequest(convertedInputMessage);
        }


        LoRaTools.LoRaMessage.LoRaPayloadData __WorkaroundGetPayloadData(LoRaTools.LoRaPhysical.Rxpk rxpk)
        {
            byte[] convertedInputMessage = Convert.FromBase64String(rxpk.data);
            return new LoRaTools.LoRaMessage.LoRaPayloadData(convertedInputMessage);
        }

        byte __WorkaroundGetNetID(LoRaTools.LoRaMessage.LoRaPayloadData loRaPayloadData)
        {
            return (byte)(loRaPayloadData.DevAddr.Span[0] & 0b01111111);
        }

        int __WorkaroundGetFcnt(LoRaTools.LoRaMessage.LoRaPayloadData loRaPayloadData) => MemoryMarshal.Read<UInt16>(loRaPayloadData.Fcnt.Span);

        byte __WorkaroundGetFPort(LoRaTools.LoRaMessage.LoRaPayloadData loRaPayloadData)
        {
            byte fportUp = 0;
            if (loRaPayloadData.Fport.Span.Length > 0)
            {
                fportUp = (byte)loRaPayloadData.Fport.Span[0];
            }

            return fportUp;
        }

        LoRaMessageType __WorkaroundGetMessageType(LoRaTools.LoRaMessage.LoRaPayloadData loRaPayloadData)
        {
            var messageType = loRaPayloadData.RawMessage[0] >> 5;
            return (LoRaMessageType)messageType;
        }

        bool __WorkaroundIsConfirmed(LoRaTools.LoRaMessage.LoRaPayloadData loRaPayloadData) => __WorkaroundGetMessageType(loRaPayloadData) == LoRaMessageType.ConfirmedDataUp;

        bool __WorkaroundIsUpwardAck(LoRaTools.LoRaMessage.LoRaPayloadData loRaPayloadData) => __WorkaroundGetMessageType(loRaPayloadData) == LoRaMessageType.ConfirmedDataUp && loRaPayloadData.GetLoRaMessage().Frmpayload.Length == 0;


        /// <summary>
        /// Process a raw message
        /// </summary>
        /// <param name="rawMessage"></param>
        /// <param name="startTimeProcessing"></param>
        /// <returns></returns>
        public async Task<LoRaTools.LoRaPhysical.Txpk> ProcessMessageAsync(byte[] rawMessage, DateTime startTimeProcessing)
        {
            LoRaMessageWrapper loraMessage = new LoRaMessageWrapper(rawMessage);
            if (loraMessage.IsLoRaMessage)
            {
                //if (RegionFactory.CurrentRegion == null)
                //    RegionFactory.Create(loraMessage.PktFwdPayload.GetPktFwdMessage().Rxpks[0]);
                
                //join message
                if (loraMessage.LoRaMessageType == LoRaMessageType.JoinRequest)
                {
                    var rxpk = loraMessage.PktFwdPayload.GetPktFwdMessage().Rxpks[0];
                    return await ProcessJoinRequestAsync(rxpk);
                }
                //normal message
                else if (loraMessage.LoRaMessageType == LoRaMessageType.UnconfirmedDataUp || loraMessage.LoRaMessageType == LoRaMessageType.ConfirmedDataUp)
                {
                    var rxpk = loraMessage.PktFwdPayload.GetPktFwdMessage().Rxpks[0];
                    return await ProcessLoRaMessageAsync(rxpk, startTimeProcessing);
                }
            }
            return null;
        }

        // Process LoRa message where the payload is of type LoRaPayloadData
        public Task<LoRaTools.LoRaPhysical.Txpk> ProcessLoRaMessageAsync(LoRaTools.LoRaPhysical.Rxpk rxpk) => ProcessLoRaMessageAsync(rxpk, DateTime.UtcNow);
        
        // Process LoRa message where the payload is of type LoRaPayloadData
        public async Task<LoRaTools.LoRaPhysical.Txpk> ProcessLoRaMessageAsync(LoRaTools.LoRaPhysical.Rxpk rxpk, DateTime startTime)
        {
            var loraPayload = __WorkaroundGetPayloadData(rxpk);
            var devAddr = loraPayload.DevAddr;

            if (this.loraRegion == null)
            {
                this.loraRegion = RegionFactory.Create(rxpk);
                if (this.loraRegion == null)
                {
                    Logger.Log(LoRaTools.Utils.ConversionHelper.ByteArrayToString(devAddr), "invalid/unsupported region, current supported regions are (eu and us)", Logger.LoggingLevel.Info);
                    return null;
                }
            }

            var timeWatcher = new LoRaOperationTimeWatcher(this.loraRegion, startTime);
            using (var processLogger = new ProcessLogger(timeWatcher, devAddr))
            {
                if (!IsValidNetId(__WorkaroundGetNetID(loraPayload)))
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
                    return null;
                }

                // Add context to logger
                processLogger.SetDevEUI(loRaDevice.DevEUI);

                var frameCounterStrategy = (loRaDevice.GatewayID == configuration.GatewayID) ?
                    frameCounterUpdateStrategyFactory.GetSingleGatewayStrategy() :
                    frameCounterUpdateStrategyFactory.GetMultiGatewayStrategy();


                var payloadFcnt = this.__WorkaroundGetFcnt(loraPayload);
                var requiresConfirmation = __WorkaroundIsConfirmed(loraPayload);


                using (new LoRaDeviceFrameCounterSession(loRaDevice, frameCounterStrategy))
                {
                    // Leaf devices that restart lose the counter. In relax mode we accept the incoming frame counter
                    // ABP device does not reset the Fcnt so in relax mode we should reset for 0 (LMIC based) or 1
                    if (loRaDevice.IsABP && loRaDevice.IsABPRelaxedFrameCounter && loRaDevice.FCntUp > 0 && payloadFcnt <= 1)
                    {
                        // known problem when device restarts, starts fcnt from zero
                        //loraDeviceInfo.SetFcntUp(0);
                        //loraDeviceInfo.SetFcntDown(0);
                        //_ = SaveFcnt(loraDeviceInfo, force: true);
                        //if (loraDeviceInfo.GatewayID == null)
                        //    await ABPFcntCacheReset(loraDeviceInfo);
                        _ = frameCounterStrategy.ResetAsync(loRaDevice);
                    }

                    // Reply attack or confirmed reply
                    // Confirmed resubmit: A confirmed message that was received previously but we did not answer in time
                    // Device will send it again and we just need to return an ack (but also check for C2D to send it over)
                    var isConfirmedResubmit = false;
                    if (payloadFcnt <= loRaDevice.FCntUp)
                    {
                        // Future: Keep track of how many times we acked the confirmed message (4+ times we skip)
                        //if it is confirmed most probably we did not ack in time before or device lost the ack packet so we should continue but not send the msg to iothub 
                        if (requiresConfirmation && payloadFcnt == loRaDevice.FCntUp)
                        {
                            isConfirmedResubmit = true;
                        }
                        else
                        {
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
                        var validFcntUp = payloadFcnt > loRaDevice.FCntUp;
                        if (validFcntUp)
                        {
                            Logger.Log(loRaDevice.DevEUI, $"valid frame counter, msg: {payloadFcnt} server: {loRaDevice.FCntUp}", Logger.LoggingLevel.Info);

                            object payloadData = null;
                            // if it is an upward acknowledgement from the device it does not have a payload
                            // This is confirmation from leaf device that he received a C2D confirmed
                            if (!__WorkaroundIsUpwardAck(loraPayload))
                            {
                                var decryptedPayloadData = loraPayload.PerformEncryption(loRaDevice.AppSKey);
                                var fportUp = __WorkaroundGetFPort(loraPayload);

                                if (string.IsNullOrEmpty(loRaDevice.SensorDecoder))
                                {
                                    Logger.Log(loRaDevice.DevEUI, $"no decoder set in device twin. port: {fportUp}", Logger.LoggingLevel.Full);
                                    payloadData = Convert.ToBase64String(decryptedPayloadData);
                                }
                                else
                                {
                                    Logger.Log(loRaDevice.DevEUI, $"decoding with: {loRaDevice.SensorDecoder} port: {fportUp}", Logger.LoggingLevel.Full);
                                    payloadData = await payloadDecoder.DecodeMessage(decryptedPayloadData, fportUp, loRaDevice.SensorDecoder);
                                }
                            }


                            // What do we need to send an UpAck to IoT Hub?
                            // What is the content of the message
                            // TODO Future: Don't wait if it is an unconfirmed message
                            await SendDeviceEventAsync(loRaDevice, rxpk, payloadData, timeWatcher);

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
                    if (requiresConfirmation && timeToSecondWindow <= (LoRaOperationTimeWatcher.ExpectedTimeToCheckCloudToDeviceMessage + LoRaOperationTimeWatcher.ExpectedTimeToPackageAndSendMessage))
                    {

                        return new LoRaTools.LoRaPhysical.Txpk()
                        {
                        };
                    }

                    // ReceiveAsync has a longer timeout
                    // But we wait less that the timeout (available time before 2nd window)
                    // if message is received after timeout, keep it in loraDeviceInfo and return the next call
                    timeToSecondWindow = timeWatcher.GetRemainingTimeToReceiveSecondWindow(loRaDevice);
                    var cloudToDeviceMessage = await GetAndValidateCloudToDeviceMessageAsync(loRaDevice, timeToSecondWindow - LoRaOperationTimeWatcher.ExpectedTimeToCheckCloudToDeviceMessage);

                    var resultPayloadData = new LoRaPayloadData();
                    //loraPayload.IsConfirmed() ? LoRaMessageType.ConfirmedDataDown : LoRaMessageType.UnconfirmedDataDown);

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
                        if (timeToSecondWindow > LoRaOperationTimeWatcher.ExpectedTimeToPackageAndSendMessage)
                        {
                            var additionalMsg = await GetAndValidateCloudToDeviceMessageAsync(loRaDevice, timeToSecondWindow - LoRaOperationTimeWatcher.ExpectedTimeToCheckCloudToDeviceMessage);
                            if (additionalMsg != null)
                            {
                                resultPayloadData.FPending = true;
                                _ = loRaDevice.AbandonCloudToDeviceMessageAsync(additionalMsg);
                            }
                        }

                        // prepare message to device
                        //returnPayloadData.SetData(c2dMsg.Body, loraDeviceInfo.DevAddr, loraDeviceInfo.AppSKey);
                        //returnPayloadData.FportDown = (byte)(c2dMsg.Properties["fport"]);
                        //if (c2dMsg.Properties["confirmed"] == "true")
                        //    returnPayloadData.SetConfirmed();

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
                    //if (rxpk.IsConfirmed())
                    //    txpk.SetAsAcknoledgement();
                    var receiveWindowToUse = timeWatcher.ResolveReceiveWindowToUse(loRaDevice);
                    if (receiveWindowToUse == 0)
                    {
                        // TODO: abandon cloud?
                        if (cloudToDeviceMessage != null)
                            _ = loRaDevice.AbandonCloudToDeviceMessageAsync(cloudToDeviceMessage);

                        // no time to send response...
                        return null;
                    }

                    if (cloudToDeviceMessage != null)
                        _ = loRaDevice.CompleteCloudToDeviceMessageAsync(cloudToDeviceMessage);

                    //_ = SaveFcnt(loraDeviceInfo, force: false);

                    return new LoRaTools.LoRaPhysical.Txpk()
                    {


                    };
                    // return Txpk.Create(receiveWindowToUse, payloadToDevice, loraDeviceInfo.NwkSKey);
                }
            }
        }

        /// <summary>
        /// Gets and validates a cloud to device message
        /// If the message is invalid it will be completed and return value will be null
        /// </summary>
        /// <param name="loRaDevice"></param>
        /// <param name="timeout"></param>
        /// <returns></returns>
        private async Task<Message> GetAndValidateCloudToDeviceMessageAsync(LoRaDevice loRaDevice, TimeSpan timeout)
        {
            var cloudToDeviceMessage = await loRaDevice.ReceiveCloudToDeviceAsync(timeout);
            if (cloudToDeviceMessage != null && !ValidateCloudToDeviceMessage(loRaDevice, cloudToDeviceMessage))
            {
                // complete message and set to null
                _ = loRaDevice.CompleteCloudToDeviceMessageAsync(cloudToDeviceMessage);
                cloudToDeviceMessage = null;
            }

            return cloudToDeviceMessage;
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
        private async Task SendDeviceEventAsync(LoRaDevice loRaDevice, LoRaTools.LoRaPhysical.Rxpk rxpk, object payloadData, LoRaOperationTimeWatcher timeWatcher)
        {            
            var deviceTelemetry = new DeviceTelemetry(rxpk);
            deviceTelemetry.DeviceEUI = loRaDevice.DevEUI;
            deviceTelemetry.GatewayID = this.configuration.GatewayID;
            deviceTelemetry.Edgets = (long)((timeWatcher.Start - DateTime.UnixEpoch).TotalMilliseconds);

            if (payloadData != null)
            {
                deviceTelemetry.data = JsonConvert.SerializeObject(payloadData, Formatting.None);
            }

            var messageJson = JsonConvert.SerializeObject(deviceTelemetry, Formatting.None);
            await loRaDevice.SendEventAsync(messageJson);

            Logger.Log(loRaDevice.DevEUI, $"message '{deviceTelemetry.data}' sent to hub", Logger.LoggingLevel.Info);
        }


        bool IsValidNetId(byte netid)
        {
            return true;
        }


        /// <summary>
        /// Process OTAA join request
        /// </summary>
        public async Task<LoRaTools.LoRaPhysical.Txpk> ProcessJoinRequestAsync(LoRaTools.LoRaPhysical.Rxpk rxpk)
        {
            if (this.loraRegion == null)
                this.loraRegion = RegionFactory.Create(rxpk);

            var timeWatcher = new LoRaOperationTimeWatcher(this.loraRegion);
            using (var processLogger = new ProcessLogger(timeWatcher))
            {
                var joinReq = __WorkaroundGetPayloadJoinRequest(rxpk);

                byte[] udpMsgForPktForwarder = new Byte[0];

                joinReq.DevEUI.Span.Reverse();
                joinReq.AppEUI.Span.Reverse();
                var devEUI = LoRaTools.Utils.ConversionHelper.ByteArrayToString(joinReq.DevEUI);

                // set context to logger
                processLogger.SetDevEUI(devEUI);


                var devNonce = LoRaTools.Utils.ConversionHelper.ByteArrayToString(joinReq.DevNonce);
                Logger.Log(devEUI, $"join request received", Logger.LoggingLevel.Info);

                var appEUI = LoRaTools.Utils.ConversionHelper.ByteArrayToString(joinReq.AppEUI);

                var loRaDevice = await this.deviceRegistry.GetDeviceForJoinRequestAsync(devEUI, appEUI, devNonce);
                if (loRaDevice == null)
                    return null;

                if (!joinReq.CheckMic(loRaDevice.AppKey))
                {
                    Logger.Log(devEUI, $"join request MIC invalid", Logger.LoggingLevel.Info);
                }

                if (loRaDevice.AppEUI != appEUI)
                {
                    string errorMsg = $"AppEUI for OTAA does not match for device";
                    Logger.Log(devEUI, errorMsg, Logger.LoggingLevel.Error);
                    return null;
                }

                //Make sure that is a new request and not a replay         
                if (!string.IsNullOrEmpty(loRaDevice.DevNonce) && loRaDevice.DevNonce == devNonce)
                {

                    string errorMsg = $"DevNonce already used by this device";
                    Logger.Log(devEUI, errorMsg, Logger.LoggingLevel.Info);
                    loRaDevice.IsJoinValid = false;
                    return null;
                }


                //Check that the device is joining through the linked gateway and not another
                if (!string.IsNullOrEmpty(loRaDevice.GatewayID) && !string.Equals(loRaDevice.GatewayID, configuration.GatewayID, StringComparison.InvariantCultureIgnoreCase))
                {
                    Logger.Log(devEUI, $"trying to join not through its linked gateway, ignoring join request", Logger.LoggingLevel.Info);
                    loRaDevice.IsJoinValid = false;
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
                    Logger.Log(devEUI, $"processing of the join request took too long, sending no message", Logger.LoggingLevel.Info);
                    return null;
                }


                var deviceUpdateSucceeded = await loRaDevice.UpdateAfterJoinAsync(devAddr, nwkSKey, appSKey, appNonce, devNonce, LoRaTools.Utils.ConversionHelper.ByteArrayToString(netId));
                if (!deviceUpdateSucceeded)
                {
                    Logger.Log(devEUI, $"join request could not save twins, join refused", Logger.LoggingLevel.Error);
                    return null;
                }


                Array.Reverse(netId);
                Array.Reverse(appNonceBytes);
                var loRaPayloadJoinAccept = new LoRaPayloadJoinAccept();
                
                    ////NETID 0 / 1 is default test 
                    //LoRaTools.Utils.ConversionHelper.ByteArrayToString(netId),
                    ////todo add app key management
                    //loRaDevice.AppKey,
                    ////todo add device address management
                    //devAddr,
                    //appNonce,
                    //new byte[] { 0 },
                    //new byte[] { 0 },
                    //null
                    //);

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

                this.deviceRegistry.UpdateDeviceAfterJoin(loRaDevice);

                // create txpk with join lora accept
                //var joinAccept = new LoRaPayloadJoinAccept();
                var result = new LoRaTools.LoRaPhysical.Txpk()
                {

                };

                //Logger.Log(devEui, String.Format("join accept sent with ID {0}",
                //    ConversionHelper.ByteArrayToString(loraMessage.PhysicalPayload.token)),
                //    Logger.LoggingLevel.Full);

                //Logger.Log(devEui, $"join request refused", Logger.LoggingLevel.Info);
            
                return result;
            }
        }
    }
}