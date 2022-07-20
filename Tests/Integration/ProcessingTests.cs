// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Integration
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using LoRaTools;
    using LoRaTools.LoRaMessage;
    using LoRaWan.NetworkServer;
    using LoRaWan.Tests.Common;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Shared;
    using Microsoft.Extensions.Caching.Memory;
    using Moq;
    using Xunit;
    using Xunit.Abstractions;
    using IoTHubDeviceInfo = NetworkServer.IoTHubDeviceInfo;

    // End to end tests without external dependencies (IoT Hub, Service Facade Function)
    // General message processor tests (Join tests are handled in other class)
    public class ProcessingTests : MessageProcessorTestBase
    {
        public ProcessingTests(ITestOutputHelper testOutputHelper) : base(testOutputHelper) { }

        [Theory]
        [InlineData(ServerGatewayID, 0, 0, 0)]
        [InlineData(ServerGatewayID, 0, 1, 1)]
        [InlineData(ServerGatewayID, 0, 100, 20)]
        [InlineData(ServerGatewayID, 1, 0, 0)]
        [InlineData(ServerGatewayID, 1, 1, 1)]
        [InlineData(ServerGatewayID, 1, 100, 20)]
        [InlineData(null, 0, 0, 0)]
        [InlineData(null, 0, 1, 1)]
        [InlineData(null, 0, 100, 20)]
        [InlineData(null, 1, 0, 0)]
        [InlineData(null, 1, 1, 1)]
        [InlineData(null, 1, 100, 20)]
        public async Task ABP_Cached_Device_With_Fcnt_1_Or_0_Should_Reset_Fcnt_And_Send_To_IotHub(
            string deviceGatewayID,
            uint payloadFcntUp,
            uint deviceInitialFcntUp,
            uint deviceInitialFcntDown)
        {
            var simulatedDevice = new SimulatedDevice(TestDeviceInfo.CreateABPDevice(1, gatewayID: deviceGatewayID))
            {
                FrmCntDown = deviceInitialFcntDown,
                FrmCntUp = deviceInitialFcntUp
            };

            var devEui = simulatedDevice.LoRaDevice.DevEui;
            var devAddr = simulatedDevice.LoRaDevice.DevAddr;

            // message will be sent
            LoRaDeviceTelemetry loRaDeviceTelemetry = null;
            LoRaDeviceClient.Setup(x => x.SendEventAsync(It.IsNotNull<LoRaDeviceTelemetry>(), null))
                .Callback<LoRaDeviceTelemetry, Dictionary<string, string>>((t, _) => loRaDeviceTelemetry = t)
                .ReturnsAsync(true);

            // C2D message will be checked
            LoRaDeviceClient.Setup(x => x.ReceiveAsync(It.IsNotNull<TimeSpan>()))
                .ReturnsAsync((Message)null);

            // twin will be updated with new fcnt
            int? fcntUpSavedInTwin = null;
            int? fcntDownSavedInTwin = null;

            // twin should be saved only if not starting at 0, 0
            var shouldSaveTwin = deviceInitialFcntDown != 0 || deviceInitialFcntUp != 0;
            if (shouldSaveTwin)
            {
                // Twin will be saved
                LoRaDeviceClient.Setup(x => x.UpdateReportedPropertiesAsync(It.IsNotNull<TwinCollection>(), It.IsAny<CancellationToken>()))
                    .Callback<TwinCollection, CancellationToken>((t, _) =>
                    {
                        fcntUpSavedInTwin = (int)t[TwinProperty.FCntUp];
                        fcntDownSavedInTwin = (int)t[TwinProperty.FCntDown];
                    })
                    .ReturnsAsync(true);
            }

            if (string.IsNullOrEmpty(deviceGatewayID))
            {
                LoRaDeviceApi.Setup(x => x.ABPFcntCacheResetAsync(devEui, It.IsAny<uint>(), It.IsNotNull<string>()))
                    .ReturnsAsync(true);
            }

            await using var cachedDevice = CreateLoRaDevice(simulatedDevice);
            await using var messageDispatcherDisposableValue = SetupMessageDispatcherAsync(cachedDevice);
            var messageDispatcher = messageDispatcherDisposableValue.Value;

            // sends unconfirmed message
            var unconfirmedMessagePayload = simulatedDevice.CreateUnconfirmedDataUpMessage("hello", fcnt: payloadFcntUp);
            using var request = CreateWaitableRequest(unconfirmedMessagePayload);
            messageDispatcher.DispatchRequest(request);
            Assert.True(await request.WaitCompleteAsync());
            Assert.Null(request.ResponseDownlink);

            // Ensure that a telemetry was sent
            Assert.NotNull(loRaDeviceTelemetry);
            // Assert.Equal(msgPayload, loRaDeviceTelemetry.data);

            // Ensure that the device twins were saved
            if (shouldSaveTwin)
            {
                Assert.NotNull(fcntDownSavedInTwin);
                Assert.NotNull(fcntUpSavedInTwin);
                Assert.Equal(0, fcntDownSavedInTwin.Value); // fcntDown will be set to zero
                Assert.Equal(0, fcntUpSavedInTwin.Value); // fcntUp will be set to zero
            }

            // verify that the device in device registry has correct properties and frame counters
            Assert.True(DeviceCache.TryGetForPayload(request.Payload, out var loRaDevice));
            Assert.Equal(devAddr, loRaDevice.DevAddr);
            Assert.Equal(devEui, loRaDevice.DevEUI);
            Assert.True(loRaDevice.IsABP);
            Assert.Equal(payloadFcntUp, loRaDevice.FCntUp);
            Assert.Equal(0U, loRaDevice.FCntDown); // fctn down will always be set to zero
            if (payloadFcntUp == 0)
                Assert.False(loRaDevice.HasFrameCountChanges); // no changes
            else
                Assert.True(loRaDevice.HasFrameCountChanges); // there are pending changes (fcntUp 0 => 1)

            // will update api in multi gateway scenario
            if (string.IsNullOrEmpty(deviceGatewayID))
            {
                LoRaDeviceApi.Verify(x => x.ABPFcntCacheResetAsync(devEui, It.IsAny<uint>(), It.IsNotNull<string>()), Times.Exactly(1));
            }

            LoRaDeviceClient.VerifyAll();
            LoRaDeviceApi.VerifyAll();

            LoRaDeviceClient.Setup(ldc => ldc.DisposeAsync());
        }

        [Theory]
        [InlineData(ServerGatewayID, "1234", "")]
        [InlineData(ServerGatewayID, "hello world", null)]
        [InlineData(null, "hello world", null)]
        [InlineData(null, "1234", "")]
        public async Task ABP_Unconfirmed_With_No_Decoder_Sends_Raw_Payload(string deviceGatewayID, string msgPayload, string sensorDecoder)
        {
            var simulatedDevice = new SimulatedDevice(TestDeviceInfo.CreateABPDevice(1, gatewayID: deviceGatewayID));
            var loRaDevice = CreateLoRaDevice(simulatedDevice);
            loRaDevice.SensorDecoder = sensorDecoder;

            // message will be sent
            LoRaDeviceTelemetry loRaDeviceTelemetry = null;
            LoRaDeviceClient.Setup(x => x.SendEventAsync(It.IsNotNull<LoRaDeviceTelemetry>(), null))
                .Callback<LoRaDeviceTelemetry, Dictionary<string, string>>((t, _) => loRaDeviceTelemetry = t)
                .ReturnsAsync(true);

            // C2D message will be checked
            LoRaDeviceClient.Setup(x => x.ReceiveAsync(It.IsNotNull<TimeSpan>()))
                .ReturnsAsync((Message)null);

            if (string.IsNullOrEmpty(deviceGatewayID))
            {
                LoRaDeviceApi.Setup(x => x.ABPFcntCacheResetAsync(loRaDevice.DevEUI, It.IsAny<uint>(), It.IsNotNull<string>()))
                    .ReturnsAsync(true);
            }

            // Send to message processor
            await using var messageDispatcherDisposableValue = SetupMessageDispatcherAsync(loRaDevice);
            var messageDispatcher = messageDispatcherDisposableValue.Value;

            // sends unconfirmed message
            var unconfirmedMessagePayload = simulatedDevice.CreateUnconfirmedDataUpMessage(msgPayload, fcnt: 1);
            using var request = CreateWaitableRequest(unconfirmedMessagePayload);
            messageDispatcher.DispatchRequest(request);
            Assert.True(await request.WaitCompleteAsync());
            Assert.Null(request.ResponseDownlink);

            Assert.NotNull(loRaDeviceTelemetry);
            Assert.IsType<UndecodedPayload>(loRaDeviceTelemetry.Data);
            var undecodedPayload = (UndecodedPayload)loRaDeviceTelemetry.Data;
            var expectedPayloadContent = Convert.ToBase64String(Encoding.UTF8.GetBytes(msgPayload));
            Assert.Equal(expectedPayloadContent, undecodedPayload.Value);

            LoRaDeviceClient.VerifyAll();
            LoRaDeviceApi.VerifyAll();
        }

        [Fact]
        public async Task ABP_Unconfirmed_Sends_Valid_Mac_Commands_As_Part_Of_Payload_And_Receives_Answer_As_Part_Of_Payload()
        {
            var deviceGatewayID = ServerGatewayID;
            var simulatedDevice = new SimulatedDevice(TestDeviceInfo.CreateABPDevice(1, gatewayID: deviceGatewayID));
            var loRaDevice = CreateLoRaDevice(simulatedDevice);
            loRaDevice.SensorDecoder = string.Empty;

            // message will be sent
            LoRaDeviceTelemetry loRaDeviceTelemetry = null;

            // C2D message will be checked
            LoRaDeviceClient.Setup(x => x.ReceiveAsync(It.IsNotNull<TimeSpan>()))
                .ReturnsAsync((Message)null);

            // Send to message processor
            await using var messageDispatcherDisposableValue = SetupMessageDispatcherAsync(loRaDevice);
            var messageDispatcher = messageDispatcherDisposableValue.Value;

            // sends unconfirmed mac LinkCheckCmd
            var msgPayload = "02";
            var unconfirmedMessagePayload = simulatedDevice.CreateUnconfirmedDataUpMessage(msgPayload, fcnt: 1, isHexPayload: true, fport: 0);
            // only use nwkskey
            using var request = CreateWaitableRequest(unconfirmedMessagePayload);
            messageDispatcher.DispatchRequest(request);
            Assert.True(await request.WaitCompleteAsync());
            // Server should reply with MacCommand Answer
            Assert.NotNull(request.ResponseDownlink);
            var data = new LoRaPayloadData(request.ResponseDownlink.Data);
            Assert.True(data.CheckMic(simulatedDevice.NwkSKey.Value));
            data.Serialize(simulatedDevice.NwkSKey.Value);
            var link = new LoRaTools.LinkCheckAnswer(data.Frmpayload.Span);
            Assert.NotNull(link);
            Assert.Equal(1, link.GwCnt);
            Assert.Equal(15, link.Margin);
            // Nothing should be sent to IoT Hub
            Assert.Null(loRaDeviceTelemetry);

            LoRaDeviceClient.VerifyAll();
            LoRaDeviceApi.VerifyAll();
        }

        [Fact]
        public async Task ABP_Unconfirmed_Sends_Valid_Mac_Commands_In_Fopts_And_Reply_In_Fopts()
        {
            var deviceGatewayID = ServerGatewayID;
            var simulatedDevice = new SimulatedDevice(TestDeviceInfo.CreateABPDevice(1, gatewayID: deviceGatewayID));
            var loRaDevice = CreateLoRaDevice(simulatedDevice);
            loRaDevice.SensorDecoder = string.Empty;

            // message will be sent
            LoRaDeviceTelemetry loRaDeviceTelemetry = null;
            Dictionary<string, string> eventProperties = null;
            LoRaDeviceClient.Setup(x => x.SendEventAsync(It.IsNotNull<LoRaDeviceTelemetry>(), It.IsNotNull<Dictionary<string, string>>()))
                .Callback<LoRaDeviceTelemetry, Dictionary<string, string>>((t, d) =>
                {
                    loRaDeviceTelemetry = t;
                    eventProperties = d;
                })
                .ReturnsAsync(true);

            using var cloudToDeviceMessageSetup = UsePendingCloudToDeviceMessage();

            // Send to message processor
            await using var messageDispatcherDisposableValue = SetupMessageDispatcherAsync(loRaDevice);
            var messageDispatcher = messageDispatcherDisposableValue.Value;

            // sends unconfirmed mac LinkCheckCmd
            var msgPayload = "Hello World";
            var macCommands = new List<MacCommand>()
            {
                new LinkCheckRequest()
            };
            var unconfirmedMessagePayload = simulatedDevice.CreateUnconfirmedDataUpMessage(msgPayload, fcnt: 1, macCommands: macCommands);
            using var request = CreateWaitableRequest(unconfirmedMessagePayload);
            messageDispatcher.DispatchRequest(request);
            Assert.True(await request.WaitCompleteAsync());
            // Server should reply with MacCommand Answer
            Assert.NotNull(request.ResponseDownlink);
            var data = new LoRaPayloadData(request.ResponseDownlink.Data);
            Assert.True(data.CheckMic(simulatedDevice.NwkSKey.Value));
            // FOpts are not encrypted
            var link = new LoRaTools.LinkCheckAnswer(data.Fopts.Span);
            Assert.NotNull(link);
            Assert.NotNull(eventProperties);
            Assert.Contains("LinkCheckCmd", eventProperties.Keys);
            Assert.Equal(1, link.GwCnt);
            Assert.Equal(15, link.Margin);
            // Nothing should be sent to IoT Hub
            Assert.NotNull(loRaDeviceTelemetry);

            LoRaDeviceClient.VerifyAll();
            LoRaDeviceApi.VerifyAll();
        }

        [Theory]
        [InlineData(0x00)]
        [InlineData(0x25)]
        public async Task ABP_Unconfirmed_Sends_Invalid_Mac_Commands_In_Fopts(byte macCommand)
        {
            var deviceGatewayID = ServerGatewayID;
            var simulatedDevice = new SimulatedDevice(TestDeviceInfo.CreateABPDevice(1, gatewayID: deviceGatewayID));
            var loRaDevice = CreateLoRaDevice(simulatedDevice);
            loRaDevice.SensorDecoder = string.Empty;

            // message will be sent
            LoRaDeviceTelemetry loRaDeviceTelemetry = null;
            LoRaDeviceClient.Setup(x => x.SendEventAsync(It.IsNotNull<LoRaDeviceTelemetry>(), null))
                .Callback<LoRaDeviceTelemetry, Dictionary<string, string>>((t, _) =>
                {
                    loRaDeviceTelemetry = t;
                })
                .ReturnsAsync(true);

            var cloudToDeviceMessagePayload = "C2DMessagePayload";
            using var cloudToDeviceMessageSetup = UsePendingCloudToDeviceMessage(cloudToDeviceMessagePayload);

            using var cache = EmptyMemoryCache();
            await using var loraDeviceCache = CreateDeviceCache(loRaDevice);
            await using var deviceRegistry = new LoRaDeviceRegistry(ServerConfiguration, cache, LoRaDeviceApi.Object, LoRaDeviceFactory, loraDeviceCache);

            // Send to message processor
            await using var messageDispatcher = TestMessageDispatcher.Create(
                cache,
                ServerConfiguration,
                deviceRegistry,
                FrameCounterUpdateStrategyProvider);

            // sends unconfirmed mac LinkCheckCmd
            var msgPayload = "Hello World";

            var unconfirmedMessagePayload = simulatedDevice.CreateUnconfirmedDataUpMessage(msgPayload, fcnt: 1, macCommands: MacCommand.CreateMacCommandFromBytes(new[] { macCommand }));
            // only use nwkskey
            using var request = CreateWaitableRequest(unconfirmedMessagePayload);
            messageDispatcher.DispatchRequest(request);
            Assert.True(await request.WaitCompleteAsync());
            // Server should reply with MacCommand Answer
            Assert.NotNull(request.ResponseDownlink);
            var data = new LoRaPayloadData(request.ResponseDownlink.Data);
            Assert.True(data.CheckMic(simulatedDevice.NwkSKey.Value));
            // FOpts are not encrypted
            var payload = data.GetDecryptedPayload(simulatedDevice.AppSKey.Value);
            var c2dreceivedPayload = Encoding.UTF8.GetString(payload);
            Assert.Equal(cloudToDeviceMessagePayload, c2dreceivedPayload);
            // Nothing should be sent to IoT Hub
            Assert.NotNull(loRaDeviceTelemetry);

            LoRaDeviceClient.VerifyAll();
            LoRaDeviceApi.VerifyAll();
        }

        [Fact]
        // https://github.com/Azure/iotedge-lorawan-starterkit/issues/1540.
        public async Task Secondary_Tasks_Do_Not_Impact_Downstream_Message_Delivery_And_Do_Not_Cause_Processing_Failure()
        {
            var simulatedDevice = new SimulatedDevice(TestDeviceInfo.CreateABPDevice(1, gatewayID: ServerGatewayID));
            var loRaDevice = CreateLoRaDevice(simulatedDevice);

            LoRaDeviceClient.Setup(c => c.SendEventAsync(It.IsAny<LoRaDeviceTelemetry>(), It.IsAny<Dictionary<string, string>>()))
                            .ReturnsAsync(true);

            using var cloudToDeviceMessage = UsePendingCloudToDeviceMessage(completeOperationException: new OperationCanceledException("Operation timed out."));

            await using var messageDispatcherDisposableValue = SetupMessageDispatcherAsync(loRaDevice);
            var messageDispatcher = messageDispatcherDisposableValue.Value;

            var payload = simulatedDevice.CreateConfirmedDataUpMessage("foo", fcnt: 1);
            using var request = CreateWaitableRequest(payload);

            // act
            messageDispatcher.DispatchRequest(request);
            await request.WaitCompleteAsync();

            // assert
            Assert.Single(DownstreamMessageSender.DownlinkMessages);
            Assert.True(request.ProcessingSucceeded);
        }

        [Theory]
        [InlineData("00")]
        [InlineData("26")]
        public async Task ABP_Unconfirmed_Sends_Invalid_Mac_Commands_As_Part_Of_Payload(string macCommand)
        {
            var deviceGatewayID = ServerGatewayID;
            var simulatedDevice = new SimulatedDevice(TestDeviceInfo.CreateABPDevice(1, gatewayID: deviceGatewayID));
            var loRaDevice = CreateLoRaDevice(simulatedDevice);
            loRaDevice.SensorDecoder = string.Empty;

            // message will be sent
            LoRaDeviceTelemetry loRaDeviceTelemetry = null;

            // C2D message will be checked
            LoRaDeviceClient.Setup(x => x.ReceiveAsync(It.IsNotNull<TimeSpan>()))
                .ReturnsAsync((Message)null);

            // Send to message processor
            await using var messageDispatcherDisposableValue = SetupMessageDispatcherAsync(loRaDevice);
            var messageDispatcher = messageDispatcherDisposableValue.Value;

            // sends unconfirmed mac LinkCheckCmd
            var msgPayload = macCommand;
            var unconfirmedMessagePayload = simulatedDevice.CreateUnconfirmedDataUpMessage(msgPayload, fcnt: 1, isHexPayload: true, fport: 0);
            // only use nwkskey
            using var request = CreateWaitableRequest(unconfirmedMessagePayload);
            messageDispatcher.DispatchRequest(request);
            Assert.True(await request.WaitCompleteAsync());
            // Server should not reply and discard the message
            Assert.Null(request.ResponseDownlink);
            Assert.Null(loRaDeviceTelemetry);

            LoRaDeviceClient.VerifyAll();
            LoRaDeviceApi.VerifyAll();
        }

        [Theory]
        [InlineData(0, 0)]
        [InlineData(0, 1)]
        [InlineData(255, 1)]
        [InlineData(16777215, 16777215)]
        [InlineData(127, 127)]
        [InlineData(255, 255)]
        public async Task ABP_Device_NetId_Should_Match_Server(int deviceNetId, int serverNetId)
        {
            var msgPayload = "1234";
            var simulatedDevice = new SimulatedDevice(TestDeviceInfo.CreateABPDevice(1, netId: deviceNetId));
            var loRaDevice = CreateLoRaDevice(simulatedDevice);
            loRaDevice.SensorDecoder = null;

            // message will be sent if there is a match
            var netIdMatches = deviceNetId == serverNetId;
            LoRaDeviceTelemetry loRaDeviceTelemetry = null;
            if (netIdMatches)
            {
                LoRaDeviceClient.Setup(x => x.SendEventAsync(It.IsNotNull<LoRaDeviceTelemetry>(), null))
                    .Callback<LoRaDeviceTelemetry, Dictionary<string, string>>((t, _) => loRaDeviceTelemetry = t)
                    .ReturnsAsync(true);

                // C2D message will be checked
                LoRaDeviceClient.Setup(x => x.ReceiveAsync(It.IsNotNull<TimeSpan>()))
                    .ReturnsAsync((Message)null);

                LoRaDeviceApi.Setup(x => x.ABPFcntCacheResetAsync(loRaDevice.DevEUI, It.IsAny<uint>(), It.IsNotNull<string>()))
                .ReturnsAsync(true);
            }

            ServerConfiguration.NetId = new NetId(serverNetId);

            // Send to message processor
            await using var messageDispatcherDisposableValue = SetupMessageDispatcherAsync(loRaDevice);
            var messageDispatcher = messageDispatcherDisposableValue.Value;

            // sends unconfirmed message
            var unconfirmedMessagePayload = simulatedDevice.CreateUnconfirmedDataUpMessage(msgPayload, fcnt: 1);
            using var request = CreateWaitableRequest(unconfirmedMessagePayload);
            messageDispatcher.DispatchRequest(request);
            Assert.True(await request.WaitCompleteAsync());
            Assert.Null(request.ResponseDownlink);

            if (netIdMatches)
            {
                Assert.NotNull(loRaDeviceTelemetry);
                Assert.IsType<UndecodedPayload>(loRaDeviceTelemetry.Data);
                var undecodedPayload = (UndecodedPayload)loRaDeviceTelemetry.Data;
                var expectedPayloadContent = Convert.ToBase64String(Encoding.UTF8.GetBytes(msgPayload));
                Assert.Equal(expectedPayloadContent, undecodedPayload.Value.ToString());
            }
            else
            {
                Assert.Null(loRaDeviceTelemetry);
                Assert.Equal(0U, loRaDevice.FCntUp);
            }

            LoRaDeviceClient.VerifyAll();
            LoRaDeviceApi.VerifyAll();
        }

        [Theory]
        [InlineData(ServerGatewayID, null)]
        [InlineData(ServerGatewayID, "test")]
        [InlineData(ServerGatewayID, "test", "idtest")]
        public async Task When_Ack_Message_Received_Should_Be_In_Msg_Properties(string deviceGatewayID, string data, string msgId = null)
        {
            const uint initialFcntUp = 100;
            const uint payloadFcnt = 102;
            var simulatedDevice = new SimulatedDevice(TestDeviceInfo.CreateABPDevice(1, gatewayID: deviceGatewayID))
            {
                FrmCntUp = initialFcntUp
            };

            var loRaDevice = CreateLoRaDevice(simulatedDevice);
            if (msgId != null)
                loRaDevice.LastConfirmedC2DMessageID = msgId;

            Dictionary<string, string> actualProperties = null;
            LoRaDeviceClient.Setup(x => x.SendEventAsync(It.IsAny<LoRaDeviceTelemetry>(), It.IsAny<Dictionary<string, string>>()))
                .Callback<LoRaDeviceTelemetry, Dictionary<string, string>>((t, d) => actualProperties = d)
                .ReturnsAsync(true);

            // C2D message will be checked
            LoRaDeviceClient.Setup(x => x.ReceiveAsync(It.IsNotNull<TimeSpan>()))
                .ReturnsAsync((Message)null);

            await using var messageDispatcherDisposableValue = SetupMessageDispatcherAsync(loRaDevice);
            var messageDispatcher = messageDispatcherDisposableValue.Value;

            var ackMessage = simulatedDevice.CreateUnconfirmedDataUpMessage(data, fcnt: payloadFcnt, fctrlFlags: FrameControlFlags.Ack);
            using var ackRequest = CreateWaitableRequest(ackMessage);
            messageDispatcher.DispatchRequest(ackRequest);
            Assert.True(await ackRequest.WaitCompleteAsync());
            Assert.Null(ackRequest.ResponseDownlink);
            Assert.True(DeviceCache.TryGetForPayload(ackRequest.Payload, out var loRaDeviceInfo));

            Assert.Equal(payloadFcnt, loRaDeviceInfo.FCntUp);

            Assert.NotNull(actualProperties);
            Assert.True(actualProperties.ContainsKey(NetworkServer.Constants.C2D_MSG_PROPERTY_VALUE_NAME));

            if (msgId == null)
                Assert.True(actualProperties.ContainsValue(NetworkServer.Constants.C2D_MSG_ID_PLACEHOLDER));
            else
                Assert.True(actualProperties.ContainsValue(msgId));
        }

        [Theory]
        [InlineData(ServerGatewayID, 21)]
        [InlineData(null, 21)]
        [InlineData(null, 30)]
        public async Task When_ConfirmedUp_Message_With_Same_Fcnt_Should_Send_To_Hub_And_Return_Ack(string deviceGatewayID, uint expectedFcntDown)
        {
            const uint initialFcntUp = 100;
            const uint initialFcntDown = 20;

            var simulatedDevice = new SimulatedDevice(TestDeviceInfo.CreateABPDevice(1, gatewayID: deviceGatewayID))
            {
                FrmCntUp = initialFcntUp,
                FrmCntDown = initialFcntDown
            };

            var loRaDevice = CreateLoRaDevice(simulatedDevice);

            var devEui = simulatedDevice.LoRaDevice.DevEui;

            // C2D message will be checked
            LoRaDeviceClient.Setup(x => x.ReceiveAsync(It.IsNotNull<TimeSpan>()))
                .ReturnsAsync((Message)null);

            LoRaDeviceClient.Setup(x => x.SendEventAsync(It.IsNotNull<LoRaDeviceTelemetry>(), null))
                .ReturnsAsync(true);

            // In this case we expect a call to update the reported properties of the framecounter.
            if (expectedFcntDown % 10 == 0)
            {
                LoRaDeviceClient.Setup(x => x.UpdateReportedPropertiesAsync(It.IsAny<TwinCollection>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
            }
            // Lora device api

            // in multigateway scenario the device api will be called to resolve fcntDown
            if (string.IsNullOrEmpty(deviceGatewayID))
            {
                LoRaDeviceApi
                    .Setup(x => x.ExecuteFunctionBundlerAsync(devEui, It.Is<FunctionBundlerRequest>(req => req.ClientFCntDown == initialFcntDown && req.ClientFCntUp == initialFcntUp && req.GatewayId == ServerConfiguration.GatewayID)))
                    .ReturnsAsync(() => new FunctionBundlerResult
                    {
                        AdrResult = new LoRaTools.ADR.LoRaADRResult
                        {
                            CanConfirmToDevice = true,
                            NbRepetition = 1,
                            TxPower = 0,
                            FCntDown = expectedFcntDown,
                        },
                        NextFCntDown = expectedFcntDown
                    });
            }

            // Send to message processor
            await using var messageDispatcherDisposableValue = SetupMessageDispatcherAsync(loRaDevice);
            var messageDispatcher = messageDispatcherDisposableValue.Value;

            // sends confirmed message
            var confirmedMessagePayload = simulatedDevice.CreateConfirmedDataUpMessage("repeat", fcnt: 100);
            var radio = TestUtils.GenerateTestRadioMetadata();
            using var confirmedRequest = CreateWaitableRequest(radio, confirmedMessagePayload);
            messageDispatcher.DispatchRequest(confirmedRequest);

            // ack should be received
            Assert.True(await confirmedRequest.WaitCompleteAsync());
            Assert.NotNull(confirmedRequest.ResponseDownlink);
            Assert.Single(DownstreamMessageSender.DownlinkMessages);

            var confirmedMessageResult = DownstreamMessageSender.DownlinkMessages[0];

            // validates transmission for EU868
            Assert.Equal(radio.Frequency, confirmedMessageResult.Rx1?.Frequency);
            Assert.Equal(radio.DataRate, confirmedMessageResult.Rx1?.DataRate);
            Assert.Equal(radio.UpInfo.Xtime, confirmedMessageResult.Xtime);
            Assert.Equal(radio.UpInfo.AntennaPreference, confirmedMessageResult.AntennaPreference);


            // Expected changes to fcnt:
            // FcntDown => expectedFcntDown
            Assert.Equal(initialFcntUp, loRaDevice.FCntUp);
            Assert.Equal(expectedFcntDown, loRaDevice.FCntDown);
            if (expectedFcntDown % 10 != 0)
            {
                Assert.True(loRaDevice.HasFrameCountChanges);
            }
            else
            {
                Assert.False(loRaDevice.HasFrameCountChanges);
            }

            // message should be sent to iot hub
            LoRaDeviceClient.Verify(x => x.SendEventAsync(It.IsAny<LoRaDeviceTelemetry>(), It.IsAny<Dictionary<string, string>>()), Times.Once);
            LoRaDeviceClient.VerifyAll();
            LoRaDeviceApi.VerifyAll();
        }

        /// <summary>
        /// This tests the multi gateway scenario where a 2nd gateway cannot find the joined device because IoT Hub twin has not yet been updated
        /// It device api will not find it, only once the device registry finds it the message will be sent to IoT Hub.
        /// </summary>
        [Fact]
        public async Task When_Second_Gateway_Does_Not_Find_Device_Should_Keep_Trying_On_Subsequent_Requests()
        {
            var simulatedDevice = new SimulatedDevice(TestDeviceInfo.CreateOTAADevice(1));
            var devAddr = new DevAddr(0x02aabbcc);
            var nwkSKey = TestKeys.CreateNetworkSessionKey(2);
            var appSKey = TestKeys.CreateAppSessionKey(1);
            var devEUI = simulatedDevice.DevEUI;

            simulatedDevice.SetupJoin(appSKey, nwkSKey, devAddr);

            var updatedTwin = LoRaDeviceTwin.Create(
                simulatedDevice.LoRaDevice.GetOtaaDesiredTwinProperties() with { SensorDecoder = nameof(LoRaPayloadDecoder.DecoderValueSensor) },
                new LoRaReportedTwinProperties
                {
                    AppSessionKey = appSKey,
                    NetworkSessionKey = nwkSKey,
                    DevAddr = devAddr,
                    DevNonce = new DevNonce(0xABCD)
                });

            // Twin will be loaded once
            LoRaDeviceClient.Setup(x => x.GetTwinAsync(CancellationToken.None))
                .ReturnsAsync(updatedTwin);

            // Will check received messages once
            LoRaDeviceClient.Setup(x => x.ReceiveAsync(It.IsNotNull<TimeSpan>())).ReturnsAsync((Message)null);

            // Will send the 3 unconfirmed message
            var receivedLoRaDeviceTelemetryItems = new List<LoRaDeviceTelemetry>();
            LoRaDeviceClient.Setup(x => x.SendEventAsync(It.IsAny<LoRaDeviceTelemetry>(), It.IsAny<Dictionary<string, string>>()))
                .Callback<LoRaDeviceTelemetry, Dictionary<string, string>>((t, _) => receivedLoRaDeviceTelemetryItems.Add(t))
                .ReturnsAsync(true);

            // Will try to find the iot device based on dev addr
            LoRaDeviceApi.SetupSequence(x => x.SearchByDevAddrAsync(devAddr))
                .ReturnsAsync(new SearchDevicesResult())
                .ReturnsAsync(new SearchDevicesResult())
                .ReturnsAsync(new SearchDevicesResult(new IoTHubDeviceInfo(devAddr, devEUI, "abc").AsList()));

            // Making the function bundler return a processable message only for multigw scenario
            LoRaDeviceApi
                .Setup(x => x.ExecuteFunctionBundlerAsync(devEUI, It.IsAny<FunctionBundlerRequest>()))
                .ReturnsAsync(() =>
                    new FunctionBundlerResult
                    {
                        DeduplicationResult = new DeduplicationResult { GatewayId = ServerGatewayID, CanProcess = true, IsDuplicate = false },
                        AdrResult = null,
                        NextFCntDown = 0
                    });

            using var cache = new MemoryCache(new MemoryCacheOptions());
            await using var deviceRegistry = new LoRaDeviceRegistry(
                ServerConfiguration,
                cache,
                LoRaDeviceApi.Object,
                LoRaDeviceFactory,
                DeviceCache);

            // Making the reload interval zero
            deviceRegistry.DevAddrReloadInterval = TimeSpan.Zero;

            await using var messageDispatcher = TestMessageDispatcher.Create(
               cache,
               ServerConfiguration,
               deviceRegistry,
               FrameCounterUpdateStrategyProvider);

            // Unconfirmed message #1 should fail
            var payload1 = simulatedDevice.CreateUnconfirmedDataUpMessage("1", fcnt: 1);
            using var unconfirmedRequest1 = CreateWaitableRequest(payload1);
            messageDispatcher.DispatchRequest(unconfirmedRequest1);
            Assert.True(await unconfirmedRequest1.WaitCompleteAsync());
            Assert.Null(unconfirmedRequest1.ResponseDownlink);

            await WaitForLoaderEvictionAsync();

            // Unconfirmed message #2 should fail
            var payload2 = simulatedDevice.CreateUnconfirmedDataUpMessage("2", fcnt: 2);
            using var unconfirmedRequest2 = CreateWaitableRequest(payload2);
            messageDispatcher.DispatchRequest(unconfirmedRequest2);
            Assert.True(await unconfirmedRequest2.WaitCompleteAsync());
            Assert.Null(unconfirmedRequest2.ResponseDownlink);

            await WaitForLoaderEvictionAsync();

            // Unconfirmed message #3 should succeed
            var payload3 = simulatedDevice.CreateUnconfirmedDataUpMessage("3", fcnt: 3);
            using var unconfirmedRequest3 = CreateWaitableRequest(payload3);
            messageDispatcher.DispatchRequest(unconfirmedRequest3);
            Assert.True(await unconfirmedRequest3.WaitCompleteAsync());
            Assert.Null(unconfirmedRequest3.ResponseDownlink);

            var actualTelemetryItem = Assert.Single(receivedLoRaDeviceTelemetryItems);
            Assert.NotNull(actualTelemetryItem.Data);
            var decodedPayloadValue = Assert.IsType<DecodedPayloadValue>(actualTelemetryItem.Data);
            Assert.Equal("3", decodedPayloadValue.Value.ToString());

            LoRaDeviceClient.VerifyAll();
            LoRaDeviceApi.VerifyAll();

            Task WaitForLoaderEvictionAsync() => cache.WaitForEvictionAsync(LoRaDeviceRegistry.GetDevLoaderCacheKey(devAddr), CancellationToken.None);
        }

        /// <summary>
        /// Downlink should use same rfch than uplink message
        /// RFCH stands for Concentrator "RF chain" used for RX.
        /// </summary>
        [Theory]
        [InlineData(ServerGatewayID, 1)]
        [InlineData(ServerGatewayID, 0)]
        public async Task ABP_Confirmed_Message_Should_Use_Rchf_0(string deviceGatewayID, uint rfch)
        {
            var simulatedDevice = new SimulatedDevice(TestDeviceInfo.CreateABPDevice(1, gatewayID: deviceGatewayID))
            {
                FrmCntDown = 20,
                FrmCntUp = 100
            };

            // message will be sent
            LoRaDeviceTelemetry loRaDeviceTelemetry = null;

            LoRaDeviceClient.Setup(x => x.SendEventAsync(It.IsNotNull<LoRaDeviceTelemetry>(), null))
                .Callback<LoRaDeviceTelemetry, Dictionary<string, string>>((t, _) => loRaDeviceTelemetry = t)
                .ReturnsAsync(true);

            // C2D message will be checked
            LoRaDeviceClient.Setup(x => x.ReceiveAsync(It.IsNotNull<TimeSpan>()))
                .ReturnsAsync((Message)null);

            // Send to message processor
            var cachedDevice = CreateLoRaDevice(simulatedDevice);
            await using var messageDispatcherDisposableValue = SetupMessageDispatcherAsync(cachedDevice);
            var messageDispatcher = messageDispatcherDisposableValue.Value;

            // sends unconfirmed message
            var messagePayload = simulatedDevice.CreateConfirmedDataUpMessage("1234");
            var radio = TestUtils.GenerateTestRadioMetadata(antennaPreference: rfch);
            using var request = CreateWaitableRequest(radio, messagePayload);
            messageDispatcher.DispatchRequest(request);
            Assert.True(await request.WaitCompleteAsync());
            Assert.NotNull(request.ResponseDownlink);
            Assert.Single(DownstreamMessageSender.DownlinkMessages);
            var confirmedMessageResult = DownstreamMessageSender.DownlinkMessages[0];
            Assert.Equal(radio.Frequency, confirmedMessageResult.Rx1?.Frequency);
            Assert.Equal(radio.DataRate, confirmedMessageResult.Rx1?.DataRate);
            Assert.Equal(radio.UpInfo.Xtime, confirmedMessageResult.Xtime);
            Assert.Equal(radio.UpInfo.AntennaPreference, confirmedMessageResult.AntennaPreference);

            LoRaDeviceClient.VerifyAll();
            LoRaDeviceApi.VerifyAll();
        }

        [Theory]
        [InlineData(ServerGatewayID, 1601)]
        [InlineData(ServerGatewayID, 2000)]
        [InlineData(ServerGatewayID, 5000)]
        [InlineData(null, 1600)]
        [InlineData(null, 2000)]
        [InlineData(null, 5000)]
        public async Task When_Sending_Unconfirmed_Message_To_IoT_Hub_Takes_Too_Long_Should_Not_Check_For_C2D(
            string deviceGatewayID,
            int delayInMs)
        {
            var simulatedDevice = new SimulatedDevice(TestDeviceInfo.CreateABPDevice(1, gatewayID: deviceGatewayID));

            // message will be sent
            LoRaDeviceClient.Setup(x => x.SendEventAsync(It.IsNotNull<LoRaDeviceTelemetry>(), null))
                .ReturnsAsync(true);

            if (string.IsNullOrEmpty(deviceGatewayID))
            {
                LoRaDeviceApi.Setup(x => x.ABPFcntCacheResetAsync(It.IsNotNull<DevEui>(), It.IsAny<uint>(), It.IsNotNull<string>()))
                    .ReturnsAsync(true);
            }

            // Send to message processor
            var cachedDevice = CreateLoRaDevice(simulatedDevice);
            await using var messageDispatcherDisposableValue = SetupMessageDispatcherAsync(cachedDevice);
            var messageDispatcher = messageDispatcherDisposableValue.Value;

            // sends unconfirmed message
            var unconfirmedMessagePayload = simulatedDevice.CreateUnconfirmedDataUpMessage("hello");
            using var request =
                CreateWaitableRequest(unconfirmedMessagePayload,
                                      startTimeOffset: TimeSpan.FromMilliseconds(delayInMs),
                                      useRealTimer: true);
            messageDispatcher.DispatchRequest(request);
            Assert.True(await request.WaitCompleteAsync());
            Assert.Null(request.ResponseDownlink);

            LoRaDeviceClient.Verify(x => x.ReceiveAsync(It.IsAny<TimeSpan>()), Times.Never());

            LoRaDeviceClient.VerifyAll();
            LoRaDeviceApi.VerifyAll();
        }

        /// <summary>
        /// Verifies that if the update twin takes too long that no join accepts are sent.
        /// </summary>
        [Theory]
        [InlineData(ServerGatewayID)]
        [InlineData(null)]
        public async Task ABP_When_Getting_Twin_Fails_Should_Work_On_Retry(string deviceGatewayID)
        {
            var simulatedDevice = new SimulatedDevice(TestDeviceInfo.CreateABPDevice(1, gatewayID: deviceGatewayID));
            var devEui = simulatedDevice.LoRaDevice.DevEui;
            var devAddr = simulatedDevice.DevAddr.Value;

            // Device twin will be queried
            var twin = simulatedDevice.GetDefaultAbpTwin();
            LoRaDeviceClient.SetupSequence(x => x.GetTwinAsync(CancellationToken.None))
                .ReturnsAsync((Twin)null)
                .ReturnsAsync(twin);

            // 1 message will be sent
            var receivedTelemetryItems = new List<LoRaDeviceTelemetry>();
            LoRaDeviceClient.Setup(x => x.SendEventAsync(It.IsNotNull<LoRaDeviceTelemetry>(), It.IsAny<Dictionary<string, string>>()))
                .Callback<LoRaDeviceTelemetry, Dictionary<string, string>>((t, d) => receivedTelemetryItems.Add(t))
                 .ReturnsAsync(true);

            // will check for c2d msg
            LoRaDeviceClient.Setup(x => x.ReceiveAsync(It.IsAny<TimeSpan>()))
                .ReturnsAsync((Message)null);

            // first device client will be disposed
            LoRaDeviceClient.Setup(x => x.DisposeAsync());

            // Lora device api will be search by devices with matching deveui,
            LoRaDeviceApi.Setup(x => x.SearchByDevAddrAsync(devAddr))
                .ReturnsAsync(new SearchDevicesResult(new IoTHubDeviceInfo(devAddr, devEui, "aabb").AsList()));

            // Making the function bundler return a processable message only for multigw scenario
            if (deviceGatewayID is null)
            {
                LoRaDeviceApi
                    .Setup(x => x.ExecuteFunctionBundlerAsync(devEui, It.IsAny<FunctionBundlerRequest>()))
                    .ReturnsAsync(() =>
                        new FunctionBundlerResult
                        {
                            DeduplicationResult = new DeduplicationResult { GatewayId = ServerGatewayID, CanProcess = true, IsDuplicate = false },
                            AdrResult = null,
                            NextFCntDown = 0
                        });
            }

            using var cache = NewMemoryCache();
            await using var deviceRegistry = new LoRaDeviceRegistry(ServerConfiguration, cache, LoRaDeviceApi.Object, LoRaDeviceFactory, DeviceCache);

            // Setting the interval in which we search for devices with same devAddr on server
            deviceRegistry.DevAddrReloadInterval = TimeSpan.Zero;

            await using var messageDispatcher = TestMessageDispatcher.Create(
                cache,
                ServerConfiguration,
                deviceRegistry,
                FrameCounterUpdateStrategyProvider);

            // send 1st unconfirmed message, get twin will fail
            var unconfirmedMessage1 = simulatedDevice.CreateUnconfirmedDataUpMessage("1", fcnt: 1);
            using var request1 = CreateWaitableRequest(unconfirmedMessage1);
            messageDispatcher.DispatchRequest(request1);
            Assert.True(await request1.WaitCompleteAsync());
            Assert.Null(request1.ResponseDownlink);
            Assert.Empty(DownstreamMessageSender.DownlinkMessages);

            Assert.False(DeviceCache.TryGetForPayload(request1.Payload, out _));

            await cache.WaitForEvictionAsync(LoRaDeviceRegistry.GetDevLoaderCacheKey(devAddr), CancellationToken.None);

            // sends 2nd unconfirmed message, now get twin will work
            var unconfirmedMessage2 = simulatedDevice.CreateUnconfirmedDataUpMessage("2", fcnt: 2);
            using var request2 = CreateWaitableRequest(unconfirmedMessage2);
            messageDispatcher.DispatchRequest(request2);
            Assert.True(await request2.WaitCompleteAsync());
            Assert.True(request2.ProcessingSucceeded);
            Assert.Null(request2.ResponseDownlink);
            Assert.Empty(DownstreamMessageSender.DownlinkMessages);

            Assert.True(DeviceCache.TryGetForPayload(request2.Payload, out var loRaDevice));
            Assert.Equal(simulatedDevice.NwkSKey, loRaDevice.NwkSKey);
            Assert.Equal(simulatedDevice.AppSKey, loRaDevice.AppSKey);
            Assert.Equal(devAddr, loRaDevice.DevAddr);
            Assert.Equal(2U, loRaDevice.FCntUp);

            var telemetryItem = Assert.Single(receivedTelemetryItems);
            Assert.Equal(2, telemetryItem.Fcnt);
            Assert.Equal("2", ((DecodedPayloadValue)telemetryItem.Data).Value.ToString());

            LoRaDeviceClient.VerifyAll();
            LoRaDeviceApi.VerifyAll();
        }

        /// <summary>
        /// Tests that a ABP device (already in cached or not), receives 1st message with invalid mic, 2nd with valid
        /// should send message 2 to iot hub.
        /// </summary>
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task ABP_When_First_Message_Has_Invalid_Mic_Second_Should_Send_To_Hub(bool isAlreadyInDeviceRegistryCache)
        {
            var simulatedDevice = new SimulatedDevice(TestDeviceInfo.CreateABPDevice(1, gatewayID: ServerGatewayID));

            await using var loRaDevice = CreateLoRaDevice(simulatedDevice, isAlreadyInDeviceRegistryCache);
            loRaDevice.SensorDecoder = "DecoderValueSensor";

            // message will be sent
            LoRaDeviceTelemetry loRaDeviceTelemetry = null;
            LoRaDeviceClient.Setup(x => x.SendEventAsync(It.IsNotNull<LoRaDeviceTelemetry>(), null))
                .Callback<LoRaDeviceTelemetry, Dictionary<string, string>>((t, _) => loRaDeviceTelemetry = t)
                .ReturnsAsync(true);

            if (!isAlreadyInDeviceRegistryCache)
            {
                LoRaDeviceClient.Setup(x => x.GetTwinAsync(CancellationToken.None))
                    .ReturnsAsync(simulatedDevice.GetDefaultAbpTwin());
            }

            // C2D message will be checked
            LoRaDeviceClient.Setup(x => x.ReceiveAsync(It.IsNotNull<TimeSpan>()))
                .ReturnsAsync((Message)null);

            // will search for the device twice
            LoRaDeviceApi.Setup(x => x.SearchByDevAddrAsync(loRaDevice.DevAddr.Value))
                .ReturnsAsync(new SearchDevicesResult(new IoTHubDeviceInfo(loRaDevice.DevAddr, loRaDevice.DevEUI, "aaa").AsList()));

            // Send to message processor
            await using var messageDispatcherDisposableValue = SetupMessageDispatcherAsync(isAlreadyInDeviceRegistryCache ? loRaDevice : null);
            var messageDispatcher = messageDispatcherDisposableValue.Value;

            // first message should fail
            const int firstMessageFcnt = 3;
            var wrongNwkSKey = NetworkSessionKey.Parse("00000000000000000000000000001234");
            var unconfirmedMessageWithWrongMic = simulatedDevice.CreateUnconfirmedDataUpMessage("123", fcnt: firstMessageFcnt, nwkSKey: wrongNwkSKey);
            using var requestWithWrongMic = CreateWaitableRequest(unconfirmedMessageWithWrongMic);
            messageDispatcher.DispatchRequest(requestWithWrongMic);
            Assert.True(await requestWithWrongMic.WaitCompleteAsync());
            Assert.Null(requestWithWrongMic.ResponseDownlink);
            Assert.Equal(LoRaDeviceRequestFailedReason.NotMatchingDeviceByMicCheck, requestWithWrongMic.ProcessingFailedReason);

            // second message should succeed
            const uint secondMessageFcnt = 4;
            var unconfirmedMessageWithCorrectMic = simulatedDevice.CreateUnconfirmedDataUpMessage("456", fcnt: secondMessageFcnt);
            using var requestWithCorrectMic = CreateWaitableRequest(unconfirmedMessageWithCorrectMic);
            messageDispatcher.DispatchRequest(requestWithCorrectMic);
            Assert.True(await requestWithCorrectMic.WaitCompleteAsync());
            Assert.Null(requestWithCorrectMic.ResponseDownlink);

            Assert.NotNull(loRaDeviceTelemetry);
            Assert.IsType<DecodedPayloadValue>(loRaDeviceTelemetry.Data);
            var telemetryData = (DecodedPayloadValue)loRaDeviceTelemetry.Data;
            Assert.Equal("456", telemetryData.Value.ToString());

            Assert.True(DeviceCache.TryGetForPayload(requestWithCorrectMic.Payload, out var loRaDeviceFromRegistry));
            Assert.Equal(secondMessageFcnt, loRaDeviceFromRegistry.FCntUp);
            Assert.True(loRaDeviceFromRegistry.IsOurDevice);

            LoRaDeviceApi.VerifyAll();
            LoRaDeviceClient.VerifyAll();

            LoRaDeviceClient.Setup(ldc => ldc.DisposeAsync());
        }

        /// <summary>
        /// Tests that a ABP device without AppSKey should not send message to IoT Hub.
        /// </summary>
        [Theory]
        [InlineData(TwinProperty.AppSKey)]
        [InlineData(TwinProperty.NwkSKey)]
        [InlineData(TwinProperty.DevAddr)]
        public async Task ABP_When_AppSKey_Or_NwkSKey_Or_DevAddr_Is_Missing_Should_Not_Send_Message_To_Hub(string missingProperty)
        {
            var simulatedDevice = new SimulatedDevice(TestDeviceInfo.CreateABPDevice(1, gatewayID: ServerGatewayID));

            await using var loRaDevice = CreateLoRaDevice(simulatedDevice, false);
            loRaDevice.SensorDecoder = "DecoderValueSensor";

            // will get the device twin without AppSKey
            var twin = simulatedDevice.GetDefaultAbpTwin();
            twin.Properties.Desired[missingProperty] = null;
            LoRaDeviceClient.Setup(x => x.GetTwinAsync(CancellationToken.None))
                    .ReturnsAsync(twin);
            LoRaDeviceClient.Setup(x => x.DisposeAsync());
            // Lora device api

            // will search for the device twice
            LoRaDeviceApi.Setup(x => x.SearchByDevAddrAsync(loRaDevice.DevAddr.Value))
                .ReturnsAsync(new SearchDevicesResult(new IoTHubDeviceInfo(loRaDevice.DevAddr, loRaDevice.DevEUI, "aaa").AsList()));

            // Send to message processor
            await using var messageDispatcherDisposableValue = SetupMessageDispatcherAsync();
            var messageDispatcher = messageDispatcherDisposableValue.Value;

            // message should not be processed
            using var request = CreateWaitableRequest(simulatedDevice.CreateUnconfirmedDataUpMessage("1234"));
            messageDispatcher.DispatchRequest(request);
            Assert.True(await request.WaitCompleteAsync());
            Assert.Null(request.ResponseDownlink);

            Assert.False(DeviceCache.HasRegistrations(simulatedDevice.DevAddr.Value));

            LoRaDeviceApi.VerifyAll();
            LoRaDeviceClient.Verify(x => x.GetTwinAsync(CancellationToken.None), Times.Once);
        }

        [Theory]
        [InlineData(ServerGatewayID)]
        [InlineData(null)]
        public async Task When_ConfirmedUp_Message_Is_Resubmitted_Should_Ack_3_Times(string deviceGatewayID)
        {
            const uint deviceInitialFcntUp = 100;
            const uint deviceInitialFcntDown = 20;

            var simulatedDevice = new SimulatedDevice(TestDeviceInfo.CreateABPDevice(1, gatewayID: deviceGatewayID))
            {
                FrmCntUp = deviceInitialFcntUp,
                FrmCntDown = deviceInitialFcntDown
            };

            var devEui = simulatedDevice.LoRaDevice.DevEui;

            // C2D message will be checked
            LoRaDeviceClient.Setup(x => x.ReceiveAsync(It.IsNotNull<TimeSpan>()))
                .ReturnsAsync((Message)null);

            // We will send two messages
            LoRaDeviceClient.Setup(x => x.SendEventAsync(It.Is<LoRaDeviceTelemetry>(t => t.Fcnt == deviceInitialFcntUp + 1), It.IsAny<Dictionary<string, string>>()))
                .ReturnsAsync(true);

            LoRaDeviceClient.Setup(x => x.SendEventAsync(It.Is<LoRaDeviceTelemetry>(t => t.Fcnt == deviceInitialFcntUp + 2), It.IsAny<Dictionary<string, string>>()))
                .ReturnsAsync(true);

            // in multigateway scenario the device api will be called to resolve fcntDown
            if (string.IsNullOrEmpty(deviceGatewayID))
            {
                LoRaDeviceApi.Setup(x => x.ExecuteFunctionBundlerAsync(devEui, It.Is<FunctionBundlerRequest>(r => r.ClientFCntDown == deviceInitialFcntDown && r.ClientFCntUp == deviceInitialFcntUp + 1)))
                            .ReturnsAsync((DevEui _, FunctionBundlerRequest _) => new FunctionBundlerResult { AdrResult = new LoRaTools.ADR.LoRaADRResult { CanConfirmToDevice = true, NbRepetition = 1, TxPower = 0, FCntDown = deviceInitialFcntDown + 1 }, NextFCntDown = deviceInitialFcntDown + 1 });

                LoRaDeviceApi.Setup(x => x.ExecuteFunctionBundlerAsync(devEui, It.Is<FunctionBundlerRequest>(r => r.ClientFCntDown == deviceInitialFcntDown + 1 && r.ClientFCntUp == deviceInitialFcntUp + 1)))
                            .ReturnsAsync((DevEui _, FunctionBundlerRequest _) => new FunctionBundlerResult { AdrResult = new LoRaTools.ADR.LoRaADRResult { CanConfirmToDevice = true, NbRepetition = 1, TxPower = 0, FCntDown = deviceInitialFcntDown + 2 }, NextFCntDown = deviceInitialFcntDown + 2 });

                LoRaDeviceApi.Setup(x => x.ExecuteFunctionBundlerAsync(devEui, It.Is<FunctionBundlerRequest>(r => r.ClientFCntDown == deviceInitialFcntDown + 2 && r.ClientFCntUp == deviceInitialFcntUp + 1)))
                            .ReturnsAsync((DevEui _, FunctionBundlerRequest _) => new FunctionBundlerResult { AdrResult = new LoRaTools.ADR.LoRaADRResult { CanConfirmToDevice = true, NbRepetition = 1, TxPower = 0, FCntDown = deviceInitialFcntDown + 3 }, NextFCntDown = deviceInitialFcntDown + 3 });

                LoRaDeviceApi.Setup(x => x.ExecuteFunctionBundlerAsync(devEui, It.Is<FunctionBundlerRequest>(r => r.ClientFCntDown == deviceInitialFcntDown + 3 && r.ClientFCntUp == deviceInitialFcntUp + 1)))
                            .ReturnsAsync((DevEui _, FunctionBundlerRequest _) => new FunctionBundlerResult { AdrResult = new LoRaTools.ADR.LoRaADRResult { CanConfirmToDevice = true, NbRepetition = 1, TxPower = 0, FCntDown = deviceInitialFcntDown + 4 }, NextFCntDown = deviceInitialFcntDown + 4 });

                LoRaDeviceApi.Setup(x => x.ExecuteFunctionBundlerAsync(devEui, It.Is<FunctionBundlerRequest>(r => r.ClientFCntDown == deviceInitialFcntDown + 4 && r.ClientFCntUp == deviceInitialFcntUp + 2)))
                            .ReturnsAsync((DevEui _, FunctionBundlerRequest _) => new FunctionBundlerResult { AdrResult = new LoRaTools.ADR.LoRaADRResult { CanConfirmToDevice = true, NbRepetition = 1, TxPower = 0, FCntDown = deviceInitialFcntDown + 5 }, NextFCntDown = deviceInitialFcntDown + 5 });

                LoRaDeviceApi.Setup(x => x.ExecuteFunctionBundlerAsync(devEui, It.Is<FunctionBundlerRequest>(r => r.ClientFCntDown == deviceInitialFcntDown + 5 && r.ClientFCntUp == deviceInitialFcntUp + 2)))
                            .ReturnsAsync((DevEui _, FunctionBundlerRequest _) => new FunctionBundlerResult { AdrResult = new LoRaTools.ADR.LoRaADRResult { CanConfirmToDevice = true, NbRepetition = 1, TxPower = 0, FCntDown = deviceInitialFcntDown + 6 }, NextFCntDown = deviceInitialFcntDown + 6 });

                LoRaDeviceApi.Setup(x => x.ExecuteFunctionBundlerAsync(devEui, It.Is<FunctionBundlerRequest>(r => r.ClientFCntDown == deviceInitialFcntDown + 6 && r.ClientFCntUp == deviceInitialFcntUp + 2)))
                            .ReturnsAsync((DevEui _, FunctionBundlerRequest _) => new FunctionBundlerResult { AdrResult = new LoRaTools.ADR.LoRaADRResult { CanConfirmToDevice = true, NbRepetition = 1, TxPower = 0, FCntDown = deviceInitialFcntDown + 7 }, NextFCntDown = deviceInitialFcntDown + 7 });

                LoRaDeviceApi.Setup(x => x.ExecuteFunctionBundlerAsync(devEui, It.Is<FunctionBundlerRequest>(r => r.ClientFCntDown == deviceInitialFcntDown + 7 && r.ClientFCntUp == deviceInitialFcntUp + 2)))
                            .ReturnsAsync((DevEui _, FunctionBundlerRequest _) => new FunctionBundlerResult { AdrResult = new LoRaTools.ADR.LoRaADRResult { CanConfirmToDevice = true, NbRepetition = 1, TxPower = 0, FCntDown = deviceInitialFcntDown + 8 }, NextFCntDown = deviceInitialFcntDown + 8 });
            }

            // Send to message processor
            var loRaDevice = CreateLoRaDevice(simulatedDevice);
            await using var messageDispatcherDisposableValue = SetupMessageDispatcherAsync(loRaDevice);
            var messageDispatcher = messageDispatcherDisposableValue.Value;

            // sends confirmed message
            var firstMessagePayload = simulatedDevice.CreateConfirmedDataUpMessage("repeat", fcnt: deviceInitialFcntUp + 1);

            // 1x as new fcntUp and 3x as resubmit
            for (var i = 0; i < 4; i++)
            {
                using var firstMessageRequest = CreateWaitableRequest(firstMessagePayload);
                messageDispatcher.DispatchRequest(firstMessageRequest);
                Assert.True(await firstMessageRequest.WaitCompleteAsync());

                // ack should be received
                Assert.NotNull(firstMessageRequest.ResponseDownlink);
                Assert.Equal(i + 1, DownstreamMessageSender.DownlinkMessages.Count);
            }

            // resubmitting should fail
            using var fourthRequest = CreateWaitableRequest(firstMessagePayload);
            messageDispatcher.DispatchRequest(fourthRequest);
            Assert.True(await fourthRequest.WaitCompleteAsync());
            Assert.Null(fourthRequest.ResponseDownlink);
            Assert.Equal(4, DownstreamMessageSender.DownlinkMessages.Count);
            Assert.Equal(LoRaDeviceRequestFailedReason.ConfirmationResubmitThresholdExceeded, fourthRequest.ProcessingFailedReason);

            // Sending the next fcnt with failed messages should work, including resubmit
            var secondMessagePayload = simulatedDevice.CreateConfirmedDataUpMessage("repeat", fcnt: deviceInitialFcntUp + 2);

            // 1x as new fcntUp and 3x as resubmit
            for (var i = 0; i < 4; i++)
            {
                using var request = CreateWaitableRequest(secondMessagePayload);
                messageDispatcher.DispatchRequest(request);
                Assert.True(await request.WaitCompleteAsync());

                // ack should be received
                Assert.NotNull(request.ResponseDownlink);
                Assert.Equal(i + 5, DownstreamMessageSender.DownlinkMessages.Count);
            }

            // resubmitting should fail
            using var resubmitSecondRequest = CreateWaitableRequest(secondMessagePayload);
            messageDispatcher.DispatchRequest(resubmitSecondRequest);
            Assert.True(await resubmitSecondRequest.WaitCompleteAsync());
            Assert.Null(resubmitSecondRequest.ResponseDownlink);
            Assert.Equal(8, DownstreamMessageSender.DownlinkMessages.Count);
            Assert.Equal(LoRaDeviceRequestFailedReason.ConfirmationResubmitThresholdExceeded, resubmitSecondRequest.ProcessingFailedReason);

            Assert.Equal(2 + deviceInitialFcntUp, loRaDevice.FCntUp);
            Assert.Equal(8 + deviceInitialFcntDown, loRaDevice.FCntDown);

            LoRaDeviceClient.VerifyAll();
            LoRaDeviceApi.VerifyAll();
        }

        [Fact]
        public async Task ABP_Device_With_Invalid_NetId_Should_Not_Load_Devices()
        {
            var msgPayload = "1234";
            var simulatedDevice = new SimulatedDevice(TestDeviceInfo.CreateABPDevice(1, netId: 0));

            // Send to message processor
            await using var messageDispatcherDisposableValue = SetupMessageDispatcherAsync();
            var messageDispatcher = messageDispatcherDisposableValue.Value;

            // sends unconfirmed message #1
            var unconfirmedMessagePayload1 = simulatedDevice.CreateUnconfirmedDataUpMessage(msgPayload, fcnt: 1);
            using var request1 = CreateWaitableRequest(unconfirmedMessagePayload1);
            messageDispatcher.DispatchRequest(request1);
            Assert.True(await request1.WaitCompleteAsync());
            Assert.Null(request1.ResponseDownlink);
            Assert.True(request1.ProcessingFailed);
            Assert.Equal(LoRaDeviceRequestFailedReason.InvalidNetId, request1.ProcessingFailedReason);

            // sends unconfirmed message #2
            var unconfirmedMessagePayload2 = simulatedDevice.CreateUnconfirmedDataUpMessage(msgPayload, fcnt: 2);
            using var request2 = CreateWaitableRequest(unconfirmedMessagePayload2);
            messageDispatcher.DispatchRequest(request2);
            Assert.True(await request2.WaitCompleteAsync());
            Assert.Null(request2.ResponseDownlink);
            Assert.True(request2.ProcessingFailed);
            Assert.Equal(LoRaDeviceRequestFailedReason.InvalidNetId, request2.ProcessingFailedReason);

            LoRaDeviceClient.VerifyAll();
            LoRaDeviceApi.VerifyAll();
        }

        [Fact]
        public async Task ABP_Device_With_Invalid_NetId_In_Allowed_DevAdr_Should_Be_Accepted()
        {
            var msgPayload = "1234";
            var deviceClient = new Mock<ILoRaDeviceClient>(MockBehavior.Strict);
            deviceClient.Setup(dc => dc.DisposeAsync()).Returns(ValueTask.CompletedTask);
            var simulatedDevice = new SimulatedDevice(TestDeviceInfo.CreateABPDevice(1, netId: 0, gatewayID: ServerGatewayID));

            var devAddr = simulatedDevice.LoRaDevice.DevAddr.Value;

            // Add this device to the allowed dev address list
            ServerConfiguration.AllowedDevAddresses = new HashSet<DevAddr>(1)
            {
                simulatedDevice.DevAddr.Value
            };

            // Send to message processor
            await using var messageDispatcherDisposableValue = SetupMessageDispatcherAsync();
            var messageDispatcher = messageDispatcherDisposableValue.Value;

            // device api will be searched for payload
            var searchDevicesResult = new SearchDevicesResult(new[]
            {
                new IoTHubDeviceInfo(simulatedDevice.DevAddr, simulatedDevice.DevEUI, "device1"),
                new IoTHubDeviceInfo(simulatedDevice.DevAddr, simulatedDevice.DevEUI, "device2"),
            });

            LoRaDeviceApi.Setup(x => x.SearchByDevAddrAsync(devAddr))
                .ReturnsAsync(searchDevicesResult);

            deviceClient.Setup(x => x.EnsureConnected()).Returns(true);

            deviceClient.Setup(x => x.GetTwinAsync(CancellationToken.None)).ReturnsAsync(simulatedDevice.GetDefaultAbpTwin());

            deviceClient.Setup(x => x.DisconnectAsync(It.IsAny<CancellationToken>()))
               .Returns(Task.CompletedTask);

            deviceClient.Setup(x => x.EnsureConnected())
               .Returns(true);

            deviceClient.Setup(x => x.ReceiveAsync(It.IsNotNull<TimeSpan>()))
               .ReturnsAsync((Message)null);

            LoRaDeviceFactory.SetClient(simulatedDevice.DevEUI, deviceClient.Object);
            deviceClient.Setup(x => x.UpdateReportedPropertiesAsync(It.IsNotNull<TwinCollection>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(true);

            var device1SentTelemetry = new List<LoRaDeviceTelemetry>();
            LoRaDeviceTelemetry loRaDeviceTelemetry = null;

            deviceClient.Setup(x => x.SendEventAsync(It.IsNotNull<LoRaDeviceTelemetry>(), null))
             .Callback<LoRaDeviceTelemetry, Dictionary<string, string>>((t, _) => device1SentTelemetry.Add(loRaDeviceTelemetry))
             .ReturnsAsync(true);

            // sends unconfirmed message #1
            var unconfirmedMessagePayload1 = simulatedDevice.CreateConfirmedDataUpMessage(msgPayload, fcnt: 1);
            using var request1 = CreateWaitableRequest(unconfirmedMessagePayload1);
            messageDispatcher.DispatchRequest(request1);
            Assert.True(await request1.WaitCompleteAsync());
            Assert.True(request1.ProcessingSucceeded);
            Assert.NotNull(request1.ResponseDownlink);

            // sends unconfirmed message #2
            var unconfirmedMessagePayload2 = simulatedDevice.CreateConfirmedDataUpMessage(msgPayload, fcnt: 2);
            using var request2 = CreateWaitableRequest(unconfirmedMessagePayload2);
            messageDispatcher.DispatchRequest(request2);
            Assert.True(await request2.WaitCompleteAsync());
            Assert.NotNull(request2.ResponseDownlink);
            Assert.True(request2.ProcessingSucceeded);

            LoRaDeviceClient.VerifyAll();
            LoRaDeviceApi.VerifyAll();
        }

        [Theory]
        [InlineData(1)] // ABP with soft reset
        [InlineData(11)]
        public async Task When_Loading_Multiple_Devices_With_Same_DevAddr_Should_Add_All_To_Cache_And_Process_Message(uint payloadFcntUp)
        {
            var isResetingDevice = payloadFcntUp <= 1;
            var simulatedDevice1 = new SimulatedDevice(TestDeviceInfo.CreateABPDevice(1, gatewayID: ServerGatewayID), frmCntDown: 5, frmCntUp: 10);

            var devAddr = simulatedDevice1.LoRaDevice.DevAddr.Value;

            var simulatedDevice2 = new SimulatedDevice(TestDeviceInfo.CreateABPDevice(2, gatewayID: ServerGatewayID), frmCntDown: 6, frmCntUp: 10)
            {
                DevAddr = devAddr
            };

            // message will be sent
            LoRaDeviceTelemetry loRaDeviceTelemetry = null;

            // Device client 1
            // - Get Twin
            // - Update twin (if isResetingDevice)
            // - Send event
            // - Check c2d message
            var device1SentTelemetry = new List<LoRaDeviceTelemetry>();
            var deviceClient1 = new Mock<ILoRaDeviceClient>();
            deviceClient1.Setup(x => x.SendEventAsync(It.IsNotNull<LoRaDeviceTelemetry>(), null))
                .Callback<LoRaDeviceTelemetry, Dictionary<string, string>>((t, _) => device1SentTelemetry.Add(loRaDeviceTelemetry))
                .ReturnsAsync(true);

            deviceClient1.Setup(x => x.ReceiveAsync(It.IsNotNull<TimeSpan>()))
                .ReturnsAsync((Message)null);

            deviceClient1.Setup(x => x.GetTwinAsync(CancellationToken.None)).ReturnsAsync(simulatedDevice1.GetDefaultAbpTwin());

            if (isResetingDevice)
            {
                deviceClient1.Setup(x => x.UpdateReportedPropertiesAsync(It.IsNotNull<TwinCollection>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(true);
            }

            // Device client 2
            // - Get Twin
            var deviceClient2 = new Mock<ILoRaDeviceClient>();
            deviceClient2.Setup(x => x.GetTwinAsync(CancellationToken.None)).ReturnsAsync(simulatedDevice2.GetDefaultAbpTwin());

            // device api will be searched for payload
            var searchDevicesResult = new SearchDevicesResult(new[]
            {
                new IoTHubDeviceInfo(simulatedDevice1.DevAddr, simulatedDevice1.DevEUI, "device1"),
                new IoTHubDeviceInfo(simulatedDevice2.DevAddr, simulatedDevice2.DevEUI, "device2"),
            });

            LoRaDeviceApi.Setup(x => x.SearchByDevAddrAsync(devAddr))
                .ReturnsAsync(searchDevicesResult);

            LoRaDeviceFactory.SetClient(simulatedDevice1.DevEUI, deviceClient1.Object);
            LoRaDeviceFactory.SetClient(simulatedDevice2.DevEUI, deviceClient2.Object);

            // Send to message processor
            await using var messageDispatcherDisposableValue = SetupMessageDispatcherAsync();
            var messageDispatcher = messageDispatcherDisposableValue.Value;

            // sends unconfirmed message #1
            var unconfirmedMessagePayload1 = simulatedDevice1.CreateUnconfirmedDataUpMessage("1", fcnt: payloadFcntUp);
            using var request1 = CreateWaitableRequest(unconfirmedMessagePayload1);
            messageDispatcher.DispatchRequest(request1);
            Assert.True(await request1.WaitCompleteAsync());
            Assert.Null(request1.ResponseDownlink);
            Assert.True(request1.ProcessingSucceeded);
            Assert.Single(device1SentTelemetry);

            // sends unconfirmed message #2
            var unconfirmedMessagePayload2 = simulatedDevice1.CreateUnconfirmedDataUpMessage("2", fcnt: payloadFcntUp + 1);
            using var request2 = CreateWaitableRequest(unconfirmedMessagePayload2);
            messageDispatcher.DispatchRequest(request2);
            Assert.True(await request2.WaitCompleteAsync());
            Assert.Null(request2.ResponseDownlink);
            Assert.True(request2.ProcessingSucceeded);
            Assert.Equal(2, device1SentTelemetry.Count);

            // Ensure that the devices have been cached

            Assert.Equal(2, DeviceCache.RegistrationCount(simulatedDevice1.DevAddr.Value));
            Assert.True(DeviceCache.TryGetForPayload(request1.Payload, out var loRaDevice1));

            // If the fcnt made a reset (0-1) the fcntdown is zero
            if (isResetingDevice)
            {
                Assert.Equal(0U, loRaDevice1.FCntDown);
            }
            else
            {
                Assert.Equal(simulatedDevice1.FrmCntDown + NetworkServer.Constants.MaxFcntUnsavedDelta - 1U, loRaDevice1.FCntDown);
            }

            Assert.Equal(payloadFcntUp + 1, loRaDevice1.FCntUp);

            Assert.True(DeviceCache.TryGetByDevEui(simulatedDevice2.DevEUI, out var loRaDevice2));
            Assert.Equal(simulatedDevice2.FrmCntUp, loRaDevice2.FCntUp);
            Assert.Equal(simulatedDevice2.FrmCntDown + NetworkServer.Constants.MaxFcntUnsavedDelta - 1U, loRaDevice2.FCntDown);

            deviceClient1.VerifyAll();
            deviceClient2.VerifyAll();
            LoRaDeviceClient.VerifyAll();
            LoRaDeviceApi.VerifyAll();

            // devices were loaded only once
            LoRaDeviceApi.Verify(x => x.SearchByDevAddrAsync(It.IsAny<DevAddr>()), Times.Once());
            deviceClient1.Verify(x => x.GetTwinAsync(CancellationToken.None), Times.Once());
            deviceClient2.Verify(x => x.GetTwinAsync(CancellationToken.None), Times.Once());
        }

        [Theory]
        [InlineData(1)] // ABP with soft reset
        [InlineData(11)]
        public async Task When_Loading_Multiple_Devices_With_Same_DevAddr_One_Fails_Should_Add_One_To_Cache_And_Process_Message(uint payloadFcntUp)
        {
            var simulatedDevice1 = new SimulatedDevice(TestDeviceInfo.CreateABPDevice(1, gatewayID: ServerGatewayID));

            var devAddr = simulatedDevice1.LoRaDevice.DevAddr.Value;

            var simulatedDevice2 = new SimulatedDevice(TestDeviceInfo.CreateABPDevice(2, gatewayID: ServerGatewayID))
            {
                DevAddr = devAddr
            };

            // message will be sent
            LoRaDeviceTelemetry loRaDeviceTelemetry = null;

            // Device client 1
            // - Get Twin
            // - Update twin (if isResetingDevice)
            // - Send event
            // - Check c2d message
            var device1SentTelemetry = new List<LoRaDeviceTelemetry>();
            var deviceClient1 = new Mock<ILoRaDeviceClient>();
            deviceClient1.Setup(x => x.SendEventAsync(It.IsNotNull<LoRaDeviceTelemetry>(), null))
                .Callback<LoRaDeviceTelemetry, Dictionary<string, string>>((t, _) => device1SentTelemetry.Add(loRaDeviceTelemetry))
                .ReturnsAsync(true);

            deviceClient1.Setup(x => x.ReceiveAsync(It.IsNotNull<TimeSpan>()))
                .ReturnsAsync((Message)null);

            deviceClient1.Setup(x => x.GetTwinAsync(CancellationToken.None)).ReturnsAsync(simulatedDevice1.GetDefaultAbpTwin());

            // If the framecounter is higher than 10 it will trigger an update of the framcounter in the reported properties.
            if (payloadFcntUp > 10)
            {
                deviceClient1.Setup(x => x.UpdateReportedPropertiesAsync(It.IsAny<TwinCollection>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
            }

            // Device client 2
            // - Get Twin -> throws TimeoutException
            var deviceClient2 = new Mock<ILoRaDeviceClient>();
            deviceClient2.Setup(x => x.GetTwinAsync(CancellationToken.None)).ThrowsAsync(new TimeoutException(), TimeSpan.FromMilliseconds(100));
            // device api will be searched for payload
            var searchDevicesResult = new SearchDevicesResult(new[]
            {
                new IoTHubDeviceInfo(simulatedDevice1.DevAddr, simulatedDevice1.DevEUI, "device1"),
                new IoTHubDeviceInfo(simulatedDevice2.DevAddr, simulatedDevice2.DevEUI, "device2"),
            });

            LoRaDeviceApi.Setup(x => x.SearchByDevAddrAsync(devAddr))
                .ReturnsAsync(searchDevicesResult);

            LoRaDeviceFactory.SetClient(simulatedDevice1.DevEUI, deviceClient1.Object);
            LoRaDeviceFactory.SetClient(simulatedDevice2.DevEUI, deviceClient2.Object);

            // Send to message processor
            await using var messageDispatcherDisposableValue = SetupMessageDispatcherAsync();
            var messageDispatcher = messageDispatcherDisposableValue.Value;

            // sends unconfirmed message #1
            var unconfirmedMessagePayload1 = simulatedDevice1.CreateUnconfirmedDataUpMessage("1", fcnt: payloadFcntUp);
            using var request1 = CreateWaitableRequest(unconfirmedMessagePayload1);
            messageDispatcher.DispatchRequest(request1);
            Assert.True(await request1.WaitCompleteAsync());
            Assert.Null(request1.ResponseDownlink);
            Assert.True(request1.ProcessingSucceeded);
            Assert.Single(device1SentTelemetry);

            // sends unconfirmed message #2
            var unconfirmedMessagePayload2 = simulatedDevice1.CreateUnconfirmedDataUpMessage("2", fcnt: payloadFcntUp + 1);
            using var request2 = CreateWaitableRequest(unconfirmedMessagePayload2);
            messageDispatcher.DispatchRequest(request2);
            Assert.True(await request2.WaitCompleteAsync());
            Assert.Null(request2.ResponseDownlink);
            Assert.True(request2.ProcessingSucceeded);
            Assert.Equal(2, device1SentTelemetry.Count);

            // Ensure that the device has been cached
            Assert.True(DeviceCache.TryGetForPayload(request2.Payload, out var loRaDevice1));

            // If the fcnt made a reset (0-1) the fcntdown is zero
            if (payloadFcntUp <= 1)
            {
                Assert.Equal(0U, loRaDevice1.FCntDown);
            }
            else
            {
                Assert.Equal(NetworkServer.Constants.MaxFcntUnsavedDelta - 1U, loRaDevice1.FCntDown);
            }

            Assert.Equal(payloadFcntUp + 1, loRaDevice1.FCntUp);

            deviceClient1.VerifyAll();
            deviceClient2.VerifyAll();
            LoRaDeviceClient.VerifyAll();
            LoRaDeviceApi.VerifyAll();

            // devices were loaded only once
            LoRaDeviceApi.Verify(x => x.SearchByDevAddrAsync(devAddr), Times.Once());
            deviceClient1.Verify(x => x.GetTwinAsync(CancellationToken.None), Times.Once());
            deviceClient2.Verify(x => x.GetTwinAsync(CancellationToken.None), Times.Once());
        }

        [Fact]
        public async Task When_Upstream_Is_Empty_Should_Call_Decoder_And_Send_Event_To_IoTHub()
        {
            var simDevice = new SimulatedDevice(TestDeviceInfo.CreateABPDevice(1, gatewayID: ServerGatewayID));

            var devEui = simDevice.LoRaDevice.DevEui;

            var loRaDevice = CreateLoRaDevice(simDevice);

            var receivedTelemetryItems = new List<LoRaDeviceTelemetry>();
            LoRaDeviceClient.Setup(x => x.SendEventAsync(It.IsNotNull<LoRaDeviceTelemetry>(), null))
                .ReturnsAsync(true)
                .Callback<LoRaDeviceTelemetry, Dictionary<string, string>>((t, _) => receivedTelemetryItems.Add(t));

            LoRaDeviceClient.Setup(x => x.ReceiveAsync(It.IsNotNull<TimeSpan>()))
                .ReturnsAsync((Message)null);

            var payloadDecoder = new Mock<ILoRaPayloadDecoder>(MockBehavior.Strict);
            var receivedDecodeCalls = new List<(FramePort, byte[])>();
            payloadDecoder.Setup(x => x.DecodeMessageAsync(devEui, It.IsAny<byte[]>(), FramePorts.App1, It.IsAny<string>()))
                .ReturnsAsync(new DecodePayloadResult("fport_1_decoded"))
                .Callback((DevEui _, byte[] data, FramePort fport, string decoder) => receivedDecodeCalls.Add((fport, data)));
            PayloadDecoder.SetDecoder(payloadDecoder.Object);

            // Send to message processor
            await using var messageDispatcherDisposableValue = SetupMessageDispatcherAsync(loRaDevice);
            var messageDispatcher = messageDispatcherDisposableValue.Value;

            // sends unconfirmed message
            var unconfirmedMessagePayload1 = simDevice.CreateUnconfirmedDataUpMessage(null, fcnt: 4);
            using var request1 = CreateWaitableRequest(unconfirmedMessagePayload1);
            messageDispatcher.DispatchRequest(request1);
            Assert.True(await request1.WaitCompleteAsync());
            Assert.Null(request1.ResponseDownlink);
            Assert.True(request1.ProcessingSucceeded);

            var telemetryItem = Assert.Single(receivedTelemetryItems);
            Assert.NotNull(telemetryItem.Data);
            Assert.Equal(FramePorts.App1, telemetryItem.Port);
            Assert.Equal("fport_1_decoded", telemetryItem.Data.ToString());

            var decoderCall = Assert.Single(receivedDecodeCalls);
            Assert.Equal(FramePorts.App1, decoderCall.Item1);
            // input data is empty
            Assert.Null(decoderCall.Item2);

            LoRaDeviceClient.VerifyAll();
            LoRaDeviceApi.VerifyAll();
            payloadDecoder.VerifyAll();
        }

        [Theory]
        [InlineData(10, 9)]
        [InlineData(10, 10)]
        public async Task When_Upstream_Fcnt_Is_Lower_Or_Equal_To_Device_Should_Discard_Message(uint devFcntUp, uint payloadFcnt)
        {
            var simulatedDevice = new SimulatedDevice(TestDeviceInfo.CreateABPDevice(1, gatewayID: ServerGatewayID))
            {
                FrmCntUp = devFcntUp
            };

            var devEui = simulatedDevice.LoRaDevice.DevEui;
            var devAddr = simulatedDevice.LoRaDevice.DevAddr;

            var cachedDevice = CreateLoRaDevice(simulatedDevice, false);
            using var cache = EmptyMemoryCache();
            await using var loraDeviceCache = CreateDeviceCache(cachedDevice);
            await using var deviceRegistry = new LoRaDeviceRegistry(ServerConfiguration, cache, LoRaDeviceApi.Object, LoRaDeviceFactory, loraDeviceCache);

            await using var messageDispatcherDisposableValue = SetupMessageDispatcherAsync(cachedDevice);
            var messageDispatcher = messageDispatcherDisposableValue.Value;

            // sends unconfirmed message
            var unconfirmedMessagePayload = simulatedDevice.CreateUnconfirmedDataUpMessage("hello", fcnt: payloadFcnt);
            using var request = CreateWaitableRequest(unconfirmedMessagePayload);
            messageDispatcher.DispatchRequest(request);
            Assert.True(await request.WaitCompleteAsync());
            Assert.Null(request.ResponseDownlink);
            Assert.True(request.ProcessingFailed);
            Assert.Equal(LoRaDeviceRequestFailedReason.InvalidFrameCounter, request.ProcessingFailedReason);

            // verify that the device in device registry has correct properties and frame counters
            Assert.True(loraDeviceCache.TryGetForPayload(request.Payload, out var loRaDevice));
            Assert.Equal(devAddr, loRaDevice.DevAddr);
            Assert.Equal(devEui, loRaDevice.DevEUI);
            Assert.True(loRaDevice.IsABP);
            Assert.Equal(devFcntUp, loRaDevice.FCntUp);
            Assert.Equal(0U, loRaDevice.FCntDown); // fctn down will always be set to zero
            Assert.False(loRaDevice.HasFrameCountChanges); // no changes

            LoRaDeviceClient.VerifyAll();
            LoRaDeviceApi.VerifyAll();
        }

        [Fact]
        public async Task When_Receiving_Device_Message_And_Loading_Device_Fails_Second_Message_Should_Be_Processed()
        {
            var simDevice = new SimulatedDevice(TestDeviceInfo.CreateABPDevice(1, gatewayID: ServerGatewayID))
            {
                FrmCntDown = 10,
                FrmCntUp = 50
            };

            LoRaDeviceApi.Setup(x => x.SearchByDevAddrAsync(simDevice.DevAddr.Value))
                .ReturnsAsync(new SearchDevicesResult(new IoTHubDeviceInfo(simDevice.DevAddr, simDevice.DevEUI, "123").AsList()));

            LoRaDeviceClient.SetupSequence(x => x.GetTwinAsync(CancellationToken.None))
                .ThrowsAsync(new TimeoutException())
                .ReturnsAsync(simDevice.GetDefaultAbpTwin());

            LoRaDeviceClient.Setup(x => x.SendEventAsync(It.IsNotNull<LoRaDeviceTelemetry>(), null))
                .ReturnsAsync(true);

            LoRaDeviceClient.Setup(x => x.ReceiveAsync(It.IsNotNull<TimeSpan>()))
                .ReturnsAsync((Message)null);

            await using var messageDispatcherDisposableValue = SetupMessageDispatcherAsync();
            var messageDispatcher = messageDispatcherDisposableValue.Value;

            using var request1 = CreateWaitableRequest(simDevice.CreateUnconfirmedDataUpMessage("1"));
            messageDispatcher.DispatchRequest(request1);
            Assert.True(await request1.WaitCompleteAsync());
            Assert.True(request1.ProcessingFailed);
            Assert.Equal(LoRaDeviceRequestFailedReason.NotMatchingDeviceByDevAddr, request1.ProcessingFailedReason);

            // give time for the loader to be removed from cache
            await Task.Delay(150);

            using var request2 = CreateWaitableRequest(simDevice.CreateUnconfirmedDataUpMessage("2"));
            messageDispatcher.DispatchRequest(request2);
            Assert.True(await request2.WaitCompleteAsync());
            Assert.True(request2.ProcessingSucceeded);

            LoRaDeviceApi.Verify(x => x.SearchByDevAddrAsync(simDevice.DevAddr.Value), Times.Exactly(2));
            LoRaDeviceApi.VerifyAll();
            LoRaDeviceClient.VerifyAll();
        }

        [Theory]
        [CombinatorialData]
        public async Task When_Receiving_Relax_Reset_Fcnt_Of_New_Device_Should_Not_Save_Fcnt(
            [CombinatorialValues(null, ServerGatewayID)] string gatewayID,
            [CombinatorialValues(0, 1)] uint payloadFcnt)
        {
            var simDevice = new SimulatedDevice(TestDeviceInfo.CreateABPDevice(1, gatewayID: gatewayID));

            LoRaDeviceApi.Setup(x => x.SearchByDevAddrAsync(simDevice.DevAddr.Value))
                .ReturnsAsync(new SearchDevicesResult(new IoTHubDeviceInfo(simDevice.DevAddr, simDevice.DevEUI, "123").AsList()));

            if (string.IsNullOrEmpty(gatewayID))
            {
                LoRaDeviceApi.Setup(x => x.ABPFcntCacheResetAsync(simDevice.DevEUI, It.IsAny<uint>(), It.IsNotNull<string>()))
                    .ReturnsAsync(true);
            }

            LoRaDeviceClient.Setup(x => x.GetTwinAsync(CancellationToken.None))
                .ReturnsAsync(simDevice.GetDefaultAbpTwin());

            LoRaDeviceClient.Setup(x => x.SendEventAsync(It.IsNotNull<LoRaDeviceTelemetry>(), null))
                .ReturnsAsync(true);

            LoRaDeviceClient.Setup(x => x.ReceiveAsync(It.IsNotNull<TimeSpan>()))
                .ReturnsAsync((Message)null);

            // Making the function bundler return a processable message only for multigw scenario
            if (gatewayID is null)
            {
                LoRaDeviceApi
                    .Setup(x => x.ExecuteFunctionBundlerAsync(simDevice.DevEUI, It.IsAny<FunctionBundlerRequest>()))
                    .ReturnsAsync(() =>
                        new FunctionBundlerResult
                        {
                            DeduplicationResult = new DeduplicationResult { GatewayId = ServerGatewayID, CanProcess = true, IsDuplicate = false },
                            AdrResult = null,
                            NextFCntDown = 0
                        });
            }

            await using var messageDispatcherDisposableValue = SetupMessageDispatcherAsync();
            var messageDispatcher = messageDispatcherDisposableValue.Value;

            using var request1 = CreateWaitableRequest(simDevice.CreateUnconfirmedDataUpMessage("1", fcnt: payloadFcnt));
            messageDispatcher.DispatchRequest(request1);
            Assert.True(await request1.WaitCompleteAsync());
            Assert.True(request1.ProcessingSucceeded);

            LoRaDeviceApi.VerifyAll();
            LoRaDeviceClient.VerifyAll();

            LoRaDeviceClient.Verify(x => x.UpdateReportedPropertiesAsync(It.IsAny<TwinCollection>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        private IDisposable UsePendingCloudToDeviceMessage(Exception completeOperationException = null) => UsePendingCloudToDeviceMessage(Guid.NewGuid().ToString(), completeOperationException);

        private IDisposable UsePendingCloudToDeviceMessage(string payload, Exception completeOperationException = null)
        {
            var cloudToDeviceMessage = new ReceivedLoRaCloudToDeviceMessage { Payload = payload, Fport = FramePorts.App1 }.CreateMessage();
            LoRaDeviceClient.SetupSequence(x => x.ReceiveAsync(It.IsAny<TimeSpan>()))
                            .ReturnsAsync(cloudToDeviceMessage)
                            .ReturnsAsync((Message)null); // 2nd cloud to device message does not return anything

            if (completeOperationException is { } someCompleteOperationException)
            {
                LoRaDeviceClient.Setup(x => x.CompleteAsync(cloudToDeviceMessage))
                                .ThrowsAsync(someCompleteOperationException);
            }
            else
            {
                LoRaDeviceClient.Setup(x => x.CompleteAsync(cloudToDeviceMessage))
                                .ReturnsAsync(true);
            }

            return cloudToDeviceMessage;
        }

        private AsyncDisposableValue<MessageDispatcher> SetupMessageDispatcherAsync() => SetupMessageDispatcherAsync(null);

        private AsyncDisposableValue<MessageDispatcher> SetupMessageDispatcherAsync(LoRaDevice loRaDevice)
        {
            var cache = EmptyMemoryCache();

            if (loRaDevice is { } someLoRaDevice)
            {
                DeviceCache.Register(someLoRaDevice);
            }
#pragma warning disable CA2000 // Dispose objects before losing scope (ownership transferred to caller)
            var deviceRegistry = new LoRaDeviceRegistry(ServerConfiguration, cache, LoRaDeviceApi.Object, LoRaDeviceFactory, DeviceCache);
            var messageDispatcher = TestMessageDispatcher.Create(cache, ServerConfiguration, deviceRegistry, FrameCounterUpdateStrategyProvider);
#pragma warning restore CA2000 // Dispose objects before losing scope
            return new AsyncDisposableValue<MessageDispatcher>(messageDispatcher, async () =>
            {
                cache.Dispose();
                await deviceRegistry.DisposeAsync();
                await messageDispatcher.DisposeAsync();
            });
        }
    }
}
