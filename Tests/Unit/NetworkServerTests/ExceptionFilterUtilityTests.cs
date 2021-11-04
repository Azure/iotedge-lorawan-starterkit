// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Unit.NetworkServerTests
{
    using LoRaWan.NetworkServer;
    using Moq;
    using Xunit;

    public class ExceptionFilterUtilityTests
    {
        [Fact]
        public void True_SuccessCase()
        {
            // arrange
            var action = new Mock<IAction>();

            // act
            var result = ExceptionFilterUtility.True(action.Object.Act);

            // assert
            Assert.True(result);
            action.Verify(a => a.Act(), Times.Once);
        }

        [Fact]
        public void False_SuccessCase()
        {
            // arrange
            var action = new Mock<IAction>();

            // act
            var result = ExceptionFilterUtility.False(action.Object.Act);

            // assert
            Assert.False(result);
            action.Verify(a => a.Act(), Times.Once);
        }

        public interface IAction
        {
            void Act();
        }
    }
}
