using System.Text;
using Newtonsoft.Json;

namespace SensorDecoderModule.Classes
{
    internal static class LoraDecoders
    {   
        private static string DecoderStringValueSensor(byte[] payload, uint fport)
        {
            // Convert payload containing a string back to string format
            var result = Encoding.UTF8.GetString(payload);            
            return JsonConvert.SerializeObject(new { value = result });
        }

        private static string DecoderBinaryValueSensor(byte[] payload, uint fport)
        {
            // Convert payload containing binary data to HEX string
            var hex_string = ConversionHelper.ByteArrayToString(payload);
            return JsonConvert.SerializeObject(new { value = hex_string });
        }

    }       
}
