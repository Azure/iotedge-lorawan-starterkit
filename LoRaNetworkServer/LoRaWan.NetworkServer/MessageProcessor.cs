using System;
using System.Collections.Generic;
using System.Text;

namespace LoRaWan.NetworkServer
{
    public class MessageProcessor
    {
        public void processMessage(byte[] message)
        {
            //Decrypting message

            Console.WriteLine(Encoding.UTF8.GetString(message));
        }
    }
}
