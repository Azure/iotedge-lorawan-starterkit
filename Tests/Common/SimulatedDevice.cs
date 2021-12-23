// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Common
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Runtime.InteropServices;
    using System.Security.Cryptography;
    using System.Text;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using LoRaTools;
    using LoRaTools.LoRaMessage;
    using LoRaTools.Utils;
    using LoRaWan.NetworkServer;

    /// <summary>
    /// Defines a simulated device.
    /// </summary>
    public partial class SimulatedDevice
    {
        public TestDeviceInfo LoRaDevice { get; internal set; }

        public uint FrmCntUp { get; set; }

        public uint FrmCntDown { get; set; }

        public DevNonce DevNonce { get; private set; }

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

        public SimulatedDevice(TestDeviceInfo testDeviceInfo, uint frmCntDown = 0, uint frmCntUp = 0)
        {
            LoRaDevice = testDeviceInfo;
            FrmCntDown = frmCntDown;
            FrmCntUp = frmCntUp;
        }


        public LoRaPayloadJoinRequest CreateJoinRequest(string appkey = null, DevNonce? devNonce = null )
        {
            // Some tests provide a special nonce as input to make the mic check fail.
            // We only generate one in case the value is not set.
            if (devNonce == null)
            {
                var devNonceBytes = new byte[2];
                using var random = RandomNumberGenerator.Create();
                random.GetBytes(devNonceBytes);
                DevNonce = DevNonce.Read(devNonceBytes);
            }
            else
            {
                DevNonce = devNonce.Value;
            }

            TestLogger.Log($"[{LoRaDevice.DeviceID}] Join request sent DevNonce: {DevNonce:N} / {DevNonce}");
            var joinRequest = new LoRaPayloadJoinRequest(LoRaDevice.AppEUI, LoRaDevice.DeviceID, DevNonce);
            joinRequest.SetMic(appkey ?? LoRaDevice.AppKey);

            return joinRequest;
        }


        /// <summary>
        /// Creates request to send unconfirmed data message.
        /// </summary>
        public LoRaPayloadData CreateUnconfirmedDataUpMessage(string data, uint? fcnt = null, FramePort fport = FramePorts.App1, FrameControlFlags fctrlFlags = FrameControlFlags.None, bool isHexPayload = false, IList<MacCommand> macCommands = null, string appSKey = null, string nwkSKey = null)
        {
            var devAddr = ConversionHelper.StringToByteArray(LoRaDevice.DevAddr);
            Array.Reverse(devAddr);
            fcnt ??= FrmCntUp + 1;
            FrmCntUp = fcnt.GetValueOrDefault();

            var fcntBytes = BitConverter.GetBytes((ushort)fcnt.Value);

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
                MacMessageType.UnconfirmedDataUp,
                devAddr,
                fctrlFlags,
                fcntBytes,
                macCommands,
                fport,
                payload,
                direction,
                Supports32BitFCnt ? fcnt : null);

            payloadData.PerformEncryption(appSKey is { Length: > 0 } ? appSKey : AppSKey ?? throw new InvalidOperationException($"Can't perform encryption without {nameof(AppSKey)}."));
            payloadData.SetMic(nwkSKey is { Length: > 0 } ? nwkSKey : NwkSKey ?? throw new InvalidOperationException($"Can't perform encryption without {nameof(NwkSKey)}."));

            // We want to ensure we simulate a message coming from the device, therefore only the 16 bits of the framecounter should be available.
            // The following line ensure we remove the 32 bits of the server frame counter that was generated by the constructor.
            // Some tests cases are expecting the mic check to fail because of rollover of the fcnt, if we have this value set it will never fail.
            payloadData.Reset32BitBlockInfo();
            // THIS IS NEEDED FOR TESTS AS THIS CONSTRUCTOR IS USED THERE
            // THIS WILL BE REMOVED WHEN WE MIGRATE TO USE lORAPAYLOADDATALNS INSTEAD OF LORAPAYLOAD in #1085
            // Populate the MacCommands present in the payload
            if (payloadData.Fopts.Length != 0)
                payloadData.MacCommands = MacCommand.CreateMacCommandFromBytes(payloadData.Fopts);

            return payloadData;
        }

        /// <summary>
        /// Creates request to send unconfirmed data message.
        /// </summary>
        public LoRaPayloadData CreateConfirmedDataUpMessage(string data, uint? fcnt = null, FramePort fport = FramePorts.App1, bool isHexPayload = false, string appSKey = null, string nwkSKey = null)
        {
            var devAddr = ConversionHelper.StringToByteArray(LoRaDevice.DevAddr);
            Array.Reverse(devAddr);

            fcnt ??= FrmCntUp + 1;
            FrmCntUp = fcnt.GetValueOrDefault();

            var fcntBytes = BitConverter.GetBytes((ushort)fcnt.Value);

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
            var payloadData = new LoRaPayloadData(MacMessageType.ConfirmedDataUp, devAddr, FrameControlFlags.Adr, fcntBytes, null, fport, payload, direction, Supports32BitFCnt ? fcnt : null);
            payloadData.PerformEncryption(string.IsNullOrEmpty(appSKey) ? AppSKey : appSKey);
            payloadData.SetMic(string.IsNullOrEmpty(nwkSKey)? NwkSKey : nwkSKey);
            return payloadData;
        }

        //        //// Sends unconfirmed message
        //        public async Task SendDataMessageAsync(SimulatedBasicsStation simulatedBasicsStation, LoRaRequest LoRaRequest)
        //        {
        //            using var ms = new MemoryStream();
        //            using var writer = new Utf8JsonWriter(ms);

        //            writer.WriteStartObject();
        //            writer.WriteString("radioMetadata", JsonSerializer.Serialize(LoRaRequest.RadioMetadata));
        //            writer.WriteNumber("MHdr", (uint)LoRaRequest.Payload.Mhdr.Span[0]);
        //            var payload = (LoRaPayloadDataLns)LoRaRequest.Payload;
        //            writer.WriteString("msgtype", "updf");
        //#pragma warning disable CA1507 // Use nameof to express symbol names
        //            writer.WriteNumber("DevAddr", MemoryMarshal.Read<int>(payload.DevAddr.Span));
        //#pragma warning restore CA1507 // Use nameof to express symbol names
        //            writer.WriteNumber("FCtrl", (uint)payload.FrameControlFlags);
        //            writer.WriteNumber("FCnt", MemoryMarshal.Read<uint>(payload.Fcnt.Span[0..2]));
        //            writer.WriteBase64String("FOpts", payload.Fopts.Span);
        //            writer.WriteNumber("FPort", (int)payload.Fport);
        //            writer.WriteBase64String("FRMPayload", payload.Frmpayload.Span);
        //            writer.WriteNumber("MIC", MemoryMarshal.Read<int>(payload.Mic.Span));
        //            writer.WriteEndObject();
        //            writer.Flush();

        //            await simulatedBasicsStation.SendMessageAsync(ms.ToArray());

        // TestLogger.Log($"[{LoRaDevice.DevAddr}] Sending data: {BitConverter.ToString(header).Replace("-", "")}{Encoding.UTF8.GetString(gatewayInfo)}");
        //}

        //// Sends unconfirmed message
        //public async Task SendUnconfirmedMessageAsync(SimulatedPacketForwarder simulatedPacketForwarder, string payload)
        //{
        ////    var token = await RandomTokenGenerator.GetTokenAsync();
        ////    if (LastPayload == null)
        ////        LastPayload = new PhysicalPayload(token, PhysicalIdentifier.PushData, null);
        ////    var header = LastPayload.GetSyncHeader(simulatedPacketForwarder.MacAddress.ToArray());

        ////    var unconfirmedMessage = CreateUnconfirmedDataUpMessage(payload);
        ////    LastPayload = await simulatedPacketForwarder.SendAsync(header, unconfirmedMessage.GetByteMessage());

        ////    TestLogger.Log($"[{LoRaDevice.DeviceID}] Unconfirmed data: {BitConverter.ToString(header).Replace("-", string.Empty, StringComparison.Ordinal)} {payload}");

        ////    // TestLogger.Log($"[{LoRaDevice.DevAddr}] Sending data: {BitConverter.ToString(header).Replace("-", "")}{Encoding.UTF8.GetString(gatewayInfo)}");
        ////}

        //// Sends confirmed message
        //public async Task SendConfirmedMessageAsync(SimulatedPacketForwarder simulatedPacketForwarder, string payload)
        //{
        //    //var token = await RandomTokenGenerator.GetTokenAsync();
        //    //if (LastPayload == null)
        //    //    LastPayload = new PhysicalPayload(token, PhysicalIdentifier.PushData, null);
        //    //var header = LastPayload.GetSyncHeader(simulatedPacketForwarder.MacAddress.ToArray());

        //    //var confirmedMessage = CreateConfirmedDataUpMessage(payload);
        //    //LastPayload = await simulatedPacketForwarder.SendAsync(header, confirmedMessage.GetByteMessage());

        //    //TestLogger.Log($"[{LoRaDevice.DeviceID}] Confirmed data: {BitConverter.ToString(header).Replace("-", string.Empty, StringComparison.Ordinal)} {payload}");

        //    // TestLogger.Log($"[{LoRaDevice.DevAddr}] Sending data: {BitConverter.ToString(header).Replace("-", "")}{Encoding.UTF8.GetString(gatewayInfo)}");
        //}


        private bool HandleJoinAccept(LoRaPayloadJoinAccept payload)
        {
            try
            {
                // Calculate the keys
                var netid = payload.NetID.ToArray();
                Array.Reverse(netid);
                var appNonce = payload.AppNonce.ToArray();
                Array.Reverse(appNonce);
                var devNonce = DevNonce;
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

        // Performs join
        public async Task<bool> JoinAsync(LoRaRequest joinRequest, SimulatedBasicsStation basicsStation, int timeoutInMs = 30 * 1000)
        {
            using var joinCompleted = new SemaphoreSlim(0);
            var joinRequestPayload = (LoRaPayloadJoinRequest)joinRequest.Payload;


            basicsStation.SubscribeOnce((response) =>
            {
                // to be done
                // handle join
                var joinAcceptstring = JsonSerializer.Deserialize<JoinAcceptResponse>(response);
                // is null in case no other 
                if (joinAcceptstring?.Pdu != null)
                {
                    var joinAccept = new LoRaPayloadJoinAccept(ConversionHelper.StringToByteArray(joinAcceptstring.Pdu), AppKey);

                    var result = HandleJoinAccept(joinAccept); // may need to return bool and only release if true.
                    joinCompleted.Release();

                    return result;
                }

                return false;
            });

            await basicsStation.SendMessageAsync(JsonSerializer.Serialize(new
            {
                JoinEui = JoinEui.Read(joinRequestPayload.AppEUI.Span).ToString("G", null),
                msgtype = "jreq",
                DevEui = DevEui.Read(joinRequestPayload.DevEUI.Span).ToString("G", null),
                DevNonce = joinRequestPayload.DevNonce.AsUInt16,
                MHdr = (uint)joinRequestPayload.Mhdr.Span[0],
                MIC = MemoryMarshal.Read<int>(joinRequestPayload.Mic.Span),
                DR = joinRequest.RadioMetadata.DataRate,
                Freq = joinRequest.RadioMetadata.Frequency.AsUInt64,
                upinfo = new
                {
                    gpstime = joinRequest.RadioMetadata.UpInfo.GpsTime,
                    rctx = 10,
                    rssi = joinRequest.RadioMetadata.UpInfo.ReceivedSignalStrengthIndication,
                    xtime = joinRequest.RadioMetadata.UpInfo.Xtime,
                    snr = joinRequest.RadioMetadata.UpInfo.SignalNoiseRatio
                }
            }));

#if DEBUG
            if (System.Diagnostics.Debugger.IsAttached)
            {
                timeoutInMs = 60 * 1000;
            }
#endif

            return await joinCompleted.WaitAsync(timeoutInMs);
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
