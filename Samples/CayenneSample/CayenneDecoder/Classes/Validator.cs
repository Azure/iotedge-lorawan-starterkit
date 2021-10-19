namespace CayenneDecoderModule.Classes
{
    using System.Net;

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
                if (!string.IsNullOrEmpty(error))
                    error += " and ";
                error += "Payload missing";
            }

            if (!string.IsNullOrEmpty(error))
            {
                throw new WebException(error);
            }
        }
    }
}
