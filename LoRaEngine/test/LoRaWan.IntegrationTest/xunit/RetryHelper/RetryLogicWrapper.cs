// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.IntegrationTest.RetryHelper
{
    using System;
    using System.Runtime.InteropServices.ComTypes;
    using System.Threading;
    using System.Threading.Tasks;
    using Xunit.Abstractions;
    using Xunit.Sdk;

    public static class RetryLogicWrapper
    {
        internal static async Task<RunSummary> PerformRetry(
                                                Func<IMessageSink,
                                                IMessageBus,
                                                object[],
                                                ExceptionAggregator,
                                                CancellationTokenSource, Task<RunSummary>> executer,
                                                int maxRetries,
                                                IMessageSink diagnosticMessageSink,
                                                IMessageBus messageBus,
                                                object[] constructorArguments,
                                                ExceptionAggregator aggregator,
                                                CancellationTokenSource cancellationTokenSource)
        {
            var runCount = 0;

            while (true)
            {
                using (var delayedMessageBus = new DelayedMessageBus(messageBus))
                {
                    var summary = await executer(diagnosticMessageSink, delayedMessageBus, constructorArguments, aggregator, cancellationTokenSource);
                    if (aggregator.HasExceptions || summary.Failed == 0 || ++runCount >= maxRetries)
                    {
                        delayedMessageBus.Complete();
                        return summary;
                    }

                    diagnosticMessageSink.OnMessage(new DiagnosticMessage("Execution failed (attempt #{0}), retrying...", runCount));
                }

                await Task.Delay(5000);
            }
        }
    }
}
