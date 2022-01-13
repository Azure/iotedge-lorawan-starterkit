// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Unit.NetworkServer
{
    using System;
    using System.Threading.Tasks;
    using LoRaWan.NetworkServer;
    using Moq;
    using Xunit;

    public class TaskUtilTests
    {
        [Fact]
        public async Task When_Failure_Invokes_Logging_Action()
        {
            // arrange
            var log = new Mock<Action<Exception>>();
#pragma warning disable CA2201 // Do not raise reserved exception types (tests general exception handling)
            var ex = new Exception("some exception");
#pragma warning restore CA2201 // Do not raise reserved exception types

            // act
            Task Act() => TaskUtil.RunOnThreadPool(() => throw ex, log.Object, null);

            // assert
            var actual = await Assert.ThrowsAsync<Exception>(Act);
            Assert.Same(actual, ex);
            log.Verify(l => l.Invoke(ex), Times.Once);
        }

        [Fact]
        public async Task When_Success_Does_Not_Invoke_Logging_Action()
        {
            // arrange
            var log = new Mock<Action<Exception>>();

            // act
            await TaskUtil.RunOnThreadPool(() => Task.CompletedTask, log.Object, null);

            // assert
            log.Verify(l => l.Invoke(It.IsAny<Exception>()), Times.Never);
        }
    }
}
