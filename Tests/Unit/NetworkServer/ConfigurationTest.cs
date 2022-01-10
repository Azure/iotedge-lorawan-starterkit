// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Unit.NetworkServer
{
    using System;
    using System.Collections.Generic;
    using LoRaWan.NetworkServer;
    using LoRaWan.Tests.Common;
    using Xunit;

    public class ConfigurationTest
    {
        [Theory]
        [MemberData(nameof(AllowedDevAddressesInput))]
        public void Should_Setup_Allowed_Dev_Addresses_Correctly(string inputAllowedDevAddrValues, DevAddr[] expectedAllowedDevAddrValues)
        {
            var envVariables = new[] { ("AllowedDevAddresses", inputAllowedDevAddrValues), ("FACADE_SERVER_URL", "https://aka.ms") };

            try
            {
                foreach (var (key, value) in envVariables)
                    Environment.SetEnvironmentVariable(key, value);

                var networkServerConfiguration = NetworkServerConfiguration.CreateFromEnvironmentVariables();
                Assert.Equal(expectedAllowedDevAddrValues.Length, networkServerConfiguration.AllowedDevAddresses.Count);
                foreach (var devAddr in expectedAllowedDevAddrValues)
                {
                    Assert.Contains(devAddr, networkServerConfiguration.AllowedDevAddresses);
                }
            }
            finally
            {
                foreach (var (key, _) in envVariables)
                    Environment.SetEnvironmentVariable(key, string.Empty);
            }
        }

        public static TheoryData<string, DevAddr[]> AllowedDevAddressesInput =>
            TheoryDataFactory.From(("0228B1B1;", new[] { new DevAddr(0x0228b1b1) }),
                                   ("0228B1B1;0228B1B2", new DevAddr[] { new DevAddr(0x0228b1b1), new DevAddr(0x0228b1b2) }),
                                   ("ads;0228B1B2;", new DevAddr[] { new DevAddr(0x0228b1b2) }));
    }
}
