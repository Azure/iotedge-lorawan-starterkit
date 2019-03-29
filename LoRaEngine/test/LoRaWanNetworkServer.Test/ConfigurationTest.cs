// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer.Test
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using LoRaWan.NetworkServer;
    using Xunit;
    using Xunit.Extensions;

    public class ConfigurationTest
    {
        [Theory]
        [MemberData(nameof(AllowedDevAddressesInput))]
        public void Should_Setup_Allowed_Dev_Addresses_Correctly(string inputAllowedDevAddrValues, HashSet<string> expectedAllowedDevAddrValues)
        {
            Environment.SetEnvironmentVariable("AllowedDevAddresses", inputAllowedDevAddrValues);
            NetworkServerConfiguration networkServerConfiguration = NetworkServerConfiguration.CreateFromEnviromentVariables();
            Assert.Equal(expectedAllowedDevAddrValues.Count, networkServerConfiguration.AllowedDevAddresses.Count);
            foreach (string devAddr in expectedAllowedDevAddrValues)
            {
                Assert.Contains(devAddr, networkServerConfiguration.AllowedDevAddresses);
            }
        }

        public static IEnumerable<object[]> AllowedDevAddressesInput
        {
            get
            {
                return new[]
                {
                    new object[]
                    {
                        "0228B1B1;", new HashSet<string>()
                        {
                            "0228B1B1", string.Empty
                        }
                    },
                    new object[]
                    {
                        "0228B1B1;0228B1B2", new HashSet<string>()
                        {
                            "0228B1B1", "0228B1B2"
                        }
                    },
                    new object[]
                    {
                        "ads;0228B1B2;", new HashSet<string>()
                        {
                            "ads", string.Empty, "0228B1B2"
                        }
                    }
            };
            }
        }
    }
}
