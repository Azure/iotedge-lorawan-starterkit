// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Shared
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Net.Sockets;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using LoRaTools;
    using LoRaTools.LoRaPhysical;
    using Newtonsoft.Json;

    public sealed class SimulatedPacketForwarder : IDisposable
    {
        public Rxpk Rxpk { get; set; }

        // Used for the point 0. Always increase
        public long TimeAtBoot { get; internal set; }

        public byte[] MacAddress { get; }

        public SimulatedPacketForwarder(IPEndPoint networkServerIPEndpoint, Rxpk rxpk = null)
        {
            var ip = IPAddress.Any;
            var port = 1681;
            var endPoint = new IPEndPoint(ip, port);

            this.udpClient = new UdpClient(endPoint);
            this.networkServerIPEndpoint = networkServerIPEndpoint;
            TimeAtBoot = DateTimeOffset.Now.UtcTicks;
            MacAddress = Utility.GetMacAddress();

            Rxpk = rxpk ?? new Rxpk()
            {
                Chan = 7,
                Rfch = 1,
                Freq = 903.700000,
                Stat = 1,
                Modu = "LORA",
                Datr = "SF10BW125",
                Codr = "4/5",
                Rssi = -17,
                Lsnr = 12.0f
            };

            TestLogger.Log($"*** Simulated Packed Forwarder created: {ip}:{port} ***");
        }

        string CreateMessagePacket(byte[] data)
        {
            Rxpk.Data = Convert.ToBase64String(data);
            Rxpk.Size = (uint)data.Length;
            // tmst it is time in micro seconds
            var now = DateTimeOffset.UtcNow;
            var tmst = (now.UtcTicks - TimeAtBoot) / (TimeSpan.TicksPerMillisecond / 1000);
            if (tmst >= uint.MaxValue)
            {
                tmst -= uint.MaxValue;
                TimeAtBoot = now.UtcTicks - tmst;
            }

            Rxpk.Tmst = Convert.ToUInt32(tmst);

            return JsonConvert.SerializeObject(Rxpk);
        }

        private readonly UdpClient udpClient;
        private readonly IPEndPoint networkServerIPEndpoint;
        readonly Random random = new Random();
        private CancellationTokenSource cancellationTokenSource;
        private Task pushDataTask;
        private Task pullDataTask;
        private Task listenerTask;

        byte[] GetRandomToken()
        {
            // random is not thread safe
            var token = new byte[2];
            lock (this.random)
            {
                this.random.NextBytes(token);
            }

            return token;
        }

        public void Start()
        {
            this.cancellationTokenSource = new CancellationTokenSource();
            this.pushDataTask = Task.Run(async () => await PushDataAsync(this.cancellationTokenSource.Token));
            this.pullDataTask = Task.Run(async () => await PullDataAsync(this.cancellationTokenSource.Token));
            this.listenerTask = Task.Run(async () => await ListenAsync(this.cancellationTokenSource.Token));
        }

        async Task ListenAsync(CancellationToken cts)
        {
            try
            {
                var currentToken = new byte[2];
                while (!cts.IsCancellationRequested)
                {
                    var receivedResults = await this.udpClient.ReceiveAsync();

                    // If 4, it may mean we received a confirmation
                    if (receivedResults.Buffer.Length >= 4)
                    {
                        var identifier = PhysicalPayload.GetIdentifierFromPayload(receivedResults.Buffer);
                        currentToken[0] = receivedResults.Buffer[1];
                        currentToken[1] = receivedResults.Buffer[2];
                        TestLogger.Log($"[PKTFORWARDER] Received {identifier}");

                        if (identifier == PhysicalIdentifier.PullResp)
                        {
                            if (this.subscribers.Count > 0)
                            {
                                Func<byte[], bool> subscriberToRemove = null;

                                foreach (var subscriber in this.subscribers)
                                {
                                    if (subscriber(receivedResults.Buffer))
                                    {
                                        subscriberToRemove = subscriber;
                                        break;
                                    }
                                }

                                if (subscriberToRemove != null)
                                {
                                    this.subscribers.Remove(subscriberToRemove);
                                }
                            }
                        }
                    }
                }
            }
            catch (ObjectDisposedException)
            {
            }
        }

        readonly HashSet<Func<byte[], bool>> subscribers = new HashSet<Func<byte[], bool>>();

        internal void SubscribeOnce(Func<byte[], bool> value)
        {
            this.subscribers.Add(value);
        }

        async Task PullDataAsync(CancellationToken cts)
        {
            try
            {
                while (!cts.IsCancellationRequested)
                {
                    var sync = new PhysicalPayload(GetRandomToken(), PhysicalIdentifier.PullData, null);
                    var data = sync.GetSyncHeader(MacAddress);
                    await this.udpClient.SendAsync(data, data.Length, this.networkServerIPEndpoint);
                    await Task.Delay(30000, cts);
                }
            }
            catch (TaskCanceledException)
            {
            }
            catch (Exception ex)
            {
                TestLogger.Log($"Error in {nameof(PullDataAsync)}. {ex}");
            }
        }

        async Task PushDataAsync(CancellationToken cts)
        {
            try
            {
                while (!cts.IsCancellationRequested)
                {
                    var sync = new PhysicalPayload(GetRandomToken(), PhysicalIdentifier.PushData, null);
                    var data = sync.GetSyncHeader(MacAddress);
                    await this.udpClient.SendAsync(data, data.Length, this.networkServerIPEndpoint);
                    await Task.Delay(10000, cts);
                }
            }
            catch (TaskCanceledException)
            {
            }
            catch (Exception ex)
            {
                TestLogger.Log($"Error in {nameof(PushDataAsync)}. {ex}");
            }
        }

        internal async Task<PhysicalPayload> SendAsync(byte[] syncHeader, byte[] data)
        {
            var rxpkgateway = CreateMessagePacket(data);
            var msg = "{\"rxpk\":[" + rxpkgateway + "]}";

            var gatewayInfo = Encoding.UTF8.GetBytes(msg);
            var packetData = new byte[syncHeader.Length + gatewayInfo.Length];
            Array.Copy(syncHeader, packetData, syncHeader.Length);
            Array.Copy(gatewayInfo, 0, packetData, syncHeader.Length, gatewayInfo.Length);

            var physicalPayload = new PhysicalPayload(packetData);

            await this.udpClient.SendAsync(packetData, packetData.Length, this.networkServerIPEndpoint);

            return physicalPayload;
        }

        public async Task StopAsync()
        {
            this.cancellationTokenSource?.Cancel();

            // wait until the stop push data job is finished
            if (this.pushDataTask != null)
            {
                await this.pushDataTask;
                this.pushDataTask = null;
            }

            // wait until the stop pull data job is finished
            if (this.pullDataTask != null)
            {
                await this.pullDataTask;
                this.pullDataTask = null;
            }

            // listener will stop once we dispose the udp client
            this.udpClient?.Dispose();
        }

        public void Dispose()
        {
            StopAsync().GetAwaiter().GetResult();
            GC.SuppressFinalize(this);
        }
    }
}
