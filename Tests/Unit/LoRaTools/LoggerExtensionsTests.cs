// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

namespace LoRaWan.Tests.Unit.LoRaTools
{
    using System;
    using System.Collections.Generic;
    using global::LoRaTools;
    using LoRaWan.Tests.Common;
    using Microsoft.Extensions.Logging;
    using Moq;
    using Xunit;

    public sealed class LoggerExtensionsTests
    {
        private readonly List<Dictionary<string, object>> actualScopes = new();
        private readonly ILogger logger;

        public LoggerExtensionsTests()
        {
            var loggerMock = new Mock<ILogger>();
            this.logger = loggerMock.Object;
            loggerMock.Setup(l => l.BeginScope(It.IsAny<Dictionary<string, object>>()))
                      .Callback((Dictionary<string, object> scope) => this.actualScopes.Add(scope));
        }

        public static TheoryData<DevAddr?, string?> BeginDeviceAddressScope_TheoryData() => TheoryDataFactory.From(new (DevAddr?, string?)[]
        {
            (new DevAddr(1), "00000001"),
            (null, null)
        });

        [Theory]
        [MemberData(nameof(BeginDeviceAddressScope_TheoryData))]
        public void BeginDeviceAddressScope_Success(DevAddr? devAddr, string? expectedScopeValue) =>
            AssertBeginScope(logger => logger.BeginDeviceAddressScope(devAddr), expectedScopeValue, "DevAddr");

        public static TheoryData<DevEui?, string?> BeginDeviceScope_TheoryData() => TheoryDataFactory.From(new (DevEui?, string?)[]
        {
            (new DevEui(1), "0000000000000001"),
            (null, null)
        });

        [Theory]
        [MemberData(nameof(BeginDeviceScope_TheoryData))]
        public void BeginDeviceScope_Success(DevEui? devEui, string? expectedScopeValue) =>
            AssertBeginScope(logger => logger.BeginDeviceScope(devEui), expectedScopeValue, "DevEUI");

        private void AssertBeginScope(Func<ILogger, IDisposable> act, string? expectedScopeValue, string expectedKey)
        {
            using (var scope = act(this.logger)) { /* noop */ }

            if (expectedScopeValue is object someExpectedScopeValue)
            {
                var actualScopeDictionary = Assert.Single(this.actualScopes);
                var actualScope = Assert.Single(actualScopeDictionary);
                Assert.Equal(KeyValuePair.Create(expectedKey, someExpectedScopeValue), actualScope);
            }
            else
            {
                Assert.Empty(this.actualScopes);
            }
        }

        [Fact]
        public void BeginEuiScope_Success() =>
            AssertBeginScope(logger => logger.BeginEuiScope(new StationEui(1)), "0000000000000001", "StationEUI");
    }
}
