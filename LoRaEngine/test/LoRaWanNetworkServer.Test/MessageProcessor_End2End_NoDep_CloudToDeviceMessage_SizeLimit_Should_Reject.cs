// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer.Test
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using LoRaTools;
    using LoRaTools.LoRaMessage;
    using LoRaTools.LoRaPhysical;
    using LoRaTools.Regions;
    using LoRaTools.Utils;
    using LoRaWan.NetworkServer;
    using LoRaWan.Test.Shared;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Shared;
    using Microsoft.Extensions.Caching.Memory;
    using Moq;
    using Xunit;

    [Collection(TestConstants.C2D_Size_Limit_TestCollectionName)]
    public class MessageProcessor_End2End_NoDep_CloudToDeviceMessage_SizeLimit_Should_Reject : MessageProcessor_End2End_NoDep_CloudToDeviceMessage_SizeLimitBase
    {
        [Theory]
        [CombinatorialData]
        public async Task Should_Reject(
            bool isConfirmed,
            bool hasMacInUpstream,
            bool hasMacInC2D,
            [CombinatorialValues("SF10BW125", "SF9BW125", "SF8BW125", "SF7BW125")] string datr)
        {
            const int InitialDeviceFcntUp = 9;
            const int InitialDeviceFcntDown = 20;

            var simulatedDevice = new SimulatedDevice(
                TestDeviceInfo.CreateABPDevice(1, gatewayID: this.ServerConfiguration.GatewayID),
                frmCntUp: InitialDeviceFcntUp,
                frmCntDown: InitialDeviceFcntDown);

            var loraDevice = this.CreateLoRaDevice(simulatedDevice);

            Rxpk rxpk = this.CreateUpstreamRxpk(isConfirmed, hasMacInUpstream, datr, simulatedDevice);

            if (!hasMacInUpstream)
            {
                this.LoRaDeviceClient.Setup(x => x.SendEventAsync(It.IsNotNull<LoRaDeviceTelemetry>(), null))
                    .ReturnsAsync(true);
            }

            var euRegion = RegionManager.EU868;
            var c2dMessageMacCommand = new DevStatusRequest();
            var c2dMessageMacCommandSize = hasMacInC2D ? c2dMessageMacCommand.Length : 0;
            var upstreamMessageMacCommandSize = 0;
            string expectedDownlinkDatr;

            if (hasMacInUpstream)
            {
                upstreamMessageMacCommandSize = new LinkCheckAnswer(1, 1).Length;
            }

            expectedDownlinkDatr = datr;

            var c2dPayloadSize = euRegion.GetMaxPayloadSize(expectedDownlinkDatr)
                - c2dMessageMacCommandSize
                + 1 // make message too long on purpose
                - Constants.LORA_PROTOCOL_OVERHEAD_SIZE;

            var c2dMessagePayload = TestUtils.GeneratePayload("123457890", (int)c2dPayloadSize);

            var c2dMessage = new ReceivedLoRaCloudToDeviceMessage()
            {
                Payload = c2dMessagePayload,
                Fport = 1,
            };

            if (hasMacInC2D)
            {
                c2dMessage.MacCommands = new[] { c2dMessageMacCommand };
            }

            var cloudToDeviceMessage = c2dMessage.CreateMessage();

            this.LoRaDeviceClient.Setup(x => x.ReceiveAsync(It.IsAny<TimeSpan>()))
                .ReturnsAsync(cloudToDeviceMessage);

            this.LoRaDeviceClient.Setup(x => x.RejectAsync(cloudToDeviceMessage))
                .ReturnsAsync(true);

            var deviceRegistry = new LoRaDeviceRegistry(this.ServerConfiguration, this.NewNonEmptyCache(loraDevice), this.LoRaDeviceApi.Object, this.LoRaDeviceFactory);

            // Send to message processor
            var messageProcessor = new MessageDispatcher(
                this.ServerConfiguration,
                deviceRegistry,
                this.FrameCounterUpdateStrategyProvider);

            var request = this.CreateWaitableRequest(rxpk);
            messageProcessor.DispatchRequest(request);

            // Expectations
            // 1. Message was sent to IoT Hub
            Assert.True(await request.WaitCompleteAsync());
            Assert.True(request.ProcessingSucceeded);

            var shouldHaveADownlink = isConfirmed || hasMacInUpstream;

            // 2. Return is downstream message ONLY
            // if is confirmed or had Mac commands in upstream message
            if (shouldHaveADownlink)
            {
                Assert.NotNull(request.ResponseDownlink);
                Assert.Equal(expectedDownlinkDatr, request.ResponseDownlink.Txpk.Datr);

                var downlinkMessage = this.PacketForwarder.DownlinkMessages[0];
                var payloadDataDown = new LoRaPayloadData(Convert.FromBase64String(downlinkMessage.Txpk.Data));
                payloadDataDown.PerformEncryption(loraDevice.AppSKey);

                Assert.Equal(payloadDataDown.DevAddr.ToArray(), LoRaTools.Utils.ConversionHelper.StringToByteArray(loraDevice.DevAddr));
                Assert.Equal(LoRaMessageType.UnconfirmedDataDown, payloadDataDown.LoRaMessageType);

                if (hasMacInUpstream)
                {
                    Assert.Equal(new LinkCheckAnswer(1, 1).Length, payloadDataDown.Frmpayload.Length);
                    Assert.Equal(0, payloadDataDown.GetFPort());
                }
            }
            else
            {
                Assert.Null(request.ResponseDownlink);
            }

            this.LoRaDeviceClient.VerifyAll();
            this.LoRaDeviceApi.VerifyAll();
        }
    }
}
