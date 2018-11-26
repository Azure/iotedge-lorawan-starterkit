using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Azure.Devices;
using Microsoft.Azure.Devices.Shared;
using Microsoft.Azure.EventHubs;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;

namespace LoRaWan.IntegrationTest
{
    public partial class IntegrationTestFixture : IDisposable, IAsyncLifetime
    {
        internal string GetMessageIdentifier(EventData eventData) 
        {
            eventData.Properties.TryGetValue("messageIdentifier", out var actualMessageIdentifier);
            return actualMessageIdentifier?.ToString();
        }      
       

        bool IsDeviceMessage(string expectedDeviceID, string expectedDataValue, EventData eventData, string eventDeviceID, string eventDataMessageBody)
        {
            if (eventDeviceID != null && eventDeviceID == expectedDeviceID)
            {
                try
                {
                    var messageJson = JObject.Parse(eventDataMessageBody);
                    if (messageJson != null)
                    {
                        var data = messageJson["data"];                            
                        return (data != null && data.ToString(Formatting.None) == expectedDataValue);
                    }
                }
                catch (Exception ex)
                {
                    TestLogger.Log($"Error searching device payload: {eventDataMessageBody}. {ex.ToString()}");
                }
            } 
           
            return false;
        }

        // Asserts leaf device message payload exists. It searches inside the payload "data" property. Has built-in retries
        public async Task AssertIoTHubDeviceMessageExistsAsync(string deviceID, string expectedDataValue, SearchLogOptions options = null)
        {           
            var assertLevel = this.Configuration.NetworkServerModuleLogAssertLevel;
            if (options != null && options.TreatAsError.HasValue)
                assertLevel = options.TreatAsError.Value ? IoTHubAssertLevel.Error : IoTHubAssertLevel.Warning;

            if (assertLevel == IoTHubAssertLevel.Ignore)
                return;

            var searchResult = await this.SearchIoTHubMessageAsync(
                (eventData, eventDeviceID, eventDataMessageBody) => IsDeviceMessage(deviceID, expectedDataValue, eventData, eventDeviceID, eventDataMessageBody),
                new SearchLogOptions
                {
                    Description = options?.Description ?? expectedDataValue,
                    TreatAsError = options?.TreatAsError,
                });

            if (assertLevel == IoTHubAssertLevel.Error)
            {
                var logs = string.Join("\n\t", searchResult.Logs.TakeLast(5));
                Assert.True(searchResult.Found, $"Searching for {expectedDataValue} failed for device {deviceID}. Current log content: [{logs}]");
            }
            else if (assertLevel == IoTHubAssertLevel.Warning)
            {
                if (searchResult.Found)
                {
                    TestLogger.Log($"'{expectedDataValue}' for device {deviceID} found in logs? {searchResult.Found}");
                }
                else
                {
                    var logs = string.Join("\n\t", searchResult.Logs.TakeLast(5));
                    TestLogger.Log($"'{expectedDataValue}' for device {deviceID} found in logs? {searchResult.Found}. Logs: [{logs}]");
                }
            }                    
        }


         // Asserts network server module log contains
        public async Task AssertNetworkServerModuleLogStartsWithAsync(string logMessageStart)
        {
            if (this.Configuration.NetworkServerModuleLogAssertLevel == IoTHubAssertLevel.Ignore)
                return;

            await this.AssertNetworkServerModuleLogExistsAsync((input) => logMessageStart.StartsWith(logMessageStart), new SearchLogOptions(logMessageStart));
        }

        public async Task<SearchLogResult> SearchNetworkServerModuleAsync(Func<string, bool> predicate, SearchLogOptions options = null)
        {
            SearchLogResult searchResult;
            if (this.udpLogListener != null)
                 searchResult = await SearchUdpLogs(predicate, options);
            else
                searchResult = await SearchIoTHubLogs(predicate, options);

            return searchResult;
        }

        // Asserts Network Server Module log exists. It has built-in retries and delays
        public async Task AssertNetworkServerModuleLogExistsAsync(Func<string, bool> predicate, SearchLogOptions options)
        {
            if (this.Configuration.NetworkServerModuleLogAssertLevel == IoTHubAssertLevel.Ignore)
                return;
            
            SearchLogResult searchResult;
            if (this.udpLogListener != null)
                 searchResult = await SearchUdpLogs(predicate, options);
            else
                searchResult = await SearchIoTHubLogs(predicate, options);

            if (this.Configuration.NetworkServerModuleLogAssertLevel == IoTHubAssertLevel.Error)
            {
                var logs = string.Join("\n\t", searchResult.Logs.TakeLast(5));
                Assert.True(searchResult.Found, $"Searching for {options?.Description ?? "??"} failed. Current log content: [{logs}]");
            }
            else if (this.Configuration.NetworkServerModuleLogAssertLevel == IoTHubAssertLevel.Warning)
            {
                if (searchResult.Found)
                {
                    TestLogger.Log($"'{options?.Description ?? "??"}' found in logs? {searchResult.Found}");
                }
                else
                {
                    var logs = string.Join("\n\t", searchResult.Logs.TakeLast(5));
                    TestLogger.Log($"'{options?.Description ?? "??"}' found in logs? {searchResult.Found}. Logs: [{logs}]");
                }
            }            
        }

