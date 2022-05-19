// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Unit.LoraKeysManagerFacade
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using global::LoraKeysManagerFacade;
    using global::LoRaTools;
    using Microsoft.Azure.Devices;
    using Microsoft.Azure.Devices.Common.Exceptions;
    using Microsoft.Extensions.Logging.Abstractions;
    using Moq;
    using Xunit;

    public class ClearLnsCacheTest
    {
        private readonly Mock<IEdgeDeviceGetter> edgeDeviceGetter;
        private readonly Mock<IServiceClient> serviceClient;
        private readonly Mock<IChannelPublisher> channelPublisher;
        private readonly ClearLnsCache clearLnsCache;

        public ClearLnsCacheTest()
        {
            this.edgeDeviceGetter = new Mock<IEdgeDeviceGetter>();
            this.serviceClient = new Mock<IServiceClient>();
            this.channelPublisher = new Mock<IChannelPublisher>();
            this.clearLnsCache = new ClearLnsCache(this.edgeDeviceGetter.Object, this.serviceClient.Object, this.channelPublisher.Object, NullLogger<ClearLnsCache>.Instance);
        }

        [Fact]
        public async Task ClearLnsCacheInternalAsync_Invokes_Both_Edge_And_Non_Edge_Devices()
        {
            //arrange
            var listEdgeDevices = new List<string> { "edge1", "edge2" };
            this.edgeDeviceGetter.Setup(m => m.ListEdgeDevicesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(listEdgeDevices);

            this.serviceClient.Setup(m => m.InvokeDeviceMethodAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CloudToDeviceMethod>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new CloudToDeviceMethodResult() { Status = 200 });

            //act
            await this.clearLnsCache.ClearLnsCacheInternalAsync(default);

            //assert
            foreach (var edgeDevice in listEdgeDevices)
            {
                this.serviceClient.Verify(c => c.InvokeDeviceMethodAsync(edgeDevice,
                                                                    LoraKeysManagerFacadeConstants.NetworkServerModuleId,
                                                                    It.Is<CloudToDeviceMethod>(c => c.MethodName == LoraKeysManagerFacadeConstants.ClearCacheMethodName),
                                                                    It.IsAny<CancellationToken>()), Times.Once());
            }

            this.channelPublisher.Verify(c => c.PublishAsync(LoraKeysManagerFacadeConstants.ClearCacheMethodName, It.Is<LnsRemoteCall>(r => r.Kind == RemoteCallKind.ClearCache)), Times.Once());
        }

        [Fact]
        public async Task ClearLnsCacheInternalAsync_Invokes_Only_Pub_Sub_When_No_Edge_Devices()
        {
            //arrange
            this.edgeDeviceGetter.Setup(m => m.ListEdgeDevicesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(Array.Empty<string>());

            this.serviceClient.Setup(m => m.InvokeDeviceMethodAsync(It.IsAny<string>(),
                                                                    It.IsAny<string>(),
                                                                    It.IsAny<CloudToDeviceMethod>(),
                                                                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new CloudToDeviceMethodResult() { Status = 200 });

            //act
            await this.clearLnsCache.ClearLnsCacheInternalAsync(default);

            //assert
            this.serviceClient.VerifyNoOtherCalls();

            this.channelPublisher.Verify(c => c.PublishAsync(LoraKeysManagerFacadeConstants.ClearCacheMethodName,
                                                             It.Is<LnsRemoteCall>(r => r.Kind == RemoteCallKind.ClearCache)), Times.Once());
        }

        [Fact]
        public async Task ClearLnsCacheInternalAsync_Succeeds_Even_If_A_Single_Edge_Device_Is_A_Non_Lns_One()
        {
            //arrange
            var listEdgeDevices = new List<string> { "edge1", "edge2" };
            this.edgeDeviceGetter.Setup(m => m.ListEdgeDevicesAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(listEdgeDevices);

            this.serviceClient.Setup(m => m.InvokeDeviceMethodAsync("edge1",
                                                                    It.IsAny<string>(),
                                                                    It.IsAny<CloudToDeviceMethod>(),
                                                                    It.IsAny<CancellationToken>()))
                .Throws(new DeviceNotFoundException("edge1"));

            this.serviceClient.Setup(m => m.InvokeDeviceMethodAsync("edge2",
                                                                    It.IsAny<string>(),
                                                                    It.IsAny<CloudToDeviceMethod>(),
                                                                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new CloudToDeviceMethodResult() { Status = 200 });

            //act
            await this.clearLnsCache.ClearLnsCacheInternalAsync(default);

            //assert
            this.channelPublisher.Verify(c => c.PublishAsync(LoraKeysManagerFacadeConstants.ClearCacheMethodName,
                                                             It.Is<LnsRemoteCall>(r => r.Kind == RemoteCallKind.ClearCache)), Times.Once());
        }
    }
}
