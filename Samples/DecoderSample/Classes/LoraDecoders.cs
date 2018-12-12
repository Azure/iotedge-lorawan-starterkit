using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace SensorDecoderModule.Classes
{
    internal static class LoraDecoders
    {   
        private static string DecoderValueSensor(byte[] payload, uint fport)
        {
            var result = Encoding.UTF8.GetString(payload);            
            return JsonConvert.SerializeObject(new { value = result });
        }
    }       
}
