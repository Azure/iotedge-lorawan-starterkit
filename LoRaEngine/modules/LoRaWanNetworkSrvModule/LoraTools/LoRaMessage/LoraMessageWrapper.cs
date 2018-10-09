//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//
using LoRaTools;
using LoRaWan;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Security;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
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
        /// see 
        /// </summary>
        public bool IsLoRaMessage = false;
        public LoRaGenericPayload PayloadMessage { get; set; }
        public LoRaMetada LoraMetadata { get; set; }
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
                LoraMetadata = new LoRaMetada(PhysicalPayload.message);
                // set up the parts of the raw message   
                // status message
                if (LoraMetadata.RawB64data != null)
                {
                    byte[] convertedInputMessage = Convert.FromBase64String(LoraMetadata.RawB64data);
                    var messageType = convertedInputMessage[0] >> 5;
                    LoRaMessageType = (LoRaMessageType)messageType;
                    // Uplink Message
                    if (messageType == (int)LoRaMessageType.UnconfirmedDataUp)
                    {
                        PayloadMessage = new LoRaPayloadStandardData(convertedInputMessage);
                    }
                    else if (messageType == (int)LoRaMessageType.ConfirmedDataUp)
                    {
                        PayloadMessage = new LoRaPayloadStandardData(convertedInputMessage);
                    }
                    else if (messageType == (int)LoRaMessageType.JoinRequest)
                    {
                        PayloadMessage = new LoRaPayloadJoinRequest(convertedInputMessage);
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
        public LoRaMessageWrapper(LoRaGenericPayload payload, LoRaMessageType type, byte[] physicalToken)
        {
            // construct a Join Accept Message
            if (type == LoRaMessageType.JoinAccept)
            {
                PayloadMessage = (LoRaPayloadJoinAccept)payload;
                LoraMetadata = new LoRaMetada(PayloadMessage, type);
                var downlinkmsg = new DownlinkPktFwdMessage(LoraMetadata.RawB64data);
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

        public LoRaMessageWrapper(LoRaGenericPayload payload, LoRaMessageType type, byte[] physicalToken, string _datr, uint _rfch, double _freq, long _tmst)
        {
            // construct a Join Accept Message
            if (type == LoRaMessageType.JoinAccept)
            {
                PayloadMessage = (LoRaPayloadJoinAccept)payload;
                LoraMetadata = new LoRaMetada(PayloadMessage, type);
                var downlinkmsg = new DownlinkPktFwdMessage(LoraMetadata.RawB64data, _datr, _rfch, _freq, _tmst);

                var jsonMsg = JsonConvert.SerializeObject(downlinkmsg);
                Logger.Log($"JoinAccept {jsonMsg}", Logger.LoggingLevel.Full);
                var messageBytes = Encoding.Default.GetBytes(jsonMsg);

                PhysicalPayload = new PhysicalPayload(physicalToken, PhysicalIdentifier.PULL_RESP, messageBytes);
            }
            else if (type == LoRaMessageType.UnconfirmedDataDown)
            {
                PayloadMessage = (LoRaPayloadStandardData)payload;
                LoraMetadata = new LoRaMetada(PayloadMessage, type);
                var downlinkmsg = new DownlinkPktFwdMessage(LoraMetadata.RawB64data, _datr, _rfch, _freq, _tmst + 1000000);

                var jsonMsg = JsonConvert.SerializeObject(downlinkmsg);
                Logger.Log($"UnconfirmedDataDown {jsonMsg}", Logger.LoggingLevel.Full);
                var messageBytes = Encoding.Default.GetBytes(jsonMsg);

                PhysicalPayload = new PhysicalPayload(physicalToken, PhysicalIdentifier.PULL_RESP, messageBytes);
            }
            else if (type == LoRaMessageType.ConfirmedDataDown)
            {
                PayloadMessage = (LoRaPayloadStandardData)payload;
                LoraMetadata = new LoRaMetada(PayloadMessage, type);
                var downlinkmsg = new DownlinkPktFwdMessage(LoraMetadata.RawB64data, _datr, _rfch, _freq, _tmst + 1000000);

                var jsonMsg = JsonConvert.SerializeObject(downlinkmsg);
                Logger.Log($"ConfirmedDataDown {jsonMsg}", Logger.LoggingLevel.Full);
                var messageBytes = Encoding.Default.GetBytes(jsonMsg);

                PhysicalPayload = new PhysicalPayload(physicalToken, PhysicalIdentifier.PULL_RESP, messageBytes);
            }
        }

        /// <summary>
        /// Method to map the Mic check to the appropriate implementation.
        /// </summary>
        /// <param name="nwskey">The Neetwork Secret Key</param>
        /// <returns>a boolean telling if the MIC is valid or not</returns>
        public bool CheckMic(string nwskey)
        {
            return ((LoRaDataPayload)PayloadMessage).CheckMic(nwskey);
        }

        /// <summary>
        /// Method to decrypt payload to the appropriate implementation.
        /// </summary>
        /// <param name="nwskey">The Application Secret Key</param>
        /// <returns>a boolean telling if the MIC is valid or not</returns>
        public byte[] DecryptPayload(string appSkey)
        {
            var retValue = ((LoRaDataPayload)PayloadMessage).PerformEncryption(appSkey);
            LoraMetadata.DecodedData = retValue;
            return retValue;
        }
    }


}
