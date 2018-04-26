using Newtonsoft.Json.Linq;
using PacketManager;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace LoRaWan.NetworkServer
{
    public class MessageProcessor
    {
        string testKey = "2B7E151628AED2A6ABF7158809CF4F3C";
        public async Task processMessage(byte[] message)
        {
            LoRaMessage loraMessage = new LoRaMessage(message);

            if(loraMessage.CheckMic(testKey))
            {
                string decryptedMessage = null;
                try
                {
                    decryptedMessage = Encoding.Default.GetString(loraMessage.DecryptPayload(testKey));
                }
                catch(Exception ex)
                {
                    Console.WriteLine($"Failed to decrypt message: {ex.Message}");
                }

                if(string.IsNullOrEmpty(decryptedMessage))
                {
                    return;
                }

                Console.WriteLine($"Sending message '{decryptedMessage}' to hub...");

                try
                {
                    using (IoTHubSender sender = new IoTHubSender("BE7A00000000888F"))
                    {
                        await sender.sendMessage(decryptedMessage);
                    }
                }
                catch(Exception ex)
                {
                    Console.WriteLine($"Failed to send message: {ex.Message}");
                }
            }
            else
            {
                Console.WriteLine("Check MIC failed! Message will be ignored...");
            }
        }
    }
}
