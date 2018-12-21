//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
namespace LoRaWan.NetworkServer
{

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

    /// <summary>
    /// Message processor (work in progress)    
    /// </summary>
    /// <remarks>
    /// Refactor of current processor with the following goals in mind
    /// - Easier to understand and extend
    /// - Unit testable
    /// </remarks>
    public class MessageProcessor2
    {
        private readonly ILoRaDeviceRegistry deviceRegistry;

        public MessageProcessor2(ILoRaDeviceRegistry deviceRegistry)
        {
            this.deviceRegistry = deviceRegistry;
        }
        

        class PhysicalPayload
        {
            public Rxpk[] GetRxpks();
        }

        class Rxpk
        {
            public LoRaPayload LoRaPayload { get; }

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
            if (!ValidNetId(netId))
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
            if (loraDeviceInfo.IsABP() && loraDeviceInfo.GatewayID != null && loraDeviceInfo.WasNotJustReadFromCache())  
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

        bool IsValidNetId(byte netid)
        {
            return true;
        }

        int NextFcntDown(LoraDeviceInfo device)
        {
            // make it thread safe
            if (GatewayID == DeviceGatewayID)
                device.FcntDown++;
            else
                device.FcntDown = AzureFunctionNextFcntDown(DevEUI);
    
            return device.FcntDown;
        }

        private SaveFcnt(LoraDeviceInfo loraDeviceInfo, bool force)
        {
            if (loraDeviceInfo.FCntUp % 10 == 0 || force)
            {
                SaveTwin(loraDeviceInfo);
            }
        }

        private Task UpdateFcntDown(LoraDeviceInfo loraDevice)
        {
            if (loraDeviceInfo.Gateway == this.Gateway)
                loraDevice.FCntDown++;

        }
    }
}