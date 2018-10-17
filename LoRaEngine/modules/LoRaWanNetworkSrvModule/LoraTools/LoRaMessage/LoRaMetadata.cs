using LoRaWan;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace LoRaTools.LoRaMessage
{
    /// <summary>
    /// Metadata about a Lora Packet, featuring a Lora Packet, the payload and the data.
    /// </summary>
    public class LoRaMetada
    {
        public PktFwdMessage FullPayload { get; set; }
        public string RawB64data { get; set; }

        

        /// <summary>
        /// Case of Uplink message. 
        /// </summary>
        /// <param name="input"></param>
        public LoRaMetada(byte[] input)
        {
            var payload = Encoding.Default.GetString(input);

            // todo ronnie implement a better logging by message type
            if (!payload.StartsWith("{\"stat"))
                Logger.Log($"DataUp {payload}", Logger.LoggingLevel.Full);


            var payloadObject = JsonConvert.DeserializeObject<UplinkPktFwdMessage>(payload);
            FullPayload = payloadObject;
            // TODO to this in a loop.
            if (payloadObject.rxpk.Count > 0)
            {
                RawB64data = payloadObject.rxpk[0].data;
            }
        }

        /// <summary>
        /// Case of Downlink message. TODO refactor this
        /// </summary>
        /// <param name="input"></param>
        public LoRaMetada(LoRaPayload payloadMessage, LoRaMessageType messageType)
        {
            if (messageType == LoRaMessageType.JoinAccept)
            {
                RawB64data = Convert.ToBase64String(((LoRaPayloadJoinAccept)payloadMessage).GetByteMessage());
            }
            else if (messageType == LoRaMessageType.UnconfirmedDataDown || messageType == LoRaMessageType.ConfirmedDataDown)
            {
                RawB64data = Convert.ToBase64String(((LoRaPayloadData)payloadMessage).GetByteMessage());
            }
        }
    }
}
