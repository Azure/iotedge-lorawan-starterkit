// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Common
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Runtime.InteropServices;
    using System.Security.Cryptography;
    using System.Text;
    using System.Text.Json;
    using System.Threading.Tasks;
    using LoRaTools;
    using LoRaTools.LoRaMessage;
    using LoRaTools.Utils;
    using LoRaWan.NetworkServer;

    /// <summary>
    /// Defines a simulated device.
    /// </summary>
    public sealed class SimulatedDevice
    {
        private readonly List<SimulatedBasicsStation> SimulatedBasicsStations = new List<SimulatedBasicsStation>();

        private readonly List<string> receivedMessages = new List<string>();

        public TestDeviceInfo LoRaDevice { get; internal set; }

        public uint FrmCntUp { get; set; }

        public uint FrmCntDown { get; set; }

        public DevNonce DevNonce { get; private set; }

        public bool IsJoined => LoRaDevice.DevAddr is not null;

        public NetId? NetId { get; internal set; }

        public AppNonce AppNonce { get; internal set; }

        public AppSessionKey? AppSKey => LoRaDevice.AppSKey;

        public NetworkSessionKey? NwkSKey => LoRaDevice.NwkSKey;

        public AppKey? AppKey => LoRaDevice.AppKey;

        public JoinEui? AppEui => LoRaDevice.AppEui;

        public char ClassType => LoRaDevice.ClassType;

        public DevAddr? DevAddr
        {
            get => LoRaDevice.DevAddr;
            set => LoRaDevice.DevAddr = value;
        }

        public DevEui DevEUI => LoRaDevice.DevEui;

        public bool Supports32BitFCnt
        {
            get => LoRaDevice.Supports32BitFCnt;
            set => LoRaDevice.Supports32BitFCnt = value;
        }

        public SimulatedDevice(TestDeviceInfo testDeviceInfo, uint frmCntDown = 0, uint frmCntUp = 0, IReadOnlyCollection<SimulatedBasicsStation> simulatedBasicsStation = null)
        {
            LoRaDevice = testDeviceInfo;
            FrmCntDown = frmCntDown;
            FrmCntUp = frmCntUp;
            SimulatedBasicsStations = simulatedBasicsStation.ToList();

            bool AddToDeviceMessageQueue(string response)
            {
                var message = JsonSerializer.Deserialize<JsonElement>(response);
                var devEui = message.GetProperty("DevEui");
                if (devEui.GetString() == DevEui.Parse(DevEUI).ToString())
                {
                    this.receivedMessages.Add(response);
                }

                return true;
            }

            ListenForAnswer(AddToDeviceMessageQueue);
        }

        public LoRaPayloadJoinRequest CreateJoinRequest(AppKey? appkey = null, DevNonce? devNonce = null)
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
            return new LoRaPayloadJoinRequest(LoRaDevice.AppEui.Value, DevEui.Parse(LoRaDevice.DeviceID), DevNonce, (appkey ?? LoRaDevice.AppKey).Value);
        }


        /// <summary>
        /// Creates request to send unconfirmed data message.
        /// </summary>
        public LoRaPayloadData CreateUnconfirmedDataUpMessage(string data, uint? fcnt = null, FramePort fport = FramePorts.App1, FrameControlFlags fctrlFlags = FrameControlFlags.None, bool isHexPayload = false, IList<MacCommand> macCommands = null, AppSessionKey? appSKey = null, NetworkSessionKey? nwkSKey = null)
        {
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
                LoRaDevice.DevAddr.Value,
                fctrlFlags,
                fcntBytes,
                macCommands,
                fport,
                payload,
                direction,
                Supports32BitFCnt ? fcnt : null);

            if (fport == FramePort.MacCommand)
            {
                payloadData.Serialize(nwkSKey is { } someNwkSessionKey ? someNwkSessionKey : NwkSKey ?? throw new InvalidOperationException($"Can't perform encryption without {nameof(NwkSKey)} when fport is set to 0."));
            }
            else
            {
                payloadData.Serialize(appSKey is { } someAppSessionKey ? someAppSessionKey : AppSKey ?? throw new InvalidOperationException($"Can't perform encryption without {nameof(AppSKey)}."));
            }
            payloadData.SetMic(nwkSKey is { } someNetworkSessionKey ? someNetworkSessionKey : NwkSKey ?? throw new InvalidOperationException($"Can't perform encryption without {nameof(NwkSKey)}."));

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
        public LoRaPayloadData CreateConfirmedDataUpMessage(string data, uint? fcnt = null, FramePort fport = FramePorts.App1, bool isHexPayload = false, AppSessionKey? appSKey = null, NetworkSessionKey? nwkSKey = null)
        {
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
            var payloadData = new LoRaPayloadData(MacMessageType.ConfirmedDataUp,
                                                  LoRaDevice.DevAddr.Value,
                                                  FrameControlFlags.Adr,
                                                  fcntBytes,
                                                  null,
                                                  fport,
                                                  payload,
                                                  direction,
                                                  Supports32BitFCnt ? fcnt : null);

            if (fport == FramePort.MacCommand)
            {
                payloadData.Serialize(nwkSKey is { } someNwkSessionKey ? someNwkSessionKey : NwkSKey ?? throw new InvalidOperationException($"Can't perform encryption without {nameof(NwkSKey)} when fport is set to 0."));
            }
            else
            {
                payloadData.Serialize(appSKey is { } someAppSessionKey ? someAppSessionKey : AppSKey ?? throw new InvalidOperationException($"Can't perform encryption without {nameof(AppSKey)}."));
            }
            payloadData.SetMic(nwkSKey is { } someNetworkSessionKey ? someNetworkSessionKey : NwkSKey ?? throw new InvalidOperationException($"Can't perform encryption without {nameof(NwkSKey)}."));

            return payloadData;
        }

        private bool HandleJoinAccept(LoRaPayloadJoinAccept payload)
        {

            // Calculate the keys
            var devNonce = DevNonce;

            if (LoRaDevice.AppKey is null)
            {
                throw new ArgumentException(nameof(LoRaDevice.AppKey));
            }

            var appSKey = OTAAKeysGenerator.CalculateAppSessionKey(payload.AppNonce, payload.NetId, devNonce, LoRaDevice.AppKey.Value);
            var nwkSKey = OTAAKeysGenerator.CalculateNetworkSessionKey(payload.AppNonce, payload.NetId, devNonce, LoRaDevice.AppKey.Value);
            var devAddr = payload.DevAddr;

            // if mic check failed, return false
            if (!payload.CheckMic(LoRaDevice.AppKey.Value))
            {
                return false;
            }

            LoRaDevice.AppSKey = appSKey;
            LoRaDevice.NwkSKey = nwkSKey;
            NetId = payload.NetId;
            AppNonce = payload.AppNonce;
            LoRaDevice.DevAddr = devAddr;

            return true;
        }

        //// Sends unconfirmed message
        public async Task SendDataMessageAsync(LoRaRequest loRaRequest)
        {
            var payload = (LoRaPayloadData)loRaRequest.Payload;

            var msg = JsonSerializer.Serialize(new
            {
                MHdr = uint.Parse(loRaRequest.Payload.MHdr.ToString(), System.Globalization.NumberStyles.HexNumber, CultureInfo.InvariantCulture),
                msgtype = "updf",
                DevAddr = int.Parse(payload.DevAddr.ToString(), System.Globalization.NumberStyles.HexNumber, CultureInfo.InvariantCulture),
                FCtrl = (uint)payload.FrameControlFlags,
                FCnt = MemoryMarshal.Read<ushort>(payload.Fcnt.Span),
                FOpts = ConversionHelper.ByteArrayToString(payload.Fopts),
                FPort = (int)payload.Fport,
                FRMPayload = ConversionHelper.ByteArrayToString(payload.Frmpayload),
                MIC = payload.Mic.Value.AsInt32,
                DR = loRaRequest.RadioMetadata.DataRate,
                Freq = loRaRequest.RadioMetadata.Frequency.AsUInt64,
                upinfo = new
                {
                    gpstime = loRaRequest.RadioMetadata.UpInfo.GpsTime,
                    rctx = 10,
                    rssi = loRaRequest.RadioMetadata.UpInfo.ReceivedSignalStrengthIndication,
                    xtime = loRaRequest.RadioMetadata.UpInfo.Xtime,
                    snr = loRaRequest.RadioMetadata.UpInfo.SignalNoiseRatio
                }
            });

            await SendMessageToBasicsStationsAsync(msg);

            TestLogger.Log($"[{payload.DevAddr}] Sending data: {payload.Frmpayload}");
        }

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
        private async Task SendMessageToBasicsStationsAsync(string message)
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
        {
            Parallel.ForEach(SimulatedBasicsStations, (basicStation) => _ = basicStation.SendMessageAsync(message));
        }

        public bool EnsureMessageResponsesAreReceived(int expectedCout)
        {
            return this.receivedMessages.Count == expectedCout;
        }

        // Performs join
        public async Task<bool> JoinAsync(LoRaRequest joinRequest, TimeSpan? timeout = null)
        {
            timeout ??= TimeSpan.FromSeconds(30);
            var joinRequestPayload = (LoRaPayloadJoinRequest)joinRequest.Payload;
            var joinSuccessfull = false;

            bool joinListenFunction(string response)
            {
                // handle join
                var joinAcceptstring = JsonSerializer.Deserialize<JoinAcceptResponse>(response);
                // is null in case it is another message coming from the station.
                if (joinAcceptstring?.Pdu != null)
                {
                    var joinAccept = new LoRaPayloadJoinAccept(ConversionHelper.StringToByteArray(joinAcceptstring.Pdu), AppKey.Value);
                    var result = HandleJoinAccept(joinAccept); // may need to return bool and only release if true.
                    joinSuccessfull |= result;
                    return result;
                }

                return false;
            }
            await SendMessageToBasicsStationsAsync(JsonSerializer.Serialize(new
            {
                JoinEui = joinRequestPayload.AppEui.ToString(),
                msgtype = "jreq",
                DevEui = joinRequestPayload.DevEUI.ToString(),
                DevNonce = joinRequestPayload.DevNonce.AsUInt16,
                MHdr = uint.Parse(joinRequestPayload.MHdr.ToString(), System.Globalization.NumberStyles.HexNumber, CultureInfo.InvariantCulture),
                MIC = joinRequestPayload.Mic.Value.AsInt32,
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
                timeout = TimeSpan.FromSeconds(60);
            }
#endif

            await AssertUtils.ContainsWithRetriesAsync((message) => joinListenFunction(message), this.receivedMessages);
            return true;
        }

        private void ListenForAnswer(Func<string, bool> func)
        {
            foreach (var basicsStation in SimulatedBasicsStations)
            {
                basicsStation.SubscribeOnce(func);
            }
        }

        /// <summary>
        /// Setups the join properties.
        /// </summary>
        public void SetupJoin(AppSessionKey appSKey, NetworkSessionKey nwkSKey, DevAddr devAddr)
        {
            LoRaDevice.AppSKey = appSKey;
            LoRaDevice.NwkSKey = nwkSKey;
            LoRaDevice.DevAddr = devAddr;
        }
    }
}
