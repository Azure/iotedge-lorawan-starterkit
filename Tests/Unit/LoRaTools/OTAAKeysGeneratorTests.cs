// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Unit.LoRaTools
{
    using System.Linq;
    using Common;
    using global::LoRaTools;
    using Xunit;

    public class OTAAKeysGeneratorTests
    {
        public static readonly (AppNonce AppNonce, NetId NetId, DevNonce DevNonce, AppKey AppKey,
                                NetworkSessionKey NetworkSessionKey,
                                AppSessionKey AppSessionKey)[] TestData =
        {
            (new AppNonce(123), new NetId(0x41), new DevNonce(456), AppKey.Parse("0102030405060708090A0B0C0D0E0F10"),
                NetworkSessionKey.Parse("D60FD79D3EEF32C277854450918E1595"),
                AppSessionKey.Parse("EFA581038515D4CE712ACE5FEAAECEF6"))
        };

        public static readonly TheoryData<AppSessionKey, AppNonce, NetId, DevNonce, AppKey> CalculateAppSessionKeyData =
            TheoryDataFactory.From(from e in TestData
                                   select (e.AppSessionKey, e.AppNonce, e.NetId, e.DevNonce, e.AppKey));

        [Theory]
        [MemberData(nameof(CalculateAppSessionKeyData))]
        public void CalculateAppSessionKey(AppSessionKey expected, AppNonce appNonce, NetId netId, DevNonce devNonce, AppKey appKey)
        {
            Assert.Equal(expected, OTAAKeysGenerator.CalculateAppSessionKey(appNonce, netId, devNonce, appKey));
        }

        public static readonly TheoryData<NetworkSessionKey, AppNonce, NetId, DevNonce, AppKey> CalculateNetworkSessionKeyData =
            TheoryDataFactory.From(from e in TestData
                                   select (e.NetworkSessionKey, e.AppNonce, e.NetId, e.DevNonce, e.AppKey));

        [Theory]
        [MemberData(nameof(CalculateNetworkSessionKeyData))]
        public void CalculateNetworkSessionKey(NetworkSessionKey expected, AppNonce appNonce, NetId netId, DevNonce devNonce, AppKey appKey)
        {
            Assert.Equal(expected, OTAAKeysGenerator.CalculateNetworkSessionKey(appNonce, netId, devNonce, appKey));
        }
    }
}
