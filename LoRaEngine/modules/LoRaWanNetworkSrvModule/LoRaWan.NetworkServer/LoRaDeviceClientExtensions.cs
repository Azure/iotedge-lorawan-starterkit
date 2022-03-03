// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

namespace LoRaWan.NetworkServer
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Reflection;
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

            private async Task<TResult> InvokeAsync<T1, T2, TResult>(T1 arg1, T2 arg2, Func<ILoRaDeviceClient, T1, T2, Task<TResult>> function,
                                                                     [CallerMemberName] string? operationName = null)
            {
                for (var attempt = 1; ; attempt++)
                {
                    try
                    {
                        _ = this.client.EnsureConnected();
                        return await function(this.client, arg1, arg2);
                    }
                    catch (Exception ex)
                        when ((ex is ObjectDisposedException
                                  || (ex is InvalidOperationException ioe
                                      && ioe.Message.StartsWith("This operation is only allowed using a successfully authenticated context.", StringComparison.OrdinalIgnoreCase)))
                              && ExceptionFilterUtility.True(() =>
                                     this.logger?.LogDebug(ex, @"Device client operation ""{Operation}"" (attempt {Attempt}/"
                                                               + MaxAttempts.ToString(CultureInfo.InvariantCulture)
                                                               + @") failed due to error: {Error}",
                                                               operationName, attempt, ex.GetBaseException().Message)))
                    {
                        // disconnect, re-connect and then retry...
                        await this.client.DisconnectAsync(CancellationToken.None);
                        if (attempt == MaxAttempts)
                            throw;
                    }
                }
            }

            public Task<Twin> GetTwinAsync(CancellationToken cancellationToken) =>
                InvokeAsync(cancellationToken, Missing.Value, static (client, cancellationToken, _) => client.GetTwinAsync(cancellationToken));

            public Task<bool> SendEventAsync(LoRaDeviceTelemetry telemetry, Dictionary<string, string> properties) =>
                InvokeAsync(telemetry, properties, static (client, telemetry, properties) => client.SendEventAsync(telemetry, properties));

            public Task<bool> UpdateReportedPropertiesAsync(TwinCollection reportedProperties, CancellationToken cancellationToken) =>
                InvokeAsync(reportedProperties, cancellationToken, static (client, reportedProperties, cancellationToken) => client.UpdateReportedPropertiesAsync(reportedProperties, cancellationToken));

            public Task<Message> ReceiveAsync(TimeSpan timeout) =>
                InvokeAsync(timeout, Missing.Value, static (client, timeout, _) => client.ReceiveAsync(timeout));

            public Task<bool> CompleteAsync(Message cloudToDeviceMessage) =>
                InvokeAsync(cloudToDeviceMessage, Missing.Value, static (client, message, _) => client.CompleteAsync(message));

            public Task<bool> AbandonAsync(Message cloudToDeviceMessage) =>
                InvokeAsync(cloudToDeviceMessage, Missing.Value, static (client, message, _) => client.AbandonAsync(message));

            public Task<bool> RejectAsync(Message cloudToDeviceMessage) =>
                InvokeAsync(cloudToDeviceMessage, Missing.Value, static (client, message, _) => client.RejectAsync(message));

            ILoRaDeviceClient IIdentityProvider<ILoRaDeviceClient>.Identity => this.client;
        }
    }
}
