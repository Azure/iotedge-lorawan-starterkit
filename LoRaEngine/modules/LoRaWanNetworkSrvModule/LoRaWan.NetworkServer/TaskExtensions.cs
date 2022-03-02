// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

namespace LoRaWan.NetworkServer
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.Linq;
    using System.Threading.Tasks;

    internal static class TaskExtensions
    {
        public static IList<Exception> GetExceptions(this ICollection<Task> tasks)
        {
            if (tasks.Any(static t => !t.IsCompleted)) throw new ArgumentException("All tasks must have completed.", nameof(tasks));

            var result = new List<Exception>();

            foreach (var task in tasks)
            {
                if (task.IsCompletedSuccessfully)
                    continue;

                if (task.IsCanceled && task.TryGetCanceledException(out var ex))
                    result.Add(ex);

                if (task is { IsFaulted: true, Exception: { } aggregateException })
                    result.Add(aggregateException.InnerExceptions is { Count: 1 } exceptions ? exceptions[0] : aggregateException);
            }

            return result;
        }

        public static bool TryGetCanceledException(this Task task, [NotNullWhen(true)] out OperationCanceledException? exception)
        {
            exception = null;

            if (!task.IsCanceled)
                return false;

            try
            {
                task.GetAwaiter().GetResult();
                return false;
            }
            catch (OperationCanceledException ex)
            {
                exception = ex;
                return true;
            }
        }
    }
}
