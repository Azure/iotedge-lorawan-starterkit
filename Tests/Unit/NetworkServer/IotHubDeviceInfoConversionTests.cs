// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

namespace LoRaWan.Tests.Unit.NetworkServer
{
    using global::LoraKeysManagerFacade;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;
    using Xunit;

    public sealed class IotHubDeviceInfoConversionTests
    {
        [Fact]
        public void Serializes_To_Expected_Json_Object()
        {
            // arrange
            var original = new IoTHubDeviceInfo { DevAddr = new DevAddr(123), DevEUI = new DevEui(234), PrimaryKey = "someprimarykey" };

            // act
            var obj = JsonConvert.DeserializeObject<JObject>(JsonConvert.SerializeObject(original));

            // assert
            Assert.NotNull(obj);
            Assert.Equal(3, obj!.Count);
            Assert.Equal(original.DevEuiString, obj["DevEUI"]);
            Assert.Equal(original.PrimaryKey, obj["PrimaryKey"]);
            Assert.Equal(original.DevAddrString, obj["DevAddr"]);
        }

        [Fact]
        public void Can_Be_Json_Deserialized_As_IoTHubDeviceInfo_From_NetworkServer()
        {
            // arrange
            var original = new IoTHubDeviceInfo { DevAddr = new DevAddr(123), DevEUI = new DevEui(234), PrimaryKey = "someprimarykey" };

            // act
            var result = JsonConvert.DeserializeObject<LoRaWan.NetworkServer.IoTHubDeviceInfo>(JsonConvert.SerializeObject(original));

            // assert
            Assert.NotNull(result);
            Assert.Equal(original.DevEUI, result!.DevEUI);
            Assert.Equal(original.PrimaryKey, result.PrimaryKey);
            Assert.Equal(original.DevAddr, result.DevAddr);
        }
    }
}
