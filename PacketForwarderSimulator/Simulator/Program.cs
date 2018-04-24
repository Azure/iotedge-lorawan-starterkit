using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Simulator
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
        static void Main(string[] args)
        {
            while (true)
            {
                byte[] leadingByte = StringToByteArray("0205DB00AA555A0000000101");
                string inputJson = "{\"rxpk\":[{\"tmst\":3121882787,\"chan\":2,\"rfch\":1,\"freq\":868.500000,\"stat\":1,\"modu\":\"LORA\",\"datr\":\"SF7BW125\",\"codr\":\"4/5\",\"lsnr\":7.0,\"rssi\":-16,\"size\":20,\"data\":\"QEa5KACANwAIXiRAODD6gSCHMSk=\"}]}";
                byte[] message = leadingByte.Concat(Encoding.Default.GetBytes(inputJson)).ToArray();
                UdpClient udpConnection = new UdpClient();
                IPEndPoint ipEndPoint = new IPEndPoint(IPAddress.Parse("10.0.28.34"), 1680);
                udpConnection.Send(message, message.Length, ipEndPoint);
                System.Threading.Thread.Sleep(1000);
            }
        }
    }
}
