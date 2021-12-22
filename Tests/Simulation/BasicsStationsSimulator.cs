// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Simulation
{

    using System;
    using System.Text.Json;
    using LoRaWan.Tests.Simulation.Models;
    using Websocket.Client;

    internal class BasicsStationsSimulator
    {
        private WebsocketClient WebsocketClient { get; set; }
        private readonly string stationEUI;

        public BasicsStationsSimulator(string stationEUI)
        {
            this.stationEUI = stationEUI;
        }

        public void ConnectAsync(Uri lnsUrl)
        {
            try
            {
                WebsocketClient = new WebsocketClient(lnsUrl)
                {
                    ReconnectTimeout = TimeSpan.FromSeconds(30)
                };
                WebsocketClient.ReconnectionHappened.Subscribe(info =>
                {
                    Console.WriteLine("Reconnection happened, type: " + info.Type);
                });
                WebsocketClient.MessageReceived.Subscribe(msg =>
                {
                    Console.WriteLine("Message received: " + msg);
                });
                WebsocketClient.Start();
                //Task.Run(() => client.Send("{ message }"));
                var routerMessage = new RouterMessage(this.stationEUI);
                var jsonString = JsonSerializer.Serialize(routerMessage);
                WebsocketClient.Send(jsonString);
            }
            catch (Exception ex)
            {
                Console.WriteLine("ERROR: " + ex.ToString());
            }
        }
    }
}
