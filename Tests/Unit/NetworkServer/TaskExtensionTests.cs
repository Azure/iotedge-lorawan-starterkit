// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

namespace LoRaWan.Tests.Unit.NetworkServer
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using LoRaWan.NetworkServer;
    using LoRaWan.Tests.Common;
    using Xunit;

    public sealed class TaskExtensionTests
    {
        [Fact]
        public void GetExceptions_When_Task_Is_Not_Completed_Throws()
        {
            var tcs = new TaskCompletionSource();
            var ex = Assert.Throws<ArgumentException>(() => new[] { tcs.Task }.GetExceptions());
            Assert.Equal("tasks", ex.ParamName);
            Assert.Contains("All tasks must have completed.", ex.Message, StringComparison.OrdinalIgnoreCase);
        }

        public static TheoryData<Task[], Exception[]> GetExceptions_Success_TheoryData() => TheoryDataFactory.From(new[]
        {
            (new[] { Task.FromException(new InvalidOperationException("A")), Task.FromException(new LoRaProcessingException("B")) },
             new Exception[] { new InvalidOperationException("A"), new LoRaProcessingException("B") }),
            (new[] { Task.CompletedTask, Task.Run(async () => { await Task.Yield(); throw new InvalidOperationException("A"); }) },
             new Exception[] { new InvalidOperationException("A") }),
            (new[] { Task.FromException(new AggregateException(new InvalidOperationException("A"))), Task.FromException(new LoRaProcessingException("B")) },
             new Exception[] { new AggregateException(new InvalidOperationException("A")), new LoRaProcessingException("B") }),
            (new[] { Task.FromException(new OperationCanceledException("A")), Task.CompletedTask },
             new Exception[] { new OperationCanceledException("A") }),
            (new[] { Task.FromException(new OperationCanceledException("A")), Task.FromException(new InvalidOperationException("B")) },
             new Exception[] { new OperationCanceledException("A"), new InvalidOperationException("B") }),
            (new[] { Task.Run(() =>
                     {
                         var tcs = new TaskCompletionSource();
                         tcs.SetException(new Exception[] { new OperationCanceledException("A"), new InvalidOperationException("B") });
                         return tcs.Task;
                     }) },
             new Exception[] { new AggregateException(new OperationCanceledException("A"), new InvalidOperationException("B")) })
        });

        [Theory]
        [MemberData(nameof(GetExceptions_Success_TheoryData))]
        public async Task GetExceptions_Success(Task[] tasks, Exception[] expectedExceptions)
        {
            // arrange
            _ = await Task.WhenAny(Task.WhenAll(tasks));

            // act
            var result = tasks.GetExceptions();

            // assert
            Assert.Equal(expectedExceptions.Select(e => e.GetType()), result.Select(e => e.GetType()));
            Assert.Equal(expectedExceptions.Select(e => e.Message), result.Select(e => e.Message));
        }

        [Fact]
        public void TryGetCanceledException_Returns_Null_If_Not_Canceled()
        {
            var tcs = new TaskCompletionSource();
            Assert.False(tcs.Task.TryGetCanceledException(out var ex));
            Assert.Null(ex);
        }

        public static TheoryData<Exception> TryGetCanceledException_Returns_Exception_TheoryData() => TheoryDataFactory.From(new Exception[]
        {
            new OperationCanceledException("A"),
            new TaskCanceledException("B")
        });

        [Theory]
        [MemberData(nameof(TryGetCanceledException_Returns_Exception_TheoryData))]
        public async Task TryGetCanceledException_Returns_Exception(Exception exception)
        {
            // arrange
            var t = Task.Run(() => throw exception);
            _ = await Task.WhenAny(t);

            // act + assert
            Assert.True(t.TryGetCanceledException(out var result));
            Assert.IsType(exception.GetType(), result);
            Assert.Equal(exception.Message, result!.Message);
        }
    }
}