        async Task<SearchLogResult> SearchUdpLogs(Func<string, bool> predicate, SearchLogOptions options = null)
        {
            var maxAttempts = options?.MaxAttempts ?? this.Configuration.EnsureHasEventMaximumTries;
            var processedEvents = new HashSet<string>();
            for (int i = 0; i < maxAttempts; i++)
            {
                if (i > 0)
                {
                    var timeToWait = i * this.Configuration.EnsureHasEventDelayBetweenReadsInSeconds;
                    if (!string.IsNullOrEmpty(options?.Description))
                    {
                        TestLogger.Log($"UDP log message '{options.Description}' not found, attempt {i}/{maxAttempts}, waiting {timeToWait} secs");
                    }
                    else
                    {
                        TestLogger.Log($"UDP log message not found, attempt {i}/{maxAttempts}, waiting {timeToWait} secs");
                    }
                    await Task.Delay(TimeSpan.FromSeconds(timeToWait));
                }

                foreach (var item in this.udpLogListener.GetEvents())
                {
                    processedEvents.Add(item);
                    if (predicate(item))
                    {
                        return new SearchLogResult(true, processedEvents);
                    }
                }
            }

            return new SearchLogResult(false, processedEvents);
        }

        // Searches IoT Hub for messages
        async Task<SearchLogResult> SearchIoTHubLogs(Func<string, bool> predicate, SearchLogOptions options = null)
        {
            var maxAttempts = options?.MaxAttempts ?? this.Configuration.EnsureHasEventMaximumTries;
            var processedEvents = new HashSet<string>();
            for (int i = 0; i < maxAttempts; i++)
            {
                if (i > 0)
                {
                    var timeToWait = i * this.Configuration.EnsureHasEventDelayBetweenReadsInSeconds;
                    if (!string.IsNullOrEmpty(options?.Description))
                    {
                        TestLogger.Log($"IoT Hub message '{options.Description}' not found, attempt {i}/{maxAttempts}, waiting {timeToWait} secs");
                    }
                    else
                    {
                        TestLogger.Log($"IoT Hub message not found, attempt {i}/{maxAttempts}, waiting {timeToWait} secs");
                    }
                    await Task.Delay(TimeSpan.FromSeconds(timeToWait));
                }

                foreach (var item in this.IoTHubMessages.GetEvents())
                {
                    var bodyText = item.Body.Count > 0 ? Encoding.UTF8.GetString(item.Body) : string.Empty;
                    processedEvents.Add(bodyText);
                    if (predicate(bodyText))
                    {
                        return new SearchLogResult(true, processedEvents);
                    }
                }
            }

            return new SearchLogResult(false, processedEvents);
        }

        // Searches IoT Hub for messages
        internal async Task<SearchLogResult> SearchIoTHubMessageAsync(Func<EventData, string, string, bool> predicate, SearchLogOptions options = null)
        {
            var maxAttempts = options?.MaxAttempts ?? this.Configuration.EnsureHasEventMaximumTries;
            var processedEvents = new HashSet<string>();
            for (int i = 0; i < maxAttempts; i++)
            {
                if (i > 0)
                {
                    var timeToWait = i * this.Configuration.EnsureHasEventDelayBetweenReadsInSeconds;
                    if (!string.IsNullOrEmpty(options?.Description))
                    {
                        TestLogger.Log($"IoT Hub message '{options.Description}' not found, attempt {i}/{maxAttempts}, waiting {timeToWait} secs");
                    }
                    else
                    {
                        TestLogger.Log($"IoT Hub message not found, attempt {i}/{maxAttempts}, waiting {timeToWait} secs");
                    }
                    await Task.Delay(TimeSpan.FromSeconds(timeToWait));
                }

                foreach (var item in this.IoTHubMessages.GetEvents())
                {
                    try
                    {
                        var bodyText = item.Body.Count > 0 ? Encoding.UTF8.GetString(item.Body) : string.Empty;
                        processedEvents.Add(bodyText);
                        item.SystemProperties.TryGetValue("iothub-connection-device-id", out var deviceId);
                        if (predicate(item, deviceId?.ToString() ?? string.Empty, bodyText))
                        {
                            return new SearchLogResult(true, processedEvents);
                        }
                    }
                    catch (Exception ex)
                    {
                        TestLogger.Log("Error searching in IoT Hub message log: " + ex.ToString());
                    }
                }
            }

            return new SearchLogResult(false, processedEvents);
        }
    }
}