using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Net;
using CayenneDecoderModule.Classes;
using System.Reflection;
using System.Text;

namespace CayenneDecoderModule.Controllers
{
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

            Type decoderType = typeof(LoraDecoders);
            MethodInfo toInvoke = decoderType.GetMethod(decoder, BindingFlags.Static | BindingFlags.NonPublic);

            if (toInvoke != null)
            {
                return (string)toInvoke.Invoke(null, new object[] { Convert.FromBase64String(payload), Convert.ToUInt16(fport) });
            }
            else
            {
                throw new WebException( $"Decoder {decoder} not found.");
            }
        }
    }
}
