using System;
using System.Text;
using PacketManager;
using System.Linq;
using Newtonsoft.Json;

namespace AESDemo
{


    class Program
    {

        public static byte[] StringToByteArray(string hex)
        {
            return Enumerable.Range(0, hex.Length)
                             .Where(x => x % 2 == 0)
                             .Select(x => Convert.ToByte(hex.Substring(x, 2), 16))
                             .ToArray();
        }
        static Program()
        {  
        }
        static void Main(string[] args)
        {
            byte[] leadingByte = StringToByteArray("0205DB00AA555A0000000101");
            
            string inputJson = "{\"rxpk\":[{\"tmst\":3121882787,\"chan\":2,\"rfch\":1,\"freq\":868.500000,\"stat\":1,\"modu\":\"LORA\",\"datr\":\"SF7BW125\",\"codr\":\"4/5\",\"lsnr\":7.0,\"rssi\":-16,\"size\":20,\"data\":\"QEa5KACANwAIXiRAODD6gSCHMSk=\"}]}";
            
            byte[] messageraw = leadingByte.Concat(Encoding.Default.GetBytes(inputJson)).ToArray();
            LoRaMessage message = new LoRaMessage(messageraw);
            Console.WriteLine("decrypted " + (message.DecryptPayload("2B7E151628AED2A6ABF7158809CF4F3C")));
            Console.WriteLine("mic is valid: "+message.CheckMic("2B7E151628AED2A6ABF7158809CF4F3C"));
            Console.Read();
        }
    }
}
