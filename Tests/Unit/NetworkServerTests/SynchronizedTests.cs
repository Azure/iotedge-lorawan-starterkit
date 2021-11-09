// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Unit.NetworkServer
{
    using LoRaWan.NetworkServer;
    using Xunit;

    public class SynchronizedTests
    {
        [Fact]
        public void Write_Returns_False_If_Value_Is_Same()
        {
            // arrange
            const string value = "foo";
            var synchronized = new Synchronized<string>(value);

            // act
            var result = synchronized.Write(value);

            // assert
            Assert.False(result);
            Assert.Equal(value, synchronized.Read());
        }

        [Fact]
        public void Write_Returns_True_If_Value_Is_Same()
        {
            // arrange
            const string value = "bar";
            var synchronized = new Synchronized<string>("foo");

            // act
            var result = synchronized.Write(value);

            // assert
            Assert.True(result);
            Assert.Equal(value, synchronized.Read());
        }

        [Fact]
        public void Read_Success()
        {
            // arrange
            const string value = "foo";
            var synchronized = new Synchronized<string>(value);

            // act
            var result = synchronized.Read();

            // assert
            Assert.Equal(value, result);
        }

        [Fact]
        public void ReadDirty_Success()
        {
            // arrange
            const string value = "foo";
            var synchronized = new Synchronized<string>(value);

            // act
            var result = synchronized.ReadDirty();

            // assert
            Assert.Equal(value, result);
        }

        [Fact]
        public void Constructor_Mutex_And_Value_Success()
        {
            // arrange
            const string value = "bar";

            // act
            var synchronized = new Synchronized<string>(new object(), value);

            // assert
            Assert.Equal(value, synchronized.Read());
        }
    }
}
