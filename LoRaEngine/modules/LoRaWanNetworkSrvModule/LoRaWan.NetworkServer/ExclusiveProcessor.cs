// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

#pragma warning disable CA1003 // Use generic event handler instances (suppressed for performance)

namespace LoRaWan.NetworkServer
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    public sealed class ExclusiveProcessor<T>
    {
        private readonly IScheduler scheduler;
        private readonly IEqualityComparer<T> comparer;
        private readonly SemaphoreSlim processingLock = new(1);
        private readonly List<T> queue = new();

        public event EventHandler<T>? Submitted;
        public event EventHandler<(T InterruptedProcessor, T InterruptingProcessor)>? Interrupted;
        public event EventHandler<T>? Processing;
        public event EventHandler<(T Processor, IProcessingOutcome Outcome)>? Processed;

#pragma warning disable CA1034 // Nested types should not be visible (tightly related)
        public interface IScheduler
#pragma warning restore CA1034 // Nested types should not be visible
        {
            T SelectNext(IReadOnlyList<T> processes);
        }

        public static readonly IScheduler DefaultScheduler = new FifoScheduler();

        private sealed class FifoScheduler : IScheduler
        {
            public T SelectNext(IReadOnlyList<T> processes) => processes[0];
        }

        public ExclusiveProcessor() : this(DefaultScheduler) { }

        public ExclusiveProcessor(IScheduler scheduler) : this(scheduler, null) { }

        public ExclusiveProcessor(IScheduler scheduler, IEqualityComparer<T>? comparer)
        {
            this.scheduler = scheduler;
            this.comparer = comparer ?? EqualityComparer<T>.Default;
        }

#pragma warning disable CA1034 // Nested types should not be visible (by design)

        public interface IProcessingOutcome
        {
            Task Task { get; }
            DateTimeOffset SubmissionTime { get; }
            TimeSpan WaitDuration { get; }
            TimeSpan RunDuration { get; }
        }

        public record ProcessingOutcome<TResult>(TResult Result,
                                                 DateTimeOffset SubmissionTime,
                                                 TimeSpan WaitDuration,
                                                 TimeSpan RunDuration)
        {
#pragma warning disable CA1062 // Validate arguments of public methods (operators don't usually throw)
            public static implicit operator TResult(ProcessingOutcome<TResult> outcome) => outcome.Result;
#pragma warning restore CA1062 // Validate arguments of public methods
        }

#pragma warning restore CA1034 // Nested types should not be visible

        private sealed record ProcessingTaskOutcome<TResult> :
            ProcessingOutcome<Task<TResult>>,
            IProcessingOutcome
        {
            public ProcessingTaskOutcome(Task<TResult> task,
                                         DateTimeOffset submissionTime,
                                         TimeSpan waitDuration,
                                         TimeSpan runDuration) :
                base(task, submissionTime, waitDuration, runDuration)
            { }

            Task IProcessingOutcome.Task => Result;
        }

        public async Task<ProcessingOutcome<TResult>> ProcessAsync<TResult>(T processor, Func<Task<TResult>> function)
        {
            var outcome = await TryProcessAsync(processor, function);
            return new ProcessingOutcome<TResult>(await outcome.Result, outcome.SubmissionTime, outcome.WaitDuration, outcome.RunDuration);
        }

        public async Task<ProcessingOutcome<Task<TResult>>> TryProcessAsync<TResult>(T processor, Func<Task<TResult>> function)
        {
            ArgumentNullException.ThrowIfNull(function, nameof(function));

            var submissionTime = DateTime.UtcNow;

            lock (this.queue)
                this.queue.Add(processor);

            Submitted?.Invoke(this, processor);

            while (true)
            {
                await this.processingLock.WaitAsync();

                try
                {
                    (bool, T) next = default;

                    lock (this.queue)
                    {
                        var nextProcessor = this.scheduler.SelectNext(this.queue);
                        if (!this.comparer.Equals(nextProcessor, processor))
                            next = (true, nextProcessor);
                        else
                            _ = this.queue.Remove(processor);
                    }

                    if (next is (true, { } someNextProcessor))
                    {
                        Interrupted?.Invoke(this, (processor, someNextProcessor));
                    }
                    else
                    {
                        Processing?.Invoke(this, processor);
                        var startTime = DateTime.UtcNow;
                        var task = await Task.WhenAny(function());
                        var endTime = DateTime.UtcNow;
                        var outcome = new ProcessingTaskOutcome<TResult>(task, submissionTime, startTime - submissionTime, endTime - startTime);
                        Processed?.Invoke(this, (processor, outcome));
                        return outcome;
                    }
                }
                finally
                {
                    _ = this.processingLock.Release();
                }
            }
        }
    }
}
