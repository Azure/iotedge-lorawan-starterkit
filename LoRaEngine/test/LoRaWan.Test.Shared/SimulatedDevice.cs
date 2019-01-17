//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//
using LoRaTools;
using LoRaTools.LoRaMessage;
using LoRaTools.LoRaPhysical;
using LoRaTools.Utils;
using LoRaWan;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace LoRaWan.Test.Shared
{
    /// <summary>
    /// Defines a simulated device
    /// </summary>
    public class SimulatedDevice
    {
        public TestDeviceInfo LoRaDevice { get; internal set; }
        public int FrmCntUp { get; set; }
        public int FrmCntDown { get; set; }

        public PhysicalPayload LastPayload { get; set; }
        public string DevNonce { get; private set; }
        public bool IsJoined => !string.IsNullOrEmpty(LoRaDevice.DevAddr);
        public string NetId { get; internal set; }
        public string AppNonce { get; internal set; }
        public string AppSKey => LoRaDevice.AppSKey;
        public string NwkSKey => LoRaDevice.NwkSKey;
        public string AppKey => LoRaDevice.AppKey;
        public string AppEUI => LoRaDevice.AppEUI;
        public string DevAddr => LoRaDevice.DevAddr;
        public string DevEUI => LoRaDevice.DeviceID;

        SemaphoreSlim joinFinished;

        private bool isFirstJoinRequest = true;
        public SimulatedDevice(TestDeviceInfo testDeviceInfo, int frmCntDown = 0, int frmCntUp = 0)
        {
            this.LoRaDevice = testDeviceInfo;

            FrmCntDown = frmCntDown;
            FrmCntUp = frmCntUp;
    

            if (!this.IsJoined)
                this.joinFinished = new SemaphoreSlim(0);
        }

        public LoRaPayloadJoinRequest CreateJoinRequest()
        {
            byte[] devNonce = new byte[2];
            if ((string.IsNullOrEmpty(this.DevNonce)) || (!isFirstJoinRequest))
            {                               
                Random random = new Random();
                // DevNonce[0] = 0xC8; DevNonce[1] = 0x86;
                random.NextBytes(devNonce);   
                this.DevNonce = BitConverter.ToString(devNonce).Replace("-", "");
                Array.Reverse(devNonce);
                isFirstJoinRequest = false;
            }
            else
            {
                devNonce = ConversionHelper.StringToByteArray(this.DevNonce);
                Array.Reverse(devNonce);
            }

            TestLogger.Log($"[{LoRaDevice.DeviceID}] Join request sent DevNonce: {BitConverter.ToString(devNonce).Replace("-","")} / {this.DevNonce}");
            var join = new LoRaPayloadJoinRequest(LoRaDevice.AppEUI, LoRaDevice.DeviceID, devNonce);
                
            return join;
        }

        /// <summary>
        /// Creates request to send unconfirmed data message
        /// </summary>
        /// <param name="data"></param>
        /// <param name="fport"></param>
        /// <returns></returns>
        public LoRaPayloadData CreateUnconfirmedDataUpMessage(string data, int? fcnt = null, byte fport = 1, byte fctrl=0x80)
        {
            byte[] devAddr = ConversionHelper.StringToByteArray(LoRaDevice.DevAddr);
            Array.Reverse(devAddr);
            byte[] fCtrl = new byte[] { fctrl };
            fcnt = fcnt ?? this.FrmCntUp + 1;
            var fcntBytes = BitConverter.GetBytes((UInt16)fcnt.Value);

            byte[] fopts = new byte[0];
            byte[] fPort = new byte[] { fport };
            //TestLogger.Log($"{LoRaDevice.DeviceID}: Simulated data: {data}");
            byte[] payload = null;
            if (data != null)
            {
                payload = Encoding.UTF8.GetBytes(data);
                Array.Reverse(payload);
            }
            // 0 = uplink, 1 = downlink
            int direction = 0;

            var payloadData = new LoRaPayloadData(LoRaMessageType.UnconfirmedDataUp, devAddr, fCtrl, fcntBytes, fopts, fPort, payload, direction);
            return payloadData;
        }

        /// <summary>
        /// Creates request to send unconfirmed data message
        /// </summary>
        /// <param name="data"></param>
        /// <param name="fport"></param>
        /// <returns></returns>
        public LoRaPayloadData CreateConfirmedDataUpMessage(string data, int? fcnt = null, byte fport = 1)
        {
            byte[] devAddr = ConversionHelper.StringToByteArray(LoRaDevice.DevAddr);
            Array.Reverse(devAddr);
            byte[] fCtrl = new byte[] { 0x80 };
            
            fcnt = fcnt ?? this.FrmCntUp + 1;
            var fcntBytes = BitConverter.GetBytes((UInt16)fcnt.Value);

            byte[] fopts = new byte[0];
            byte[] fPort = new byte[] { fport };           
            byte[] payload = Encoding.UTF8.GetBytes(data);
            Array.Reverse(payload);

            // 0 = uplink, 1 = downlink
            int direction = 0;
            var payloadData = new LoRaPayloadData(LoRaMessageType.ConfirmedDataUp, devAddr, fCtrl, fcntBytes, fopts, fPort, payload, direction);

            return payloadData;
        }

        // Sends unconfirmed message
        public async Task SendUnconfirmedMessageAsync(SimulatedPacketForwarder simulatedPacketForwarder, string payload)
        {
            var token = await RandomTokenGenerator.GetTokenAsync();
            if (this.LastPayload == null)
                this.LastPayload = new PhysicalPayload(token, PhysicalIdentifier.PUSH_DATA, null);
            var header = this.LastPayload.GetSyncHeader(simulatedPacketForwarder.MacAddress);

            var unconfirmedMessage = this.CreateUnconfirmedDataUpMessage(payload);
            this.LastPayload = await simulatedPacketForwarder.SendAsync(header, unconfirmedMessage.GetByteMessage());
            
            TestLogger.Log($"[{this.LoRaDevice.DeviceID}] Unconfirmed data: {BitConverter.ToString(header).Replace("-", "")} {payload}");

            //TestLogger.Log($"[{this.LoRaDevice.DevAddr}] Sending data: {BitConverter.ToString(header).Replace("-", "")}{Encoding.UTF8.GetString(gatewayInfo)}");
        }

        // Performs join
        public async Task<bool> JoinAsync(SimulatedPacketForwarder packetForwarder, int timeoutInMs = 30 * 1000)
        {
            if (this.IsJoined)
                return true;

            var token = await RandomTokenGenerator.GetTokenAsync();
            this.LastPayload = new PhysicalPayload(token, PhysicalIdentifier.PUSH_DATA, null);
            var header = this.LastPayload.GetSyncHeader(packetForwarder.MacAddress);

            var joinRequest = this.CreateJoinRequest();
            var joinCompleted = new SemaphoreSlim(0);
            packetForwarder.SubscribeOnce(token, PhysicalIdentifier.PULL_RESP, (response) => {
                var txpk=  Txpk.CreateTxpk(response, this.LoRaDevice.AppKey);
                // handle join
                var loraMessage = new LoRaMessageWrapper(txpk, this.LoRaDevice.AppKey);
                if (loraMessage.LoRaMessageType == LoRaMessageType.JoinAccept)
                {
                    this.HandleJoinAccept(loraMessage);
                    joinCompleted.Release();
                }
            });

            await packetForwarder.SendAsync(header, joinRequest.GetByteMessage());
            TestLogger.Log($"[{this.LoRaDevice.DeviceID}] Join request: {BitConverter.ToString(header).Replace("-", "")}");

            return await joinCompleted.WaitAsync(timeoutInMs);
        }


        void HandleJoinAccept(LoRaMessageWrapper loraMessage)
        {
            var payload = (LoRaPayloadJoinAccept)loraMessage.LoRaPayloadMessage;
            // TODO Need to check if the time is not passed 

            // Calculate the keys
            var netid = payload.NetID.ToArray();
            Array.Reverse(netid);
            var appNonce = payload.AppNonce.ToArray();
            Array.Reverse(appNonce);
            var devNonce = ConversionHelper.StringToByteArray(this.DevNonce);
            Array.Reverse(devNonce);
            var deviceAppKey = ConversionHelper.StringToByteArray(this.LoRaDevice.AppKey);
            var appSKey = payload.CalculateKey(LoRaPayloadKeyType.AppSKey, appNonce, netid, devNonce, deviceAppKey);
            this.LoRaDevice.AppSKey = BitConverter.ToString(appSKey).Replace("-", "");
            var nwkSKey = payload.CalculateKey(LoRaPayloadKeyType.AppSKey, appNonce, netid, devNonce, deviceAppKey);
            this.LoRaDevice.NwkSKey = BitConverter.ToString(nwkSKey).Replace("-", "");
            this.NetId = BitConverter.ToString(netid).Replace("-", "");
            this.AppNonce = BitConverter.ToString(appNonce).Replace("-", "");
            var devAdd = payload.DevAddr;
            //Array.Reverse(devAdd);
            this.LoRaDevice.DevAddr = BitConverter.ToString(devAdd.ToArray()).Replace("-", "");
        }

        /// <summary>
        /// Setups the join properties
        /// </summary>
        /// <param name="appSKey"></param>
        /// <param name="nwkSKey"></param>
        /// <param name="devAddr"></param>
        public void SetupJoin(string appSKey, string nwkSKey, string devAddr)
        {
            this.LoRaDevice.AppSKey = appSKey;
            this.LoRaDevice.NwkSKey = nwkSKey;
            this.LoRaDevice.DevAddr = devAddr;
        }
    }
}
