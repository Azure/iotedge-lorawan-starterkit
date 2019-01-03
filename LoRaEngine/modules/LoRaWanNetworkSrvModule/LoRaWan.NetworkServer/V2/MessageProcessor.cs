//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using LoRaTools;
using LoRaTools.LoRaMessage;
using LoRaTools.Regions;
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
        /// <returns></returns>
        public async Task<LoRaTools.LoRaPhysical.Txpk> ProcessMessageAsync(byte[] rawMessage)
        {
            var startTimeProcessing = DateTime.UtcNow;
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
                    return await ProcessLoRaMessage(rxpk);
                }
            }
            return null;
        }

        public async Task<LoRaTools.LoRaPhysical.Txpk> ProcessLoRaMessage(LoRaTools.LoRaPhysical.Rxpk rxpk)
        {
            if (this.loraRegion == null)
                this.loraRegion = RegionFactory.Create(rxpk);

            var timeWatcher = new LoRaOperationTimeWatcher(this.loraRegion);
        
            var loraPayload = __WorkaroundGetPayloadData(rxpk);
            var devAddr = loraPayload.DevAddr;
            var netId = loraPayload;
            if (!IsValidNetId(__WorkaroundGetNetID(netId)))
            {
                //Log("Invalid netid");
                return null;
            }


            // Find device that matches:
            // - devAddr
            // - mic check (requires: loraDeviceInfo.NwkSKey or loraDeviceInfo.AppKey, rxpk.LoraPayload.Mic)
            // - gateway id
            var loraDeviceInfo = await deviceRegistry.GetDeviceForPayloadAsync(loraPayload);
            if (loraDeviceInfo == null)
                return null;

            var frameCounterStrategy = (loraDeviceInfo.GatewayID == configuration.GatewayID) ?
                frameCounterUpdateStrategyFactory.GetSingleGatewayStrategy() :
                frameCounterUpdateStrategyFactory.GetMultiGatewayStrategy();

            // Reply attack or confirmed reply
            // Confirmed resubmit: A confirmed message that was received previously but we did not answer in time
            // Device will send it again and we just need to return an ack (but also check for C2D to send it over)
            var isConfirmedResubmit = false;
            if (this.__WorkaroundGetFcnt(loraPayload) <= loraDeviceInfo.FCntUp)
            {
                // Future: Keep track of how many times we acked the confirmed message (4+ times we skip)
                //if it is confirmed most probably we did not ack in time before or device lost the ack packet so we should continue but not send the msg to iothub 
                if (__WorkaroundIsConfirmed(loraPayload) && this.__WorkaroundGetFcnt(loraPayload) == loraDeviceInfo.FCntUp)
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
            if (loraDeviceInfo.IsABP && loraDeviceInfo.IsABPRelaxedFrameCounter && loraDeviceInfo.FCntUp > 0 && __WorkaroundGetFcnt(loraPayload) <= 1)
            {
                // known problem when device restarts, starts fcnt from zero
                //loraDeviceInfo.SetFcntUp(0);
                //loraDeviceInfo.SetFcntDown(0);
                //_ = SaveFcnt(loraDeviceInfo, force: true);
                //if (loraDeviceInfo.GatewayID == null)
                //    await ABPFcntCacheReset(loraDeviceInfo);
                _ = frameCounterStrategy.ResetAsync(loraDeviceInfo);
            }

            // If it is confirmed it require us to update the frame counter down
            // Multiple gateways: in redis, otherwise in device twin
            if (__WorkaroundIsConfirmed(loraPayload))
            {
                fcntDown = await frameCounterStrategy.NextFcntDown(loraDeviceInfo);
                //fcntDown = NextFcntDown(loraDeviceInfo);
            }


            if (!isConfirmedResubmit)
            {
                var validFcntUp = __WorkaroundGetFcnt(loraPayload) > loraDeviceInfo.FCntUp;
                if (validFcntUp)
                {
                    object payloadData = null;
                    // if it is an upward acknowledgement from the device it does not have a payload
                    // This is confirmation from leaf device that he received a C2D confirmed
                    if (!__WorkaroundIsUpwardAck(loraPayload))
                    {
                        var decryptedPayload = loraPayload.PerformEncryption(loraDeviceInfo.AppSKey);
                        payloadData = await payloadDecoder.DecodeMessage(decryptedPayload, __WorkaroundGetFPort(loraPayload), loraDeviceInfo.SensorDecoder);
                    }


                    // What do we need to send an UpAck to IoT Hub?
                    // What is the content of the message
                    // TODO Future: Don't wait if it is an unconfirmed message
                    await SendDeviceEventAsync(loraDeviceInfo, rxpk, payloadData);

                    loraDeviceInfo.SetFcntUp(__WorkaroundGetFcnt(loraPayload));
                }
            }

            // We check if we have time to futher progress or not
            // C2D checks are quite expensive so if we are really late we just stop here
            var timeToSecondWindow = timeWatcher.GetTimeToSecondWindow(loraDeviceInfo);
            if (timeToSecondWindow < LoRaOperationTimeWatcher.ExpectedTimeToPackageAndSendMessage)
            {
                return null;
            }

            // If it is confirmed and we don't have time to check c2d and send to device we return now
            if (__WorkaroundIsConfirmed(loraPayload) && timeToSecondWindow <= (LoRaOperationTimeWatcher.ExpectedTimeToCheckCloudToDeviceMessage + LoRaOperationTimeWatcher.ExpectedTimeToPackageAndSendMessage))
            {

                //_ = SaveFcnt(loraDeviceInfo, force: false);
                _ = frameCounterStrategy.UpdateAsync(loraDeviceInfo);
                return new LoRaTools.LoRaPhysical.Txpk()
                {
                };
            }

            // ReceiveAsync has a longer timeout
            // But we wait less that the timeout (available time before 2nd window)
            // if message is received after timeout, keep it in loraDeviceInfo and return the next call
            timeToSecondWindow = timeWatcher.GetTimeToSecondWindow(loraDeviceInfo);
            var cloudToDeviceMessage = await loraDeviceInfo.ReceiveCloudToDeviceAsync(timeout: timeToSecondWindow - LoRaOperationTimeWatcher.ExpectedTimeToCheckCloudToDeviceMessage);
            if (cloudToDeviceMessage != null && !ValidateCloudToDeviceMessage(loraDeviceInfo, cloudToDeviceMessage))
            {
                // complete message and set to null
                _ = loraDeviceInfo.CompleteCloudToDeviceMessageAsync(cloudToDeviceMessage);
                cloudToDeviceMessage = null;
            }

            var resultPayloadData = new LoRaPayloadData();
            //loraPayload.IsConfirmed() ? LoRaMessageType.ConfirmedDataDown : LoRaMessageType.UnconfirmedDataDown);

            if (cloudToDeviceMessage != null)
            {
                if (!__WorkaroundIsConfirmed(loraPayload))
                {
                    // The message coming from the device was not confirmed, therefore we did not computed the frame count down
                    // Now we need to increment because there is a C2D message to be sent
                    fcntDown = await frameCounterStrategy.NextFcntDown(loraDeviceInfo);
                }

                timeToSecondWindow = timeWatcher.GetTimeToSecondWindow(loraDeviceInfo);
                if (timeToSecondWindow > LoRaOperationTimeWatcher.ExpectedTimeToPackageAndSendMessage)
                {
                    var additionalMsg = await loraDeviceInfo.ReceiveCloudToDeviceAsync(timeout: timeToSecondWindow - LoRaOperationTimeWatcher.ExpectedTimeToCheckCloudToDeviceMessage);
                    if (additionalMsg != null)
                    {
                        resultPayloadData.FPending = true;
                        _ = loraDeviceInfo.AbandonCloudToDeviceMessageAsync(additionalMsg);
                    }
                }

                // prepare message to device
                //returnPayloadData.SetData(c2dMsg.Body, loraDeviceInfo.DevAddr, loraDeviceInfo.AppSKey);
                //returnPayloadData.FportDown = (byte)(c2dMsg.Properties["fport"]);
                //if (c2dMsg.Properties["confirmed"] == "true")
                //    returnPayloadData.SetConfirmed();

            }


            // No C2D message and request was not confirmed, return nothing
            if (!__WorkaroundIsConfirmed(loraPayload) && cloudToDeviceMessage == null)
            {
                //await SaveFnct(loraDeviceInfo, force: false);
                await frameCounterStrategy.UpdateAsync(loraDeviceInfo);
                return null;
            }

            // We did it in the LoRaPayloadData constructor
            // we got here:
            // a) was a confirm request
            // b) we have a c2d message
            //if (rxpk.IsConfirmed())
            //    txpk.SetAsAcknoledgement();


            var downReceiveWindow = 1;
            if (!loraDeviceInfo.AlwaysUseSecondWindow && timeWatcher.InTimeForFirstWindow(loraDeviceInfo))
                downReceiveWindow = 1;
            else if (timeWatcher.InTimeForSecondWindow(loraDeviceInfo))
                downReceiveWindow = 2;
            else
            {
                // TODO: verify if we should call Abandon message
                return null;
            }

            if (cloudToDeviceMessage != null)
                _ = loraDeviceInfo.CompleteCloudToDeviceMessageAsync(cloudToDeviceMessage);

            _ = frameCounterStrategy.UpdateAsync(loraDeviceInfo);
            //_ = SaveFcnt(loraDeviceInfo, force: false);

            return new LoRaTools.LoRaPhysical.Txpk()
            {


            };
            // return Txpk.Create(downReceiveWindow, payloadToDevice, loraDeviceInfo.NwkSKey);
        }
        

        private bool ValidateCloudToDeviceMessage(LoRaDevice loraDeviceInfo, Message cloudToDeviceMsg)
        {
            return true;
        }

        private async Task SendDeviceEventAsync(LoRaDevice loraDeviceInfo, LoRaTools.LoRaPhysical.Rxpk rxpk, object payloadData)
        {
            // TODO: check how the data is encapsulated into IoT Hub
            var messageJson = JsonConvert.SerializeObject(rxpk);

            await loraDeviceInfo.SendEventAsync(messageJson);
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

            var joinReq = __WorkaroundGetPayloadJoinRequest(rxpk);

            byte[] udpMsgForPktForwarder = new Byte[0];

            joinReq.DevEUI.Span.Reverse();
            joinReq.AppEUI.Span.Reverse();
            var devEUI = LoRaTools.Utils.ConversionHelper.ByteArrayToString(joinReq.DevEUI);
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
            var timeToFirstWindow = timeWatcher.GetTimeToJoinAcceptFirstWindow();
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

            Logger.Log(devEUI, $"processing time: {timeWatcher.GetElapsedTime()}", Logger.LoggingLevel.Info);


            return result;
        }
    }
}