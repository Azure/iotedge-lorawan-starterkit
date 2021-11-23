// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

namespace LoRaWan.NetworkServer
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    internal static class MetricRegistry
    {
        public const string Namespace = "LoRaWan";
        public const string MetricsVersion = "1.0";
        public const string GatewayIdTagName = "GatewayId";

        public static readonly CustomMetric JoinRequests = new CustomMetric("JoinRequests", "Number of join requests", MetricType.Counter, new[] { GatewayIdTagName });
        private static readonly ICollection<CustomMetric> Registry = new[]
        {
            JoinRequests
        };

        public static readonly IDictionary<string, CustomMetric> RegistryLookup = new Dictionary<string, CustomMetric>(Registry.ToDictionary(m => m.Name, m => m), StringComparer.OrdinalIgnoreCase);
    }

    internal interface IMetricExporter : IDisposable
    {
        void Start();
    }

    internal record CustomMetric(string Name, string Description, MetricType Type, string[] Tags);

    internal static class MetricExporterHelper
    {
        /// <summary>
        /// Gets the tags ordered by the original order of the tags.
        /// </summary>
        public static string[] GetTagsInOrder(IEnumerable<string> tagNames, ReadOnlySpan<KeyValuePair<string, object?>> tags)
        {
            var tagLookup = new Dictionary<string, object?>(tags.ToArray(), StringComparer.OrdinalIgnoreCase);
            return tagNames.Select(t => tagLookup.TryGetValue(t, out var v) ? (v?.ToString() ?? string.Empty) : string.Empty).ToArray();
        }
    }

    internal enum MetricType
    {
        Counter,
        Histogram
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
}
