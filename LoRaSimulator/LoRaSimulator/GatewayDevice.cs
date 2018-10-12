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

        public GatewayDevice(string json)
        {
            rxpk = JsonConvert.DeserializeObject<Rxpk>(json);            
        }

        public string GetMessage(byte[] data)
        {
            rxpk.data = Convert.ToBase64String(data);
            //setup time to universdal format
            rxpk.time = DateTime.UtcNow.ToString("O");
            // TODO: convert the proper time
            rxpk.tmms = 12345678;
            return JsonConvert.SerializeObject(rxpk);
        }

    }
}
