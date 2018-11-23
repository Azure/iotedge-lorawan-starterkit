using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace LoRaWan.IntegrationTest
{
    public class UdpLogListener : IDisposable
    {
        private UdpClient udpClient;

        private readonly ConcurrentQueue<string> events;

        public bool LogToConsole { get; set; } = true;

        public void ResetEvents() => this.events.Clear();

    

        public IReadOnlyCollection<string> GetEvents() => this.events; 

        public UdpLogListener(int port)
        {
            this.events = new ConcurrentQueue<string>();
            this.udpClient = new UdpClient(new IPEndPoint(IPAddress.Any, port));
        }

        void OnMessageReceived(string msg)
        {
            this.events.Enqueue(msg);

            if(this.LogToConsole)
            {                
                Console.WriteLine($"[UDPLog]: {msg}");
            }
        }

        public void Start()
        {
            Task.Run(async () => {
                try
                {
                    while (true)
                    {
                        var msg = await udpClient.ReceiveAsync();
                        if (msg != null && msg.Buffer != null)
                        {
                            var text = Encoding.UTF8.GetString(msg.Buffer);
                            OnMessageReceived(text);
                        }
                    }
                }
                catch (ObjectDisposedException)
                {
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error in UDP listener: {ex.ToString()}");
                }
            });
        }

        public void Dispose()
        {          
            // wait until the runner is finalized
            this.udpClient?.Dispose();

            this.udpClient = null;

            // stop the runner
            GC.SuppressFinalize(this);
        }
    }

}