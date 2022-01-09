// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Unit.NetworkServer
{
    using System.Linq;
    using LoRaWan.NetworkServer;
    using LoRaWan.Tests.Common;
    using Newtonsoft.Json;
    using Xunit;

    public class IotHubDeviceInfoTests
    {
        public static TheoryData<IoTHubDeviceInfo> Serialize_Deserialize_Composition_Should_Preserve_Information_TheoryData() => TheoryDataFactory.From(
            from networkSessionKey in new[] { (NetworkSessionKey?)null, TestKeys.CreateNetworkSessionKey(3) }
            select new IoTHubDeviceInfo
            {
                DevAddr = new DevAddr(1).ToString(),
                DevEUI = new DevEui(2).ToString(),
                GatewayId = "foo",
                NwkSKey = networkSessionKey,
                PrimaryKey = TestKeys.CreateAppKey(4).ToString()
            });

        [Theory]
        [MemberData(nameof(Serialize_Deserialize_Composition_Should_Preserve_Information_TheoryData))]
        public void Serialize_Deserialize_Composition_Should_Preserve_Information(IoTHubDeviceInfo initial)
        {
            // act
            var result = JsonConvert.DeserializeObject<IoTHubDeviceInfo>(JsonConvert.SerializeObject(initial));

            // assert
            Assert.Equal(initial.DevAddr, result.DevAddr);
            Assert.Equal(initial.DevEUI, result.DevEUI);
            Assert.Equal(initial.GatewayId, result.GatewayId);
            Assert.Equal(initial.NwkSKey, result.NwkSKey);
            Assert.Equal(initial.PrimaryKey, result.PrimaryKey);
        }
    }
}
