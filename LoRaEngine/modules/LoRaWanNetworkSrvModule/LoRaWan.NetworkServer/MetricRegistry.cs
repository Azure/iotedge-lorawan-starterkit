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
        public const string GatewayIdTagName = "GatewayId";

        public static readonly CustomMetric JoinRequests = new CustomMetric("JoinRequests", "Number of join requests", MetricType.Counter, new[] { GatewayIdTagName });
        public static readonly CustomMetric ActiveStationConnections = new CustomMetric("ActiveStationConnections", "Number of active station connections", MetricType.Histogram, Array.Empty<string>());
        public static readonly CustomMetric StationConnectivityLost = new CustomMetric("StationConnectivityLost", "Counts the number of station connectivities that were lost", MetricType.Counter, new[] { GatewayIdTagName });
        public static readonly CustomMetric ReceiveWindowHits = new CustomMetric("ReceiveWindowHits", "Receive window hits", MetricType.Counter, new[] { GatewayIdTagName });
        public static readonly CustomMetric ReceiveWindowMisses = new CustomMetric("ReceiveWindowMisses", "Receive window misses", MetricType.Counter, new[] { GatewayIdTagName });
        public static readonly CustomMetric D2CMessagesReceived = new CustomMetric("D2CMessagesReceived", "Number of D2C messages received", MetricType.Counter, new[] { GatewayIdTagName });

        private static readonly ICollection<CustomMetric> Registry = new[]
        {
            JoinRequests,
            ActiveStationConnections,
            StationConnectivityLost,
            ReceiveWindowHits,
            ReceiveWindowMisses,
            D2CMessagesReceived
        };

        public static readonly IDictionary<string, CustomMetric> RegistryLookup =
            new Dictionary<string, CustomMetric>(Registry.ToDictionary(m => m.Name, m => m), StringComparer.OrdinalIgnoreCase);
    }

    internal record CustomMetric(string Name, string Description, MetricType Type, string[] Tags);

    internal enum MetricType
    {
        Counter,
        Histogram
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
        public AsyncLocal<StationEui?> StationEui { get; } = new AsyncLocal<StationEui?>();
    }

    internal static class MetricExporterHelper
    {
        /// <summary>
        /// Gets the tags ordered by the original order of the tags.
        /// Falls back to common tags that are present in the RegistryMetricTagBag.
        /// </summary>
        public static string[] GetTagsInOrder(IEnumerable<string> tagNames, ReadOnlySpan<KeyValuePair<string, object?>> tags, RegistryMetricTagBag metricTagBag)
        {
            var tagLookup = new Dictionary<string, object?>(tags.ToArray(), StringComparer.OrdinalIgnoreCase);
            return tagNames.Select(t => GetNonEmptyTagValue(t, tagLookup, metricTagBag)).ToArray();
            static string GetNonEmptyTagValue(string tag, IDictionary<string, object?> tagLookup, RegistryMetricTagBag registryMetricTagBag)
            {
                if (tagLookup.TryGetValue(tag, out var tagValue))
                {
                    var tagString = tagValue?.ToString();
                    if (!string.IsNullOrEmpty(tagString)) return tagString;
                }
                else if (tag.Equals(MetricRegistry.GatewayIdTagName, StringComparison.OrdinalIgnoreCase) && registryMetricTagBag.StationEui.Value is { } stationEui)
                {
                    return stationEui.ToString();
                }

                throw new LoRaProcessingException($"Tag '{tag}' is not defined.", LoRaProcessingErrorCode.TagNotSet);
            }
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
    }
}
