// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer.PacketForwarder
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Net;
    using System.Net.Http;
    using System.Net.Sockets;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using LoRaTools;
    using LoRaTools.ADR;
    using LoRaTools.LoRaMessage;
    using LoRaTools.LoRaPhysical;
    using LoRaTools.Utils;
    using LoRaWan.NetworkServer.ADR;
    using LoRaWan.NetworkServer.Common;
    using LoRaWan.Shared;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Client.Transport.Mqtt;
    using Microsoft.Azure.Devices.Shared;
    using Microsoft.Extensions.Caching.Memory;
    using Microsoft.Extensions.Logging;
    using Newtonsoft.Json;

    /// <summary>
    /// Defines udp Server communicating with packet forwarder.
    /// </summary>
    public class UdpServer : PhysicalClient, IPacketForwarder
    {
        const int PORT = 1680;

        readonly SemaphoreSlim randomLock = new SemaphoreSlim(1);
        readonly Random random = new Random();
        private volatile int pullAckRemoteLoRaAggregatorPort = 0;
        private volatile string pullAckRemoteLoRaAddress = null;

        UdpClient udpClient;

        private async Task<byte[]> GetTokenAsync()
        {
            try
            {
                await this.randomLock.WaitAsync();
                byte[] token = new byte[2];
                this.random.NextBytes(token);
                return token;
            }
            finally
            {
                this.randomLock.Release();
            }
        }

        // Creates a new instance of UdpServer
        public UdpServer(
            NetworkServerConfiguration configuration,
            MessageDispatcher messageDispatcher,
            LoRaDeviceAPIServiceBase loRaDeviceAPIService,
            ILoRaDeviceRegistry loRaDeviceRegistry)
            : base(
                configuration,
                messageDispatcher,
                loRaDeviceAPIService,
                loRaDeviceRegistry)
        {
        }

        async Task UdpSendMessageAsync(byte[] messageToSend, string remoteLoRaAggregatorIp, int remoteLoRaAggregatorPort)
        {
            if (messageToSend != null && messageToSend.Length != 0)
            {
                await this.udpClient.SendAsync(messageToSend, messageToSend.Length, remoteLoRaAggregatorIp, remoteLoRaAggregatorPort);
            }
        }

        public async override Task RunServerProcess(CancellationToken cancellationToken)
        {
            IPEndPoint endPoint = new IPEndPoint(IPAddress.Any, PORT);
            this.udpClient = new UdpClient(endPoint);

            Logger.LogAlways($"LoRaWAN server started on port {PORT}");

            while (true)
            {
                UdpReceiveResult receivedResults = await this.udpClient.ReceiveAsync();
                var startTimeProcessing = DateTime.UtcNow;

                switch (PhysicalPayload.GetIdentifierFromPayload(receivedResults.Buffer))
                {
                    // In this case we have a keep-alive PULL_DATA packet we don't need to start the engine and can return immediately a response to the challenge
                    case PhysicalIdentifier.PULL_DATA:
                        if (this.pullAckRemoteLoRaAggregatorPort == 0)
                        {
                            this.pullAckRemoteLoRaAggregatorPort = receivedResults.RemoteEndPoint.Port;
                            this.pullAckRemoteLoRaAddress = receivedResults.RemoteEndPoint.Address.ToString();
                        }

                        this.SendAcknowledgementMessage(receivedResults, (int)PhysicalIdentifier.PULL_ACK, receivedResults.RemoteEndPoint);
                        break;

                    // This is a PUSH_DATA (upstream message).
                    case PhysicalIdentifier.PUSH_DATA:
                        this.SendAcknowledgementMessage(receivedResults, (int)PhysicalIdentifier.PUSH_ACK, receivedResults.RemoteEndPoint);
                        this.DispatchMessages(receivedResults.Buffer, startTimeProcessing);

                        break;

                    // This is a ack to a transmission we did previously
                    case PhysicalIdentifier.TX_ACK:
                        if (receivedResults.Buffer.Length == 12)
                        {
                            Logger.Log(
                                "UDP",
                                $"packet with id {ConversionHelper.ByteArrayToString(receivedResults.Buffer.RangeSubset(1, 2))} successfully transmitted by the aggregator",
                                LogLevel.Debug);
                        }
                        else
                        {
                            var logMsg = string.Format(
                                    "packet with id {0} had a problem to be transmitted over the air :{1}",
                                    receivedResults.Buffer.Length > 2 ? ConversionHelper.ByteArrayToString(receivedResults.Buffer.RangeSubset(1, 2)) : string.Empty,
                                    receivedResults.Buffer.Length > 12 ? Encoding.UTF8.GetString(receivedResults.Buffer.RangeSubset(12, receivedResults.Buffer.Length - 12)) : string.Empty);
                            Logger.Log("UDP", logMsg, LogLevel.Error);
                        }

                        break;

                    default:
                        Logger.Log("UDP", "unknown packet type or length being received", LogLevel.Error);
                        break;
                }
            }
        }

        private void DispatchMessages(byte[] buffer, DateTime startTimeProcessing)
        {
            try
            {
                List<Rxpk> messageRxpks = Rxpk.CreateRxpk(buffer);
                if (messageRxpks != null)
                {
                    foreach (var rxpk in messageRxpks)
                    {
                        this.messageDispatcher.DispatchRequest(new LoRaPktFwdRequest(rxpk, this, startTimeProcessing));
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Log("UDP", $"failed to dispatch messages: {ex.Message}", LogLevel.Error);
            }
        }

        private void SendAcknowledgementMessage(UdpReceiveResult receivedResults, byte messageType, IPEndPoint remoteEndpoint)
        {
            byte[] response = new byte[4]
            {
                receivedResults.Buffer[0],
                receivedResults.Buffer[1],
                receivedResults.Buffer[2],
                messageType
            };
            _ = this.UdpSendMessageAsync(response, remoteEndpoint.Address.ToString(), remoteEndpoint.Port);
        }

        async Task IPacketForwarder.SendDownstreamAsync(DownlinkPktFwdMessage downstreamMessage)
        {
            try
            {
                if (downstreamMessage?.Txpk != null)
                {
                    var jsonMsg = JsonConvert.SerializeObject(downstreamMessage);
                    var messageByte = Encoding.UTF8.GetBytes(jsonMsg);
                    var token = await this.GetTokenAsync();
                    PhysicalPayload pyld = new PhysicalPayload(token, PhysicalIdentifier.PULL_RESP, messageByte);
                    if (this.pullAckRemoteLoRaAggregatorPort != 0 && !string.IsNullOrEmpty(this.pullAckRemoteLoRaAddress))
                    {
                        Logger.Log("UDP", $"sending message with ID {ConversionHelper.ByteArrayToString(token)}, to {this.pullAckRemoteLoRaAddress}:{this.pullAckRemoteLoRaAggregatorPort}", LogLevel.Debug);
                        await this.UdpSendMessageAsync(pyld.GetMessage(), this.pullAckRemoteLoRaAddress, this.pullAckRemoteLoRaAggregatorPort);
                        Logger.Log("UDP", $"message sent with ID {ConversionHelper.ByteArrayToString(token)}", LogLevel.Debug);
                    }
                    else
                    {
                        Logger.Log(
                            "UDP",
                            "waiting for first pull_ack message from the packet forwarder. The received message was discarded as the network server is still starting.",
                            LogLevel.Debug);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Log("UDP", $"error processing the message {ex.Message}, {ex.StackTrace}", LogLevel.Error);
            }
        }

        public void SetClassCMessageSender(DefaultClassCDevicesMessageSender classCMessageSender) => this.classCMessageSender = classCMessageSender;

        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!this.disposedValue)
            {
                if (disposing)
                {
                    this.udpClient?.Dispose();
                    this.udpClient = null;
                }

                this.disposedValue = true;
            }
        }

        public override void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}