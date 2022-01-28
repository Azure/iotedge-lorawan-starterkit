// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Unit.LoRaTools.LoRaMessage
{
    using LoRaWan.Tests.Common;
    using Xunit;

    public class LoRaPayloadTest
    {
        [Fact]
        public void When_Data_Payload_Needs_Confirmation_RequiresConfirmation_Should_Return_True()
        {
            // arrange
            var simulatedDevice = new SimulatedDevice(TestDeviceInfo.CreateABPDevice(0));
            var dataPayload = simulatedDevice.CreateConfirmedDataUpMessage("payload");
            using var loraRequest = WaitableLoRaRequest.Create(dataPayload);

            // act/assert
            Assert.True(dataPayload.RequiresConfirmation);
        }

        [Fact]
        public void When_Data_Payload_Does_Not_Need_Confirmation_RequiresConfirmation_Should_Return_False()
        {
            // arrange
            var simulatedDevice = new SimulatedDevice(TestDeviceInfo.CreateABPDevice(0));
            var dataPayload = simulatedDevice.CreateUnconfirmedDataUpMessage("payload");
            using var loraRequest = WaitableLoRaRequest.Create(dataPayload);

            // act/assert
            Assert.False(dataPayload.RequiresConfirmation);
        }
    }
}
