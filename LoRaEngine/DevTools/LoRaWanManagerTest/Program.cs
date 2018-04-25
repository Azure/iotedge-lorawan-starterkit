using System;

using System.Text;

using PacketManager;

using System.Linq;

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
            LoRaMessage message = new LoRaMessage(StringToByteArray("40AE130426800000016F895D98810714E3268295"));
            Console.WriteLine("decrypted "+Encoding.Default.GetString(message.DecryptPayload(StringToByteArray("0A501524F8EA5FCBF9BDB5AD7D126F75"))));
            Console.WriteLine("mic is valid: "+message.CheckMic(StringToByteArray("99D58493D1205B43EFF938F0F66C339E")));
            Console.Read();
        }
    }
}
