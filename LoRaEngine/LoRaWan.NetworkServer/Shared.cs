using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;

namespace LoRaWan.NetworkServer
{
    public static class Shared
    {
        public static ConcurrentDictionary<string, LoraKeys> loraKeysList = new ConcurrentDictionary<string, LoraKeys>();
    }
}
