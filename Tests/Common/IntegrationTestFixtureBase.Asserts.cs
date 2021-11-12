// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Common
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

    /// <summary>
    /// Integration test fixture.
    /// </summary>
    public abstract partial class IntegrationTestFixtureBase : IDisposable, IAsyncLifetime
    {
        internal static string GetMessageIdentifier(EventData eventData)
        {
            eventData.Properties.TryGetValue("messageIdentifier", out var actualMessageIdentifier);
            return actualMessageIdentifier?.ToString();
        }

        private static bool IsDeviceMessage(string expectedDeviceID, string jsonPropertyToValidate, string expectedValue, string eventDeviceID, string eventDataMessageBody)
        {
            if (eventDeviceID != null && eventDeviceID == expectedDeviceID)
            {
                try
                {
                    var messageJson = JObject.Parse(eventDataMessageBody);
                    if (messageJson != null)
                    {
                        var data = messageJson[jsonPropertyToValidate];
                        return data != null && data.ToString(Formatting.None) == expectedValue;
                    }
                }
                catch (Exception ex)
                {
                    TestLogger.Log($"Error searching device payload: {eventDataMessageBody}. {ex}");
                }
            }

            return false;
        }

        public async Task AssertIoTHubDeviceMessageExistsAsync(string deviceID, string targetJsonProperty, string expectedJsonValue, SearchLogOptions options = null)
        {
            var assertionLevel = Configuration.IoTHubAssertLevel;
            if (options != null && options.TreatAsError.HasValue)
                assertionLevel = options.TreatAsError.Value ? LogValidationAssertLevel.Error : LogValidationAssertLevel.Warning;

            if (assertionLevel == LogValidationAssertLevel.Ignore)
                return;

            var searchResult = await SearchIoTHubMessageAsync(
                (eventData, eventDeviceID, eventDataMessageBody) => IsDeviceMessage(deviceID, targetJsonProperty, expectedJsonValue, eventDeviceID, eventDataMessageBody),
                new SearchLogOptions
                {
                    Description = options?.Description ?? $"\"{targetJsonProperty}\": {expectedJsonValue}",
                    TreatAsError = options?.TreatAsError,
                });

            if (assertionLevel == LogValidationAssertLevel.Error)
            {
                var logs = string.Join("\n\t", searchResult.Logs.TakeLast(5));
                Assert.True(searchResult.Found, $"Searching for \"{targetJsonProperty}\": {expectedJsonValue} failed for device {deviceID}. Current log content: [{logs}]");
            }
            else if (assertionLevel == LogValidationAssertLevel.Warning)
            {
                if (!searchResult.Found)
                {
                    var logs = string.Join("\n\t", searchResult.Logs.TakeLast(5));
                    TestLogger.Log($"[WARN] \"{targetJsonProperty}\": {expectedJsonValue} for device {deviceID} found in logs? {searchResult.Found}. Logs: [{logs}]");
                }
            }
        }

        // Asserts leaf device message payload exists. It searches inside the payload "data" property. Has built-in retries
        public async Task AssertIoTHubDeviceMessageExistsAsync(string deviceID, string expectedDataValue, SearchLogOptions options = null)
        {
            await AssertIoTHubDeviceMessageExistsAsync(deviceID, "data", expectedDataValue, options);
        }

        // Asserts network server module log contains
        public async Task<SearchLogResult> AssertNetworkServerModuleLogStartsWithAsync(string logMessageStart)
        {
            if (Configuration.NetworkServerModuleLogAssertLevel == LogValidationAssertLevel.Ignore)
                return null;

            return await AssertNetworkServerModuleLogExistsAsync((input) => input.StartsWith(logMessageStart, StringComparison.Ordinal), new SearchLogOptions(logMessageStart));
        }

        public async Task AssertNetworkServerModuleLogStartsWithAsync(string logMessageStart1, string logMessageStart2)
        {
            if (Configuration.NetworkServerModuleLogAssertLevel == LogValidationAssertLevel.Ignore)
                return;

            await AssertNetworkServerModuleLogExistsAsync(
                (input) => input.StartsWith(logMessageStart1, StringComparison.Ordinal) || input.StartsWith(logMessageStart2, StringComparison.Ordinal),
                new SearchLogOptions(string.Concat(logMessageStart1, " or ", logMessageStart2)));
        }

        public async Task<SearchLogResult> SearchNetworkServerModuleAsync(Func<string, bool> predicate, SearchLogOptions options = null) =>
            this.tcpLogListener != null
                ? await SearchUdpLogs(predicate, options)
                : await SearchIoTHubLogs(predicate, options);

        /// <summary>
        /// Searches the UDP log, matching the passed in predicate to
        /// ensure a particular message got reported by all gateways.
        /// The number of gateways can be driven by configuration.
        /// <see cref="TestConfiguration.NumberOfGateways"/>.
        /// </summary>
        /// <param name="predicate">predicate used to match the log entries.</param>
        /// <param name="maxAttempts">max retry attempts if the message is not found on all Gateways.</param>
        /// <returns>true, if it was found on all configured gateways otherwise false.</returns>
        public async Task<bool> ValidateMultiGatewaySources(Func<string, bool> predicate, int maxAttempts = 5)
        {
            var numberOfGw = Configuration.NumberOfGateways;
            var sourceIds = new HashSet<string>(numberOfGw);
            for (var i = 0; i < maxAttempts && sourceIds.Count < numberOfGw; i++)
            {
                if (i > 0)
                {
                    var timeToWait = i * Configuration.EnsureHasEventDelayBetweenReadsInSeconds;
                    await Task.Delay(TimeSpan.FromSeconds(timeToWait));
                }

                foreach (var item in this.tcpLogListener.Events)
                {
                    var (message, sourceId) = SearchLogEvent.Parse(item);
                    if (predicate(message))
                    {
                        if (!string.IsNullOrEmpty(sourceId))
                        {
                            sourceIds.Add(sourceId);
                        }

                        if (sourceIds.Count == numberOfGw)
                        {
                            return true;
                        }
                    }
                }
            }

            return sourceIds.Count == numberOfGw;
        }

        public async Task AssertSingleGatewaySource(Func<string, bool> predicate, int maxAttempts = 5)
        {
            var numberOfGw = Configuration.NumberOfGateways;
            var sourceIds = new HashSet<string>(numberOfGw);
            for (var i = 0; i < maxAttempts && sourceIds.Count < numberOfGw; i++)
            {
                if (i > 0)
                {
                    var timeToWait = i * Configuration.EnsureHasEventDelayBetweenReadsInSeconds;
                    await Task.Delay(TimeSpan.FromSeconds(timeToWait));
                }

                foreach (var item in this.tcpLogListener.Events)
                {
                    var (message, sourceId) = SearchLogEvent.Parse(item);
                    if (predicate(message))
                    {
                        if (!string.IsNullOrEmpty(sourceId))
                        {
                            sourceIds.Add(sourceId);
                        }
                    }
                }

                if (sourceIds.Count == 1)
                {
                    return;
                }
            }

            Assert.True(sourceIds.Count == 1, $"Not 1 source, but {sourceIds.Count}");
        }

        /// <summary>
        /// This makes sure that after a join, the DevAddr is
        /// available from the twins. Call this in case of an OTTA join with
        /// a multi GW setup, where you want to make sure both GW will be able
        /// to process the next message.
        /// </summary>
        /// <param name="serialLog">serial log from the attached device.</param>
        /// <param name="devEUI">The device EUI of the current device.</param>
        public async Task<bool> WaitForTwinSyncAfterJoinAsync(IReadOnlyCollection<string> serialLog, string devEUI)
        {
            var joinConfirmMsg = serialLog.FirstOrDefault(s => s.StartsWith("+JOIN: NetID", StringComparison.Ordinal));
            Assert.NotNull(joinConfirmMsg);
            var devAddr = joinConfirmMsg[(joinConfirmMsg.LastIndexOf(' ') + 1)..];
            devAddr = devAddr.Replace(":", string.Empty, StringComparison.Ordinal);

            // wait for the twins to be stored and published -> all GW need the same state
            const int DelayForJoinTwinStore = 20 * 1000;
            const string DevAddrProperty = "DevAddr";
            const int MaxRuns = 10;
            var reported = false;
            for (var i = 0; i < MaxRuns && !reported; i++)
            {
                await Task.Delay(DelayForJoinTwinStore);

                var twins = await GetTwinAsync(devEUI);
                if (twins.Properties.Reported.Contains(DevAddrProperty))
                {
                    reported = devAddr.Equals(twins.Properties.Reported[DevAddrProperty].Value as string, StringComparison.OrdinalIgnoreCase);
                }
            }

            if (!reported)
            {
                TestLogger.Log($"[WARNING] Twin sync after join did not happen for device {devEUI}");
            }

            return reported;
        }

        // Asserts Network Server Module log exists. It has built-in retries and delays
        public async Task<SearchLogResult> AssertNetworkServerModuleLogExistsAsync(Func<string, bool> predicate, SearchLogOptions options)
        {
            if (Configuration.NetworkServerModuleLogAssertLevel == LogValidationAssertLevel.Ignore)
                return null;

            SearchLogResult searchResult;
            if (this.tcpLogListener != null)
                searchResult = await SearchUdpLogs(predicate, options);
            else
                searchResult = await SearchIoTHubLogs(predicate, options);

            if (!searchResult.Found)
            {
                if (Configuration.NetworkServerModuleLogAssertLevel == LogValidationAssertLevel.Error)
                {
                    var logs = string.Join("\n\t", searchResult.Logs.TakeLast(5));
                    Assert.True(searchResult.Found, $"Searching for {options?.Description ?? "??"} failed. Current log content: [{logs}]");
                }
                else if (Configuration.NetworkServerModuleLogAssertLevel == LogValidationAssertLevel.Warning)
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
        public async Task CheckAnswerTimingAsync(uint rxDelay, bool isSecondWindow, string gatewayId)
        {
            var upstreamMessageTimingResult = await TryFindMessageTimeAsync("rawdata", gatewayId, true);
            var downstreamMessageTimingResult = await TryFindMessageTimeAsync("txpk", gatewayId, false);

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
        private async Task<FindTimeResult> TryFindMessageTimeAsync(string message, string sourceIdFilter, bool isUpstream = false)
        {
            const string token = @"""tmst"":";
            SearchLogResult log;
            if (isUpstream)
            {
                log = await SearchIoTHubLogs(x => x.Contains(message, StringComparison.Ordinal), new SearchLogOptions { SourceIdFilter = sourceIdFilter });
            }
            else
            {
                log = await SearchUdpLogs(x => x.Contains(message, StringComparison.Ordinal), new SearchLogOptions { SourceIdFilter = sourceIdFilter });
            }

            var timeIndexStart = log.FoundLogResult.IndexOf(token, StringComparison.Ordinal) + token.Length;
            var timeIndexStop = log.FoundLogResult.IndexOf(",", timeIndexStart, StringComparison.Ordinal);
            uint parsedValue = 0;
            var success = false;
            if (timeIndexStart > 0 && timeIndexStop > 0)
            {
                if (uint.TryParse(log.FoundLogResult[timeIndexStart..timeIndexStop], out parsedValue))
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

        private async Task<SearchLogResult> SearchUdpLogs(Func<SearchLogEvent, bool> predicate, SearchLogOptions options = null)
        {
            var maxAttempts = options?.MaxAttempts ?? Configuration.EnsureHasEventMaximumTries;
            var processedEvents = new HashSet<SearchLogEvent>();
            for (var i = 0; i < maxAttempts; i++)
            {
                if (i > 0)
                {
                    var timeToWait = i * Configuration.EnsureHasEventDelayBetweenReadsInSeconds;
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

                var sourceIdFilter = options?.SourceIdFilter;

                foreach (var item in this.tcpLogListener.Events)
                {
                    var searchLogEvent = new SearchLogEvent(item);
                    processedEvents.Add(searchLogEvent);
                    if (!string.IsNullOrEmpty(sourceIdFilter) && !sourceIdFilter.Equals(searchLogEvent.SourceId, StringComparison.Ordinal))
                    {
                        continue;
                    }

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

        private async Task<SearchLogResult> SearchUdpLogs(Func<string, bool> predicate, SearchLogOptions options = null)
        {
            return await SearchUdpLogs(evt => predicate(evt.Message), options);
        }

        private async Task<SearchLogResult> SearchIoTHubLogs(Func<SearchLogEvent, bool> predicate, SearchLogOptions options = null)
        {
            var maxAttempts = options?.MaxAttempts ?? Configuration.EnsureHasEventMaximumTries;
            var processedEvents = new HashSet<SearchLogEvent>();
            for (var i = 0; i < maxAttempts; i++)
            {
                if (i > 0)
                {
                    var timeToWait = i * Configuration.EnsureHasEventDelayBetweenReadsInSeconds;
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

                foreach (var item in IoTHubMessages.Events)
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
        private async Task<SearchLogResult> SearchIoTHubLogs(Func<string, bool> predicate, SearchLogOptions options = null)
        {
            return await SearchIoTHubLogs(evt => predicate(evt.Message), options);
        }

        // Searches IoT Hub for messages
        internal async Task<SearchLogResult> SearchIoTHubMessageAsync(Func<EventData, string, string, bool> predicate, SearchLogOptions options = null)
        {
            var maxAttempts = options?.MaxAttempts ?? Configuration.EnsureHasEventMaximumTries;
            var processedEvents = new HashSet<SearchLogEvent>();
            for (var i = 0; i < maxAttempts; i++)
            {
                if (i > 0)
                {
                    var timeToWait = i * Configuration.EnsureHasEventDelayBetweenReadsInSeconds;
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

                foreach (var item in IoTHubMessages.Events)
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
