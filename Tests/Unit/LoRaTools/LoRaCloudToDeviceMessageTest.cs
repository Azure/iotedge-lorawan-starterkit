// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Unit.LoRaTools.CommonAPI
{
    using global::LoRaTools;
    using global::LoRaTools.CommonAPI;
    using LoRaWan.Tests.Common;
    using Xunit;

    public class LoRaCloudToDeviceMessageTest
    {
        private static FramePort ReservedFramePort(byte n) => (FramePort)checked((byte)(FramePort.ReservedMin + n));

        [Fact]
        public void When_FPort_Is_For_Mac_Command_Should_Be_Invalid_For_Payload()
        {
            var payloadC2d = new LoRaCloudToDeviceMessage()
            {
                MessageId = "01",
                Payload = "hello",
                Fport = FramePort.MacCommand,
            };

            Assert.False(payloadC2d.IsValid(out var errorMessage));
            Assert.Equal("invalid MAC command fport usage in cloud to device message '01'", errorMessage);

            var rawPayloadC2d = new LoRaCloudToDeviceMessage()
            {
                MessageId = "01",
                RawPayload = "AAAA",
                Fport = FramePort.MacCommand,
            };

            Assert.False(rawPayloadC2d.IsValid(out errorMessage));
            Assert.Equal("invalid MAC command fport usage in cloud to device message '01'", errorMessage);
        }

        [Fact]
        public void When_FPort_Is_Reserved_Should_Be_Invalid()
        {
            var payloadC2d = new LoRaCloudToDeviceMessage()
            {
                MessageId = "01",
                Payload = "hello",
                Fport = ReservedFramePort(0),
            };

            Assert.False(payloadC2d.IsValid(out var errorMessage));
            Assert.Equal("invalid fport '225' in cloud to device message '01'", errorMessage);

            var rawPayloadC2d = new LoRaCloudToDeviceMessage()
            {
                MessageId = "02",
                RawPayload = "AAAA",
                Fport = ReservedFramePort(1),
            };

            Assert.False(rawPayloadC2d.IsValid(out errorMessage));
            Assert.Equal("invalid fport '226' in cloud to device message '02'", errorMessage);
        }

        [Fact]
        public void When_Has_Only_MacCommand_And_FPort_0_Should_Be_Valid()
        {
            var c2d = new LoRaCloudToDeviceMessage()
            {
                MessageId = "01",
                MacCommands = { new DevStatusRequest() },
                Fport = FramePort.MacCommand,
            };

            Assert.True(c2d.IsValid(out var errorMessage));
            Assert.Null(errorMessage);
        }

        [Fact]
        public void When_Has_Only_Payload_And_Valid_FPort_Should_Be_Valid()
        {
            var payloadC2d = new LoRaCloudToDeviceMessage()
            {
                MessageId = "01",
                Payload = "hello",
                MacCommands = { new DevStatusRequest() },
                Fport = FramePorts.App1,
            };

            Assert.True(payloadC2d.IsValid(out var errorMessage));
            Assert.Null(errorMessage);

            var rawPayloadC2d = new LoRaCloudToDeviceMessage()
            {
                MessageId = "01",
                RawPayload = "AAAA",
                MacCommands = { new DevStatusRequest() },
                Fport = FramePorts.App2,
            };

            Assert.True(rawPayloadC2d.IsValid(out errorMessage));
            Assert.Null(errorMessage);
        }

        [Fact]
        public void When_Has_Payload_And_MacCommand_Non_Zero_FPort_Should_Be_Valid()
        {
            var payloadC2d = new LoRaCloudToDeviceMessage()
            {
                MessageId = "01",
                Payload = "hello",
                Fport = FramePorts.App1,
            };

            Assert.True(payloadC2d.IsValid(out var errorMessage));
            Assert.Null(errorMessage);

            var rawPayloadC2d = new LoRaCloudToDeviceMessage()
            {
                MessageId = "01",
                RawPayload = "AAAA",
                Fport = FramePorts.App2,
            };

            Assert.True(rawPayloadC2d.IsValid(out errorMessage));
            Assert.Null(errorMessage);
        }

        [Fact]
        public void When_Has_Payload_And_MacCommand_Zero_FPort_Should_Be_Invalid()
        {
            var payloadC2d = new LoRaCloudToDeviceMessage()
            {
                MessageId = "01",
                Payload = "hello",
                Fport = FramePort.MacCommand,
                MacCommands = { new DevStatusRequest() },
            };

            Assert.False(payloadC2d.IsValid(out var errorMessage));
            Assert.Equal("invalid MAC command fport usage in cloud to device message '01'", errorMessage);

            var rawPayloadC2d = new LoRaCloudToDeviceMessage()
            {
                MessageId = "02",
                RawPayload = "AAAA",
                Fport = FramePort.MacCommand,
                MacCommands = { new DevStatusRequest() },
            };

            Assert.False(rawPayloadC2d.IsValid(out errorMessage));
            Assert.Equal("invalid MAC command fport usage in cloud to device message '02'", errorMessage);
        }

        [Fact]
        public void When_Fport_0_And_Payload_Is_Empty_Should_Be_Invalid()
        {
            var c2d = new LoRaCloudToDeviceMessage();
            Assert.False(c2d.IsValid(out var errorMessage));
            Assert.Equal("invalid MAC command fport usage in cloud to device message ''", errorMessage);
        }
    }
}
