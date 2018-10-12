using System;
using System.Threading.Tasks;
using Microsoft.Azure.Devices;
using Microsoft.Azure.Devices.Shared;
using Microsoft.Azure.EventHubs;

namespace LoRaWan.IntegrationTest
{
    public class IntegrationTestFixture : IDisposable
    {
        public const string MESSAGE_IDENTIFIER_PROPERTY_NAME = "messageIdentifier";
        RegistryManager registryManager;

        public TestConfiguration Configuration { get; }

        public EventHubDataCollector Events { get; private set; }
     
        public IntegrationTestFixture()
        {
            this.Configuration = TestConfiguration.GetConfiguration();

            if (!string.IsNullOrEmpty(Configuration.IoTHubEventHubConnectionString))
            {
                this.Events = new EventHubDataCollector(Configuration.IoTHubEventHubConnectionString, Configuration.IoTHubEventHubConsumerGroup);
                var startTask = this.Events.Start();                
                startTask.ConfigureAwait(false).GetAwaiter().GetResult();
            }
        }

        internal string GetMessageIdentifier(EventData eventData) 
        {
            eventData.Properties.TryGetValue("messageIdentifier", out var actualMessageIdentifier);
            return actualMessageIdentifier?.ToString();
        }      

        public async Task<bool> EnsureHasEvent(Func<EventData, string, string, bool> predicate)
        {
            for (int i = 0; i < this.Configuration.EnsureHasEventMaximumTries; i++)
            {
                foreach (var item in this.Events.GetEvents())
                {
                    var bodyText = System.Text.UTF8Encoding.UTF8.GetString(item.Body);
                    item.SystemProperties.TryGetValue("iothub-connection-device-id", out var deviceId);
                    if (predicate(item, deviceId?.ToString(), bodyText))
                    {
                        return true;
                    }

                }

                await Task.Delay(TimeSpan.FromSeconds(this.Configuration.EnsureHasEventDelayBetweenReadsInSeconds));
            }

            return false;
        }


        public void Dispose()
        {
            this.Events?.Dispose();
            this.Events = null;
            this.registryManager?.Dispose();
            this.registryManager = null;

            GC.SuppressFinalize(this);
        }

        internal async Task<Twin> GetTwinAsync(string deviceId)
        {
            var rm = this.registryManager ?? (this.registryManager = RegistryManager.CreateFromConnectionString(this.Configuration.IoTHubConnectionString));
            return await rm.GetTwinAsync(deviceId);            
        }
    }
}
