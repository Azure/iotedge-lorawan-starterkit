using LoRaTools;
using Newtonsoft.Json;
using PacketManager;
using System;
using System.Collections.Generic;
using System.Text;

namespace LoRaSimulator
{
    public class GatewayDevice
    {
        public Rxpk rxpk { get; set; }
        // Used for the point 0. Always increase
        public DateTimeOffset TimeAtBoot { get; internal set; }
        public GatewayDevice(string json)
        {
            rxpk = JsonConvert.DeserializeObject<Rxpk>(json);
            TimeAtBoot = DateTimeOffset.UtcNow;

        }

        public string GetMessage(byte[] data)
        {
            rxpk.data = Convert.ToBase64String(data);
            rxpk.size = (uint)data.Length;
            // tmst it is time in micro seconds
            var tmst = (DateTimeOffset.UtcNow.UtcTicks - TimeAtBoot.UtcTicks) / (TimeSpan.TicksPerMillisecond / 1000);
            if (tmst >= UInt32.MaxValue)
            {
                TimeAtBoot = DateTimeOffset.UtcNow;
                tmst = 0;
            }
            rxpk.tmst = Convert.ToUInt32(tmst);

            return JsonConvert.SerializeObject(rxpk);
        }

    }
}
