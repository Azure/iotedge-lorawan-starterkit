// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer.Test
{
    using System.Linq;
    using System.Text;
    using LoRaTools;
    using LoRaWan.NetworkServer;
    using Microsoft.Azure.Devices.Client;
    using Moq;
    using Xunit;

    public class LoRaCloudToDeviceMessageWrapperTest
    {
        private readonly LoRaDevice sampleDevice;

        public LoRaCloudToDeviceMessageWrapperTest()
        {
            var connectionManager = TestUtils.CreateConnectionManager();
            this.sampleDevice = new LoRaDevice("123131", "1231231232132", connectionManager);

            connectionManager.Register(this.sampleDevice, new Mock<ILoRaDeviceClient>().Object);
        }

        [Fact]
        public void When_Body_Is_Empty_Should_Not_Be_Valid()
        {
            var message = new Message();
            var target = new LoRaCloudToDeviceMessageWrapper(this.sampleDevice, message);
            Assert.False(target.IsValid(out var errorMessage));
            Assert.Equal("cloud message does not have a body", errorMessage);
        }

        [Fact]
        public void When_Body_Is_Empty_Text_Should_Not_Be_Valid()
        {
            var message = new Message(Encoding.UTF8.GetBytes(string.Empty));
            var target = new LoRaCloudToDeviceMessageWrapper(this.sampleDevice, message);
            Assert.False(target.IsValid(out var errorMessage));
            Assert.Equal("cloud message does not have a body", errorMessage);
        }

        [Theory]
        [InlineData("{")]
        public void When_Body_Is_Not_Valid_Json_Should_Not_Be_Valid(string json)
        {
            var message = new Message(Encoding.UTF8.GetBytes(json));
            var target = new LoRaCloudToDeviceMessageWrapper(this.sampleDevice, message);
            Assert.False(target.IsValid(out var errorMessage));
            Assert.Equal($"could not parse cloud to device message: {json}", errorMessage);
        }

        [Theory]
        [InlineData("{\"fport\":1, \"payload\":\"asd\", \"macCommands\": [ { \"cid\": \"test\" }] }")]
        [InlineData("{\"fport\":1, \"payload\":\"asd\", \"macCommands\": [ { \"cid\": \"DevStatusCmd2\" }] }")]
        public void When_Body_Has_Invalid_MacCommand_Should_Not_Be_Valid(string json)
        {
            var message = new Message(Encoding.UTF8.GetBytes(json));
            var target = new LoRaCloudToDeviceMessageWrapper(this.sampleDevice, message);
            Assert.False(target.IsValid(out var errorMessage));
            Assert.Equal($"could not parse cloud to device message: {json}", errorMessage);
        }

        [Theory]
        [InlineData("{\"fport\":1, \"payload\": \"Hello\"}", "Hello")]
        public void When_Body_With_Text_Payload_Is_Valid_Json_Should_Be_Valid(string json, string expectedPayload)
        {
            var message = new Message(Encoding.UTF8.GetBytes(json));
            var target = new LoRaCloudToDeviceMessageWrapper(this.sampleDevice, message);
            Assert.True(target.IsValid(out _));
            Assert.Equal(expectedPayload, Encoding.UTF8.GetString(target.GetPayload()));
        }

        [Theory]
        [InlineData("{\"fport\":1, \"payload\":\"asd\", \"macCommands\": [ { \"cid\": \"DevStatusCmd\" }] }")]
        public void When_Body_Has_MacCommand_Should_Contain_It_List(string json)
        {
            var message = new Message(Encoding.UTF8.GetBytes(json));
            var target = new LoRaCloudToDeviceMessageWrapper(this.sampleDevice, message);
            Assert.True(target.IsValid(out _));
            Assert.Single(target.MacCommands);
            Assert.IsType<DevStatusRequest>(target.MacCommands.First());
        }

        [Theory]
        [InlineData("{\"fport\":1, \"payload\":\"asd\", \"macCommands\": [ { \"cid\": \"DutyCycleCmd\", \"dutyCyclePL\": 2 }] }")]
        public void When_Body_Has_MacCommand_With_Parameters_Should_Contain_It_List(string json)
        {
            var message = new Message(Encoding.UTF8.GetBytes(json));
            var target = new LoRaCloudToDeviceMessageWrapper(this.sampleDevice, message);
            Assert.True(target.IsValid(out _));
            Assert.Single(target.MacCommands);
            Assert.IsType<DutyCycleRequest>(target.MacCommands.First());
            var dutyCycleCmd = (DutyCycleRequest)target.MacCommands.First();
            Assert.Equal(2, dutyCycleCmd.DutyCyclePL);
        }
    }
}
