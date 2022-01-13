// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Unit
{
    using Moq;
    using System;
    using Xunit;

    public class ExceptionFilterUtilityTests
    {
        [Fact]
        public void True_SuccessCase()
        {
            // arrange
            var action = new Mock<Action>();

            // act
            var result = ExceptionFilterUtility.True(action.Object);

            // assert
            Assert.True(result);
            action.Verify(a => a.Invoke(), Times.Once);
        }

        [Fact]
        public void True_Two_Arguments_SuccessCase()
        {
            // arrange
            var action = new Mock<Action>();

            // act
            var result = ExceptionFilterUtility.True(action.Object, action.Object);

            // assert
            Assert.True(result);
            action.Verify(a => a.Invoke(), Times.Exactly(2));
        }

        [Fact]
        public void False_SuccessCase()
        {
            // arrange
            var action = new Mock<Action>();

            // act
            var result = ExceptionFilterUtility.False(action.Object);

            // assert
            Assert.False(result);
            action.Verify(a => a.Invoke(), Times.Once);
        }

        [Fact]
        public void False_Two_Arguments_SuccessCase()
        {
            // arrange
            var action = new Mock<Action>();

            // act
            var result = ExceptionFilterUtility.False(action.Object, action.Object);

            // assert
            Assert.False(result);
            action.Verify(a => a.Invoke(), Times.Exactly(2));
        }
    }
}
