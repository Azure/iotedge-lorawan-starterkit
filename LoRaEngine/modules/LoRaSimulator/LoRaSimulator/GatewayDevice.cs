using LoRaTools;
using LoRaTools.LoRaPhysical;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace LoRaSimulator
{
    public class GatewayDevice
    {
        public Rxpk rxpk { get; set; }
        // Used for the point 0. Always increase
        public long TimeAtBoot { get; internal set; }
        public GatewayDevice(string json)
        {
            rxpk = JsonConvert.DeserializeObject<Rxpk>(json);
            TimeAtBoot = DateTimeOffset.Now.UtcTicks;

        }

        public string GetMessage(byte[] data)
        {
            rxpk.data = Convert.ToBase64String(data);
            rxpk.size = (uint)data.Length;
            // tmst it is time in micro seconds
            var now = DateTimeOffset.UtcNow;
            var tmst = (now.UtcTicks - TimeAtBoot) / (TimeSpan.TicksPerMillisecond / 1000);
            if (tmst >= UInt32.MaxValue)
            {
                tmst = tmst - UInt32.MaxValue;
                TimeAtBoot = now.UtcTicks - tmst;
            }
            rxpk.tmst = Convert.ToUInt32(tmst);

            return JsonConvert.SerializeObject(rxpk);
        }

    }
}
