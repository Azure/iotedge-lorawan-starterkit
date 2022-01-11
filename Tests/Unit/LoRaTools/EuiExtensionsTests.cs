// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

namespace LoRaWan.Tests.Unit.LoRaTools
{
    using global::LoRaTools.Utils;
    using Xunit;

    public sealed class EuiExtensionsTests
    {
        [Fact]
        public void AsIotHubDeviceId_Success()
        {
            var devEui = new DevEui(0x1A2B3C);
            var result = devEui.AsIotHubDeviceId();
            Assert.Equal("00000000001A2B3C", result);
        }
    }
}
