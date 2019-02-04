// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Test.Shared
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.Azure.EventHubs;

    public class EventHubDataCollector : IPartitionReceiveHandler, IDisposable
    {
        private readonly ConcurrentQueue<EventData> events;
        private readonly string connectionString;
        private EventHubClient eventHubClient;
        List<PartitionReceiver> receivers;

        HashSet<Action<IEnumerable<EventData>>> subscribers;

        public bool LogToConsole { get; set; } = true;

        public string ConsumerGroupName { get; set; } = "$Default";

        public EventHubDataCollector(string connectionString)
            : this(connectionString, null)
        {
        }

        public EventHubDataCollector(string connectionString, string consumerGroupName)
        {
            this.connectionString = connectionString;
            this.eventHubClient = EventHubClient.CreateFromConnectionString(connectionString);
            this.events = new ConcurrentQueue<EventData>();
            this.receivers = new List<PartitionReceiver>();
            if (!string.IsNullOrEmpty(consumerGroupName))
                this.ConsumerGroupName = consumerGroupName;

            this.subscribers = new HashSet<Action<IEnumerable<EventData>>>();
        }

        public async Task StartAsync()
        {
            if (this.receivers.Count > 0)
                throw new InvalidOperationException("Already started");

            if (this.LogToConsole)
            {
                TestLogger.Log($"Connecting to IoT Hub Event Hub @{this.connectionString} using consumer group {this.ConsumerGroupName}");
            }

            var rti = await this.eventHubClient.GetRuntimeInformationAsync();
            foreach (var partitionId in rti.PartitionIds)
            {
                var receiver = this.eventHubClient.CreateReceiver(this.ConsumerGroupName, partitionId, EventPosition.FromEnqueuedTime(DateTime.UtcNow.Subtract(TimeSpan.FromMinutes(1))));
                receiver.SetReceiveHandler(this);
                this.receivers.Add(receiver);
            }
        }

        public void ResetEvents()
        {
            TestLogger.Log($"*** Clearing iot hub logs ({this.events.Count}) ***");
            this.events.Clear();
        }

        public void Subscribe(Action<IEnumerable<EventData>> subscriber)
        {
            this.subscribers.Add(subscriber);
        }

        public void Unsubscribe(Action<IEnumerable<EventData>> subscriber)
        {
            this.subscribers.Remove(subscriber);
        }

        public IReadOnlyCollection<EventData> GetEvents() => this.events;

        Task IPartitionReceiveHandler.ProcessEventsAsync(IEnumerable<EventData> events)
        {
            try
            {
                if (this.subscribers.Count > 0)
                {
                    foreach (var subscriber in this.subscribers)
                    {
                        subscriber(events);
                    }
                }

                foreach (var item in events)
                {
                    this.events.Enqueue(item);

                    if (this.LogToConsole)
                    {
                        var bodyText = Encoding.UTF8.GetString(item.Body);
                        TestLogger.Log($"[IOTHUB] {bodyText}");
                    }
                }
            }
            catch (Exception ex)
            {
                TestLogger.Log($"Error processing iot hub event. {ex.ToString()}");
            }

            return Task.FromResult(0);
        }

        Task IPartitionReceiveHandler.ProcessErrorAsync(Exception error)
        {
            Console.Error.WriteLine(error.ToString());
            return Task.FromResult(0);
        }

        int maxBatchSize = 32;

        int IPartitionReceiveHandler.MaxBatchSize { get => this.maxBatchSize; set => this.maxBatchSize = value; }

        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            TestLogger.Log($"{nameof(EventHubDataCollector)} disposed");

            if (!this.disposedValue)
            {
                if (disposing)
                {
                    for (int i = this.receivers.Count - 1; i >= 0; i--)
                    {
                        try
                        {
                            this.receivers[i].SetReceiveHandler(null);
                            this.receivers[i].Close();
                        }
                        catch (Exception ex)
                        {
                            TestLogger.Log($"Error closing event hub receiver: {ex.ToString()}");
                        }

                        this.receivers.RemoveAt(i);
                    }

                    this.eventHubClient.Close();
                    this.eventHubClient = null;
                }

                // TODO: free unmanaged resources(unmanaged objects) and override a finalizer below.
                // TODO: set large fields to null.
                this.disposedValue = true;
            }
        }

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}
