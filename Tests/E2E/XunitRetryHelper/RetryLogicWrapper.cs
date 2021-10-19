// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace XunitRetryHelper
{
    using System;
    using System.Diagnostics;
    using System.Globalization;
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
                                                XunitTestCase testCase,
                                                CancellationTokenSource cancellationTokenSource)
        {
            var testRetryCount = 0;
            var rnd = new Random();

            using var delayedMessageBus = new DelayedMessageBus(messageBus);
            while (true)
            {
                var summary = await executer(diagnosticMessageSink, delayedMessageBus, constructorArguments, aggregator, cancellationTokenSource);

                var lastAssert = delayedMessageBus.LastFailure;

                if (aggregator.HasExceptions || summary.Failed == 0 || testRetryCount + 1 > maxRetries)
                {
                    delayedMessageBus.Complete();

                    if (testRetryCount > 0)
                    {
                        LogMessage($"test '{testCase.DisplayName}' retry finished. Number of failed tests of last run: {summary.Failed}, retry state: {testRetryCount}/{maxRetries}");

                        if (summary.Failed == 0)
                        {
                            LogMessage($"test '{testCase.DisplayName}' succeeded after {testRetryCount}/{maxRetries} executions.");
                        }
                        else if (testRetryCount == maxRetries)
                        {
                            LogMessage($"test '{testCase.DisplayName}' failed after {testRetryCount}/{maxRetries} executions.", lastAssert);
                        }
                    }

                    if (aggregator.HasExceptions)
                    {
                        LogMessage($"test '{testCase.DisplayName}' failed with exception. {aggregator.ToException()}");
                    }

                    return summary;
                }

                testRetryCount++;
                var retryDelay = (int)Math.Min(180_000, Math.Pow(2, testRetryCount) * rnd.Next(5000, 30_000));
                var msg = $"performing retry number {testRetryCount}/{maxRetries} in {retryDelay}ms for Test '{testCase.DisplayName}'";
                LogMessage(msg, lastAssert);

                await Task.Delay(retryDelay, cancellationTokenSource.Token);
            }
        }

        private static void LogMessage(string msg, TestFailed assertInfo = null)
        {
            msg = $"{DateTime.UtcNow.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture)} [Test] {msg}";

            if (assertInfo != null)
            {
                var messages = assertInfo.Messages != null ? string.Join(',', assertInfo.Messages) : string.Empty;
                var stackTrace = assertInfo.StackTraces != null ? string.Join(',', assertInfo.StackTraces) : string.Empty;
                msg = string.Concat(msg, Environment.NewLine, messages, Environment.NewLine, stackTrace);
            }

            if (Debugger.IsAttached)
            {
                Debug.WriteLine(msg);
            }
            else
            {
                Console.WriteLine(msg);
            }
        }
    }
}
