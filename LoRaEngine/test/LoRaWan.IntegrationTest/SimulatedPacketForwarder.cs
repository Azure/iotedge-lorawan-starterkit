using LoRaTools;
using LoRaTools.LoRaMessage;
using LoRaTools.LoRaPhysical;
using Newtonsoft.Json;
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
    public sealed class SimulatedPacketForwarder : IDisposable
    {
        public Rxpk rxpk { get; set; }
        // Used for the point 0. Always increase
        public long TimeAtBoot { get; internal set; }

        public byte[] MacAddress { get; }
        public SimulatedPacketForwarder(IPEndPoint networkServerIPEndpoint, Rxpk rxpk = null)
        {
            IPEndPoint endPoint = new IPEndPoint(IPAddress.Any, 1681);
            this.udpClient = new UdpClient(endPoint);
            this.networkServerIPEndpoint = networkServerIPEndpoint;
            TimeAtBoot = DateTimeOffset.Now.UtcTicks;
            this.MacAddress = Utility.GetMacAddress();

            this.rxpk = rxpk ?? new Rxpk()
            {
                chan = 7,
                rfch = 1,
                freq = 903.700000,
                stat = 1,
                modu = "LORA",
                datr = "SF10BW125",
                codr = "4/5",
                rssi = -17,
                lsnr = 12.0f
            };
        }

        string CreateMessagePacket(byte[] data)
        {
            rxpk.data = Convert.ToBase64String(data);
            rxpk.size = (uint)data.Length;
            // tmst it is time in micro seconds
            var now = DateTimeOffset.UtcNow;
            var tmst = (now.UtcTicks - TimeAtBoot) / (TimeSpan.TicksPerMillisecond / 1000);
            if (tmst >= UInt32.MaxValue)
            {
                tmst = tmst - UInt32.MaxValue;
                TimeAtBoot = now.UtcTicks - tmst;
            }
            rxpk.tmst = Convert.ToUInt32(tmst);

            return JsonConvert.SerializeObject(rxpk);
        }

        Random random = new Random();
        private CancellationTokenSource cancellationTokenSource;
        private Task pushDataTask;
        private Task listenerTask;
        private readonly UdpClient udpClient;
        private readonly IPEndPoint networkServerIPEndpoint;

        byte[] GetRandomToken()
        {
            // random is not thread safe
            byte[] token = new byte[2];
            lock (random)
            {
                random.NextBytes(token);
            }

            return token;
        }

        public void Start()
        {
            this.cancellationTokenSource = new CancellationTokenSource();
            this.pushDataTask = Task.Run(async() => await this.PushDataAsync(this.cancellationTokenSource.Token));
            this.listenerTask = Task.Run(async() => await this.ListenAsync(this.cancellationTokenSource.Token));
        }

        async Task ListenAsync(CancellationToken cts)
        {
            try
            {
                var currentToken = new byte[2];
                while (!cts.IsCancellationRequested)
                {
                    var receivedResults = await udpClient.ReceiveAsync();

                    // If 4, it may mean we received a confirmation
                    if (receivedResults.Buffer.Length >= 4)
                    {
                        var identifier = PhysicalPayload.GetIdentifierFromPayload(receivedResults.Buffer);
                        currentToken[0] = receivedResults.Buffer[1];
                        currentToken[1] = receivedResults.Buffer[2];
                        var tokenKey = CreateTokenKey(currentToken, identifier);
                        TestLogger.Log($"[PKTFORWARDER] Received {identifier.ToString()} with token {tokenKey}");
                        
                        if (this.subscribers.TryGetValue(tokenKey, out var subscriber))
                        {
                            subscriber(receivedResults.Buffer);  
                            this.subscribers.Remove(tokenKey, out _);             
                        }   
                    }
                }
            }
            catch (ObjectDisposedException)
            {

            }        
        }

        ConcurrentDictionary<UInt32, Action<byte[]>> subscribers = new ConcurrentDictionary<UInt32, Action<byte[]>>();
        internal void SubscribeOnce(byte[] token, PhysicalIdentifier type, Action<byte[]> value)
        {
            subscribers.TryAdd(CreateTokenKey(token, type), value);
        }

        UInt32 CreateTokenKey(byte[] token, PhysicalIdentifier type)
        {
            if (token == null || token.Length != 2)
            {
                throw new Exception("Token must be an array with 2 elements");
            }

            var key = new byte[] { token[0], token[1], 0, (byte)type };
            return BitConverter.ToUInt32(key, 0);
        }


        async Task PushDataAsync(CancellationToken cts)
        {
            try
            {
                while (!cts.IsCancellationRequested)
                {
                    var sync = new PhysicalPayload(GetRandomToken(), PhysicalIdentifier.PUSH_DATA, null);
                    var data = sync.GetSyncHeader(this.MacAddress);
                    await udpClient.SendAsync(data, data.Length, this.networkServerIPEndpoint);
                    await Task.Delay(10000, cts);
                }
            }
            catch (TaskCanceledException)
            {
                
            }
            catch (Exception ex)
            {
                TestLogger.Log($"Error in {nameof(PushDataAsync)}. {ex.ToString()}");
            }
        }

        internal async Task SendAsync(byte[] syncHeader, byte[] data)
        {
            var rxpkgateway = this.CreateMessagePacket(data);
            var msg = "{\"rxpk\":[" + rxpkgateway + "]}";

            var gatewayInfo = Encoding.UTF8.GetBytes(msg);
            byte[] packetData = new byte[syncHeader.Length + gatewayInfo.Length];
            Array.Copy(syncHeader, packetData, syncHeader.Length);
            Array.Copy(gatewayInfo, 0, packetData, syncHeader.Length, gatewayInfo.Length);
            
            await udpClient.SendAsync(packetData, packetData.Length, networkServerIPEndpoint);
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
