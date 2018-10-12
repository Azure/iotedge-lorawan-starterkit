using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace LoRaSimulator
{
    class MessageProcessor : IDisposable
    {
        private DateTime startTimeProcessing;

        public async Task processMessage(byte[] message)
        {
            startTimeProcessing = DateTime.UtcNow;

            // Join accepted messages will arrive
            // don't forget to send the acknoledge of reception

            // or message 2 device from downlink messages
            // don't forget to send the acknoledge of reception

        }

        public void Dispose()
        {
            throw new NotImplementedException();
        }
    }
}
