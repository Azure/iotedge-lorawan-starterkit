using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Text;

namespace LoRaWan.NetworkServer
{
    public class MessageProcessor
    {
        const int msgPreambSize = 12;
        public void processMessage(byte[] message)
        {
            //Decode message
            byte[] preamb = new byte[msgPreambSize];
            byte[] msgBody = new byte[message.Length - msgPreambSize];
            Array.Copy(message, 0, preamb, 0, msgPreambSize);
            Array.Copy(message, msgPreambSize, msgBody, 0, message.Length - msgPreambSize);

            string msgSting = Encoding.Default.GetString(msgBody);
            //--------------

            //Getting data payloads
            var vals = JObject.Parse(msgSting).SelectTokens("rxpk[*].data");

            if(vals != null)
            {
                foreach (var val in vals)
                {
                    Console.WriteLine(val);
                }
            }
        }
    }
}
