// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

namespace LoRaWan.Tests.Unit.NetworkServer
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using global::LoRaTools;
    using LoRaWan.NetworkServer;
    using Moq;
    using Xunit;

    public sealed class CloudControlHostTests
    {
        private const string GatewayId = "lns-1";
        private readonly Mock<ILnsRemoteCallHandler> lnsRemoteCallHandler;
        private readonly Mock<ILnsRemoteCallListener> lnsRemoteCallListener;
        private readonly CloudControlHost subject;

        public CloudControlHostTests()
        {
            this.lnsRemoteCallHandler = new Mock<ILnsRemoteCallHandler>();
            this.lnsRemoteCallListener = new Mock<ILnsRemoteCallListener>();
            this.subject = new CloudControlHost(this.lnsRemoteCallListener.Object, this.lnsRemoteCallHandler.Object, new NetworkServerConfiguration { GatewayID = GatewayId });
        }

        [Fact]
        public async Task ExecuteAsync_Subscribes_To_LnsRemoteCallHandler()
        {
            // arrange
            Func<LnsRemoteCall, Task> actualHandler = _ => Task.CompletedTask;
            this.lnsRemoteCallListener
                .Setup(l => l.SubscribeAsync(GatewayId, It.IsAny<Func<LnsRemoteCall, Task>>(), It.IsAny<CancellationToken>()))
                .Callback((string _, Func<LnsRemoteCall, Task> handler, CancellationToken _) => actualHandler = handler);

            // act
            await this.subject.StartAsync(CancellationToken.None);

            // assert
            this.lnsRemoteCallListener.Verify(l => l.SubscribeAsync(GatewayId, It.IsAny<Func<LnsRemoteCall, Task>>(), It.IsAny<CancellationToken>()));
            await actualHandler.Invoke(new LnsRemoteCall(RemoteCallKind.CloseConnection, null));
            this.lnsRemoteCallHandler.Verify(l => l.ExecuteAsync(It.IsAny<LnsRemoteCall>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task StopAsync_Unsubscribes()
        {
            // act
            await this.subject.StopAsync(CancellationToken.None);

            // assert
            this.lnsRemoteCallListener.Verify(l => l.UnsubscribeAsync(GatewayId, It.IsAny<CancellationToken>()), Times.Once);
        }
    }
}
