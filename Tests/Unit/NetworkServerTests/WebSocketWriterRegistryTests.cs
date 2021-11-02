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
            WebSocketWriterHandle<string, string> Act() => this.sut.Register("foo", writer.Object);
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
            await this.sut.SendAsync(key, "bar", CancellationToken.None);
            oldWriter.Verify(w => w.SendAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
            newWriter.Verify(w => w.SendAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task SendAsync_Succeeds_When_Writer_Is_Registered()
        {
            // arrange
            using var cts = new CancellationTokenSource();
            const string key = "foo";
            const string message = "bar";
            var writer = CreateAndRegisterWebSocketWriterMock(key);

            // act
            await this.sut.SendAsync(key, message, cts.Token);

            // assert
            writer.Verify(sw => sw.SendAsync(message, cts.Token), Times.Once);
        }

        [Fact]
        public async Task SendAsync_Throws_When_Writer_Is_Not_Registered()
        {
            await CustomAssert.WriterIsNotRegistered(this.sut, "foo");
        }

        [Fact]
        public async Task SendAsync_Deregisters_When_Connection_Is_Closed_Prematurely()
        {
            // arrange
            const string key = "foo";
            var writer = CreateAndRegisterWebSocketWriterMock(key);
            writer.Setup(w => w.SendAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                  .Throws(new WebSocketException(WebSocketError.ConnectionClosedPrematurely));

            // act
            await this.sut.SendAsync(key, "bar", CancellationToken.None);

            // assert
            await CustomAssert.WriterIsNotRegistered(this.sut, key);
        }

        [Fact]
        public async Task Deregister_Removes_And_Returns_Registered_Writer()
        {
            // arrange
            const string key = "foo";
            var writer = CreateAndRegisterWebSocketWriterMock(key);

            // act
            var deregisteredWriter = this.sut.Deregister(key);

            // assert
            await CustomAssert.WriterIsNotRegistered(this.sut, key);
            Assert.Same(writer.Object, deregisteredWriter);
        }

        [Fact]
        public async Task Deregister_Is_Idempotent()
        {
            // arrange
            const string key = "foo";
            var writer = CreateAndRegisterWebSocketWriterMock(key);

            // act
            var firstDeregistration = this.sut.Deregister(key);
            var secondDeregistration = this.sut.Deregister(key);

            // assert
            Assert.Same(writer.Object, firstDeregistration);
            Assert.Null(secondDeregistration);
            await CustomAssert.WriterIsNotRegistered(this.sut, key);
        }

        [Fact]
        public async Task Prune_Removes_Closed_Entries_Only()
        {
            // arrange
            const string staleKey = "foo";
            var staleWebSocketWriter = CreateAndRegisterWebSocketWriterMock(staleKey);
            staleWebSocketWriter.SetupGet(w => w.IsClosed).Returns(true);
            const string activeKey = "bar";
            var activeWebSocketWriter = CreateAndRegisterWebSocketWriterMock(activeKey);
            activeWebSocketWriter.SetupGet(w => w.IsClosed).Returns(false);

            // act
            var result = this.sut.Prune();

            // assert
            var prunedKey = Assert.Single(result);
            Assert.Equal(staleKey, prunedKey);
            await CustomAssert.WriterIsNotRegistered(this.sut, staleKey);
            await CustomAssert.WriterIsRegistered(this.sut, activeKey, activeWebSocketWriter);
        }

        [Fact]
        public async Task RunPrunerAsync_Prunes_Until_Canceled()
        {
            // arrange
            Mock<IWebSocketWriter<string>> CreateWebSocketWriterMock(string key, bool firstIsClosed, bool secondIsClosed)
            {
                var result = CreateAndRegisterWebSocketWriterMock(key);
                _ = result.SetupSequence(w => w.IsClosed).Returns(firstIsClosed).Returns(secondIsClosed);
                return result;
            }

            const string staleKey = "foo";
            _ = CreateWebSocketWriterMock(staleKey, true, true);
            const string transitioningKey = "bar";
            _ = CreateWebSocketWriterMock(transitioningKey, false, true);
            const string activeKey = "baz";
            var activeWebSocketWriter = CreateWebSocketWriterMock(activeKey, false, false);
            using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

            // act
            Task Act() => this.sut.RunPrunerAsync(TimeSpan.Zero, cts.Token);

            // assert
            await Assert.ThrowsAsync<TaskCanceledException>(Act);
            await CustomAssert.WriterIsNotRegistered(this.sut, staleKey);
            await CustomAssert.WriterIsNotRegistered(this.sut, transitioningKey);
            await CustomAssert.WriterIsRegistered(this.sut, activeKey, activeWebSocketWriter);
        }

        private Mock<IWebSocketWriter<string>> CreateAndRegisterWebSocketWriterMock(string key)
        {
            var result = new Mock<IWebSocketWriter<string>>();
            this.sut.Register(key, result.Object);
            return result;
        }

        private static class CustomAssert
        {
            public static Task WriterIsNotRegistered(WebSocketWriterRegistry<string, string> webSocketWriterRegistry, string key) =>
                Assert.ThrowsAsync<KeyNotFoundException>(async () => await webSocketWriterRegistry.SendAsync(key, "bar", CancellationToken.None));

            public static async Task WriterIsRegistered(WebSocketWriterRegistry<string, string> webSocketWriterRegistry,
                                                        string key,
                                                        Mock<IWebSocketWriter<string>> webSocketWriter)
            {
                const string message = "baz";
                await webSocketWriterRegistry.SendAsync(key, message, CancellationToken.None);
                webSocketWriter.Verify(w => w.SendAsync(message, CancellationToken.None), Times.Once);
            }
        }
    }
}
