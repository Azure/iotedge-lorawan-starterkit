// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Unit.NetworkServer
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using LoRaWan.NetworkServer;
    using Xunit;
    using static MoreLinq.Extensions.IndexExtension;
    using static MoreLinq.Extensions.ShuffleExtension;

    public class ExclusiveProcessorTests
    {
        private readonly ExclusiveProcessor<int> subject = new();
        private readonly DateTimeOffset testStartTime = DateTimeOffset.UtcNow;

        [Fact]
        public async Task TryProcessAsync_With_Successful_Function_Returns_Outcome()
        {
            var outcome = await this.subject.TryProcessAsync(1, () => Task.FromResult(42));

            Assert.True(outcome.SubmissionTime >= this.testStartTime);
            Assert.NotEqual(TimeSpan.Zero, outcome.WaitDuration);
            Assert.NotEqual(TimeSpan.Zero, outcome.RunDuration);
            Assert.True(outcome.Result.IsCompletedSuccessfully);
            Assert.Equal(42, await outcome.Result);
        }

        [Fact]
        public async Task TryProcessAsync_With_Canceled_Function_Returns_Outcome()
        {
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            var outcome = await this.subject.TryProcessAsync(1, () => Task.FromCanceled<int>(cts.Token));

            Assert.True(outcome.SubmissionTime >= this.testStartTime);
            Assert.NotEqual(TimeSpan.Zero, outcome.WaitDuration);
            Assert.NotEqual(TimeSpan.Zero, outcome.RunDuration);
            Assert.True(outcome.Result.IsCanceled);
        }

        [Fact]
        public async Task TryProcessAsync_With_Erroneous_Function_Returns_Outcome()
        {
#pragma warning disable CA2201 // Do not raise reserved exception types
            var ex = new ApplicationException();
#pragma warning restore CA2201 // Do not raise reserved exception types

            var outcome = await this.subject.TryProcessAsync(1, () => Task.FromException<int>(ex));

            Assert.True(outcome.SubmissionTime >= this.testStartTime);
            Assert.NotEqual(TimeSpan.Zero, outcome.WaitDuration);
            Assert.NotEqual(TimeSpan.Zero, outcome.RunDuration);
            Assert.True(outcome.Result.IsFaulted);
            var aggregateException = outcome.Result.Exception;
            Assert.NotNull(aggregateException);
            Assert.Same(ex, Assert.Single(aggregateException.InnerExceptions));
        }

        [Fact]
        public async Task TryProcessAsync_Raises_Events()
        {
            (object Sender, int Args) submittedEvent = default;
            (object Sender, int Args) processingEvent = default;
            (object Sender, (int, ExclusiveProcessor<int>.IProcessingOutcome) Args) processedEvent = default;

            this.subject.Submitted += (sender, args) => submittedEvent = (sender, args);
            this.subject.Processing += (sender, args) => processingEvent = (sender, args);
            this.subject.Processed += (sender, args) => processedEvent = (sender, args);

            const int id = 1;
            var outcome = await this.subject.TryProcessAsync(id, () => Task.FromResult(42));

            Assert.Same(this.subject, submittedEvent.Sender);
            Assert.Equal(id, submittedEvent.Args);

            Assert.Same(this.subject, processingEvent.Sender);
            Assert.Equal(id, processingEvent.Args);

            Assert.Same(this.subject, processedEvent.Sender);
            var (_, (processedId, processedOutcome)) = processedEvent;
            Assert.Equal(id, processedId);
            Assert.Equal((object)outcome, processedOutcome);
        }

        [Fact]
        public async Task ProcessAsync_With_Successful_Function_Returns_Outcome()
        {
            var outcome = await this.subject.ProcessAsync(1, () => Task.FromResult(42));

            Assert.True(outcome.SubmissionTime >= this.testStartTime);
            Assert.NotEqual(TimeSpan.Zero, outcome.WaitDuration);
            Assert.NotEqual(TimeSpan.Zero, outcome.RunDuration);
            Assert.Equal(42, outcome.Result);
        }

        [Fact]
        public async Task ProcessAsync_With_Canceled_Function_Throws_TaskCanceledException()
        {
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            var ex = await Assert.ThrowsAsync<TaskCanceledException>(() =>
                this.subject.ProcessAsync(1, () => Task.FromCanceled<int>(cts.Token)));

            Assert.Equal(cts.Token, ex.CancellationToken);
        }

        [Fact]
        public async Task ProcessAsync_With_Erroneous_Function_Throws_Thrown_Exception()
        {
#pragma warning disable CA2201 // Do not raise reserved exception types
            var ex = new ApplicationException();
#pragma warning restore CA2201 // Do not raise reserved exception types

            var actual = await Assert.ThrowsAsync<ApplicationException>(() =>
                this.subject.ProcessAsync(1, () => Task.FromException<int>(ex)));

            Assert.Same(ex, actual);
        }

        [Fact]
        public async Task ProcessAsync_Raises_Events()
        {
            (object Sender, int Args) submittedEvent = default;
            (object Sender, int Args) processingEvent = default;
            (object Sender, (int, ExclusiveProcessor<int>.IProcessingOutcome) Args) processedEvent = default;

            this.subject.Submitted += (sender, args) => submittedEvent = (sender, args);
            this.subject.Processing += (sender, args) => processingEvent = (sender, args);
            this.subject.Processed += (sender, args) => processedEvent = (sender, args);

            const int id = 1;
            var outcome = await this.subject.ProcessAsync(id, () => Task.FromResult(42));

            Assert.Same(this.subject, submittedEvent.Sender);
            Assert.Equal(id, submittedEvent.Args);

            Assert.Same(this.subject, processingEvent.Sender);
            Assert.Equal(id, processingEvent.Args);

            Assert.Same(this.subject, processedEvent.Sender);
            var (_, (processedId, processedOutcome)) = processedEvent;
            Assert.Equal(id, processedId);
            Assert.True(processedOutcome.Task.IsCompletedSuccessfully);
            Assert.Equal(outcome.SubmissionTime, processedOutcome.SubmissionTime);
            Assert.Equal(outcome.WaitDuration, processedOutcome.WaitDuration);
            Assert.Equal(outcome.RunDuration, processedOutcome.RunDuration);
        }

        [Theory]
        [InlineData(10)]
        public async Task ProcessAsync_Is_Fifo_By_Default(int processorCount)
        {
            var processorIds = Enumerable.Range(1, processorCount).ToArray();
            var (submittedList, processedList) = await TestProcessing(this.subject, processorIds);

            Assert.Equal(processedList, submittedList);
            Assert.Equal(processedList, processorIds);
        }

        private sealed class LifoScheduler<T> : ExclusiveProcessor<T>.IScheduler
        {
            public List<T> SelectionList { get; } = new();

            public T SelectNext(IReadOnlyList<T> processes)
            {
                var next = processes[^1];
                if (SelectionList.Count == 0 || !EqualityComparer<T>.Default.Equals(SelectionList[^1], next))
                    SelectionList.Add(next);
                return next;
            }
        }

        [Theory]
        [InlineData(10)]
        public async Task ProcessAsync_Uses_Scheduler(int processorCount)
        {
            var processorIds = Enumerable.Range(1, processorCount).ToArray();
            var scheduler = new LifoScheduler<int>();
            var subject = new ExclusiveProcessor<int>(scheduler);
            var (submittedList, processedList) = await TestProcessing(subject, processorIds);

            Assert.Equal(submittedList, processorIds);
            Assert.Equal(scheduler.SelectionList, processedList);
        }

        private static async Task<(List<int> SubmittedList, List<int> ProcessedList)>
            TestProcessing(ExclusiveProcessor<int> subject, IEnumerable<int> processorIds)
        {
            var submittedList = new List<int>();
            var processedList = new List<int>();

            subject.Submitted += (_, args) => submittedList.Add(args);
            subject.Processed += (_, args) => processedList.Add(args.Processor);

            var processors = processorIds.Select(id => (Id: id, TaskCompletionSource: new TaskCompletionSource<string>()))
                                         .ToArray();

            var tasks = new Task<ExclusiveProcessor<int>.ProcessingOutcome<string>>[processors.Length];

            foreach (var (i, (id, process)) in processors.Index())
            {
                tasks[i] = subject.ProcessAsync(id, () => process.Task);

                Assert.Equal(i + 1, submittedList.Count);
                Assert.Empty(processedList);
            }

            var task = Task.WhenAll(tasks);

            var results = processors.Select(e => e.Id.ToString(CultureInfo.InvariantCulture)).ToArray();

            // Complete each in some random (shuffled) order.

            foreach (var ((_, processor), result) in processors.Zip(results).Shuffle())
                processor.SetResult(result);

            Assert.Equal(results, from outcome in await task select outcome.Result);

            return (submittedList, processedList);
        }
    }
}
