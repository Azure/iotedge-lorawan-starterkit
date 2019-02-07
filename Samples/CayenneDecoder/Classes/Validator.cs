using System.Net;

namespace CayenneDecoderModule.Classes
{
    public static class Validator
    {
        public static void ValidateParameters(string fport, string payload)
        {
            var error = "";

            if (fport == null)
            {
                error += "Fport missing";
            }
            if (payload == null)
            {
                if (error != "")
                    error += " and ";
                error += "Payload missing";
            }

            if (error != "")
            {
                throw new WebException(error);
            }
        }
    }
}
