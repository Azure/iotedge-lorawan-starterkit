// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Shared
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using LoRaTools;
    using LoRaTools.LoRaMessage;
    using LoRaTools.LoRaPhysical;
    using LoRaTools.Utils;

    /// <summary>
    /// Defines a simulated device.
    /// </summary>
    public class SimulatedDevice
    {
        public TestDeviceInfo LoRaDevice { get; internal set; }

        public uint FrmCntUp { get; set; }

        public uint FrmCntDown { get; set; }

        public PhysicalPayload LastPayload { get; set; }

        public string DevNonce { get; private set; }

        public bool IsJoined => !string.IsNullOrEmpty(LoRaDevice.DevAddr);

        public string NetId { get; internal set; }

        public string AppNonce { get; internal set; }

        public string AppSKey => LoRaDevice.AppSKey;

        public string NwkSKey => LoRaDevice.NwkSKey;

        public string AppKey => LoRaDevice.AppKey;

        public string AppEUI => LoRaDevice.AppEUI;

        public char ClassType => LoRaDevice.ClassType;

        public string DevAddr
        {
            get => LoRaDevice.DevAddr;
            set => LoRaDevice.DevAddr = value;
        }

        public string DevEUI => LoRaDevice.DeviceID;

        public bool Supports32BitFCnt
        {
            get => LoRaDevice.Supports32BitFCnt;
            set => LoRaDevice.Supports32BitFCnt = value;
        }

        private bool isFirstJoinRequest = true;

        public SimulatedDevice(TestDeviceInfo testDeviceInfo, uint frmCntDown = 0, uint frmCntUp = 0)
        {
            LoRaDevice = testDeviceInfo;
            FrmCntDown = frmCntDown;
            FrmCntUp = frmCntUp;
        }

        public LoRaPayloadJoinRequest CreateJoinRequest()
        {
            var devNonce = new byte[2];
            if (string.IsNullOrEmpty(DevNonce) || (!this.isFirstJoinRequest))
            {
                var random = new Random();
                // DevNonce[0] = 0xC8; DevNonce[1] = 0x86;
                random.NextBytes(devNonce);
                DevNonce = BitConverter.ToString(devNonce).Replace("-", string.Empty, StringComparison.Ordinal);
                Array.Reverse(devNonce);
                this.isFirstJoinRequest = false;
            }
            else
            {
                devNonce = ConversionHelper.StringToByteArray(DevNonce);
                Array.Reverse(devNonce);
            }

            TestLogger.Log($"[{LoRaDevice.DeviceID}] Join request sent DevNonce: {BitConverter.ToString(devNonce).Replace("-", string.Empty, StringComparison.Ordinal)} / {DevNonce}");
            var joinRequest = new LoRaPayloadJoinRequest(LoRaDevice.AppEUI, LoRaDevice.DeviceID, devNonce);
            joinRequest.SetMic(LoRaDevice.AppKey);

            return joinRequest;
        }

        public UplinkPktFwdMessage CreateUnconfirmedMessageUplink(string data, uint? fcnt = null, byte fport = 1, byte fctrl = 0) => CreateUnconfirmedDataUpMessage(data, fcnt, fport, fctrl).SerializeUplink(AppSKey, NwkSKey);

        /// <summary>
        /// Creates request to send unconfirmed data message.
        /// </summary>
        public LoRaPayloadData CreateUnconfirmedDataUpMessage(string data, uint? fcnt = null, byte fport = 1, byte fctrl = 0, bool isHexPayload = false, IList<MacCommand> macCommands = null)
        {
            var devAddr = ConversionHelper.StringToByteArray(LoRaDevice.DevAddr);
            Array.Reverse(devAddr);
            var fCtrl = new byte[] { fctrl };
            fcnt ??= FrmCntUp + 1;
            FrmCntUp = fcnt.GetValueOrDefault();

            var fcntBytes = BitConverter.GetBytes((ushort)fcnt.Value);

            var fPort = new byte[] { fport };
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
            var direction = 0;

            var payloadData = new LoRaPayloadData(
                LoRaMessageType.UnconfirmedDataUp,
                devAddr,
                fCtrl,
                fcntBytes,
                macCommands,
                fPort,
                payload,
                direction,
                Supports32BitFCnt ? fcnt : null);

            return payloadData;
        }

        public UplinkPktFwdMessage CreateConfirmedMessageUplink(string data, uint? fcnt = null, byte fport = 1) => CreateConfirmedDataUpMessage(data, fcnt, fport).SerializeUplink(AppSKey, NwkSKey);

        /// <summary>
        /// Creates request to send unconfirmed data message.
        /// </summary>
        public LoRaPayloadData CreateConfirmedDataUpMessage(string data, uint? fcnt = null, byte fport = 1, bool isHexPayload = false)
        {
            var devAddr = ConversionHelper.StringToByteArray(LoRaDevice.DevAddr);
            Array.Reverse(devAddr);
            var fCtrl = new byte[] { 0x80 };

            fcnt ??= FrmCntUp + 1;
            FrmCntUp = fcnt.GetValueOrDefault();

            var fcntBytes = BitConverter.GetBytes((ushort)fcnt.Value);

            var fPort = new byte[] { fport };

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
            var direction = 0;
            var payloadData = new LoRaPayloadData(LoRaMessageType.ConfirmedDataUp, devAddr, fCtrl, fcntBytes, null, fPort, payload, direction, Supports32BitFCnt ? fcnt : null);

            return payloadData;
        }

        // Sends unconfirmed message
        public async Task SendUnconfirmedMessageAsync(SimulatedPacketForwarder simulatedPacketForwarder, string payload)
        {
            var token = await RandomTokenGenerator.GetTokenAsync();
            if (LastPayload == null)
                LastPayload = new PhysicalPayload(token, PhysicalIdentifier.PushData, null);
            var header = LastPayload.GetSyncHeader(simulatedPacketForwarder.MacAddress.ToArray());

            var unconfirmedMessage = CreateUnconfirmedDataUpMessage(payload);
            unconfirmedMessage.SerializeUplink(AppSKey, NwkSKey);
            LastPayload = await simulatedPacketForwarder.SendAsync(header, unconfirmedMessage.GetByteMessage());

            TestLogger.Log($"[{LoRaDevice.DeviceID}] Unconfirmed data: {BitConverter.ToString(header).Replace("-", string.Empty, StringComparison.Ordinal)} {payload}");

            // TestLogger.Log($"[{LoRaDevice.DevAddr}] Sending data: {BitConverter.ToString(header).Replace("-", "")}{Encoding.UTF8.GetString(gatewayInfo)}");
        }

        // Sends confirmed message
        public async Task SendConfirmedMessageAsync(SimulatedPacketForwarder simulatedPacketForwarder, string payload)
        {
            var token = await RandomTokenGenerator.GetTokenAsync();
            if (LastPayload == null)
                LastPayload = new PhysicalPayload(token, PhysicalIdentifier.PushData, null);
            var header = LastPayload.GetSyncHeader(simulatedPacketForwarder.MacAddress.ToArray());

            var confirmedMessage = CreateConfirmedDataUpMessage(payload);
            confirmedMessage.SerializeUplink(AppSKey, NwkSKey);
            LastPayload = await simulatedPacketForwarder.SendAsync(header, confirmedMessage.GetByteMessage());

            TestLogger.Log($"[{LoRaDevice.DeviceID}] Confirmed data: {BitConverter.ToString(header).Replace("-", string.Empty, StringComparison.Ordinal)} {payload}");

            // TestLogger.Log($"[{LoRaDevice.DevAddr}] Sending data: {BitConverter.ToString(header).Replace("-", "")}{Encoding.UTF8.GetString(gatewayInfo)}");
        }

        // Performs join
        public async Task<bool> JoinAsync(SimulatedPacketForwarder packetForwarder, int timeoutInMs = 30 * 1000)
        {
            if (IsJoined)
                return true;

            var token = await RandomTokenGenerator.GetTokenAsync();
            LastPayload = new PhysicalPayload(token, PhysicalIdentifier.PushData, null);
            var header = LastPayload.GetSyncHeader(packetForwarder.MacAddress.ToArray());

            var joinRequest = CreateJoinRequest();
            using var joinCompleted = new SemaphoreSlim(0);

            var joinRequestUplinkMessage = joinRequest.SerializeUplink(AppKey);

            packetForwarder.SubscribeOnce((response) =>
            {
                // handle join
                var txpk = Txpk.CreateTxpk(response);
                var convertedInputMessage = Convert.FromBase64String(txpk.Data);

                var joinAccept = new LoRaPayloadJoinAccept(convertedInputMessage, AppKey);

                var result = HandleJoinAccept(joinAccept); // may need to return bool and only release if true.
                joinCompleted.Release();

                return result;
            });

            await packetForwarder.SendAsync(header, joinRequest.GetByteMessage());

            TestLogger.Log($"[{LoRaDevice.DeviceID}] Join request: {BitConverter.ToString(header).Replace("-", string.Empty, StringComparison.Ordinal)}");

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
                var devNonce = ConversionHelper.StringToByteArray(DevNonce);
                Array.Reverse(devNonce);
                var deviceAppKey = ConversionHelper.StringToByteArray(LoRaDevice.AppKey);
                var appSKey = LoRaPayload.CalculateKey(LoRaPayloadKeyType.AppSKey, appNonce, netid, devNonce, deviceAppKey);
                var nwkSKey = LoRaPayload.CalculateKey(LoRaPayloadKeyType.NwkSkey, appNonce, netid, devNonce, deviceAppKey);
                var devAddr = payload.DevAddr;

                // if mic check failed, return false
                /*
                if (!payload.CheckMic(BitConverter.ToString(nwkSKey).Replace("-", "")))
                {
                    return false;
                }
                */

                LoRaDevice.AppSKey = BitConverter.ToString(appSKey).Replace("-", string.Empty, StringComparison.Ordinal);
                LoRaDevice.NwkSKey = BitConverter.ToString(nwkSKey).Replace("-", string.Empty, StringComparison.Ordinal);
                NetId = BitConverter.ToString(netid).Replace("-", string.Empty, StringComparison.Ordinal);
                AppNonce = BitConverter.ToString(appNonce).Replace("-", string.Empty, StringComparison.Ordinal);
                LoRaDevice.DevAddr = BitConverter.ToString(devAddr.ToArray()).Replace("-", string.Empty, StringComparison.Ordinal);

                return true;
            }
            catch
            {
            }

            return false;
        }

        /// <summary>
        /// Setups the join properties.
        /// </summary>
        public void SetupJoin(string appSKey, string nwkSKey, string devAddr)
        {
            LoRaDevice.AppSKey = appSKey;
            LoRaDevice.NwkSKey = nwkSKey;
            LoRaDevice.DevAddr = devAddr;
        }
    }
}
