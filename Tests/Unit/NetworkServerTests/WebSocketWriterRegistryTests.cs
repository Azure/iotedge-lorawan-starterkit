// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Unit.NetworkServerTests
{
    using System;
    using System.Collections.Generic;
    using System.Net.WebSockets;
    using System.Threading;
    using System.Threading.Tasks;
    using LoRaWan.NetworkServer;
    using Microsoft.Extensions.Logging.Abstractions;
    using Moq;
    using Xunit;

    public class WebSocketWriterRegistryTests
    {
        private readonly WebSocketWriterRegistry<string, string> sut;

        public WebSocketWriterRegistryTests()
        {
            this.sut = new WebSocketWriterRegistry<string, string>(NullLogger<WebSocketWriterRegistry<string, string>>.Instance);
        }

        [Fact]
        public void Register_Is_Idempotent()
        {
            // arrange
            var writer = new Mock<IWebSocketWriter<string>>();

            // act
            IWebSocketWriterHandle<string> Act() => this.sut.Register("foo", writer.Object);
            var firstHandle = Act();
            var secondHandle = Act();

            // assert
            Assert.Same(firstHandle, secondHandle);
        }

        [Fact]
        public async Task Register_Overwrites_Previous_Writer_When_Given_New_Writer()
        {
            // arrange
            const string key = "foo";
            var oldWriter = new Mock<IWebSocketWriter<string>>();
            var oldHandler = this.sut.Register(key, oldWriter.Object);
            var newWriter = new Mock<IWebSocketWriter<string>>();

            // act
            var newHandler = this.sut.Register(key, newWriter.Object);

            // assert
            Assert.Same(oldHandler, newHandler);
            // new handler/writer is used for sending
            await newHandler.SendAsync("bar", CancellationToken.None);
            oldWriter.Verify(w => w.SendAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
            newWriter.Verify(w => w.SendAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task SendAsync_Succeeds_When_Writer_Is_Registered()
        {
            // arrange
            using var cts = new CancellationTokenSource();
            const string message = "bar";
            var (handle, writer) = CreateAndRegisterWebSocketWriterMock("foo");

            // act
            await handle.SendAsync(message, cts.Token);

            // assert
            writer.Verify(sw => sw.SendAsync(message, cts.Token), Times.Once);
        }

        [Fact]
        public async Task SendAsync_Deregisters_When_Connection_Is_Closed_Prematurely()
        {
            // arrange
            var (handle, writer) = CreateAndRegisterWebSocketWriterMock("foo");
            writer.Setup(w => w.SendAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                  .Throws(new WebSocketException(WebSocketError.ConnectionClosedPrematurely));

            // act
            await handle.SendAsync("bar", CancellationToken.None);

            // assert
            await CustomAssert.WriterIsNotRegistered(handle);
        }

        [Fact]
        public async Task Deregister_Removes_And_Returns_Registered_Writer()
        {
            // arrange
            const string key = "foo";
            var (handle, writer) = CreateAndRegisterWebSocketWriterMock(key);

            // act
            var deregisteredWriter = this.sut.Deregister(key);

            // assert
            await CustomAssert.WriterIsNotRegistered(handle);
            Assert.Same(writer.Object, deregisteredWriter);
        }

        [Fact]
        public async Task Deregister_Is_Idempotent()
        {
            // arrange
            const string key = "foo";
            var (handle, writer) = CreateAndRegisterWebSocketWriterMock(key);

            // act
            var firstWriter = this.sut.Deregister(key);
            var secondWriter = this.sut.Deregister(key);

            // assert
            Assert.Same(writer.Object, firstWriter);
            Assert.Null(secondWriter);
            await CustomAssert.WriterIsNotRegistered(handle);
        }

        [Fact]
        public async Task Prune_Removes_Closed_Entries_Only()
        {
            // arrange
            const string staleKey = "foo";
            var (staleHandle, staleWriter) = CreateAndRegisterWebSocketWriterMock(staleKey);
            staleWriter.SetupGet(w => w.IsClosed).Returns(true);
            const string activeKey = "bar";
            var (activeHandle, activeWriter) = CreateAndRegisterWebSocketWriterMock(activeKey);
            activeWriter.SetupGet(w => w.IsClosed).Returns(false);

            // act
            var result = this.sut.Prune();

            // assert
            var prunedKey = Assert.Single(result);
            Assert.Equal(staleKey, prunedKey);
            await CustomAssert.WriterIsNotRegistered(staleHandle);
            await CustomAssert.WriterIsRegistered(activeHandle, activeWriter);
        }

        [Fact]
        public async Task RunPrunerAsync_Prunes_Until_Canceled()
        {
            // arrange
            (IWebSocketWriterHandle<string>, Mock<IWebSocketWriter<string>>)
                CreateWebSocketWriterMock(string key, bool firstIsClosed, bool secondIsClosed)
            {
                var (handle, result) = CreateAndRegisterWebSocketWriterMock(key);
                _ = result.SetupSequence(w => w.IsClosed).Returns(firstIsClosed).Returns(secondIsClosed);
                return (handle, result);
            }

            var (staleHandle, _) = CreateWebSocketWriterMock("foo", true, true);
            var (transitioningHandle, _) = CreateWebSocketWriterMock("bar", false, true);
            var (activeHandle, activeWriter) = CreateWebSocketWriterMock("baz", false, false);
            using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

            // act
            Task Act() => this.sut.RunPrunerAsync(TimeSpan.Zero, cts.Token);

            // assert
            await Assert.ThrowsAsync<TaskCanceledException>(Act);
            await CustomAssert.WriterIsNotRegistered(staleHandle);
            await CustomAssert.WriterIsNotRegistered(transitioningHandle);
            await CustomAssert.WriterIsRegistered(activeHandle, activeWriter);
        }

        [Fact]
        public void TryGetHandle_Returns_False_With_Null_Handle_When_Not_Registered_Under_Given_Key()
        {
            var succeeded = this.sut.TryGetHandle("foo", out var handle);

            Assert.False(succeeded);
            Assert.Null(handle);
        }

        [Fact]
        public void TryGetHandle_Returns_True_With_Handle_Registered_Under_Key()
        {
            const string key = "key";
            var (initialHandle, _) = CreateAndRegisterWebSocketWriterMock(key);

            var succeeded = this.sut.TryGetHandle(key, out var handle);

            Assert.True(succeeded);
            Assert.Same(initialHandle, handle);
        }

        private (IWebSocketWriterHandle<string> Handle, Mock<IWebSocketWriter<string>> WriterMock)
            CreateAndRegisterWebSocketWriterMock(string key)
        {
            var writerMock = new Mock<IWebSocketWriter<string>>();
            return (this.sut.Register(key, writerMock.Object), writerMock);
        }

        private static class CustomAssert
        {
            public static Task WriterIsNotRegistered(IWebSocketWriterHandle<string> handle) =>
                Assert.ThrowsAsync<KeyNotFoundException>(async () => await handle.SendAsync("bar", CancellationToken.None));

            public static async Task WriterIsRegistered(IWebSocketWriterHandle<string> handle,
                                                        Mock<IWebSocketWriter<string>> writer)
            {
                const string message = "baz";
                await handle.SendAsync(message, CancellationToken.None);
                writer.Verify(w => w.SendAsync(message, CancellationToken.None), Times.Once);
            }
        }
    }
}
