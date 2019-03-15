// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using LoRaTools;
    using LoRaTools.LoRaMessage;
    using LoRaTools.LoRaPhysical;
    using LoRaTools.Utils;
    using LoRaWan;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    /// <summary>
    /// Defines a simulated device
    /// </summary>
    public class SimulatedDevice
    {
        public TestDeviceInfo LoRaDevice { get; internal set; }

        public uint FrmCntUp { get; set; }

        public uint FrmCntDown { get; set; }

        public PhysicalPayload LastPayload { get; set; }

        public string DevNonce { get; private set; }

        public bool IsJoined => !string.IsNullOrEmpty(this.LoRaDevice.DevAddr);

        public string NetId { get; internal set; }

        public string AppNonce { get; internal set; }

        public string AppSKey => this.LoRaDevice.AppSKey;

        public string NwkSKey => this.LoRaDevice.NwkSKey;

        public string AppKey => this.LoRaDevice.AppKey;

        public string AppEUI => this.LoRaDevice.AppEUI;

        public char ClassType => this.LoRaDevice.ClassType;

        public string DevAddr
        {
            get { return this.LoRaDevice.DevAddr; }
            set { this.LoRaDevice.DevAddr = value; }
        }

        public string DevEUI => this.LoRaDevice.DeviceID;

        public bool Supports32BitFCnt
        {
            get { return this.LoRaDevice.Supports32BitFCnt; }
            set { this.LoRaDevice.Supports32BitFCnt = value; }
        }

        SemaphoreSlim joinFinished;

        private bool isFirstJoinRequest = true;

        public SimulatedDevice(TestDeviceInfo testDeviceInfo, uint frmCntDown = 0, uint frmCntUp = 0)
        {
            this.LoRaDevice = testDeviceInfo;

            this.FrmCntDown = frmCntDown;
            this.FrmCntUp = frmCntUp;

            if (!this.IsJoined)
                this.joinFinished = new SemaphoreSlim(0);
        }

        public LoRaPayloadJoinRequest CreateJoinRequest()
        {
            byte[] devNonce = new byte[2];
            if (string.IsNullOrEmpty(this.DevNonce) || (!this.isFirstJoinRequest))
            {
                Random random = new Random();
                // DevNonce[0] = 0xC8; DevNonce[1] = 0x86;
                random.NextBytes(devNonce);
                this.DevNonce = BitConverter.ToString(devNonce).Replace("-", string.Empty);
                Array.Reverse(devNonce);
                this.isFirstJoinRequest = false;
            }
            else
            {
                devNonce = ConversionHelper.StringToByteArray(this.DevNonce);
                Array.Reverse(devNonce);
            }

            TestLogger.Log($"[{this.LoRaDevice.DeviceID}] Join request sent DevNonce: {BitConverter.ToString(devNonce).Replace("-", string.Empty)} / {this.DevNonce}");
            var joinRequest = new LoRaPayloadJoinRequest(this.LoRaDevice.AppEUI, this.LoRaDevice.DeviceID, devNonce);
            joinRequest.SetMic(this.LoRaDevice.AppKey);

            return joinRequest;
        }

        public UplinkPktFwdMessage CreateUnconfirmedMessageUplink(string data, uint? fcnt = null, byte fport = 1, byte fctrl = 0) => this.CreateUnconfirmedDataUpMessage(data, fcnt, fport, fctrl).SerializeUplink(this.AppSKey, this.NwkSKey);

        /// <summary>
        /// Creates request to send unconfirmed data message
        /// </summary>
        public LoRaPayloadData CreateUnconfirmedDataUpMessage(string data, uint? fcnt = null, byte fport = 1, byte fctrl = 0, bool isHexPayload = false, List<MacCommand> macCommands = null)
        {
            byte[] devAddr = ConversionHelper.StringToByteArray(this.LoRaDevice.DevAddr);
            Array.Reverse(devAddr);
            byte[] fCtrl = new byte[] { fctrl };
            fcnt = fcnt ?? this.FrmCntUp + 1;
            this.FrmCntUp = fcnt.GetValueOrDefault();

            var fcntBytes = BitConverter.GetBytes((ushort)fcnt.Value);

            byte[] fPort = new byte[] { fport };
            // TestLogger.Log($"{LoRaDevice.DeviceID}: Simulated data: {data}");
            byte[] payload = null;
            if (data != null)
            {
                if (!isHexPayload)
                {
                    payload = Encoding.UTF8.GetBytes(data);
                }
                else
                {
                    payload = ConversionHelper.StringToByteArray(data);
                }

                Array.Reverse(payload);
            }

            // 0 = uplink, 1 = downlink
            int direction = 0;

            var payloadData = new LoRaPayloadData(
                LoRaMessageType.UnconfirmedDataUp,
                devAddr,
                fCtrl,
                fcntBytes,
                macCommands,
                fPort,
                payload,
                direction,
                this.Supports32BitFCnt ? fcnt : (uint?)null);

            return payloadData;
        }

        public UplinkPktFwdMessage CreateConfirmedMessageUplink(string data, uint? fcnt = null, byte fport = 1) => this.CreateConfirmedDataUpMessage(data, fcnt, fport).SerializeUplink(this.AppSKey, this.NwkSKey);

        /// <summary>
        /// Creates request to send unconfirmed data message
        /// </summary>
        public LoRaPayloadData CreateConfirmedDataUpMessage(string data, uint? fcnt = null, byte fport = 1, bool isHexPayload = false)
        {
            byte[] devAddr = ConversionHelper.StringToByteArray(this.LoRaDevice.DevAddr);
            Array.Reverse(devAddr);
            byte[] fCtrl = new byte[] { 0x80 };

            fcnt = fcnt ?? this.FrmCntUp + 1;
            this.FrmCntUp = fcnt.GetValueOrDefault();

            var fcntBytes = BitConverter.GetBytes((ushort)fcnt.Value);

            byte[] fPort = new byte[] { fport };

            byte[] payload = null;

            if (data != null)
            {
                if (!isHexPayload)
                {
                    payload = Encoding.UTF8.GetBytes(data);
                }
                else
                {
                    payload = ConversionHelper.StringToByteArray(data);
                }

                Array.Reverse(payload);
            }

            // 0 = uplink, 1 = downlink
            int direction = 0;
            var payloadData = new LoRaPayloadData(LoRaMessageType.ConfirmedDataUp, devAddr, fCtrl, fcntBytes, null, fPort, payload, direction, this.Supports32BitFCnt ? fcnt : (uint?)null);

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
            unconfirmedMessage.SerializeUplink(this.AppSKey, this.NwkSKey);
            this.LastPayload = await simulatedPacketForwarder.SendAsync(header, unconfirmedMessage.GetByteMessage());

            TestLogger.Log($"[{this.LoRaDevice.DeviceID}] Unconfirmed data: {BitConverter.ToString(header).Replace("-", string.Empty)} {payload}");

            // TestLogger.Log($"[{this.LoRaDevice.DevAddr}] Sending data: {BitConverter.ToString(header).Replace("-", "")}{Encoding.UTF8.GetString(gatewayInfo)}");
        }

        // Sends confirmed message
        public async Task SendConfirmedMessageAsync(SimulatedPacketForwarder simulatedPacketForwarder, string payload)
        {
            var token = await RandomTokenGenerator.GetTokenAsync();
            if (this.LastPayload == null)
                this.LastPayload = new PhysicalPayload(token, PhysicalIdentifier.PUSH_DATA, null);
            var header = this.LastPayload.GetSyncHeader(simulatedPacketForwarder.MacAddress);

            var confirmedMessage = this.CreateConfirmedDataUpMessage(payload);
            confirmedMessage.SerializeUplink(this.AppSKey, this.NwkSKey);
            this.LastPayload = await simulatedPacketForwarder.SendAsync(header, confirmedMessage.GetByteMessage());

            TestLogger.Log($"[{this.LoRaDevice.DeviceID}] Confirmed data: {BitConverter.ToString(header).Replace("-", string.Empty)} {payload}");

            // TestLogger.Log($"[{this.LoRaDevice.DevAddr}] Sending data: {BitConverter.ToString(header).Replace("-", "")}{Encoding.UTF8.GetString(gatewayInfo)}");
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

            var joinRequestUplinkMessage = joinRequest.SerializeUplink(this.AppKey);

            packetForwarder.SubscribeOnce((response) =>
            {
                // handle join
                var txpk = Txpk.CreateTxpk(response, this.LoRaDevice.AppKey);
                byte[] convertedInputMessage = Convert.FromBase64String(txpk.Data);

                var joinAccept = new LoRaPayloadJoinAccept(convertedInputMessage, this.AppKey);

                var result = this.HandleJoinAccept(joinAccept); // may need to return bool and only release if true.
                joinCompleted.Release();

                return result;
            });

            await packetForwarder.SendAsync(header, joinRequest.GetByteMessage());

            TestLogger.Log($"[{this.LoRaDevice.DeviceID}] Join request: {BitConverter.ToString(header).Replace("-", string.Empty)}");

#if DEBUG
            if (System.Diagnostics.Debugger.IsAttached)
            {
                timeoutInMs = 60 * 1000;
            }
#endif

            return await joinCompleted.WaitAsync(timeoutInMs);
        }

        bool HandleJoinAccept(LoRaPayloadJoinAccept payload)
        {
            try
            {
                // Calculate the keys
                var netid = payload.NetID.ToArray();
                Array.Reverse(netid);
                var appNonce = payload.AppNonce.ToArray();
                Array.Reverse(appNonce);
                var devNonce = ConversionHelper.StringToByteArray(this.DevNonce);
                Array.Reverse(devNonce);
                var deviceAppKey = ConversionHelper.StringToByteArray(this.LoRaDevice.AppKey);
                var appSKey = payload.CalculateKey(LoRaPayloadKeyType.AppSKey, appNonce, netid, devNonce, deviceAppKey);
                var nwkSKey = payload.CalculateKey(LoRaPayloadKeyType.NwkSkey, appNonce, netid, devNonce, deviceAppKey);
                var devAddr = payload.DevAddr;

                // if mic check failed, return false
                /*
                if (!payload.CheckMic(BitConverter.ToString(nwkSKey).Replace("-", "")))
                {
                    return false;
                }
                */

                this.LoRaDevice.AppSKey = BitConverter.ToString(appSKey).Replace("-", string.Empty);
                this.LoRaDevice.NwkSKey = BitConverter.ToString(nwkSKey).Replace("-", string.Empty);
                this.NetId = BitConverter.ToString(netid).Replace("-", string.Empty);
                this.AppNonce = BitConverter.ToString(appNonce).Replace("-", string.Empty);
                this.LoRaDevice.DevAddr = BitConverter.ToString(devAddr.ToArray()).Replace("-", string.Empty);

                return true;
            }
            catch
            {
            }

            return false;
        }

        /// <summary>
        /// Setups the join properties
        /// </summary>
        public void SetupJoin(string appSKey, string nwkSKey, string devAddr)
        {
            this.LoRaDevice.AppSKey = appSKey;
            this.LoRaDevice.NwkSKey = nwkSKey;
            this.LoRaDevice.DevAddr = devAddr;
        }
    }
}
