// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

namespace LoRaWan.NetworkServer
{
    using System;
    using System.Collections.Generic;
    using System.Runtime.CompilerServices;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Shared;
    using Microsoft.Extensions.Logging;

    public static class LoRaDeviceClientExtensions
    {
        public static ILoRaDeviceClient AddResiliency(this ILoRaDeviceClient client, ILoggerFactory? loggerFactory)
        {
            ArgumentNullException.ThrowIfNull(client, nameof(client));
            return client is ResilientClient ? client : new ResilientClient(client, loggerFactory?.CreateLogger<ResilientClient>());
        }

        private sealed class ResilientClient : ILoRaDeviceClient, IIdentityProvider<ILoRaDeviceClient>
        {
            private const int MaxAttempts = 3;

            private readonly ILoRaDeviceClient client;
            private readonly ILogger? logger;

            public ResilientClient(ILoRaDeviceClient client, ILogger<ResilientClient>? logger)
            {
                this.client = client;
                this.logger = logger;
            }

            public bool EnsureConnected() => this.client.EnsureConnected();
            public Task DisconnectAsync(CancellationToken cancellationToken) => this.client.DisconnectAsync(cancellationToken);
            ValueTask IAsyncDisposable.DisposeAsync() => this.client.DisposeAsync();

            private sealed class Void
            {
                public static readonly Void Value = new();
                private Void() { }
            }

            private Task<TResult> InvokeAsync<TResult>(Operation<Void, Void, TResult> operation,
                                                       CancellationToken cancellationToken,
                                                       [CallerMemberName] string? operationName = null) =>
                InvokeAsync(operation, Void.Value, Void.Value, cancellationToken, operationName);

            private Task<TResult> InvokeAsync<T, TResult>(Operation<T, Void, TResult> operation, T arg,
                                                          CancellationToken cancellationToken,
                                                          [CallerMemberName] string? operationName = null) =>
                InvokeAsync(operation, arg, Void.Value, cancellationToken, operationName);

            private async Task<TResult> InvokeAsync<T1, T2, TResult>(Operation<T1, T2, TResult> operation, T1 arg1, T2 arg2,
                                                                     CancellationToken cancellationToken,
                                                                     [CallerMemberName] string? operationName = null)
            {
                for (var attempt = 1; ; attempt++)
                {
                    try
                    {
                        _ = this.client.EnsureConnected();
                        return await operation(this.client, arg1, arg2, cancellationToken);
                    }
                    catch (Exception ex)
                        when (attempt < MaxAttempts
                              && (ex is ObjectDisposedException
                                  || (ex is InvalidOperationException ioe
                                      && ioe.Message.StartsWith("This operation is only allowed using a successfully authenticated context.", StringComparison.OrdinalIgnoreCase)))
                              && ExceptionFilterUtility.True(() => this.logger?.LogWarning(ex, @"Device client operation ""{Operation}"" failed due to: {Error}", operationName, ex.GetBaseException().Message)))
                    {
                        // disconnect, re-connect and then retry...
                        await this.client.DisconnectAsync(CancellationToken.None);
                    }
                }
            }

            private delegate Task<TResult> Operation<in T1, in T2, TResult>(ILoRaDeviceClient client, T1 arg1, T2 arg2, CancellationToken cancellationToken);

            private static class Operations
            {
                public static readonly Operation<Void, Void, Twin> GetTwin = (client, _, _, cancellationToken) => client.GetTwinAsync(cancellationToken);
                public static readonly Operation<LoRaDeviceTelemetry, Dictionary<string, string>, bool> SendEvent = (client, telemetry, properties, _) => client.SendEventAsync(telemetry, properties);
                public static readonly Operation<TwinCollection, Void, bool> UpdateReportedProperties = (client, reportedProperties, _, cancellationToken) => client.UpdateReportedPropertiesAsync(reportedProperties, cancellationToken);
                public static readonly Operation<TimeSpan, Void, Message> Receive = (client, timeout, _, _) => client.ReceiveAsync(timeout);
                public static readonly Operation<Message, Void, bool> Complete = (client, message, _, _) => client.CompleteAsync(message);
                public static readonly Operation<Message, Void, bool> Abandon = (client, message, _, _) => client.AbandonAsync(message);
                public static readonly Operation<Message, Void, bool> Reject = (client, message, _, _) => client.RejectAsync(message);
            }

            public Task<Twin> GetTwinAsync(CancellationToken cancellationToken) =>
                InvokeAsync(Operations.GetTwin, cancellationToken);

            public Task<bool> SendEventAsync(LoRaDeviceTelemetry telemetry, Dictionary<string, string> properties) =>
                InvokeAsync(Operations.SendEvent, telemetry, properties, CancellationToken.None);

            public Task<bool> UpdateReportedPropertiesAsync(TwinCollection reportedProperties, CancellationToken cancellationToken) =>
                InvokeAsync(Operations.UpdateReportedProperties, reportedProperties, cancellationToken);

            public Task<Message> ReceiveAsync(TimeSpan timeout) =>
                InvokeAsync(Operations.Receive, timeout, CancellationToken.None);

            public Task<bool> CompleteAsync(Message cloudToDeviceMessage) =>
                InvokeAsync(Operations.Complete, cloudToDeviceMessage, CancellationToken.None);

            public Task<bool> AbandonAsync(Message cloudToDeviceMessage) =>
                InvokeAsync(Operations.Abandon, cloudToDeviceMessage, CancellationToken.None);

            public Task<bool> RejectAsync(Message cloudToDeviceMessage) =>
                InvokeAsync(Operations.Reject, cloudToDeviceMessage, CancellationToken.None);

            ILoRaDeviceClient IIdentityProvider<ILoRaDeviceClient>.Identity => this.client;
        }
    }
}
