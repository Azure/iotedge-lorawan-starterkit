// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

namespace LoRaWan.NetworkServer
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.Metrics;
    using System.Linq;
    using System.Threading;

    internal static class MetricRegistry
    {
        public const string Namespace = "LoRaWan";
        public const string Version = "1.0";
        public const string ConcentratorIdTagName = "ConcentratorId";
        public const string GatewayIdTagName = "GatewayId";
        public const string ReceiveWindowTagName = "ReceiveWindow";

        public static readonly CustomMetric JoinRequests = new CustomMetric("JoinRequests", "Number of join requests", MetricType.Counter, new[] { GatewayIdTagName, ConcentratorIdTagName });
        public static readonly CustomMetric ActiveStationConnections = new CustomMetric("ActiveStationConnections", "Number of active station connections", MetricType.ObservableGauge, new[] { GatewayIdTagName });
        public static readonly CustomMetric StationConnectivityLost = new CustomMetric("StationConnectivityLost", "Counts the number of station connectivities that were lost", MetricType.Counter, new[] { GatewayIdTagName, ConcentratorIdTagName });
        public static readonly CustomMetric ReceiveWindowHits = new CustomMetric("ReceiveWindowHits", "Receive window hits", MetricType.Counter, new[] { GatewayIdTagName, ConcentratorIdTagName, ReceiveWindowTagName });
        public static readonly CustomMetric ReceiveWindowMisses = new CustomMetric("ReceiveWindowMisses", "Receive window misses", MetricType.Counter, new[] { GatewayIdTagName, ConcentratorIdTagName });
        public static readonly CustomMetric UnhandledExceptions = new CustomMetric("UnhandledExceptions", "Number of unhandled exceptions", MetricType.Counter, new[] { GatewayIdTagName });
        public static readonly CustomMetric D2CMessageDeliveryLatency = new CustomHistogram("D2CMessageDeliveryLatency", "D2C delivery latency (in milliseconds)", MetricType.Histogram, new[] { GatewayIdTagName, ConcentratorIdTagName },
                                                                                            BucketStart: 100, BucketWidth: 50, BucketCount: 45);
        public static readonly CustomMetric D2CMessagesReceived = new CustomMetric("D2CMessagesReceived", "Number of D2C messages received", MetricType.Counter, new[] { GatewayIdTagName, ConcentratorIdTagName });
        public static readonly CustomMetric D2CMessageSize = new CustomHistogram("D2CMessageSize", "Size of D2C messages (in bytes)", MetricType.Histogram, new[] { GatewayIdTagName, ConcentratorIdTagName },
                                                                                 BucketStart: 5, BucketWidth: 10, BucketCount: 26);
        public static readonly CustomMetric C2DMessageTooLong = new CustomMetric("C2DMessageTooLong", "Number of C2D messages that were too long to be sent downstream", MetricType.Counter, new[] { GatewayIdTagName, ConcentratorIdTagName });

        private static readonly ICollection<CustomMetric> Registry = new[]
        {
            JoinRequests,
            ActiveStationConnections,
            StationConnectivityLost,
            ReceiveWindowHits,
            ReceiveWindowMisses,
            UnhandledExceptions,
            D2CMessageDeliveryLatency,
            D2CMessagesReceived,
            D2CMessageSize,
            C2DMessageTooLong
        };

        public static readonly IDictionary<string, CustomMetric> RegistryLookup =
            new Dictionary<string, CustomMetric>(Registry.ToDictionary(m => m.Name, m => m), StringComparer.OrdinalIgnoreCase);
    }

    internal record CustomMetric(string Name, string Description, MetricType Type, string[] Tags);

    internal record CustomHistogram(string Name, string Description, MetricType Type, string[] Tags,
                                    double BucketStart, double BucketWidth, int BucketCount)
        : CustomMetric(Name, Description, Type, Tags);

    internal enum MetricType
    {
        Counter,
        Histogram,
        ObservableGauge
    }

    internal interface IMetricExporter : IDisposable
    {
        void Start();
    }

    internal sealed class CompositeMetricExporter : IMetricExporter
    {
        private readonly IMetricExporter? first;
        private readonly IMetricExporter? second;

        public CompositeMetricExporter(IMetricExporter? first, IMetricExporter? second)
        {
            this.first = first;
            this.second = second;
        }

        public void Dispose()
        {
            this.first?.Dispose();
            this.second?.Dispose();
        }

        public void Start()
        {
            if (this.first is { } f)
                f.Start();

            if (this.second is { } s)
                s.Start();
        }
    }

    /// <summary>
    /// Container for station EUI tags that are used as a tag when raising metrics.
    /// This helps us avoiding passing the station EUI down the stack.
    /// </summary>
    internal sealed class RegistryMetricTagBag
    {
        public RegistryMetricTagBag(NetworkServerConfiguration networkServerConfiguration)
        {
            GatewayId = string.IsNullOrEmpty(networkServerConfiguration.GatewayID) ? "unknown" : networkServerConfiguration.GatewayID;
        }

        public AsyncLocal<StationEui?> StationEui { get; } = new AsyncLocal<StationEui?>();
        public string GatewayId { get; init; }
    }

    internal static class MetricExporterHelper
    {
        /// <summary>
        /// Gets the tags ordered by the original order of the tags.
        /// Falls back to common tags that are present in the RegistryMetricTagBag.
        /// </summary>
        public static string[] GetTagsInOrder(IReadOnlyList<string> tagNames, ReadOnlySpan<KeyValuePair<string, object?>> tags, RegistryMetricTagBag metricTagBag)
        {
            var tagsMatchedCount = 0; // Used to validate that all tag values that are raised are defined in the custom metric registry.
            var result = new string[tagNames.Count];
            for (var i = 0; i < tagNames.Count; ++i)
            {
                var tagName = tagNames[i];
                string? tagValue = null;

                // nested loop since we will only ever have less than approximately five custom dimensions
                for (var j = 0; j < tags.Length; ++j)
                {
                    if (tagName == tags[j].Key)
                    {
                        tagValue = tags[j].Value?.ToString();
                        ++tagsMatchedCount;
                        break;
                    }
                }

                // fall back to tag bag lookup
                if (tagValue == null)
                {
                    tagValue = tagName switch
                    {
                        MetricRegistry.ConcentratorIdTagName when metricTagBag.StationEui.Value is { } stationEui => stationEui.ToString(),
                        MetricRegistry.GatewayIdTagName => metricTagBag.GatewayId,
                        _ => null
                    };
                }

                if (string.IsNullOrEmpty(tagValue))
                    throw new LoRaProcessingException($"Tag '{tagName}' is not defined.", LoRaProcessingErrorCode.TagNotSet);

                result[i] = tagValue;
            }

            if (tagsMatchedCount != tags.Length)
                throw new InvalidOperationException("Some tags raised are not defined in custom metric registry. Make sure that all tags that you raise are defined in the metric registry.");

            return result;
        }
    }

    internal static class MetricsExtensions
    {
        public static Counter<T> CreateCounter<T>(this Meter meter, CustomMetric customMetric) where T : struct =>
            customMetric.Type == MetricType.Counter
            ? meter.CreateCounter<T>(customMetric.Name, description: customMetric.Description)
            : throw new ArgumentException("Custom metric must of type Counter", nameof(customMetric));

        public static Histogram<T> CreateHistogram<T>(this Meter meter, CustomMetric customMetric) where T : struct =>
            customMetric.Type == MetricType.Histogram
            ? meter.CreateHistogram<T>(customMetric.Name, description: customMetric.Description)
            : throw new ArgumentException("Custom metric must of type Histogram", nameof(customMetric));

        public static ObservableGauge<T> CreateObservableGauge<T>(this Meter meter, CustomMetric customMetric, Func<T> observeValue) where T : struct =>
            customMetric.Type == MetricType.ObservableGauge
            ? meter.CreateObservableGauge(customMetric.Name, observeValue, description: customMetric.Description)
            : throw new ArgumentException("Custom metric must of type Histogram", nameof(customMetric));
    }
}
