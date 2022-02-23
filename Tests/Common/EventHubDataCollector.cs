// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Common
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Azure.Messaging.EventHubs;
    using Azure.Messaging.EventHubs.Consumer;

    public sealed class EventHubDataCollector : IAsyncDisposable
    {
        private readonly ConcurrentQueue<EventData> events;
        private readonly string connectionString;
        private readonly EventHubConsumerClient consumer;
        private readonly string consumerGroupName;
        private readonly CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
        private bool started;
        private Task receiveAsync;

        public EventHubDataCollector(string connectionString)
            : this(connectionString, null)
        { }

        public EventHubDataCollector(string connectionString, string consumerGroupName)
        {
            this.connectionString = connectionString;
            this.consumerGroupName = !string.IsNullOrEmpty(consumerGroupName) ? consumerGroupName : EventHubConsumerClient.DefaultConsumerGroupName;
            this.consumer = new EventHubConsumerClient(this.consumerGroupName, connectionString);
            this.events = new ConcurrentQueue<EventData>();
        }

        public async Task StartAsync()
        {
            if (this.started)
                throw new InvalidOperationException("Already started");

            this.started = true;
            TestLogger.Log($"Connecting to IoT Hub Event Hub @{this.connectionString} using consumer group {this.consumerGroupName}");

            var eventPosition = EventPosition.FromEnqueuedTime(DateTimeOffset.Now.Subtract(TimeSpan.FromMinutes(1)));
            this.receiveAsync =
                Task.WhenAll(from partitionId in await this.consumer.GetPartitionIdsAsync(this.cancellationTokenSource.Token)
                             select ProcessEventsAsync(partitionId, eventPosition, this.cancellationTokenSource.Token));
        }

        public async Task StopAsync()
        {
            if (!this.started)
                throw new InvalidOperationException("Processing has not yet started.");

            this.cancellationTokenSource.Cancel();

            try
            {
                await this.receiveAsync;
            }
            catch (OperationCanceledException)
            {
                // expected cancellation of receive operation.
            }
        }

        public void ResetEvents()
        {
            TestLogger.Log($"*** Clearing iot hub logs ({this.events.Count}) ***");
            this.events.Clear();
        }

        public IReadOnlyCollection<EventData> Events => this.events;

        public async Task ProcessEventsAsync(string partitionId, EventPosition eventPosition, CancellationToken cancellationToken)
        {
            await foreach (var item in this.consumer.ReadEventsFromPartitionAsync(partitionId, eventPosition, cancellationToken))
            {
                try
                {
                    var eventData = item.Data;
                    this.events.Enqueue(eventData);
                    var bodyText = Encoding.UTF8.GetString(eventData.EventBody);
                    TestLogger.Log($"[IOTHUB] {bodyText}");
                }
                catch (Exception ex)
                {
                    TestLogger.Log($"Error processing iot hub event. {ex}");
                }
            }
        }

        public async ValueTask DisposeAsync()
        {
            this.cancellationTokenSource.Cancel();
            this.cancellationTokenSource.Dispose();

            await this.consumer.CloseAsync();
            await this.consumer.DisposeAsync();

            TestLogger.Log($"{nameof(EventHubDataCollector)} disposed");
        }
    }
}
