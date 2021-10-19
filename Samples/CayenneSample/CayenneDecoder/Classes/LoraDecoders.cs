#pragma warning disable IDE0051 // Remove unused private members
#pragma warning disable CA1801 // Review unused parameters
#pragma warning disable IDE0060 // Remove unused parameter
#pragma warning disable CA1812 // Remove unused class

namespace CayenneDecoderModule.Classes
{
    using Newtonsoft.Json;

    internal static class LoraDecoders
    {
        private static string CayenneDecoder(byte[] payload, uint fport)
        {
            var cayenneDecoder = new CayenneDecoder(payload);

            // Return a JSON string containing the decoded data
            return JsonConvert.SerializeObject(new { value = cayenneDecoder.CayenneDevice });
        }
    }
}
