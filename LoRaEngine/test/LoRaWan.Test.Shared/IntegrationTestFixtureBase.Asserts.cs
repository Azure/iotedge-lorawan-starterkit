// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.Azure.EventHubs;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;
    using Xunit;

    public abstract partial class IntegrationTestFixtureBase : IDisposable, IAsyncLifetime
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
                        return data != null && data.ToString(Formatting.None) == expectedDataValue;
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
            var assertionLevel = this.Configuration.IoTHubAssertLevel;
            if (options != null && options.TreatAsError.HasValue)
                assertionLevel = options.TreatAsError.Value ? LogValidationAssertLevel.Error : LogValidationAssertLevel.Warning;

            if (assertionLevel == LogValidationAssertLevel.Ignore)
                return;

            var searchResult = await this.SearchIoTHubMessageAsync(
                (eventData, eventDeviceID, eventDataMessageBody) => this.IsDeviceMessage(deviceID, expectedDataValue, eventData, eventDeviceID, eventDataMessageBody),
                new SearchLogOptions
                {
                    Description = options?.Description ?? expectedDataValue,
                    TreatAsError = options?.TreatAsError,
                });

            if (assertionLevel == LogValidationAssertLevel.Error)
            {
                var logs = string.Join("\n\t", searchResult.Logs.TakeLast(5));
                Assert.True(searchResult.Found, $"Searching for {expectedDataValue} failed for device {deviceID}. Current log content: [{logs}]");
            }
            else if (assertionLevel == LogValidationAssertLevel.Warning)
            {
                if (!searchResult.Found)
                {
                    var logs = string.Join("\n\t", searchResult.Logs.TakeLast(5));
                    TestLogger.Log($"[WARN] '{expectedDataValue}' for device {deviceID} found in logs? {searchResult.Found}. Logs: [{logs}]");
                }
            }
        }

        // Asserts network server module log contains
        public async Task<SearchLogResult> AssertNetworkServerModuleLogStartsWithAsync(string logMessageStart)
        {
            if (this.Configuration.NetworkServerModuleLogAssertLevel == LogValidationAssertLevel.Ignore)
                return null;

            return await this.AssertNetworkServerModuleLogExistsAsync((input) => input.StartsWith(logMessageStart), new SearchLogOptions(logMessageStart));
        }

        public async Task AssertNetworkServerModuleLogStartsWithAsync(string logMessageStart1, string logMessageStart2)
        {
            if (this.Configuration.NetworkServerModuleLogAssertLevel == LogValidationAssertLevel.Ignore)
                return;

            await this.AssertNetworkServerModuleLogExistsAsync(
                (input) => input.StartsWith(logMessageStart1) || input.StartsWith(logMessageStart2),
                new SearchLogOptions(string.Concat(logMessageStart1, " or ", logMessageStart2)));
        }

        public async Task<SearchLogResult> SearchNetworkServerModuleAsync(Func<string, bool> predicate, SearchLogOptions options = null)
        {
            SearchLogResult searchResult;
            if (this.udpLogListener != null)
                searchResult = await this.SearchUdpLogs(predicate, options);
            else
                searchResult = await this.SearchIoTHubLogs(predicate, options);

            return searchResult;
        }

        // Asserts Network Server Module log exists. It has built-in retries and delays
        public async Task<SearchLogResult> AssertNetworkServerModuleLogExistsAsync(Func<string, bool> predicate, SearchLogOptions options)
        {
            if (this.Configuration.NetworkServerModuleLogAssertLevel == LogValidationAssertLevel.Ignore)
                return null;

            SearchLogResult searchResult;
            if (this.udpLogListener != null)
                searchResult = await this.SearchUdpLogs(predicate, options);
            else
                searchResult = await this.SearchIoTHubLogs(predicate, options);

            if (!searchResult.Found)
            {
                if (this.Configuration.NetworkServerModuleLogAssertLevel == LogValidationAssertLevel.Error)
                {
                    var logs = string.Join("\n\t", searchResult.Logs.TakeLast(5));
                    Assert.True(searchResult.Found, $"Searching for {options?.Description ?? "??"} failed. Current log content: [{logs}]");
                }
                else if (this.Configuration.NetworkServerModuleLogAssertLevel == LogValidationAssertLevel.Warning)
                {
                    if (!searchResult.Found)
                    {
                        var logs = string.Join("\n\t", searchResult.Logs.TakeLast(5));
                        TestLogger.Log($"[WARN] '{options?.Description ?? "??"}' found in logs? {searchResult.Found}. Logs: [{logs}]");
                    }
                }
            }

            return searchResult;
        }

        /// <summary>
        /// Check for The timing between an upstream message and a downstream one.
        /// The method stop at first occurence found and is not expected to perform against multiple DS/US messages.
        /// </summary>
        public async Task CheckAnswerTimingAsync(uint rxDelay, bool isSecondWindow)
        {
            var upstreamMessageTimingResult = await this.TryFindMessageTimeAsync("rxpk");
            var downstreamMessageTimingResult = await this.TryFindMessageTimeAsync("txpk");

            if (upstreamMessageTimingResult.Success && upstreamMessageTimingResult.Success)
            {
                Assert.Equal(rxDelay + (isSecondWindow ? 1000000 : 0), downstreamMessageTimingResult.Result - upstreamMessageTimingResult.Result);
            }
            else
            {
                Assert.True(false, "Could not procees with timestamp message search.");
            }
        }

        /// <summary>
        /// Helper method to find the time of the message that contain the message argument.
        /// </summary>
        async Task<FindTimeResult> TryFindMessageTimeAsync(string message)
        {
            const string token = @"""tmst"":";
            var log = await this.SearchUdpLogs(x => x.Contains(message));
            int timeIndexStart = log.FoundLogResult.IndexOf(token) + token.Length;
            int timeIndexStop = log.FoundLogResult.IndexOf(",", timeIndexStart);
            uint parsedValue = 0;
            bool success = false;
            if (timeIndexStart > 0 && timeIndexStop > 0)
            {
                if (uint.TryParse(log.FoundLogResult.Substring(timeIndexStart, timeIndexStop - timeIndexStart), out parsedValue))
                {
                    success = true;
                }
            }

            return new FindTimeResult()
            {
                Success = success,
                Result = parsedValue
            };
        }

        async Task<SearchLogResult> SearchUdpLogs(Func<SearchLogEvent, bool> predicate, SearchLogOptions options = null)
        {
            var maxAttempts = options?.MaxAttempts ?? this.Configuration.EnsureHasEventMaximumTries;
            var processedEvents = new HashSet<SearchLogEvent>();
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
                    var searchLogEvent = new SearchLogEvent(item);
                    processedEvents.Add(searchLogEvent);
                    if (predicate(searchLogEvent))
                    {
                        return new SearchLogResult(true, processedEvents, item)
                        {
                            MatchedEvent = searchLogEvent
                        };
                    }
                }
            }

            return new SearchLogResult(false, processedEvents);
        }

        async Task<SearchLogResult> SearchUdpLogs(Func<string, bool> predicate, SearchLogOptions options = null)
        {
            return await this.SearchUdpLogs(evt => predicate(evt.Message), options);
        }

        async Task<SearchLogResult> SearchIoTHubLogs(Func<SearchLogEvent, bool> predicate, SearchLogOptions options = null)
        {
            var maxAttempts = options?.MaxAttempts ?? this.Configuration.EnsureHasEventMaximumTries;
            var processedEvents = new HashSet<SearchLogEvent>();
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
                    var searchLogEvent = new SearchLogEvent
                    {
                        Message = bodyText,
                        SourceId = item.GetDeviceId()
                    };

                    processedEvents.Add(searchLogEvent);
                    if (predicate(searchLogEvent))
                    {
                        return new SearchLogResult(true, processedEvents, bodyText)
                        {
                            MatchedEvent = searchLogEvent
                        };
                    }
                }
            }

            return new SearchLogResult(false, processedEvents);
        }

        // Searches IoT Hub for messages
        async Task<SearchLogResult> SearchIoTHubLogs(Func<string, bool> predicate, SearchLogOptions options = null)
        {
            return await this.SearchIoTHubLogs(evt => predicate(evt.Message), options);
        }

        // Searches IoT Hub for messages
        internal async Task<SearchLogResult> SearchIoTHubMessageAsync(Func<EventData, string, string, bool> predicate, SearchLogOptions options = null)
        {
            var maxAttempts = options?.MaxAttempts ?? this.Configuration.EnsureHasEventMaximumTries;
            var processedEvents = new HashSet<SearchLogEvent>();
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
                        var searchLogEvent = new SearchLogEvent
                        {
                            SourceId = item.GetDeviceId(),
                            Message = bodyText
                        };

                        processedEvents.Add(searchLogEvent);

                        item.SystemProperties.TryGetValue("iothub-connection-device-id", out var deviceId);
                        if (predicate(item, deviceId?.ToString() ?? string.Empty, bodyText))
                        {
                            return new SearchLogResult(true, processedEvents, bodyText);
                        }
                    }
                    catch (Exception ex)
                    {
                        TestLogger.Log("Error searching in IoT Hub message log: " + ex.ToString());
                    }
                }
            }

            return new SearchLogResult(false, processedEvents, string.Empty);
        }
    }
}