// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Unit.NetworkServer
{
    using System;
    using System.Collections.Generic;
    using LoRaWan.NetworkServer;
    using LoRaWan.Tests.Common;
    using Microsoft.Extensions.Configuration;
    using Xunit;

    public class ConfigurationTest
    {
        [Theory]
        [MemberData(nameof(AllowedDevAddressesInput))]
        public void Should_Setup_Allowed_Dev_Addresses_Correctly(string inputAllowedDevAddrValues, DevAddr[] expectedAllowedDevAddrValues)
        {
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string>()
            {
                ["AllowedDevAddresses"] = inputAllowedDevAddrValues,
                ["FACADE_SERVER_URL"] = "https://aka.ms",
                ["HOSTNAME"] = "test",
                ["IOTHUBHOSTNAME"] = "test"
            }).Build();

            var networkServerConfiguration = NetworkServerConfiguration.Create(configuration);
            Assert.Equal(expectedAllowedDevAddrValues.Length, networkServerConfiguration.AllowedDevAddresses.Count);
            foreach (var devAddr in expectedAllowedDevAddrValues)
            {
                Assert.Contains(devAddr, networkServerConfiguration.AllowedDevAddresses);
            }
        }

        [Theory]
        [InlineData(true, true)]
        [InlineData(true, false)]
        [InlineData(false, true)]
        [InlineData(false, false)]
        public void Should_Throw_On_Invalid_Cloud_Configuration_When_Redis_Connection_String_Not_Set(bool shouldSetRedisString, bool isCloudDeployment)
        {
            // arrange
            var cloudDeploymentKey = "CLOUD_DEPLOYMENT";
            var key = "REDIS_CONNECTION_STRING";
            var value = "someValue";

            var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string>
            {
                ["HOSTNAME"] = "test",
                ["IOTHUBHOSTNAME"] = "test",
            });

            if (isCloudDeployment)
            {
                configuration.AddInMemoryCollection(new Dictionary<string, string>
                {
                    [cloudDeploymentKey] = true.ToString(),
                    ["ENABLE_GATEWAY"] = false.ToString(),
                });
            }

            if (shouldSetRedisString)
            {
                configuration.AddInMemoryCollection(new Dictionary<string, string>
                {
                    [key] = value,
                });
            }

            // act and assert
            if (isCloudDeployment && !shouldSetRedisString)
            {
                _ = Assert.Throws<InvalidOperationException>(() => NetworkServerConfiguration.Create(configuration.Build()));
            }
            else
            {
                _ = NetworkServerConfiguration.Create(configuration.Build());
            }
        }


        public static TheoryData<string, DevAddr[]> AllowedDevAddressesInput =>
            TheoryDataFactory.From(("0228B1B1;", new[] { new DevAddr(0x0228b1b1) }),
                                   ("0228B1B1;0228B1B2", new DevAddr[] { new DevAddr(0x0228b1b1), new DevAddr(0x0228b1b2) }),
                                   ("ads;0228B1B2;", new DevAddr[] { new DevAddr(0x0228b1b2) }));

        [Theory]
        [CombinatorialData]
        public void EnableGatewayTrue_IoTModuleFalse_IsNotSupported(bool cloud_deployment, bool enable_gateway)
        {
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string>
            {
                ["CLOUD_DEPLOYMENT"] = cloud_deployment.ToString(),
                ["ENABLE_GATEWAY"] = enable_gateway.ToString(),
                ["REDIS_CONNECTION_STRING"] = "someString",
                ["HOSTNAME"] = "test",
                ["IOTHUBHOSTNAME"] = "test"
            }).Build();

            if (cloud_deployment && enable_gateway)
            {
                Assert.Throws<NotSupportedException>(() => {
                    var networkServerConfiguration = NetworkServerConfiguration.Create(configuration);
                });
            }
            else
            {
                var networkServerConfiguration = NetworkServerConfiguration.Create(configuration);
            }
        }

        [Fact]
        public void ProcessingDelayIsConfigurable()
        {
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string>
            {
                ["PROCESSING_DELAY_IN_MS"] = "500",
                ["HOSTNAME"] = "test",
                ["IOTHUBHOSTNAME"] = "test"
            }).Build();

            var networkServerConfiguration = NetworkServerConfiguration.Create(configuration);

            Assert.Equal(500, networkServerConfiguration.ProcessingDelayInMilliseconds);
        }

        [Fact]
        public void ProcessingDelayCanUseFallback()
        {
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string>
            {
                ["HOSTNAME"] = "test",
                ["IOTHUBHOSTNAME"] = "test"
            }).Build();

            var networkServerConfiguration = NetworkServerConfiguration.Create(configuration);

            Assert.Equal(Constants.DefaultProcessingDelayInMilliseconds, networkServerConfiguration.ProcessingDelayInMilliseconds);
        }
    }
}
