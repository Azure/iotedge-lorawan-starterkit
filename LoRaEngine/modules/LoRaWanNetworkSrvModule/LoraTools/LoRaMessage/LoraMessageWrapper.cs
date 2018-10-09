//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//
using LoRaTools.LoRaPhysical;
using LoRaWan;
using Newtonsoft.Json;
using System;
using System.Text;

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
        public LoRaMessageWrapper(byte[] inputMessage)
        {
            // packet normally sent by the gateway as heartbeat. TODO find more elegant way to integrate.
            PhysicalPayload = new PhysicalPayload(inputMessage);
            if (PhysicalPayload.message != null)
            {
                var payload = Encoding.Default.GetString(PhysicalPayload.message);

                // todo ronnie implement a better logging by message type
                if (!payload.StartsWith("{\"stat"))
                    Logger.Log($"DataUp {payload}", Logger.LoggingLevel.Full);

                var payloadObject = JsonConvert.DeserializeObject<UplinkPktFwdMessage>(payload);
                PktFwdPayload = payloadObject;
                // set up the parts of the raw message   
                // status message
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
                else
                {
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

        public LoRaMessageWrapper(LoRaPayload payload, LoRaMessageType type, byte[] physicalToken, string _datr, uint _rfch, double _freq, long _tmst)
        {
            LoRaPayloadMessage = payload;
            PktFwdPayload = new DownlinkPktFwdMessage(Convert.ToBase64String(LoRaPayloadMessage.GetByteMessage()), _datr, _rfch, _freq, _tmst);
            var jsonMsg = JsonConvert.SerializeObject(PktFwdPayload);
            Logger.Log($"{type.ToString()} {jsonMsg}", Logger.LoggingLevel.Full);
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
