// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

namespace LoRaWan.NetworkServer
{
    using System;
    using System.Diagnostics.Metrics;
    using System.Threading.Tasks;

    internal static class TaskUtil
    {
        public static Task RunOnThreadPool(Func<Task> task, Action<Exception> log, Counter<int>? exceptionCount) =>
            Task.Run(async () =>
            {
                try
                {
                    await task();
                }
                catch (Exception ex) when (ExceptionFilterUtility.False(() => log(ex), () => exceptionCount?.Add(1)))
                {
                    throw;
                }
            });
    }
}
