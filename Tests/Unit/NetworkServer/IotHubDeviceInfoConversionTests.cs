// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

namespace LoRaWan.Tests.Unit.NetworkServer
{
    using global::LoraKeysManagerFacade;
    using Newtonsoft.Json;
    using Xunit;

    public sealed class IotHubDeviceInfoConversionTests
    {
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
