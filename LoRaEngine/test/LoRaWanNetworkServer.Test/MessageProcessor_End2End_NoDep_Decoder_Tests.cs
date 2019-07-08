// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer.Test
{
    using System;
    using System.Collections.Generic;
    using System.Net.Http;
    using System.Text;
    using System.Threading.Tasks;
    using LoRaWan.NetworkServer;
    using LoRaWan.Test.Shared;
    using Microsoft.Azure.Devices.Client;
    using Moq;
    using Newtonsoft.Json;
    using Xunit;

    // End to end tests without external dependencies (IoT Hub, Service Facade Function)
    // Decoder tests tests
    public class MessageProcessor_End2End_NoDep_Decoder_Tests : MessageProcessorTestBase
    {
        public MessageProcessor_End2End_NoDep_Decoder_Tests()
        {
        }

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
        ///     "edgets": 1550217512748
        /// }
        /// </summary>
        [Theory]
        [InlineData(ServerGatewayID, "1234", "")]
        [InlineData(ServerGatewayID, "hello world", null)]
        [InlineData(null, "hello world", null)]
        [InlineData(null, "1234", "")]
        public async Task When_No_Decoder_Is_Defined_Sends_Raw_Payload(string deviceGatewayID, string msgPayload, string sensorDecoder)
        {
            var simulatedDevice = new SimulatedDevice(TestDeviceInfo.CreateABPDevice(1, gatewayID: deviceGatewayID));
            var loRaDevice = this.CreateLoRaDevice(simulatedDevice);
            loRaDevice.SensorDecoder = sensorDecoder;

            // message will be sent
            LoRaDeviceTelemetry loRaDeviceTelemetry = null;
            this.LoRaDeviceClient.Setup(x => x.SendEventAsync(It.IsNotNull<LoRaDeviceTelemetry>(), null))
                .Callback<LoRaDeviceTelemetry, Dictionary<string, string>>((t, _) => loRaDeviceTelemetry = t)
                .ReturnsAsync(true);

            // C2D message will be checked
            this.LoRaDeviceClient.Setup(x => x.ReceiveAsync(It.IsNotNull<TimeSpan>()))
                .ReturnsAsync((Message)null);

            if (string.IsNullOrEmpty(deviceGatewayID))
            {
                // multi GW will reset
                this.LoRaDeviceApi.Setup(x => x.ABPFcntCacheResetAsync(It.IsNotNull<string>(), It.IsAny<uint>(), It.IsNotNull<string>()))
                    .ReturnsAsync(true);
            }

            var deviceRegistry = new LoRaDeviceRegistry(this.ServerConfiguration, this.NewNonEmptyCache(loRaDevice), this.LoRaDeviceApi.Object, this.LoRaDeviceFactory);

            // Send to message processor
            var messageDispatcher = new MessageDispatcher(
                this.ServerConfiguration,
                deviceRegistry,
                this.FrameCounterUpdateStrategyProvider);

            // sends unconfirmed message
            var unconfirmedMessagePayload = simulatedDevice.CreateUnconfirmedDataUpMessage(msgPayload, fcnt: 1);
            var rxpk = unconfirmedMessagePayload.SerializeUplink(simulatedDevice.AppSKey, simulatedDevice.NwkSKey).Rxpk[0];
            var request = new WaitableLoRaRequest(rxpk, this.PacketForwarder);
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
            var expectedTelemetryJson = $"{{\"time\":null,\"tmms\":0,\"tmst\":0,\"freq\":868.3,\"chan\":0,\"rfch\":1,\"stat\":0,\"modu\":\"LORA\",\"datr\":\"SF10BW125\",\"codr\":\"4/5\",\"rssi\":0,\"lsnr\":0.0,\"size\":{loRaDeviceTelemetry.Size},\"data\":{{\"value\":\"{rawPayload}\"}},\"port\":1,\"fcnt\":1,\"rawdata\":\"{rawPayload}\",\"eui\":\"0000000000000001\",\"gatewayid\":\"test-gateway\",\"edgets\":{loRaDeviceTelemetry.Edgets}}}";
            Assert.Equal(expectedTelemetryJson, actualJsonTelemetry);

            this.LoRaDeviceClient.VerifyAll();
            this.LoRaDeviceApi.VerifyAll();
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
        /// }
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
            var loRaDevice = this.CreateLoRaDevice(simulatedDevice);
            loRaDevice.SensorDecoder = "DecoderValueSensor";

            // message will be sent
            LoRaDeviceTelemetry loRaDeviceTelemetry = null;
            this.LoRaDeviceClient.Setup(x => x.SendEventAsync(It.IsNotNull<LoRaDeviceTelemetry>(), null))
                .Callback<LoRaDeviceTelemetry, Dictionary<string, string>>((t, _) => loRaDeviceTelemetry = t)
                .ReturnsAsync(true);

            // C2D message will be checked
            this.LoRaDeviceClient.Setup(x => x.ReceiveAsync(It.IsNotNull<TimeSpan>()))
                .ReturnsAsync((Message)null);

            var deviceRegistry = new LoRaDeviceRegistry(this.ServerConfiguration, this.NewNonEmptyCache(loRaDevice), this.LoRaDeviceApi.Object, this.LoRaDeviceFactory);

            // Send to message processor
            var messageDispatcher = new MessageDispatcher(
                this.ServerConfiguration,
                deviceRegistry,
                this.FrameCounterUpdateStrategyProvider);

            // sends unconfirmed message
            var unconfirmedMessagePayload = simulatedDevice.CreateUnconfirmedDataUpMessage(msgPayload, fcnt: 1);
            var rxpk = unconfirmedMessagePayload.SerializeUplink(simulatedDevice.AppSKey, simulatedDevice.NwkSKey).Rxpk[0];
            var request = new WaitableLoRaRequest(rxpk, this.PacketForwarder);
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
            var expectedTelemetryJson = $"{{\"time\":null,\"tmms\":0,\"tmst\":0,\"freq\":868.3,\"chan\":0,\"rfch\":1,\"stat\":0,\"modu\":\"LORA\",\"datr\":\"SF10BW125\",\"codr\":\"4/5\",\"rssi\":0,\"lsnr\":0.0,\"size\":{loRaDeviceTelemetry.Size},\"data\":{{\"value\":{expectedValueQuotes}{msgPayload}{expectedValueQuotes}}},\"port\":1,\"fcnt\":1,\"rawdata\":\"{rawPayload}\",\"eui\":\"0000000000000001\",\"gatewayid\":\"test-gateway\",\"edgets\":{loRaDeviceTelemetry.Edgets}}}";
            Assert.Equal(expectedTelemetryJson, actualJsonTelemetry);

            this.LoRaDeviceApi.VerifyAll();
            this.LoRaDeviceClient.VerifyAll();
        }

        /// <summary>
        /// SensorDecoder: DecoderValueSensor
        /// Rxpk: additional properties "x_power":22.3, "x_wind":"NE"
        /// Payload: "1"
        /// Decoder result: { "value": 1 }
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
        ///         "value":1
        ///     },
        ///     "port": 1,
        ///     "fcnt": 1,
        ///     "rawdata": "JDE=",
        ///     "eui": "0000000000000001",
        ///     "gatewayid": "test-gateway",
        ///     "edgets": 1550223375041,
        ///     "x_power":22.3,
        ///     "x_wind":"NE"
        /// }
        /// </summary>
        [Fact]
        public async Task When_Rxpk_Has_Additional_Information_Should_Include_In_Telemetry()
        {
            const string payload = "1";
            var simulatedDevice = new SimulatedDevice(TestDeviceInfo.CreateABPDevice(1, gatewayID: ServerGatewayID));
            var loRaDevice = this.CreateLoRaDevice(simulatedDevice);
            loRaDevice.SensorDecoder = "DecoderValueSensor";

            // message will be sent
            LoRaDeviceTelemetry loRaDeviceTelemetry = null;
            this.LoRaDeviceClient.Setup(x => x.SendEventAsync(It.IsNotNull<LoRaDeviceTelemetry>(), null))
                .Callback<LoRaDeviceTelemetry, Dictionary<string, string>>((t, _) => loRaDeviceTelemetry = t)
                .ReturnsAsync(true);

            // C2D message will be checked
            this.LoRaDeviceClient.Setup(x => x.ReceiveAsync(It.IsNotNull<TimeSpan>()))
                .ReturnsAsync((Message)null);

            var deviceRegistry = new LoRaDeviceRegistry(this.ServerConfiguration, this.NewNonEmptyCache(loRaDevice), this.LoRaDeviceApi.Object, this.LoRaDeviceFactory);

            // Send to message processor
            var messageDispatcher = new MessageDispatcher(
                this.ServerConfiguration,
                deviceRegistry,
                this.FrameCounterUpdateStrategyProvider);

            // sends unconfirmed message
            var unconfirmedMessagePayload = simulatedDevice.CreateUnconfirmedDataUpMessage(payload, fcnt: 1);
            var rxpk = unconfirmedMessagePayload.SerializeUplink(simulatedDevice.AppSKey, simulatedDevice.NwkSKey).Rxpk[0];
            rxpk.ExtraData["x_power"] = 22.3;
            rxpk.ExtraData["x_wind"] = "NE";
            var request = new WaitableLoRaRequest(rxpk, this.PacketForwarder);
            messageDispatcher.DispatchRequest(request);
            Assert.True(await request.WaitCompleteAsync());

            Assert.NotNull(loRaDeviceTelemetry);
            Assert.Equal(2, loRaDeviceTelemetry.ExtraData.Count);
            Assert.Equal(22.3, loRaDeviceTelemetry.ExtraData["x_power"]);
            Assert.Equal("NE", loRaDeviceTelemetry.ExtraData["x_wind"]);
            var rawPayload = Convert.ToBase64String(Encoding.UTF8.GetBytes(payload));
            Assert.Equal(rawPayload, loRaDeviceTelemetry.Rawdata);
            Assert.IsType<DecodedPayloadValue>(loRaDeviceTelemetry.Data);
            var decodedPayload = (DecodedPayloadValue)loRaDeviceTelemetry.Data;
            Assert.Equal(1L, decodedPayload.Value);

            // Validate json
            var actualJsonTelemetry = JsonConvert.SerializeObject(loRaDeviceTelemetry, Formatting.None);
            var expectedTelemetryJson = $"{{\"time\":null,\"tmms\":0,\"tmst\":0,\"freq\":868.3,\"chan\":0,\"rfch\":1,\"stat\":0,\"modu\":\"LORA\",\"datr\":\"SF10BW125\",\"codr\":\"4/5\",\"rssi\":0,\"lsnr\":0.0,\"size\":{loRaDeviceTelemetry.Size},\"data\":{{\"value\":1}},\"port\":1,\"fcnt\":1,\"rawdata\":\"{rawPayload}\",\"eui\":\"0000000000000001\",\"gatewayid\":\"test-gateway\",\"edgets\":{loRaDeviceTelemetry.Edgets},\"x_power\":22.3,\"x_wind\":\"NE\"}}";
            Assert.Equal(expectedTelemetryJson, actualJsonTelemetry);

            this.LoRaDeviceApi.VerifyAll();
            this.LoRaDeviceClient.VerifyAll();
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
        ///     "edgets": 1550223375041
        /// }
        /// </summary>
        [Fact]
        public async Task When_Using_Custom_Decoder_Returns_String_Should_Send_Decoded_Value()
        {
            var simulatedDevice = new SimulatedDevice(TestDeviceInfo.CreateABPDevice(1, gatewayID: ServerGatewayID));
            var loRaDevice = this.CreateLoRaDevice(simulatedDevice);
            loRaDevice.SensorDecoder = "http://customdecoder/test1";

            // message will be sent
            LoRaDeviceTelemetry loRaDeviceTelemetry = null;
            this.LoRaDeviceClient.Setup(x => x.SendEventAsync(It.IsNotNull<LoRaDeviceTelemetry>(), null))
                .Callback<LoRaDeviceTelemetry, Dictionary<string, string>>((t, _) => loRaDeviceTelemetry = t)
                .ReturnsAsync(true);

            // C2D message will be checked
            this.LoRaDeviceClient.Setup(x => x.ReceiveAsync(It.IsNotNull<TimeSpan>()))
                .ReturnsAsync((Message)null);

            var deviceRegistry = new LoRaDeviceRegistry(this.ServerConfiguration, this.NewNonEmptyCache(loRaDevice), this.LoRaDeviceApi.Object, this.LoRaDeviceFactory);

            // Send to message processor
            var messageDispatcher = new MessageDispatcher(
                this.ServerConfiguration,
                deviceRegistry,
                this.FrameCounterUpdateStrategyProvider);

            var httpMessageHandler = new HttpMessageHandlerMock();
            httpMessageHandler.SetupHandler((r) =>
            {
                return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                {
                    Content = new StringContent("decoded", Encoding.UTF8, "application/text"),
                };
            });

            this.PayloadDecoder.SetDecoder(new LoRaPayloadDecoder(new HttpClient(httpMessageHandler)));

            // sends unconfirmed message
            var unconfirmedMessagePayload = simulatedDevice.CreateUnconfirmedDataUpMessage("1", fcnt: 1);
            var rxpk = unconfirmedMessagePayload.SerializeUplink(simulatedDevice.AppSKey, simulatedDevice.NwkSKey).Rxpk[0];
            var request = new WaitableLoRaRequest(rxpk, this.PacketForwarder);
            messageDispatcher.DispatchRequest(request);
            Assert.True(await request.WaitCompleteAsync());

            Assert.NotNull(loRaDeviceTelemetry);
            Assert.NotNull(loRaDeviceTelemetry.Data);
            var rawPayload = Convert.ToBase64String(Encoding.UTF8.GetBytes("1"));
            Assert.Equal(rawPayload, loRaDeviceTelemetry.Rawdata);

            // Validate json
            var actualJsonTelemetry = JsonConvert.SerializeObject(loRaDeviceTelemetry, Formatting.None);
            var expectedTelemetryJson = $"{{\"time\":null,\"tmms\":0,\"tmst\":0,\"freq\":868.3,\"chan\":0,\"rfch\":1,\"stat\":0,\"modu\":\"LORA\",\"datr\":\"SF10BW125\",\"codr\":\"4/5\",\"rssi\":0,\"lsnr\":0.0,\"size\":{loRaDeviceTelemetry.Size},\"data\":\"decoded\",\"port\":1,\"fcnt\":1,\"rawdata\":\"{rawPayload}\",\"eui\":\"0000000000000001\",\"gatewayid\":\"test-gateway\",\"edgets\":{loRaDeviceTelemetry.Edgets}}}";
            Assert.Equal(expectedTelemetryJson, actualJsonTelemetry);

            this.LoRaDeviceApi.VerifyAll();
            this.LoRaDeviceClient.VerifyAll();
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
        ///     "edgets": 1550223375041
        /// }
        /// </summary>
        [Fact]
        public async Task When_Using_Custom_Decoder_Returns_Empty_Should_Send_Decoded_Value()
        {
            var simulatedDevice = new SimulatedDevice(TestDeviceInfo.CreateABPDevice(1, gatewayID: ServerGatewayID));
            var loRaDevice = this.CreateLoRaDevice(simulatedDevice);
            loRaDevice.SensorDecoder = "http://customdecoder/test1";

            // message will be sent
            LoRaDeviceTelemetry loRaDeviceTelemetry = null;
            this.LoRaDeviceClient.Setup(x => x.SendEventAsync(It.IsNotNull<LoRaDeviceTelemetry>(), null))
                .Callback<LoRaDeviceTelemetry, Dictionary<string, string>>((t, _) => loRaDeviceTelemetry = t)
                .ReturnsAsync(true);

            // C2D message will be checked
            this.LoRaDeviceClient.Setup(x => x.ReceiveAsync(It.IsNotNull<TimeSpan>()))
                .ReturnsAsync((Message)null);

            var deviceRegistry = new LoRaDeviceRegistry(this.ServerConfiguration, this.NewNonEmptyCache(loRaDevice), this.LoRaDeviceApi.Object, this.LoRaDeviceFactory);

            // Send to message processor
            var messageDispatcher = new MessageDispatcher(
                this.ServerConfiguration,
                deviceRegistry,
                this.FrameCounterUpdateStrategyProvider);

            var httpMessageHandler = new HttpMessageHandlerMock();
            httpMessageHandler.SetupHandler((r) =>
            {
                return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                {
                    Content = new StringContent(string.Empty, Encoding.UTF8, "application/json"),
                };
            });

            this.PayloadDecoder.SetDecoder(new LoRaPayloadDecoder(new HttpClient(httpMessageHandler)));

            // sends unconfirmed message
            var unconfirmedMessagePayload = simulatedDevice.CreateUnconfirmedDataUpMessage("1", fcnt: 1);
            var rxpk = unconfirmedMessagePayload.SerializeUplink(simulatedDevice.AppSKey, simulatedDevice.NwkSKey).Rxpk[0];
            var request = new WaitableLoRaRequest(rxpk, this.PacketForwarder);
            messageDispatcher.DispatchRequest(request);
            Assert.True(await request.WaitCompleteAsync());

            Assert.NotNull(loRaDeviceTelemetry);
            Assert.NotNull(loRaDeviceTelemetry.Data);
            var rawPayload = Convert.ToBase64String(Encoding.UTF8.GetBytes("1"));
            Assert.Equal(rawPayload, loRaDeviceTelemetry.Rawdata);

            // Validate json
            var actualJsonTelemetry = JsonConvert.SerializeObject(loRaDeviceTelemetry, Formatting.None);
            var expectedTelemetryJson = $"{{\"time\":null,\"tmms\":0,\"tmst\":0,\"freq\":868.3,\"chan\":0,\"rfch\":1,\"stat\":0,\"modu\":\"LORA\",\"datr\":\"SF10BW125\",\"codr\":\"4/5\",\"rssi\":0,\"lsnr\":0.0,\"size\":{loRaDeviceTelemetry.Size},\"data\":\"\",\"port\":1,\"fcnt\":1,\"rawdata\":\"{rawPayload}\",\"eui\":\"0000000000000001\",\"gatewayid\":\"test-gateway\",\"edgets\":{loRaDeviceTelemetry.Edgets}}}";
            Assert.Equal(expectedTelemetryJson, actualJsonTelemetry);

            this.LoRaDeviceApi.VerifyAll();
            this.LoRaDeviceClient.VerifyAll();
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
        ///     "edgets": 1550223375041
        /// }
        /// </summary>
        [Fact]
        public async Task When_Using_Custom_Decoder_Returns_JsonString_Should_Send_Decoded_Value()
        {
            var simulatedDevice = new SimulatedDevice(TestDeviceInfo.CreateABPDevice(1, gatewayID: ServerGatewayID));
            var loRaDevice = this.CreateLoRaDevice(simulatedDevice);
            loRaDevice.SensorDecoder = "http://customdecoder/test1";

            // message will be sent
            LoRaDeviceTelemetry loRaDeviceTelemetry = null;
            this.LoRaDeviceClient.Setup(x => x.SendEventAsync(It.IsNotNull<LoRaDeviceTelemetry>(), null))
                .Callback<LoRaDeviceTelemetry, Dictionary<string, string>>((t, _) => loRaDeviceTelemetry = t)
                .ReturnsAsync(true);

            // C2D message will be checked
            this.LoRaDeviceClient.Setup(x => x.ReceiveAsync(It.IsNotNull<TimeSpan>()))
                .ReturnsAsync((Message)null);

            var deviceRegistry = new LoRaDeviceRegistry(this.ServerConfiguration, this.NewNonEmptyCache(loRaDevice), this.LoRaDeviceApi.Object, this.LoRaDeviceFactory);

            // Send to message processor
            var messageDispatcher = new MessageDispatcher(
                this.ServerConfiguration,
                deviceRegistry,
                this.FrameCounterUpdateStrategyProvider);

            var httpMessageHandler = new HttpMessageHandlerMock();
            httpMessageHandler.SetupHandler((r) =>
            {
                return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                {
                    Content = new StringContent("{\"value\":\"decoded\"}", Encoding.UTF8, "application/json"),
                };
            });

            this.PayloadDecoder.SetDecoder(new LoRaPayloadDecoder(new HttpClient(httpMessageHandler)));

            // sends unconfirmed message
            var unconfirmedMessagePayload = simulatedDevice.CreateUnconfirmedDataUpMessage("1", fcnt: 1);
            var rxpk = unconfirmedMessagePayload.SerializeUplink(simulatedDevice.AppSKey, simulatedDevice.NwkSKey).Rxpk[0];
            var request = new WaitableLoRaRequest(rxpk, this.PacketForwarder);
            messageDispatcher.DispatchRequest(request);
            Assert.True(await request.WaitCompleteAsync());

            Assert.NotNull(loRaDeviceTelemetry);
            Assert.NotNull(loRaDeviceTelemetry.Data);
            var rawPayload = Convert.ToBase64String(Encoding.UTF8.GetBytes("1"));
            Assert.Equal(rawPayload, loRaDeviceTelemetry.Rawdata);

            // Validate json
            var actualJsonTelemetry = JsonConvert.SerializeObject(loRaDeviceTelemetry, Formatting.None);
            var expectedTelemetryJson = $"{{\"time\":null,\"tmms\":0,\"tmst\":0,\"freq\":868.3,\"chan\":0,\"rfch\":1,\"stat\":0,\"modu\":\"LORA\",\"datr\":\"SF10BW125\",\"codr\":\"4/5\",\"rssi\":0,\"lsnr\":0.0,\"size\":{loRaDeviceTelemetry.Size},\"data\":{{\"value\":\"decoded\"}},\"port\":1,\"fcnt\":1,\"rawdata\":\"{rawPayload}\",\"eui\":\"0000000000000001\",\"gatewayid\":\"test-gateway\",\"edgets\":{loRaDeviceTelemetry.Edgets}}}";
            Assert.Equal(expectedTelemetryJson, actualJsonTelemetry);

            this.LoRaDeviceApi.VerifyAll();
            this.LoRaDeviceClient.VerifyAll();
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
        ///     "edgets": 1550223375041
        /// }
        /// </summary>
        [Fact]
        public async Task When_Using_Custom_Decoder_Returns_Complex_Object_Should_Send_Decoded_Value()
        {
            var simulatedDevice = new SimulatedDevice(TestDeviceInfo.CreateABPDevice(1, gatewayID: ServerGatewayID));
            var loRaDevice = this.CreateLoRaDevice(simulatedDevice);
            loRaDevice.SensorDecoder = "http://customdecoder/test1";

            // message will be sent
            LoRaDeviceTelemetry loRaDeviceTelemetry = null;
            this.LoRaDeviceClient.Setup(x => x.SendEventAsync(It.IsNotNull<LoRaDeviceTelemetry>(), null))
                .Callback<LoRaDeviceTelemetry, Dictionary<string, string>>((t, _) => loRaDeviceTelemetry = t)
                .ReturnsAsync(true);

            // C2D message will be checked
            this.LoRaDeviceClient.Setup(x => x.ReceiveAsync(It.IsNotNull<TimeSpan>()))
                .ReturnsAsync((Message)null);

            var deviceRegistry = new LoRaDeviceRegistry(this.ServerConfiguration, this.NewNonEmptyCache(loRaDevice), this.LoRaDeviceApi.Object, this.LoRaDeviceFactory);

            // Send to message processor
            var messageDispatcher = new MessageDispatcher(
                this.ServerConfiguration,
                deviceRegistry,
                this.FrameCounterUpdateStrategyProvider);

            var decodedObject = new { temp = 10, humidity = 22.1, text = "abc" };

            var httpMessageHandler = new HttpMessageHandlerMock();
            httpMessageHandler.SetupHandler((r) =>
            {
                return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                {
                    Content = new StringContent(JsonConvert.SerializeObject(decodedObject), Encoding.UTF8, "application/json"),
                };
            });

            this.PayloadDecoder.SetDecoder(new LoRaPayloadDecoder(new HttpClient(httpMessageHandler)));

            // sends unconfirmed message
            var unconfirmedMessagePayload = simulatedDevice.CreateUnconfirmedDataUpMessage("1", fcnt: 1);
            var rxpk = unconfirmedMessagePayload.SerializeUplink(simulatedDevice.AppSKey, simulatedDevice.NwkSKey).Rxpk[0];
            var request = new WaitableLoRaRequest(rxpk, this.PacketForwarder);
            messageDispatcher.DispatchRequest(request);
            Assert.True(await request.WaitCompleteAsync());

            Assert.NotNull(loRaDeviceTelemetry);
            var rawPayload = Convert.ToBase64String(Encoding.UTF8.GetBytes("1"));
            Assert.Equal(rawPayload, loRaDeviceTelemetry.Rawdata);

            // Validate json
            var actualJsonTelemetry = JsonConvert.SerializeObject(loRaDeviceTelemetry, Formatting.None);
            var expectedTelemetryJson = $"{{\"time\":null,\"tmms\":0,\"tmst\":0,\"freq\":868.3,\"chan\":0,\"rfch\":1,\"stat\":0,\"modu\":\"LORA\",\"datr\":\"SF10BW125\",\"codr\":\"4/5\",\"rssi\":0,\"lsnr\":0.0,\"size\":{loRaDeviceTelemetry.Size},\"data\":{{\"temp\":10,\"humidity\":22.1,\"text\":\"abc\"}},\"port\":1,\"fcnt\":1,\"rawdata\":\"{rawPayload}\",\"eui\":\"0000000000000001\",\"gatewayid\":\"test-gateway\",\"edgets\":{loRaDeviceTelemetry.Edgets}}}";
            Assert.Equal(expectedTelemetryJson, actualJsonTelemetry);

            this.LoRaDeviceApi.VerifyAll();
            this.LoRaDeviceClient.VerifyAll();
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
        ///     "edgets": 1550223375041
        /// }
        /// </summary>
        [Fact]
        public async Task When_Using_Custom_Fails_Returns_Sets_Error_Information_In_Value()
        {
            var simulatedDevice = new SimulatedDevice(TestDeviceInfo.CreateABPDevice(1, gatewayID: ServerGatewayID));
            var loRaDevice = this.CreateLoRaDevice(simulatedDevice);
            loRaDevice.SensorDecoder = "http://customdecoder/test1";

            // message will be sent
            LoRaDeviceTelemetry loRaDeviceTelemetry = null;
            this.LoRaDeviceClient.Setup(x => x.SendEventAsync(It.IsNotNull<LoRaDeviceTelemetry>(), null))
                .Callback<LoRaDeviceTelemetry, Dictionary<string, string>>((t, _) => loRaDeviceTelemetry = t)
                .ReturnsAsync(true);

            // C2D message will be checked
            this.LoRaDeviceClient.Setup(x => x.ReceiveAsync(It.IsNotNull<TimeSpan>()))
                .ReturnsAsync((Message)null);

            var deviceRegistry = new LoRaDeviceRegistry(this.ServerConfiguration, this.NewNonEmptyCache(loRaDevice), this.LoRaDeviceApi.Object, this.LoRaDeviceFactory);

            // Send to message processor
            var messageDispatcher = new MessageDispatcher(
                this.ServerConfiguration,
                deviceRegistry,
                this.FrameCounterUpdateStrategyProvider);

            var httpMessageHandler = new HttpMessageHandlerMock();
            httpMessageHandler.SetupHandler((r) =>
            {
                return new HttpResponseMessage(System.Net.HttpStatusCode.BadRequest)
                {
                    Content = new StringContent("my error", Encoding.UTF8, "application/json"),
                };
            });

            this.PayloadDecoder.SetDecoder(new LoRaPayloadDecoder(new HttpClient(httpMessageHandler)));

            // sends unconfirmed message
            var unconfirmedMessagePayload = simulatedDevice.CreateUnconfirmedDataUpMessage("1", fcnt: 1);
            var rxpk = unconfirmedMessagePayload.SerializeUplink(simulatedDevice.AppSKey, simulatedDevice.NwkSKey).Rxpk[0];
            var request = new WaitableLoRaRequest(rxpk, this.PacketForwarder);
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
            var expectedTelemetryJson = $"{{\"time\":null,\"tmms\":0,\"tmst\":0,\"freq\":868.3,\"chan\":0,\"rfch\":1,\"stat\":0,\"modu\":\"LORA\",\"datr\":\"SF10BW125\",\"codr\":\"4/5\",\"rssi\":0,\"lsnr\":0.0,\"size\":{loRaDeviceTelemetry.Size},\"data\":{{\"error\":\"SensorDecoderModule 'http://customdecoder/test1?devEUI=0000000000000001&fport=1&payload=MQ%3d%3d' returned bad request.\",\"errorDetail\":\"my error\"}},\"port\":1,\"fcnt\":1,\"rawdata\":\"{rawPayload}\",\"eui\":\"0000000000000001\",\"gatewayid\":\"test-gateway\",\"edgets\":{loRaDeviceTelemetry.Edgets}}}";
            Assert.Equal(expectedTelemetryJson, actualJsonTelemetry);

            this.LoRaDeviceApi.VerifyAll();
            this.LoRaDeviceClient.VerifyAll();
        }

        [Fact]
        public async Task When_Resent_Message_Using_Custom_Decoder_Returns_Complex_Object_Should_Send_Decoded_Value()
        {
            var simulatedDevice = new SimulatedDevice(TestDeviceInfo.CreateABPDevice(1, gatewayID: ServerGatewayID));
            var loRaDevice = this.CreateLoRaDevice(simulatedDevice);
            loRaDevice.SensorDecoder = "http://customdecoder/test1";

            // message will be sent
            LoRaDeviceTelemetry loRaDeviceTelemetry = null;
            this.LoRaDeviceClient.Setup(x => x.SendEventAsync(It.IsNotNull<LoRaDeviceTelemetry>(), null))
                .Callback<LoRaDeviceTelemetry, Dictionary<string, string>>((t, _) => loRaDeviceTelemetry = t)
                .ReturnsAsync(true);

            // C2D message will be checked
            // this.LoRaDeviceClient.Setup(x => x.ReceiveAsync(It.IsNotNull<TimeSpan>()))
            // .ReturnsAsync((Message)null);
            var deviceRegistry = new LoRaDeviceRegistry(this.ServerConfiguration, this.NewNonEmptyCache(loRaDevice), this.LoRaDeviceApi.Object, this.LoRaDeviceFactory);

            // Send to message processor
            var messageDispatcher = new MessageDispatcher(
                this.ServerConfiguration,
                deviceRegistry,
                this.FrameCounterUpdateStrategyProvider);

            var decodedObject = new { temp = 10, humidity = 22.1, text = "abc", cloudToDeviceMessage = new { test = 1 } };

            var httpMessageHandler = new HttpMessageHandlerMock();
            httpMessageHandler.SetupHandler((r) =>
            {
                return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                {
                    Content = new StringContent(JsonConvert.SerializeObject(decodedObject), Encoding.UTF8, "application/json"),
                };
            });

            this.PayloadDecoder.SetDecoder(new LoRaPayloadDecoder(new HttpClient(httpMessageHandler)));

            // sends unconfirmed message
            var unconfirmedMessagePayload = simulatedDevice.CreateUnconfirmedDataUpMessage("1", fcnt: 10);
            var rxpk = unconfirmedMessagePayload.SerializeUplink(simulatedDevice.AppSKey, simulatedDevice.NwkSKey).Rxpk[0];
            var request = new WaitableLoRaRequest(rxpk, this.PacketForwarder);
            messageDispatcher.DispatchRequest(request);
            Assert.True(await request.WaitCompleteAsync());
            Assert.NotNull(request.ResponseDownlink);

            Assert.NotNull(loRaDeviceTelemetry);
            var rawPayload = Convert.ToBase64String(Encoding.UTF8.GetBytes("1"));
            Assert.Equal(rawPayload, loRaDeviceTelemetry.Rawdata);

            // Validate json
            var actualJsonTelemetry = JsonConvert.SerializeObject(loRaDeviceTelemetry, Formatting.None);
            var expectedTelemetryJson = $"{{\"time\":null,\"tmms\":0,\"tmst\":0,\"freq\":868.3,\"chan\":0,\"rfch\":1,\"stat\":0,\"modu\":\"LORA\",\"datr\":\"SF10BW125\",\"codr\":\"4/5\",\"rssi\":0,\"lsnr\":0.0,\"size\":{loRaDeviceTelemetry.Size},\"data\":{{\"temp\":10,\"humidity\":22.1,\"text\":\"abc\"}},\"port\":1,\"fcnt\":10,\"rawdata\":\"{rawPayload}\",\"eui\":\"0000000000000001\",\"gatewayid\":\"test-gateway\",\"edgets\":{loRaDeviceTelemetry.Edgets}}}";
            Assert.Equal(expectedTelemetryJson, actualJsonTelemetry);

            // send a second message with same fcnt to simulate
            // sends unconfirmed message
            var confirmedMessagePayload = simulatedDevice.CreateConfirmedDataUpMessage("1", fcnt: 10);
            var rxpk2 = confirmedMessagePayload.SerializeUplink(simulatedDevice.AppSKey, simulatedDevice.NwkSKey).Rxpk[0];
            var request2 = new WaitableLoRaRequest(rxpk2, this.PacketForwarder);
            messageDispatcher.DispatchRequest(request2);
            Assert.True(await request2.WaitCompleteAsync());
            Assert.NotNull(request2.ResponseDownlink);
            Assert.NotNull(loRaDeviceTelemetry);
            var rawPayload2 = Convert.ToBase64String(Encoding.UTF8.GetBytes("1"));
            Assert.Equal(rawPayload, loRaDeviceTelemetry.Rawdata);
            // Only the first message should be sent
            this.LoRaDeviceClient.Verify(x => x.SendEventAsync(It.IsNotNull<LoRaDeviceTelemetry>(), null), Times.Once());
            this.LoRaDeviceApi.VerifyAll();
            this.LoRaDeviceClient.VerifyAll();
        }
    }
}