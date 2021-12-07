// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Unit.NetworkServer
{
    using global::LoRaTools.LoRaMessage;
    using LoRaWan.NetworkServer;
    using LoRaWan.Tests.Common;
    using Xunit;

    public class LoRaRequestTest
    {
        [Fact]
        public void When_Data_Payload_Needs_Confirmation_RequiresConfirmation_Should_Return_True()
        {
            // arrange
            var simulatedDevice = new SimulatedDevice(TestDeviceInfo.CreateABPDevice(0));
            var dataPayload = simulatedDevice.CreateConfirmedDataUpMessage("payload");
            using var loraRequest = WaitableLoRaRequest.Create(dataPayload);

            // act/assert
            Assert.True(LoRaRequest.RequiresConfirmation(loraRequest));
        }

        [Fact]
        public void When_Not_Data_Payload_RequiresConfirmation_Should_Return_False()
        {
            // arrange
            var simulatedDevice = new SimulatedDevice(TestDeviceInfo.CreateOTAADevice(0));
            var joinRxpk = simulatedDevice.CreateJoinRequest().SerializeUplink(simulatedDevice.AppKey).Rxpk[0]; ;
            using var loraRequest = WaitableLoRaRequest.Create(joinRxpk);
            loraRequest.SetPayload(new LoRaPayloadJoinRequest());

            // act/assert
            Assert.False(LoRaRequest.RequiresConfirmation(loraRequest));
        }

        [Fact]
        public void When_Data_Payload_Does_Not_Need_Confirmation_RequiresConfirmation_Should_Return_False()
        {
            // arrange
            var simulatedDevice = new SimulatedDevice(TestDeviceInfo.CreateABPDevice(0));
            var dataPayload = simulatedDevice.CreateUnconfirmedDataUpMessage("payload");
            using var loraRequest = WaitableLoRaRequest.Create(dataPayload);

            // act/assert
            Assert.False(LoRaRequest.RequiresConfirmation(loraRequest));
        }
    }
}
