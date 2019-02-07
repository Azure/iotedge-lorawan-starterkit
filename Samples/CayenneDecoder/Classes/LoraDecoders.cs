using System.Text;
using Newtonsoft.Json;

namespace CayenneDecoderModule.Classes
{
    internal static class LoraDecoders
    {
        private static string CayenneDecoder(byte[] payload, uint fport)
        {
            CayenneDecoder cayenneDecoder = new CayenneDecoder(payload);

            // Return a JSON string containing the decoded data
            return JsonConvert.SerializeObject(new { value = cayenneDecoder.CayenneDevice });
        }
    }
}