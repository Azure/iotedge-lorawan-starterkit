namespace LoRaSimulator
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using LoRaTools;
    using LoRaTools.LoRaPhysical;
    using Newtonsoft.Json;

    public class GatewayDevice
    {
        public Rxpk Rxpk { get; set; }

        // Used for the point 0. Always increase
        public long TimeAtBoot { get; internal set; }

        public GatewayDevice(string json)
        {
            this.Rxpk = JsonConvert.DeserializeObject<Rxpk>(json);
            this.TimeAtBoot = DateTimeOffset.Now.UtcTicks;

        }

        /// <summary>
        /// Get a message
        /// </summary>
        public string GetMessage(byte[] data)
        {
            this.Rxpk.Data = Convert.ToBase64String(data);
            this.Rxpk.Size = (uint)data.Length;
            // tmst it is time in micro seconds
            var now = DateTimeOffset.UtcNow;
            var tmst = (now.UtcTicks - this.TimeAtBoot) / (TimeSpan.TicksPerMillisecond / 1000);
            if (tmst >= uint.MaxValue)
            {
                tmst = tmst - uint.MaxValue;
                this.TimeAtBoot = now.UtcTicks - tmst;
            }

            this.Rxpk.Tmst = Convert.ToUInt32(tmst);
            return JsonConvert.SerializeObject(this.Rxpk);
        }

    }
}
