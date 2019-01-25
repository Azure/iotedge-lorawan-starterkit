//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

namespace LoRaTools.LoRaMessage
{
    using System;
    using System.Text;
    using LoRaTools.LoRaPhysical;
    using LoRaTools.Utils;
    using LoRaWan;
    using Microsoft.Extensions.Logging;
    using Newtonsoft.Json;
    using static LoRaTools.LoRaMessage.LoRaPayloadData;

    public enum LoRaMessageType:byte
    {
        // Request sent by device to join
        JoinRequest,

        // Response to a join request sent to device
        JoinAccept = 32,

        // Device to cloud message, no confirmation expected
        UnconfirmedDataUp = 64,

        // Cloud to device message, no confirmation expected
        UnconfirmedDataDown = 96,

        // Device to cloud message, confirmation required
        ConfirmedDataUp = 128,

        // Cloud to device message, confirmation required
        ConfirmedDataDown = 160
    }


    [Obsolete("This class will be faded out at message processor refactory.")]
    /// <summary>
    /// class exposing Physical payload & LoRa Payload Message
    /// </summary>
    public class LoRaMessageWrapper
    {
        /// <summary>
        /// The LoRa Payload message.
        /// </summary>
        public LoRaPayload LoRaPayloadMessage { get; set; }

        /// <summary>
        /// The Json transmitted to the pktfwd.
        /// </summary>
        public PktFwdMessage PktFwdPayload { get; set; }

        /// <summary>
        /// The Message type
        /// </summary>
        public LoRaMessageType LoRaMessageType { get; set; }

        /// <summary>
        /// This contructor is used in case of uplink message, hence we don't know the message type yet
        /// </summary>
        /// <param name="rxpk"></param>
        public LoRaMessageWrapper(Rxpk rxpk)
        {
            PktFwdPayload = new UplinkPktFwdMessage(rxpk);
            byte[] convertedInputMessage = Convert.FromBase64String(rxpk.data);
            var messageType = convertedInputMessage[0];
            LoRaMessageType = (LoRaMessageType)messageType;
            // Uplink Message
            if (messageType == (int)LoRaMessageType.UnconfirmedDataUp)
            {
                LoRaPayloadMessage = new LoRaPayloadData(convertedInputMessage);
            }
            else if (messageType == (int)LoRaMessageType.ConfirmedDataUp)
            {
                LoRaPayloadMessage = new LoRaPayloadData(convertedInputMessage);
            }
            else if (messageType == (int)LoRaMessageType.JoinRequest)
            {
                LoRaPayloadMessage = new LoRaPayloadJoinRequest(convertedInputMessage);
            }

        }

        public LoRaMessageWrapper(LoRaPayload payload, LoRaMessageType type)
        {
            LoRaPayloadMessage = payload;
            // construct a Join Accept Message
            if (type == LoRaMessageType.JoinAccept)
            {
                var downlinkmsg = new DownlinkPktFwdMessage(Convert.ToBase64String(payload.GetByteMessage()));
                PktFwdPayload = downlinkmsg;
                var jsonMsg = JsonConvert.SerializeObject(downlinkmsg);
                var messageBytes = Encoding.Default.GetBytes(jsonMsg);
            }
        }



            /// <summary>
            /// This contructor is used in case of downlink message
            /// </summary>
            /// <param name="inputMessage"></param>
            /// <param name="type">
            /// 0 = Join Request
            /// 1 = Join Accept
            /// 2 = Unconfirmed Data up
            /// 3 = Unconfirmed Data down
            /// 4 = Confirmed Data up
            /// 5 = Confirmed Data down
            /// 6 = Rejoin Request</param>
            public LoRaMessageWrapper(Txpk txpk,string appKey)
        {
            if (txpk.data != null)
            {
                byte[] convertedInputMessage = Convert.FromBase64String(txpk.data);
                var messageType = convertedInputMessage[0] >> 5;
                LoRaMessageType = (LoRaMessageType)messageType;
                if (messageType == (int)LoRaMessageType.JoinAccept)
                {
                    LoRaPayloadMessage = new LoRaPayloadJoinAccept(convertedInputMessage, appKey);
                }
            }
        }

        

        public LoRaMessageWrapper(LoRaPayload payload, LoRaMessageType type, string datr, uint rfch, double freq, long tmst)
        {
            LoRaPayloadMessage = payload;
            PktFwdPayload = new DownlinkPktFwdMessage(Convert.ToBase64String(LoRaPayloadMessage.GetByteMessage()), datr, rfch, freq, tmst);
            var jsonMsg = JsonConvert.SerializeObject(PktFwdPayload);
            var devEUI = payload.GetLoRaMessage().DevEUI;
            if (devEUI.Length != 0)
            {
                Logger.Log(ConversionHelper.ByteArrayToString(devEUI.Span.ToArray()), $"{((LoRaMessageType)(payload.Mhdr.Span[0])).ToString()} {jsonMsg}", LogLevel.Debug);
            }else
            {
                Logger.Log(ConversionHelper.ByteArrayToString(payload.DevAddr.Span.ToArray()), $"{((LoRaMessageType)(payload.Mhdr.Span[0])).ToString()} {jsonMsg}", LogLevel.Debug);
            }
        }

        /// <summary>
        /// Method to map the Mic check to the appropriate implementation.
        /// </summary>
        /// <param name="nwskey">The Neetwork Secret Key</param>
        /// <returns>a boolean telling if the MIC is valid or not</returns>
        public bool CheckMic(string nwskey)
        {
            return LoRaPayloadMessage.CheckMic(nwskey);
        }

        /// <summary>
        /// Method to decrypt payload to the appropriate implementation.
        /// </summary>
        /// <param name="nwskey">The Application Secret Key</param>
        /// <returns>a boolean telling if the MIC is valid or not</returns>
        public byte[] DecryptPayload(string appSkey)
        {
            if (LoRaPayloadMessage is LoRaPayloadData loRaPayloadData)
                return loRaPayloadData.GetDecryptedPayload(appSkey);

            return LoRaPayloadMessage.PerformEncryption(appSkey);
        }
    }
}
