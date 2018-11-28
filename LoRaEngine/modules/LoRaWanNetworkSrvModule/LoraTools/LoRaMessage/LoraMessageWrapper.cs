//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//
using LoRaWan;
using Newtonsoft.Json;
using System;
using System.Text;
using LoRaTools.LoRaPhysical;
using LoRaTools.Utils;
using static LoRaTools.LoRaMessage.LoRaPayloadData;

namespace LoRaTools.LoRaMessage
{
    public enum LoRaMessageType
    {
        JoinRequest,
        JoinAccept,
        UnconfirmedDataUp,
        UnconfirmedDataDown,
        ConfirmedDataUp,
        ConfirmedDataDown,
        RFU,
        Proprietary
    }
    /// <summary>
    /// class exposing Physical payload & LoRa Payload Message
    /// </summary>
    public class LoRaMessageWrapper
    {
        /// <summary>
        /// Does this message contains a LoRa Payload. 
        /// </summary>
        public bool IsLoRaMessage = false;
        /// <summary>
        /// The LoRa Payload message.
        /// </summary>
        public LoRaPayload LoRaPayloadMessage { get; set; }
        /// <summary>
        /// The Json transmitted to the pktfwd.
        /// </summary>
        public PktFwdMessage PktFwdPayload { get; set; }
        /// <summary>
        /// The Physical message transmitted to the pktfwd.
        /// </summary>
        public PhysicalPayload PhysicalPayload { get; set; }

        /// <summary>
        /// The Message type
        /// </summary>
        public LoRaMessageType LoRaMessageType { get; set; }

        /// <summary>
        /// This contructor is used in case of uplink message, hence we don't know the message type yet
        /// </summary>
        /// <param name="inputMessage"></param>
        public LoRaMessageWrapper(byte[] inputMessage, bool server = false, string AppKey = "")
        {
            // packet normally sent by the gateway as heartbeat. TODO find more elegant way to integrate.
            PhysicalPayload = new PhysicalPayload(inputMessage, server);
            if (PhysicalPayload.message != null)
            {
                var payload = Encoding.Default.GetString(PhysicalPayload.message);

                // todo ronnie implement a better logging by message type
                if (!payload.StartsWith("{\"stat"))
                {
                    Logger.Log($"Physical dataUp {payload}", Logger.LoggingLevel.Full);

                    // Deserialized for uplink messages
                    var payloadObject = JsonConvert.DeserializeObject<UplinkPktFwdMessage>(payload);
                    PktFwdPayload = payloadObject;
                    // set up the parts of the raw message   
                    // status message
                    if (PktFwdPayload != null)
                    {
                        // if there is no packet, then it maybe a downlink message
                        if (PktFwdPayload.GetPktFwdMessage().Rxpks.Count > 0)
                        {
                            if (PktFwdPayload.GetPktFwdMessage().Rxpks[0].data != null)
                            {
                                byte[] convertedInputMessage = Convert.FromBase64String(PktFwdPayload.GetPktFwdMessage().Rxpks[0].data);
                                var messageType = convertedInputMessage[0] >> 5;
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
                                 
                                IsLoRaMessage = true;
                            }
                        }
                        else
                        {
                            // deselrialize for a downlink message
                            var payloadDownObject = JsonConvert.DeserializeObject<DownlinkPktFwdMessage>(payload);
                            if (payloadDownObject != null)
                            {
                                if (payloadDownObject.txpk != null)
                                {
                                    // if we have data, it is a downlink message
                                    if (payloadDownObject.txpk.data != null)
                                    {
                                        byte[] convertedInputMessage = Convert.FromBase64String(payloadDownObject.txpk.data);
                                        var messageType = convertedInputMessage[0] >> 5;
                                        LoRaMessageType = (LoRaMessageType)messageType;
                                        if (messageType == (int)LoRaMessageType.JoinAccept)
                                        {
                                            LoRaPayloadMessage = new LoRaPayloadJoinAccept(convertedInputMessage, AppKey);
                                        }
                                        IsLoRaMessage = true;
                                    }
                                }else
                                {
                                    Logger.Log("Error: " + payload,Logger.LoggingLevel.Full);
                                }
                            }
                        }
                    }
                }
                else
                {
                    Logger.Log($"Statistic: {payload}", Logger.LoggingLevel.Full);
                    IsLoRaMessage = false;
                }
            }
            else
            {

                IsLoRaMessage = false;
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
        public LoRaMessageWrapper(LoRaPayload payload, LoRaMessageType type, byte[] physicalToken)
        {
            LoRaPayloadMessage = payload;
            // construct a Join Accept Message
            if (type == LoRaMessageType.JoinAccept)
            {
                var downlinkmsg = new DownlinkPktFwdMessage(Convert.ToBase64String(payload.GetByteMessage()));
                PktFwdPayload = downlinkmsg;
                var jsonMsg = JsonConvert.SerializeObject(downlinkmsg);
                var messageBytes = Encoding.Default.GetBytes(jsonMsg);
                PhysicalPayload = new PhysicalPayload(physicalToken, PhysicalIdentifier.PULL_RESP, messageBytes);
            }
            else if (type == LoRaMessageType.UnconfirmedDataDown)
            {
                throw new NotImplementedException();
            }
            else if (type == LoRaMessageType.ConfirmedDataDown)
            {
                throw new NotImplementedException();
            }
        }

        public LoRaMessageWrapper(LoRaPayload payload, LoRaMessageType type, byte[] physicalToken, string datr, uint rfch, double freq, long tmst)
        {
            LoRaPayloadMessage = payload;
            PktFwdPayload = new DownlinkPktFwdMessage(Convert.ToBase64String(LoRaPayloadMessage.GetByteMessage()), datr, rfch, freq, tmst);
            var jsonMsg = JsonConvert.SerializeObject(PktFwdPayload);
            Logger.Log(ConversionHelper.ByteArrayToString(payload.DevAddr.Span.ToArray()),$"{((MType)(payload.Mhdr.Span[0])).ToString()} {jsonMsg}", Logger.LoggingLevel.Full);
            var messageBytes = Encoding.Default.GetBytes(jsonMsg);
            PhysicalPayload = new PhysicalPayload(physicalToken, PhysicalIdentifier.PULL_RESP, messageBytes);
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
            var retValue = LoRaPayloadMessage.PerformEncryption(appSkey);
            return retValue;
        }
    }


}
