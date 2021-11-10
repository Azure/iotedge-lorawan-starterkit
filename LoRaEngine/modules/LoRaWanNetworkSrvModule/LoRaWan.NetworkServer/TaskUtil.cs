// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer
{
    using System;
    using System.Threading.Tasks;

    internal static class TaskUtil
    {
        public static Task RunOnThreadPool(Func<Task> task, Action<Exception> log) =>
            Task.Run(async () =>
            {
                try
                {
                    await task();
                }
                catch (Exception ex) when (ExceptionFilterUtility.False(() => log(ex)))
                {
                    throw;
                }
            });
    }
}
