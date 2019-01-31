// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Test.Shared
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Net;
    using System.Net.Sockets;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using LoRaWan.Test.Shared;

    public class UdpLogListener : IDisposable
    {
        private readonly ConcurrentQueue<string> events;

        private UdpClient udpClient;

        public bool LogToConsole { get; set; } = true;

        public void ResetEvents()
        {
            TestLogger.Log($"*** Clearing udp logs ({this.events.Count}) ***");
            this.events.Clear();
        }

        public IReadOnlyCollection<string> GetEvents() => this.events;

        public UdpLogListener(int port)
        {
            IPAddress ip = IPAddress.Any;
            this.events = new ConcurrentQueue<string>();
            this.udpClient = new UdpClient(new IPEndPoint(ip, port));
            TestLogger.Log($"*** UDP Log Listener created: {ip}:{port} ***");
        }

        void OnMessageReceived(string msg)
        {
            this.events.Enqueue(msg);

            if (this.LogToConsole)
            {
                TestLogger.Log($"[UDPLOG] {msg}");
            }
        }

        public void Start()
        {
            Task.Run(async () =>
            {
                try
                {
                    while (true)
                    {
                        var msg = await this.udpClient.ReceiveAsync();
                        if (msg != null && msg.Buffer != null)
                        {
                            var text = Encoding.UTF8.GetString(msg.Buffer);
                            this.OnMessageReceived(text);
                        }
                    }
                }
                catch (ObjectDisposedException)
                {
                }
                catch (Exception ex)
                {
                    TestLogger.Log($"Error in UDP listener: {ex.ToString()}");
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