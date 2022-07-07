// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Common
{
    using LoRaWan.NetworkServer;
    using Microsoft.Extensions.Caching.Memory;
    using Microsoft.Extensions.Logging.Abstractions;
    using Moq;

    public static class TestMessageDispatcher
    {
        public static MessageDispatcher Create(IMemoryCache memoryCache,
                                               NetworkServerConfiguration configuration,
                                               ILoRaDeviceRegistry deviceRegistry,
                                               ILoRaDeviceFrameCounterUpdateStrategyProvider frameCounterUpdateStrategyProvider)
        {
            var concentratorDeduplication = new ConcentratorDeduplication(memoryCache, NullLogger<IConcentratorDeduplication>.Instance);

            return new MessageDispatcher(configuration,
                                         deviceRegistry,
                                         frameCounterUpdateStrategyProvider,
                                         new JoinRequestMessageHandler(configuration, concentratorDeduplication, deviceRegistry, NullLogger<JoinRequestMessageHandler>.Instance, Mock.Of<LoRaDeviceAPIServiceBase>(), null),
                                         NullLoggerFactory.Instance,
                                         NullLogger<MessageDispatcher>.Instance,
                                         null);
        }
    }
}
