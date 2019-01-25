namespace LoRaTools.LoRaPhysical
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using LoRaWan;
    using Microsoft.Extensions.Logging;
    using Newtonsoft.Json;
    using static LoRaWan.Logger;

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

        /// <summary>
        /// This method is used as part of Simulated device for testing purposes
        /// </summary>
        /// <param name="inputMessage">The Input Message bytes</param>
        /// <param name="appKey">The appKey</param>
        /// <returns></returns>
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
                    Logger.Log("Error: " + payloadDownObject.txpk, LogLevel.Debug);
                }
            }
            return null;
        }


        }

}
