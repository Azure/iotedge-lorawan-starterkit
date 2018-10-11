using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Azure.EventHubs;

namespace LoRaWan.IntegrationTest
{

    public class EventHubDataCollector : IPartitionReceiveHandler, IDisposable
    {
        private EventHubClient eventHubClient;
        private readonly ConcurrentQueue<EventData> events;

        List<PartitionReceiver> receivers;
        

        public string ConsumerGroupName { get; set; } = "$Default";
        

        public EventHubDataCollector(string connectionString) : this(connectionString, null)
        {            
        }

        public EventHubDataCollector(string connectionString, string consumerGroupName)
        {
            this.eventHubClient = EventHubClient.CreateFromConnectionString(connectionString);
            this.events = new ConcurrentQueue<EventData>();
            this.receivers = new List<PartitionReceiver>();
            if (!string.IsNullOrEmpty(consumerGroupName))
                this.ConsumerGroupName = consumerGroupName;
        }

        public async Task Start()
        {
            if (this.receivers.Count > 0)
                throw new InvalidOperationException("Already started");
            
            var rti = await this.eventHubClient.GetRuntimeInformationAsync();
            foreach (var partitionId in rti.PartitionIds)
            {
                var receiver = this.eventHubClient.CreateReceiver(this.ConsumerGroupName, partitionId, EventPosition.FromEnqueuedTime(DateTime.UtcNow.Subtract(TimeSpan.FromMinutes(1))));
                receiver.SetReceiveHandler(this);
                this.receivers.Add(receiver);
            }
        }

        public void ResetEvents() => this.events.Clear();

        public IReadOnlyCollection<EventData> GetEvents() => this.events;

        Task IPartitionReceiveHandler.ProcessEventsAsync(IEnumerable<EventData> events)
        {
            foreach (var item in events)
                this.events.Enqueue(item);

            return Task.FromResult(0);
        }

        Task IPartitionReceiveHandler.ProcessErrorAsync(Exception error)
        {
            Console.Error.WriteLine(error.ToString());
            return Task.FromResult(0);
        }

        int maxBatchSize = 32;
        int IPartitionReceiveHandler.MaxBatchSize { get => this.maxBatchSize; set => this.maxBatchSize = value; }

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    for (int i = this.receivers.Count - 1; i >= 0; i--)
                    {
                        this.receivers[i].Close();
                        this.receivers.RemoveAt(i);
                    }

                    this.eventHubClient.Close();
                    this.eventHubClient = null;
                }

                // TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
                // TODO: set large fields to null.
                disposedValue = true;
            }
        }

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        #endregion
    }
}
