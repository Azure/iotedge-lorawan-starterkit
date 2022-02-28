// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

namespace LoRaWan.Tests.Unit.NetworkServer
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using LoRaWan.NetworkServer;
    using Moq;
    using Xunit;

    public class DisposableExtensionsTests
    {
        [Fact]
        public async Task DisposeAllAsync_With_Null_Disposables_Throws()
        {
            var ex = await Assert.ThrowsAsync<ArgumentNullException>(async () =>
                await DisposableExtensions.DisposeAllAsync(null!, 0));

            Assert.Equal("disposables", ex.ParamName);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public async Task DisposeAllAsync_With_Invalid_Concurrency_Throws(int concurrency)
        {
            var ex = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () =>
                await Enumerable.Empty<IAsyncDisposable>().DisposeAllAsync(concurrency));

            Assert.Equal("concurrency", ex.ParamName);
            Assert.Equal(concurrency, ex.ActualValue);
        }

        [Fact]
        public void DisposeAllAsync_With_Empty_Sequence_Completes_Immediately()
        {
            var task = Array.Empty<IAsyncDisposable>().DisposeAllAsync(42);
            Assert.True(task.IsCompletedSuccessfully);
        }

        public enum DisposablesSourceKind { Sequence, Array, ReadOnlyCollection }

        [Theory]
        [InlineData(DisposablesSourceKind.Sequence, 10, 1)]
        [InlineData(DisposablesSourceKind.Sequence, 10, 5)]
        [InlineData(DisposablesSourceKind.Sequence, 10, 10)]
        [InlineData(DisposablesSourceKind.Array, 10, 1)]
        [InlineData(DisposablesSourceKind.Array, 10, 5)]
        [InlineData(DisposablesSourceKind.Array, 10, 10)]
        [InlineData(DisposablesSourceKind.ReadOnlyCollection, 10, 1)]
        [InlineData(DisposablesSourceKind.ReadOnlyCollection, 10, 5)]
        [InlineData(DisposablesSourceKind.ReadOnlyCollection, 10, 10)]
        public async Task DisposeAllAsync_Disposes_Each_Disposable(DisposablesSourceKind sourceKind, int count, int concurrency)
        {
            var disposableMocks = Enumerable.Range(1, count).Select(_ => new Mock<IAsyncDisposable>()).ToArray();

            var disposables = disposableMocks.Select(m => m.Object);
            switch (sourceKind)
            {
                case DisposablesSourceKind.Sequence:
                    break;
                case DisposablesSourceKind.Array:
                    disposables = disposables.ToArray();
                    break;
                case DisposablesSourceKind.ReadOnlyCollection:
                    disposables = disposables.ToArray().WrapInReadOnlyCollection();
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(sourceKind), sourceKind, null);
            }
            await disposables.DisposeAllAsync(concurrency);

            foreach (var disposableMock in disposableMocks)
                disposableMock.Verify(x => x.DisposeAsync(), Times.Once);
        }

        [Theory]
        [InlineData(10, 1)]
        [InlineData(10, 2)]
        [InlineData(10, 3)]
        [InlineData(10, 4)]
        [InlineData(10, 5)]
        [InlineData(10, 6)]
        [InlineData(10, 7)]
        [InlineData(10, 8)]
        [InlineData(10, 9)]
        [InlineData(10, 10)]
        [InlineData(10, 11)]
        public async Task DisposeAllAsync_Respects_Requested_Concurrency(int count, int concurrency)
        {
            var disposableMocks = Enumerable.Range(1, count).Select(_ => new Mock<IAsyncDisposable>()).ToArray();

            using var semaphore = new SemaphoreSlim(0, concurrency);
            var flights = 0;

            async ValueTask DisposeAsync(Task task)
            {
                // Release semaphore when # of call in flight equal concurrency.
                if (concurrency == Interlocked.Increment(ref flights))
                    semaphore.Release();

                await task;
            }

            var tcsQueue = new Queue<TaskCompletionSource>();
            foreach (var disposableMock in disposableMocks)
            {
                var tcs = new TaskCompletionSource();
                tcsQueue.Enqueue(tcs);
                disposableMock.Setup(x => x.DisposeAsync()).Returns(() => DisposeAsync(tcs.Task));
            }

            var task = disposableMocks.Select(m => m.Object).DisposeAllAsync(concurrency);

            for (var remaining = count - concurrency; remaining > 0; remaining--)
            {
                // Wait until expected # of calls are in flight
                Assert.True(await semaphore.WaitAsync(TimeSpan.FromSeconds(5)));
                Assert.Equal(concurrency, flights);
                // Decrement calls in flight because one will be completed.
                flights--;
                tcsQueue.Dequeue().SetResult();
            }

            Assert.Equal(Math.Min(concurrency, count), tcsQueue.Count);

            // Complete all remaining tasks.
            while (tcsQueue.Count > 0)
                tcsQueue.Dequeue().SetResult();

            await task;
        }
    }
}
