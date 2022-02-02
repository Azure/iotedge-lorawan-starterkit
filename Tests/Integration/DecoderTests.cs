// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Integration
{
    using System;
    using System.Collections.Generic;
    using System.Net.Http;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using LoRaWan.NetworkServer;
    using LoRaWan.Tests.Common;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Shared;
    using Moq;
    using Newtonsoft.Json;
    using Xunit;
    using Xunit.Abstractions;

    // End to end tests without external dependencies (IoT Hub, Service Facade Function)
    // Decoder tests tests
    public class DecoderTests : MessageProcessorTestBase
    {
        private readonly ITestOutputHelper testOutputHelper;

        public DecoderTests(ITestOutputHelper testOutputHelper) : base(testOutputHelper) => this.testOutputHelper = testOutputHelper;

        /// <summary>
        /// SensorDecoder: none
        /// Payload: multiple
        /// Decoder result: { "value": "base64value" }
        /// Expected data sent to IoT Hub:
        /// {
        ///     "time": null,
        ///     "tmms": 0,
        ///     "tmst": 0,
        ///     "freq": 868.3,
        ///     "chan": 0,
        ///     "rfch": 1,
        ///     "stat": 0,
        ///     "modu": "LORA",
        ///     "datr": "SF10BW125",
        ///     "codr": "4/5",
        ///     "rssi": 0,
        ///     "lsnr": 0.0,
        ///     "size": 17,
        ///     "data": {
        ///         value: "QAEAAAKAAQABD31lQaU5Vus="
        ///     },
        ///     "port": 1,
        ///     "fcnt": 1,
        ///     "rawdata": "QAEAAAKAAQABD31lQaU5Vus=",
        ///     "eui": "0000000000000001",
        ///     "gatewayid": "test-gateway",
        ///     "edgets": 1550217512748,
        ///     "stationeui": "0000000000000000",
        /// }.
        /// </summary>
        [Theory]
        [InlineData(ServerGatewayID, "1234", "")]
        [InlineData(ServerGatewayID, "hello world", null)]
        [InlineData(null, "hello world", null)]
        [InlineData(null, "1234", "")]
        public async Task When_No_Decoder_Is_Defined_Sends_Raw_Payload(string deviceGatewayID, string msgPayload, string sensorDecoder)
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
                // multi GW will reset
                LoRaDeviceApi.Setup(x => x.ABPFcntCacheResetAsync(It.IsNotNull<DevEui>(), It.IsAny<uint>(), It.IsNotNull<string>()))
                    .ReturnsAsync(true);
            }

            using var cache = EmptyMemoryCache();
            using var loraDeviceCache = CreateDeviceCache(loRaDevice);
            using var deviceRegistry = new LoRaDeviceRegistry(ServerConfiguration, cache, LoRaDeviceApi.Object, LoRaDeviceFactory, loraDeviceCache);

            // Send to message processor
            using var messageDispatcher = TestMessageDispatcher.Create(
                cache,
                ServerConfiguration,
                deviceRegistry,
                FrameCounterUpdateStrategyProvider);

            // sends unconfirmed message
            var unconfirmedMessagePayload = simulatedDevice.CreateUnconfirmedDataUpMessage(msgPayload, fcnt: 1);
            using var request = CreateWaitableRequest(unconfirmedMessagePayload);
            messageDispatcher.DispatchRequest(request);
            Assert.True(await request.WaitCompleteAsync());
            Assert.Null(request.ResponseDownlink);

            Assert.NotNull(loRaDeviceTelemetry);
            Assert.IsType<UndecodedPayload>(loRaDeviceTelemetry.Data);
            var undecodedPayload = (UndecodedPayload)loRaDeviceTelemetry.Data;
            var rawPayload = Convert.ToBase64String(Encoding.UTF8.GetBytes(msgPayload));
            Assert.Equal(rawPayload, loRaDeviceTelemetry.Rawdata);
            Assert.Equal(rawPayload, undecodedPayload.Value);

            // Validate json
            var actualJsonTelemetry = JsonConvert.SerializeObject(loRaDeviceTelemetry, Formatting.None);
            var expectedTelemetryJson = $"{{\"time\":100000,\"tmms\":100000,\"freq\":868.3,\"chan\":2,\"rfch\":1,\"modu\":\"LoRa\",\"datr\":\"SF10BW125\",\"rssi\":2.0,\"lsnr\":0.1,\"data\":{{\"value\":\"{rawPayload}\"}},\"port\":1,\"fcnt\":1,\"edgets\":{loRaDeviceTelemetry.Edgets},\"rawdata\":\"{rawPayload}\",\"eui\":\"0000000000000001\",\"gatewayid\":\"test-gateway\",\"stationeui\":\"0000000000000000\"}}";
            Assert.Equal(expectedTelemetryJson, actualJsonTelemetry);

            LoRaDeviceClient.VerifyAll();
            LoRaDeviceApi.VerifyAll();
        }

        /// <summary>
        /// SensorDecoder: DecoderValueSensor
        /// Payload: multiple
        /// Decoder result: { "value": $VALUE } where $VALUE is quoted if string
        /// Expected data sent to IoT Hub:
        /// {
        ///     "time": null,
        ///     "tmms": 0,
        ///     "tmst": 0,
        ///     "freq": 868.3,
        ///     "chan": 0,
        ///     "rfch": 1,
        ///     "stat": 0,
        ///     "modu": "LORA",
        ///     "datr": "SF10BW125",
        ///     "codr": "4/5",
        ///     "rssi": 0,
        ///     "lsnr": 0.0,
        ///     "size": 15,
        ///     "data": {
        ///         "value": "$1"
        ///     },
        ///     "port": 1,
        ///     "fcnt": 1,
        ///     "rawdata": "JDE=",
        ///     "eui": "0000000000000001",
        ///     "gatewayid": "test-gateway",
        ///     "edgets": 1550223375041
        ///     "stationeui": "0000000000000000"
        /// }.
        /// </summary>
        [Theory]
        [InlineData("hello world", "hello world")]
        [InlineData("$1", "$1")]
        [InlineData("100'000", "100'000")]
        [InlineData("1", 1L)]
        [InlineData("10", 10L)]
        [InlineData("0", 0L)]
        [InlineData("10.23", 10.23)]
        [InlineData("-10.23", -10.23)]
        public async Task When_Using_DecoderValueSensor_Should_Send_Decoded_Value(string msgPayload, object expectedValue)
        {
            var simulatedDevice = new SimulatedDevice(TestDeviceInfo.CreateABPDevice(1, gatewayID: ServerGatewayID));
            var loRaDevice = CreateLoRaDevice(simulatedDevice);
            loRaDevice.SensorDecoder = "DecoderValueSensor";

            // message will be sent
            LoRaDeviceTelemetry loRaDeviceTelemetry = null;
            LoRaDeviceClient.Setup(x => x.SendEventAsync(It.IsNotNull<LoRaDeviceTelemetry>(), null))
                .Callback<LoRaDeviceTelemetry, Dictionary<string, string>>((t, _) => loRaDeviceTelemetry = t)
                .ReturnsAsync(true);

            // C2D message will be checked
            LoRaDeviceClient.Setup(x => x.ReceiveAsync(It.IsNotNull<TimeSpan>()))
                .ReturnsAsync((Message)null);

            using var cache = EmptyMemoryCache();
            using var loraDeviceCache = CreateDeviceCache(loRaDevice);
            using var deviceRegistry = new LoRaDeviceRegistry(ServerConfiguration, cache, LoRaDeviceApi.Object, LoRaDeviceFactory, loraDeviceCache);

            // Send to message processor
            using var messageDispatcher = TestMessageDispatcher.Create(
                cache,
                ServerConfiguration,
                deviceRegistry,
                FrameCounterUpdateStrategyProvider);

            // sends unconfirmed message
            var unconfirmedMessagePayload = simulatedDevice.CreateUnconfirmedDataUpMessage(msgPayload, fcnt: 1);
            using var request = CreateWaitableRequest(unconfirmedMessagePayload);
            messageDispatcher.DispatchRequest(request);
            Assert.True(await request.WaitCompleteAsync());

            Assert.NotNull(loRaDeviceTelemetry);
            var rawPayload = Convert.ToBase64String(Encoding.UTF8.GetBytes(msgPayload));
            Assert.Equal(rawPayload, loRaDeviceTelemetry.Rawdata);
            Assert.IsType<DecodedPayloadValue>(loRaDeviceTelemetry.Data);
            var decodedPayload = (DecodedPayloadValue)loRaDeviceTelemetry.Data;
            Assert.Equal(expectedValue, decodedPayload.Value);

            // Validate json
            var actualJsonTelemetry = JsonConvert.SerializeObject(loRaDeviceTelemetry, Formatting.None);
            var expectedValueQuotes = expectedValue.GetType() == typeof(string) ? "\"" : string.Empty;
            var expectedTelemetryJson = $"{{\"time\":100000,\"tmms\":100000,\"freq\":868.3,\"chan\":2,\"rfch\":1,\"modu\":\"LoRa\",\"datr\":\"SF10BW125\",\"rssi\":2.0,\"lsnr\":0.1,\"data\":{{\"value\":{expectedValueQuotes}{msgPayload}{expectedValueQuotes}}},\"port\":1,\"fcnt\":1,\"edgets\":{loRaDeviceTelemetry.Edgets},\"rawdata\":\"{rawPayload}\",\"eui\":\"0000000000000001\",\"gatewayid\":\"test-gateway\",\"stationeui\":\"0000000000000000\"}}";
            Assert.Equal(expectedTelemetryJson, actualJsonTelemetry);

            LoRaDeviceApi.VerifyAll();
            LoRaDeviceClient.VerifyAll();
        }

        /// <summary>
        /// SensorDecoder: http://customdecoder/test1
        /// Decoder result: "decoded"
        /// Expected data sent to IoT Hub:
        /// {
        ///     "time": null,
        ///     "tmms": 0,
        ///     "tmst": 0,
        ///     "freq": 868.3,
        ///     "chan": 0,
        ///     "rfch": 1,
        ///     "stat": 0,
        ///     "modu": "LORA",
        ///     "datr": "SF10BW125",
        ///     "codr": "4/5",
        ///     "rssi": 0,
        ///     "lsnr": 0.0,
        ///     "size": 15,
        ///     "data": "decoded",
        ///     "port": 1,
        ///     "fcnt": 1,
        ///     "rawdata": "JDE=",
        ///     "eui": "0000000000000001",
        ///     "gatewayid": "test-gateway",
        ///     "edgets": 1550223375041,
        ///     "stationeui": "0000000000000000"
        /// }.
        /// </summary>
        [Fact]
        public async Task When_Using_Custom_Decoder_Returns_String_Should_Send_Decoded_Value()
        {
            var simulatedDevice = new SimulatedDevice(TestDeviceInfo.CreateABPDevice(1, gatewayID: ServerGatewayID));
            var loRaDevice = CreateLoRaDevice(simulatedDevice);
            loRaDevice.SensorDecoder = "http://customdecoder/test1";

            // message will be sent
            LoRaDeviceTelemetry loRaDeviceTelemetry = null;
            LoRaDeviceClient.Setup(x => x.SendEventAsync(It.IsNotNull<LoRaDeviceTelemetry>(), null))
                .Callback<LoRaDeviceTelemetry, Dictionary<string, string>>((t, _) => loRaDeviceTelemetry = t)
                .ReturnsAsync(true);

            // C2D message will be checked
            LoRaDeviceClient.Setup(x => x.ReceiveAsync(It.IsNotNull<TimeSpan>()))
                .ReturnsAsync((Message)null);

            using var cache = EmptyMemoryCache();
            using var loraDeviceCache = CreateDeviceCache(loRaDevice);
            using var deviceRegistry = new LoRaDeviceRegistry(ServerConfiguration, cache, LoRaDeviceApi.Object, LoRaDeviceFactory, loraDeviceCache);

            // Send to message processor
            using var messageDispatcher = TestMessageDispatcher.Create(
                cache,
                ServerConfiguration,
                deviceRegistry,
                FrameCounterUpdateStrategyProvider);

            using var httpMessageHandler = new HttpMessageHandlerMock();
            httpMessageHandler.SetupHandler((r) =>
            {
                return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                {
                    Content = new StringContent("decoded", Encoding.UTF8, "application/text"),
                };
            });

            using var httpClient = new HttpClient(httpMessageHandler);
            PayloadDecoder.SetDecoder(new LoRaPayloadDecoder(httpClient, new TestOutputLogger<LoRaPayloadDecoder>(this.testOutputHelper)));

            // sends unconfirmed message
            var unconfirmedMessagePayload = simulatedDevice.CreateUnconfirmedDataUpMessage("1", fcnt: 1);
            using var request = CreateWaitableRequest(unconfirmedMessagePayload);
            messageDispatcher.DispatchRequest(request);
            Assert.True(await request.WaitCompleteAsync());

            Assert.NotNull(loRaDeviceTelemetry);
            Assert.NotNull(loRaDeviceTelemetry.Data);
            var rawPayload = Convert.ToBase64String(Encoding.UTF8.GetBytes("1"));
            Assert.Equal(rawPayload, loRaDeviceTelemetry.Rawdata);

            // Validate json
            var actualJsonTelemetry = JsonConvert.SerializeObject(loRaDeviceTelemetry, Formatting.None);
            var expectedTelemetryJson = $"{{\"time\":100000,\"tmms\":100000,\"freq\":868.3,\"chan\":2,\"rfch\":1,\"modu\":\"LoRa\",\"datr\":\"SF10BW125\",\"rssi\":2.0,\"lsnr\":0.1,\"data\":\"decoded\",\"port\":1,\"fcnt\":1,\"edgets\":{loRaDeviceTelemetry.Edgets},\"rawdata\":\"{rawPayload}\",\"eui\":\"0000000000000001\",\"gatewayid\":\"test-gateway\",\"stationeui\":\"0000000000000000\"}}";
            Assert.Equal(expectedTelemetryJson, actualJsonTelemetry);

            LoRaDeviceApi.VerifyAll();
            LoRaDeviceClient.VerifyAll();
        }

        /// <summary>
        /// SensorDecoder: http://customdecoder/test1
        /// Decoder result: ""
        /// Expected data sent to IoT Hub:
        /// {
        ///     "time": null,
        ///     "tmms": 0,
        ///     "tmst": 0,
        ///     "freq": 868.3,
        ///     "chan": 0,
        ///     "rfch": 1,
        ///     "stat": 0,
        ///     "modu": "LORA",
        ///     "datr": "SF10BW125",
        ///     "codr": "4/5",
        ///     "rssi": 0,
        ///     "lsnr": 0.0,
        ///     "size": 15,
        ///     "data": "",
        ///     "port": 1,
        ///     "fcnt": 1,
        ///     "rawdata": "JDE=",
        ///     "eui": "0000000000000001",
        ///     "gatewayid": "test-gateway",
        ///     "edgets": 1550223375041,
        ///     "stationeui": "0000000000000000"
        /// }.
        /// </summary>
        [Fact]
        public async Task When_Using_Custom_Decoder_Returns_Empty_Should_Send_Decoded_Value()
        {
            var simulatedDevice = new SimulatedDevice(TestDeviceInfo.CreateABPDevice(1, gatewayID: ServerGatewayID));
            var loRaDevice = CreateLoRaDevice(simulatedDevice);
            loRaDevice.SensorDecoder = "http://customdecoder/test1";

            // message will be sent
            LoRaDeviceTelemetry loRaDeviceTelemetry = null;
            LoRaDeviceClient.Setup(x => x.SendEventAsync(It.IsNotNull<LoRaDeviceTelemetry>(), null))
                .Callback<LoRaDeviceTelemetry, Dictionary<string, string>>((t, _) => loRaDeviceTelemetry = t)
                .ReturnsAsync(true);

            // C2D message will be checked
            LoRaDeviceClient.Setup(x => x.ReceiveAsync(It.IsNotNull<TimeSpan>()))
                .ReturnsAsync((Message)null);

            using var cache = EmptyMemoryCache();
            using var loraDeviceCache = CreateDeviceCache(loRaDevice);
            using var deviceRegistry = new LoRaDeviceRegistry(ServerConfiguration, cache, LoRaDeviceApi.Object, LoRaDeviceFactory, loraDeviceCache);

            // Send to message processor
            using var messageDispatcher = TestMessageDispatcher.Create(
                cache,
                ServerConfiguration,
                deviceRegistry,
                FrameCounterUpdateStrategyProvider);

            using var httpMessageHandler = new HttpMessageHandlerMock();
            httpMessageHandler.SetupHandler((r) =>
            {
                return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                {
                    Content = new StringContent(string.Empty, Encoding.UTF8, "application/json"),
                };
            });

            using var httpClient = new HttpClient(httpMessageHandler);
            PayloadDecoder.SetDecoder(new LoRaPayloadDecoder(httpClient, new TestOutputLogger<LoRaPayloadDecoder>(this.testOutputHelper)));

            // sends unconfirmed message
            var unconfirmedMessagePayload = simulatedDevice.CreateUnconfirmedDataUpMessage("1", fcnt: 1);
            using var request = CreateWaitableRequest(unconfirmedMessagePayload);
            messageDispatcher.DispatchRequest(request);
            Assert.True(await request.WaitCompleteAsync());

            Assert.NotNull(loRaDeviceTelemetry);
            Assert.NotNull(loRaDeviceTelemetry.Data);
            var rawPayload = Convert.ToBase64String(Encoding.UTF8.GetBytes("1"));
            Assert.Equal(rawPayload, loRaDeviceTelemetry.Rawdata);

            // Validate json
            var actualJsonTelemetry = JsonConvert.SerializeObject(loRaDeviceTelemetry, Formatting.None);
            var expectedTelemetryJson = $"{{\"time\":100000,\"tmms\":100000,\"freq\":868.3,\"chan\":2,\"rfch\":1,\"modu\":\"LoRa\",\"datr\":\"SF10BW125\",\"rssi\":2.0,\"lsnr\":0.1,\"data\":\"\",\"port\":1,\"fcnt\":1,\"edgets\":{loRaDeviceTelemetry.Edgets},\"rawdata\":\"{rawPayload}\",\"eui\":\"0000000000000001\",\"gatewayid\":\"test-gateway\",\"stationeui\":\"0000000000000000\"}}";
            Assert.Equal(expectedTelemetryJson, actualJsonTelemetry);

            LoRaDeviceApi.VerifyAll();
            LoRaDeviceClient.VerifyAll();
        }

        /// <summary>
        /// SensorDecoder: http://customdecoder/test1
        /// Decoder result: { "value": "decoded" }
        /// Expected data sent to IoT Hub:
        /// {
        ///     "time": null,
        ///     "tmms": 0,
        ///     "tmst": 0,
        ///     "freq": 868.3,
        ///     "chan": 0,
        ///     "rfch": 1,
        ///     "stat": 0,
        ///     "modu": "LORA",
        ///     "datr": "SF10BW125",
        ///     "codr": "4/5",
        ///     "rssi": 0,
        ///     "lsnr": 0.0,
        ///     "size": 15,
        ///     "data": {
        ///         "value": "decoded"
        ///     },
        ///     "port": 1,
        ///     "fcnt": 1,
        ///     "rawdata": "JDE=",
        ///     "eui": "0000000000000001",
        ///     "gatewayid": "test-gateway",
        ///     "edgets": 1550223375041,
        ///     "stationeui": "0000000000000000"
        /// }.
        /// </summary>
        [Fact]
        public async Task When_Using_Custom_Decoder_Returns_JsonString_Should_Send_Decoded_Value()
        {
            var simulatedDevice = new SimulatedDevice(TestDeviceInfo.CreateABPDevice(1, gatewayID: ServerGatewayID));
            var loRaDevice = CreateLoRaDevice(simulatedDevice);
            loRaDevice.SensorDecoder = "http://customdecoder/test1";

            // message will be sent
            LoRaDeviceTelemetry loRaDeviceTelemetry = null;
            LoRaDeviceClient.Setup(x => x.SendEventAsync(It.IsNotNull<LoRaDeviceTelemetry>(), null))
                .Callback<LoRaDeviceTelemetry, Dictionary<string, string>>((t, _) => loRaDeviceTelemetry = t)
                .ReturnsAsync(true);

            // C2D message will be checked
            LoRaDeviceClient.Setup(x => x.ReceiveAsync(It.IsNotNull<TimeSpan>()))
                .ReturnsAsync((Message)null);

            using var cache = EmptyMemoryCache();
            using var loraDeviceCache = CreateDeviceCache(loRaDevice);
            using var deviceRegistry = new LoRaDeviceRegistry(ServerConfiguration, cache, LoRaDeviceApi.Object, LoRaDeviceFactory, loraDeviceCache);

            // Send to message processor
            using var messageDispatcher = TestMessageDispatcher.Create(
                cache,
                ServerConfiguration,
                deviceRegistry,
                FrameCounterUpdateStrategyProvider);

            using var httpMessageHandler = new HttpMessageHandlerMock();
            httpMessageHandler.SetupHandler((r) =>
            {
                return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                {
                    Content = new StringContent("{\"value\":\"decoded\"}", Encoding.UTF8, "application/json"),
                };
            });

            using var httpClient = new HttpClient(httpMessageHandler);
            PayloadDecoder.SetDecoder(new LoRaPayloadDecoder(httpClient, new TestOutputLogger<LoRaPayloadDecoder>(this.testOutputHelper)));

            // sends unconfirmed message
            var unconfirmedMessagePayload = simulatedDevice.CreateUnconfirmedDataUpMessage("1", fcnt: 1);
            using var request = CreateWaitableRequest(unconfirmedMessagePayload);
            messageDispatcher.DispatchRequest(request);
            Assert.True(await request.WaitCompleteAsync());

            Assert.NotNull(loRaDeviceTelemetry);
            Assert.NotNull(loRaDeviceTelemetry.Data);
            var rawPayload = Convert.ToBase64String(Encoding.UTF8.GetBytes("1"));
            Assert.Equal(rawPayload, loRaDeviceTelemetry.Rawdata);

            // Validate json
            var actualJsonTelemetry = JsonConvert.SerializeObject(loRaDeviceTelemetry, Formatting.None);
            var expectedTelemetryJson = $"{{\"time\":100000,\"tmms\":100000,\"freq\":868.3,\"chan\":2,\"rfch\":1,\"modu\":\"LoRa\",\"datr\":\"SF10BW125\",\"rssi\":2.0,\"lsnr\":0.1,\"data\":{{\"value\":\"decoded\"}},\"port\":1,\"fcnt\":1,\"edgets\":{loRaDeviceTelemetry.Edgets},\"rawdata\":\"{rawPayload}\",\"eui\":\"0000000000000001\",\"gatewayid\":\"test-gateway\",\"stationeui\":\"0000000000000000\"}}";
            Assert.Equal(expectedTelemetryJson, actualJsonTelemetry);

            LoRaDeviceApi.VerifyAll();
            LoRaDeviceClient.VerifyAll();
        }

        /// <summary>
        /// Using DecoderValueSensor should put the data inside a { value:"" } element
        /// {
        ///     "time": null,
        ///     "tmms": 0,
        ///     "tmst": 0,
        ///     "freq": 868.3,
        ///     "chan": 0,
        ///     "rfch": 1,
        ///     "stat": 0,
        ///     "modu": "LORA",
        ///     "datr": "SF10BW125",
        ///     "codr": "4/5",
        ///     "rssi": 0,
        ///     "lsnr": 0.0,
        ///     "size": 15,
        ///     "data": {
        ///         "temp" = 10,
        ///         "humidity" = 22.1,
        ///         "text" = "abc"
        ///     },
        ///     "port": 1,
        ///     "fcnt": 1,
        ///     "rawdata": "JDE=",
        ///     "eui": "0000000000000001",
        ///     "gatewayid": "test-gateway",
        ///     "edgets": 1550223375041,
        ///     "stationeui": "0000000000000000"
        /// }.
        /// </summary>
        [Fact]
        public async Task When_Using_Custom_Decoder_Returns_Complex_Object_Should_Send_Decoded_Value()
        {
            var simulatedDevice = new SimulatedDevice(TestDeviceInfo.CreateABPDevice(1, gatewayID: ServerGatewayID));
            var loRaDevice = CreateLoRaDevice(simulatedDevice);
            loRaDevice.SensorDecoder = "http://customdecoder/test1";

            // message will be sent
            LoRaDeviceTelemetry loRaDeviceTelemetry = null;
            LoRaDeviceClient.Setup(x => x.SendEventAsync(It.IsNotNull<LoRaDeviceTelemetry>(), null))
                .Callback<LoRaDeviceTelemetry, Dictionary<string, string>>((t, _) => loRaDeviceTelemetry = t)
                .ReturnsAsync(true);

            // C2D message will be checked
            LoRaDeviceClient.Setup(x => x.ReceiveAsync(It.IsNotNull<TimeSpan>()))
                .ReturnsAsync((Message)null);

            using var cache = EmptyMemoryCache();
            using var loraDeviceCache = CreateDeviceCache(loRaDevice);
            using var deviceRegistry = new LoRaDeviceRegistry(ServerConfiguration, cache, LoRaDeviceApi.Object, LoRaDeviceFactory, loraDeviceCache);

            // Send to message processor
            using var messageDispatcher = TestMessageDispatcher.Create(
                cache,
                ServerConfiguration,
                deviceRegistry,
                FrameCounterUpdateStrategyProvider);

            var decodedObject = new { temp = 10, humidity = 22.1, text = "abc" };

            using var httpMessageHandler = new HttpMessageHandlerMock();
            httpMessageHandler.SetupHandler((r) =>
            {
                return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                {
                    Content = new StringContent(JsonConvert.SerializeObject(decodedObject), Encoding.UTF8, "application/json"),
                };
            });

            using var httpClient = new HttpClient(httpMessageHandler);
            PayloadDecoder.SetDecoder(new LoRaPayloadDecoder(httpClient, new TestOutputLogger<LoRaPayloadDecoder>(this.testOutputHelper)));

            // sends unconfirmed message
            var unconfirmedMessagePayload = simulatedDevice.CreateUnconfirmedDataUpMessage("1", fcnt: 1);
            using var request = CreateWaitableRequest(unconfirmedMessagePayload);
            messageDispatcher.DispatchRequest(request);
            Assert.True(await request.WaitCompleteAsync());

            Assert.NotNull(loRaDeviceTelemetry);
            var rawPayload = Convert.ToBase64String(Encoding.UTF8.GetBytes("1"));
            Assert.Equal(rawPayload, loRaDeviceTelemetry.Rawdata);

            // Validate json
            var actualJsonTelemetry = JsonConvert.SerializeObject(loRaDeviceTelemetry, Formatting.None);
            var expectedTelemetryJson = $"{{\"time\":100000,\"tmms\":100000,\"freq\":868.3,\"chan\":2,\"rfch\":1,\"modu\":\"LoRa\",\"datr\":\"SF10BW125\",\"rssi\":2.0,\"lsnr\":0.1,\"data\":{{\"temp\":10,\"humidity\":22.1,\"text\":\"abc\"}},\"port\":1,\"fcnt\":1,\"edgets\":{loRaDeviceTelemetry.Edgets},\"rawdata\":\"{rawPayload}\",\"eui\":\"0000000000000001\",\"gatewayid\":\"test-gateway\",\"stationeui\":\"0000000000000000\"}}";
            Assert.Equal(expectedTelemetryJson, actualJsonTelemetry);

            LoRaDeviceApi.VerifyAll();
            LoRaDeviceClient.VerifyAll();
        }

        /// <summary>
        /// Using DecoderValueSensor should put the data inside a { value:"" } element
        /// {
        ///     "time": null,
        ///     "tmms": 0,
        ///     "tmst": 0,
        ///     "freq": 868.3,
        ///     "chan": 0,
        ///     "rfch": 1,
        ///     "stat": 0,
        ///     "modu": "LORA",
        ///     "datr": "SF10BW125",
        ///     "codr": "4/5",
        ///     "rssi": 0,
        ///     "lsnr": 0.0,
        ///     "size": 15,
        ///     "data": {
        ///         "error":"SensorDecoderModule 'http://customdecoder/test1?devEUI=0000000000000001&fport=1&payload=MQ%3d%3d' returned bad request.",
        ///         "errorDetail": "my error"
        ///     },
        ///     "port": 1,
        ///     "fcnt": 1,
        ///     "rawdata": "JDE=",
        ///     "eui": "0000000000000001",
        ///     "gatewayid": "test-gateway",
        ///     "edgets": 1550223375041,
        ///     "stationeui": "0000000000000000"
        /// }.
        /// </summary>
        [Fact]
        public async Task When_Using_Custom_Fails_Returns_Sets_Error_Information_In_Value()
        {
            var simulatedDevice = new SimulatedDevice(TestDeviceInfo.CreateABPDevice(1, gatewayID: ServerGatewayID));
            var loRaDevice = CreateLoRaDevice(simulatedDevice);
            loRaDevice.SensorDecoder = "http://customdecoder/test1";

            // message will be sent
            LoRaDeviceTelemetry loRaDeviceTelemetry = null;
            LoRaDeviceClient.Setup(x => x.SendEventAsync(It.IsNotNull<LoRaDeviceTelemetry>(), null))
                .Callback<LoRaDeviceTelemetry, Dictionary<string, string>>((t, _) => loRaDeviceTelemetry = t)
                .ReturnsAsync(true);

            // C2D message will be checked
            LoRaDeviceClient.Setup(x => x.ReceiveAsync(It.IsNotNull<TimeSpan>()))
                .ReturnsAsync((Message)null);

            using var cache = EmptyMemoryCache();
            using var loraDeviceCache = CreateDeviceCache(loRaDevice);
            using var deviceRegistry = new LoRaDeviceRegistry(ServerConfiguration, cache, LoRaDeviceApi.Object, LoRaDeviceFactory, loraDeviceCache);

            // Send to message processor
            using var messageDispatcher = TestMessageDispatcher.Create(
                cache,
                ServerConfiguration,
                deviceRegistry,
                FrameCounterUpdateStrategyProvider);

            using var httpMessageHandler = new HttpMessageHandlerMock();
            httpMessageHandler.SetupHandler((r) =>
            {
                return new HttpResponseMessage(System.Net.HttpStatusCode.BadRequest)
                {
                    Content = new StringContent("my error", Encoding.UTF8, "application/json"),
                };
            });

            using var httpClient = new HttpClient(httpMessageHandler);
            PayloadDecoder.SetDecoder(new LoRaPayloadDecoder(httpClient, new TestOutputLogger<LoRaPayloadDecoder>(this.testOutputHelper)));

            // sends unconfirmed message
            var unconfirmedMessagePayload = simulatedDevice.CreateUnconfirmedDataUpMessage("1", fcnt: 1);
            using var request = CreateWaitableRequest(unconfirmedMessagePayload);
            messageDispatcher.DispatchRequest(request);
            Assert.True(await request.WaitCompleteAsync());

            Assert.NotNull(loRaDeviceTelemetry);
            var rawPayload = Convert.ToBase64String(Encoding.UTF8.GetBytes("1"));
            Assert.Equal(rawPayload, loRaDeviceTelemetry.Rawdata);
            Assert.IsType<DecodingFailedPayload>(loRaDeviceTelemetry.Data);
            var decodedPayload = (DecodingFailedPayload)loRaDeviceTelemetry.Data;
            Assert.Equal("SensorDecoderModule 'http://customdecoder/test1?devEUI=0000000000000001&fport=1&payload=MQ%3d%3d' returned bad request.", decodedPayload.Error);
            Assert.Equal("my error", decodedPayload.ErrorDetail);

            // Validate json
            var actualJsonTelemetry = JsonConvert.SerializeObject(loRaDeviceTelemetry, Formatting.None);
            var expectedTelemetryJson = $"{{\"time\":100000,\"tmms\":100000,\"freq\":868.3,\"chan\":2,\"rfch\":1,\"modu\":\"LoRa\",\"datr\":\"SF10BW125\",\"rssi\":2.0,\"lsnr\":0.1,\"data\":{{\"error\":\"SensorDecoderModule 'http://customdecoder/test1?devEUI=0000000000000001&fport=1&payload=MQ%3d%3d' returned bad request.\",\"errorDetail\":\"my error\"}},\"port\":1,\"fcnt\":1,\"edgets\":{loRaDeviceTelemetry.Edgets},\"rawdata\":\"{ rawPayload}\",\"eui\":\"0000000000000001\",\"gatewayid\":\"test-gateway\",\"stationeui\":\"0000000000000000\"}}";
            Assert.Equal(expectedTelemetryJson, actualJsonTelemetry);

            LoRaDeviceApi.VerifyAll();
            LoRaDeviceClient.VerifyAll();
        }

        [Fact]
        public async Task When_Resent_Message_Using_Custom_Decoder_Returns_Complex_Object_Should_Send_Decoded_Value()
        {
            var simulatedDevice = new SimulatedDevice(TestDeviceInfo.CreateABPDevice(1, gatewayID: ServerGatewayID));
            var loRaDevice = CreateLoRaDevice(simulatedDevice);
            loRaDevice.SensorDecoder = "http://customdecoder/test1";

            // message will be sent
            LoRaDeviceTelemetry loRaDeviceTelemetry = null;
            _ = LoRaDeviceClient.Setup(x => x.SendEventAsync(It.IsNotNull<LoRaDeviceTelemetry>(), null))
                .Callback<LoRaDeviceTelemetry, Dictionary<string, string>>((t, _) => loRaDeviceTelemetry = t)
                .ReturnsAsync(true);
            _ = LoRaDeviceClient.Setup(x => x.UpdateReportedPropertiesAsync(It.IsAny<TwinCollection>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);

            // C2D message will be checked
            // LoRaDeviceClient.Setup(x => x.ReceiveAsync(It.IsNotNull<TimeSpan>()))
            // .ReturnsAsync((Message)null);
            using var cache = EmptyMemoryCache();
            using var loraDeviceCache = CreateDeviceCache(loRaDevice);
            using var deviceRegistry = new LoRaDeviceRegistry(ServerConfiguration, cache, LoRaDeviceApi.Object, LoRaDeviceFactory, loraDeviceCache);

            // Send to message processor
            using var messageDispatcher = TestMessageDispatcher.Create(
                cache,
                ServerConfiguration,
                deviceRegistry,
                FrameCounterUpdateStrategyProvider);

            var decodedObject = new { temp = 10, humidity = 22.1, text = "abc", cloudToDeviceMessage = new { test = 1 } };

            using var httpMessageHandler = new HttpMessageHandlerMock();
            _ = httpMessageHandler.SetupHandler((r) =>
            {
                return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                {
                    Content = new StringContent(JsonConvert.SerializeObject(decodedObject), Encoding.UTF8, "application/json"),
                };
            });

            using var httpClient = new HttpClient(httpMessageHandler);
            PayloadDecoder.SetDecoder(new LoRaPayloadDecoder(httpClient, new TestOutputLogger<LoRaPayloadDecoder>(this.testOutputHelper)));

            // sends confirmed message
            var confirmedMessagePayload = simulatedDevice.CreateConfirmedDataUpMessage("1", fcnt: 10);
            using var request = CreateWaitableRequest(confirmedMessagePayload);
            messageDispatcher.DispatchRequest(request);
            Assert.True(await request.WaitCompleteAsync());
            Assert.NotNull(request.ResponseDownlink);

            Assert.NotNull(loRaDeviceTelemetry);
            var rawPayload = Convert.ToBase64String(Encoding.UTF8.GetBytes("1"));
            Assert.Equal(rawPayload, loRaDeviceTelemetry.Rawdata);

            // Validate json
            var actualJsonTelemetry = JsonConvert.SerializeObject(loRaDeviceTelemetry, Formatting.None);

            var expectedTelemetryJson = $"{{\"time\":100000,\"tmms\":100000,\"freq\":868.3,\"chan\":2,\"rfch\":1,\"modu\":\"LoRa\",\"datr\":\"SF10BW125\",\"rssi\":2.0,\"lsnr\":0.1,\"data\":{{\"temp\":10,\"humidity\":22.1,\"text\":\"abc\"}},\"port\":1,\"fcnt\":10,\"edgets\":{loRaDeviceTelemetry.Edgets},\"rawdata\":\"{rawPayload}\",\"eui\":\"0000000000000001\",\"gatewayid\":\"test-gateway\",\"stationeui\":\"0000000000000000\"}}";
            Assert.Equal(expectedTelemetryJson, actualJsonTelemetry);

            // send a second confirmed message with same fcnt to simulate
            using var request2 = CreateWaitableRequest(confirmedMessagePayload);
            messageDispatcher.DispatchRequest(request2);
            Assert.True(await request2.WaitCompleteAsync());
            Assert.NotNull(request2.ResponseDownlink);
            Assert.NotNull(loRaDeviceTelemetry);
            var rawPayload2 = Convert.ToBase64String(Encoding.UTF8.GetBytes("1"));
            Assert.Equal(rawPayload2, loRaDeviceTelemetry.Rawdata);
            LoRaDeviceClient.Verify(x => x.SendEventAsync(It.IsNotNull<LoRaDeviceTelemetry>(), null), Times.Exactly(2));
            LoRaDeviceApi.VerifyAll();
            LoRaDeviceClient.VerifyAll();
        }
    }
}
