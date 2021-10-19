namespace CayenneDecoderModule.Controllers
{
    using System;
    using Microsoft.AspNetCore.Mvc;
    using System.Net;
    using CayenneDecoderModule.Classes;
    using System.Reflection;
    using System.Globalization;

    [Route("api")]
    [ApiController]
    public class DecoderController : ControllerBase
    {
        // GET: api/TestDecoder
        [HttpGet("{decoder}", Name = "Get")]
        [ProducesResponseType(200)]
        [ProducesResponseType(400)]
        public ActionResult<string> Get(string decoder, string fport, string payload)
        {
            // Validate that fport and payload URL parameters are present.
            Validator.ValidateParameters(fport, payload);

            var decoderType = typeof(LoraDecoders);
            var toInvoke = decoderType.GetMethod(decoder, BindingFlags.Static | BindingFlags.NonPublic);

            if (toInvoke != null)
            {
                return (string)toInvoke.Invoke(null, new object[] { Convert.FromBase64String(payload), Convert.ToUInt16(fport, CultureInfo.InvariantCulture) });
            }
            else
            {
                throw new WebException($"Decoder {decoder} not found.");
            }
        }
    }
}
