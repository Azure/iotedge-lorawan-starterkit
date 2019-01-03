using LoRaWan;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;

namespace LoRaTools.LoRaPhysical
{
    public class Rxpk
    {
        public string time;
        public uint tmms;
        public uint tmst;
        public double freq; //868
        public uint chan;
        public uint rfch;
        public int stat;
        public string modu;
        public string datr;
        public string codr;
        public int rssi;
        public float lsnr;
        public uint size;
        public string data;

        /// <summary>
        /// Required Signal-to-noise ratio to demodulate a LoRa signal given a spread Factor
        /// Spreading Factor -> Required SNR
        /// taken from https://www.semtech.com/uploads/documents/DS_SX1276-7-8-9_W_APP_V5.pdf
        /// </summary>
        private Dictionary<int, double> SpreadFactorToSNR = new Dictionary<int, double>()
         {
            { 6,  -5 },
            { 7, -7.5 },
            {8,  -10 },
            {9, -12.5 },
            {10, -15 },
            {11, -17.5 },
            {12, -20 }
        };

        [JsonExtensionData]
        public Dictionary<string, object> ExtraData { get; } = new Dictionary<string, object>();

        public Rxpk()
        {
            
        }

        // Copy constructor
        public Rxpk(Rxpk other)
        {
            if (other.ExtraData != null)
                this.ExtraData = new Dictionary<string, object>(other.ExtraData);

            this.chan = other.chan;
            this.codr = other.codr;
            this.data = other.data;
            this.datr = other.datr;
            this.freq = other.freq;
            this.lsnr = other.lsnr;
            this.modu = other.modu;
            this.rfch = other.rfch;
            this.rssi = other.rssi;
            this.size = other.size;
            this.stat = other.stat;
            this.time = other.time;
            this.tmms = other.tmms;
            this.tmst = other.tmst;
        }

        /// <summary>
        /// Get the modulation margin for MAC Commands LinkCheck
        /// </summary>
        /// <param name="input">the input physical rxpk from the packet</param>
        /// <returns></returns>
        public uint GetModulationMargin()
        {
            //required SNR:
            var requiredSNR = SpreadFactorToSNR[int.Parse(datr.Substring(datr.IndexOf("SF") + 2, datr.IndexOf("BW") - 1 - datr.IndexOf("SF") + 2))];
            //get the minimum
            uint margin = (uint)(lsnr - requiredSNR);
            if (margin < 0)
                margin = 0;
            return margin;
        }

        /// <summary>
        /// Method to create a Rxpk object from a byte array.
        /// This is typically used for an upstream communication.
        /// </summary>
        /// <param name="inputMessage">Input byte array</param>
        /// <returns>List of rxpk or null if no Rxpk was found</returns>
        public static List<Rxpk> CreateRxpk(byte[] inputMessage)
        {
            PhysicalPayload PhysicalPayload = new PhysicalPayload(inputMessage);
            if (PhysicalPayload.message != null)
            {
                var payload = Encoding.UTF8.GetString(PhysicalPayload.message);
                if (!payload.StartsWith("{\"stat"))
                {
                    Logger.Log($"Physical dataUp {payload}", Logger.LoggingLevel.Full);
                    var payloadObject = JsonConvert.DeserializeObject<UplinkPktFwdMessage>(payload);
                    if (payloadObject != null)
                    {
                        if (payloadObject.rxpk != null)
                        {
                            return payloadObject.rxpk;
                        }
                    }
                }
                else
                {
                    Logger.Log($"Statistic: {payload}", Logger.LoggingLevel.Full);
                }
            }
            return new List<Rxpk>();
        }
    }

}
