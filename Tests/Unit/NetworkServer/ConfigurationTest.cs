// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Unit.NetworkServer
{
    using System;
    using LoRaWan.NetworkServer;
    using LoRaWan.Tests.Common;
    using Xunit;

    public class ConfigurationTest
    {
        [Theory]
        [MemberData(nameof(AllowedDevAddressesInput))]
        public void Should_Setup_Allowed_Dev_Addresses_Correctly(string inputAllowedDevAddrValues, DevAddr[] expectedAllowedDevAddrValues)
        {
            var envVariables = new[]
            {
                ("AllowedDevAddresses", inputAllowedDevAddrValues),
                ("FACADE_SERVER_URL", "https://aka.ms"),
                ("HOSTNAME", "test"),
                ("IOTHUBHOSTNAME", "test")
            };

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
            var lnsConfigurationCreation = () => NetworkServerConfiguration.CreateFromEnvironmentVariables();

            Environment.SetEnvironmentVariable("HOSTNAME", "test");
            Environment.SetEnvironmentVariable("IOTHUBHOSTNAME", "test");

            if (isCloudDeployment)
            {
                Environment.SetEnvironmentVariable(cloudDeploymentKey, true.ToString());
                Environment.SetEnvironmentVariable("ENABLE_GATEWAY", false.ToString());
            }

            if (shouldSetRedisString)
                Environment.SetEnvironmentVariable(key, value);

            // act and assert
            if (isCloudDeployment && !shouldSetRedisString)
            {
                _ = Assert.Throws<InvalidOperationException>(lnsConfigurationCreation);
            }
            else
            {
                _ = lnsConfigurationCreation();
            }

            Environment.SetEnvironmentVariable(key, string.Empty);
            Environment.SetEnvironmentVariable(cloudDeploymentKey, string.Empty);
            Environment.SetEnvironmentVariable("ENABLE_GATEWAY", string.Empty);
        }


        public static TheoryData<string, DevAddr[]> AllowedDevAddressesInput =>
            TheoryDataFactory.From(("0228B1B1;", new[] { new DevAddr(0x0228b1b1) }),
                                   ("0228B1B1;0228B1B2", new DevAddr[] { new DevAddr(0x0228b1b1), new DevAddr(0x0228b1b2) }),
                                   ("ads;0228B1B2;", new DevAddr[] { new DevAddr(0x0228b1b2) }));

        [Theory]
        [CombinatorialData]
        public void EnableGatewayTrue_IoTModuleFalse_IsNotSupported(bool cloud_deployment, bool enable_gateway)
        {
            var envVariables = new[]
            {
                ("CLOUD_DEPLOYMENT", cloud_deployment.ToString()),
                ("ENABLE_GATEWAY", enable_gateway.ToString()),
                ("REDIS_CONNECTION_STRING", "someString"),
                ("HOSTNAME", "test"),
                ("IOTHUBHOSTNAME", "test")
            };

            try
            {
                foreach (var (key, value) in envVariables)
                    Environment.SetEnvironmentVariable(key, value);

                if (cloud_deployment && enable_gateway)
                {
                    Assert.Throws<NotSupportedException>(() => {
                        _ = NetworkServerConfiguration.CreateFromEnvironmentVariables();
                    });
                }
                else
                {
                    _ = NetworkServerConfiguration.CreateFromEnvironmentVariables();
                }
            }
            finally
            {
                foreach (var (key, _) in envVariables)
                    Environment.SetEnvironmentVariable(key, string.Empty);
            }
        }

        [Theory]
        [InlineData("500")]
        [InlineData("x")]
        public void ProcessingDelayIsConfigurable(string processing_delay)
        {
            var envVariables = new[]
            {
                ("PROCESSING_DELAY_IN_MS", processing_delay),
                ("HOSTNAME", "test")
            };

            try
            {
                foreach (var (key, value) in envVariables)
                    Environment.SetEnvironmentVariable(key, value);

                var networkServerConfiguration = NetworkServerConfiguration.CreateFromEnvironmentVariables();

                if (!int.TryParse(processing_delay, out var int_processing_delay))
                {
                    int_processing_delay = Constants.DefaultProcessingDelayInMilliseconds;
                }
                Assert.Equal(int_processing_delay, networkServerConfiguration.ProcessingDelayInMilliseconds);
            }
            finally
            {
                foreach (var (key, _) in envVariables)
                    Environment.SetEnvironmentVariable(key, string.Empty);
            }
        }
    }
}
