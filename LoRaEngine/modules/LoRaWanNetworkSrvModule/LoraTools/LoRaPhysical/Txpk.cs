using LoRaWan;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace LoRaTools.LoRaPhysical
{
    public class Txpk
    {
        public bool imme;
        public string data;
        public long tmst;
        public uint size;
        public double freq;
        public uint rfch;
        public string modu;
        public string datr;
        public string codr;
        public uint powe;
        public bool ipol;

        public static Txpk CreateTxpk(byte[] inputMessage, string appKey)
        {
            PhysicalPayload PhysicalPayload = new PhysicalPayload(inputMessage, true);
            var payload = Encoding.UTF8.GetString(PhysicalPayload.message);

            // deserialize for a downlink message
            //checkwith franc
            var payloadDownObject = JsonConvert.DeserializeObject<DownlinkPktFwdMessage>(payload);
            if (payloadDownObject != null)
            {
                if (payloadDownObject.txpk != null)
                {
                    return payloadDownObject.txpk;
                }
                else
                {
                    Logger.Log("Error: " + payloadDownObject.txpk, Logger.LoggingLevel.Full);
                }
            }
            return null;
        }
    }

}
