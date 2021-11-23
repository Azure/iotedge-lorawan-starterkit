// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Common
{
    using System.Collections.Generic;
    using LoRaWan.NetworkServer;
    using LoRaWan.NetworkServer.ADR;
    using Microsoft.Extensions.Caching.Memory;
    using Microsoft.Extensions.Logging.Abstractions;

    public sealed class TestLoRaDeviceFactory : LoRaDeviceFactory
    {
        private readonly ILoRaDeviceClient loRaDeviceClient;
        private readonly IDictionary<string, ILoRaDeviceClient> deviceClientMap = new Dictionary<string, ILoRaDeviceClient>();
        private readonly IDictionary<string, LoRaDevice> deviceMap = new Dictionary<string, LoRaDevice>();

        public TestLoRaDeviceFactory(ILoRaDeviceClient loRaDeviceClient, LoRaDeviceCache deviceCache, ILoRaDeviceClientConnectionManager connectionManager = null)
            : this(null, connectionManager, deviceCache, null, loRaDeviceClient)
        { }

        public TestLoRaDeviceFactory(ILoRaDeviceClient loRaDeviceClient, ILoRaDataRequestHandler requestHandler, LoRaDeviceCache deviceCache, ILoRaDeviceClientConnectionManager connectionManager = null)
            : this(requestHandler, connectionManager, deviceCache, null, loRaDeviceClient)
        { }

        public TestLoRaDeviceFactory(
            NetworkServerConfiguration configuration,
            ILoRaDeviceClient loRaDeviceClient,
            ILoRaDeviceClientConnectionManager connectionManager,
            LoRaDeviceCache deviceCache,
            ILoRaDataRequestHandler requestHandler)
            : this(requestHandler, connectionManager, deviceCache, configuration, loRaDeviceClient)
        { }

        private TestLoRaDeviceFactory(ILoRaDataRequestHandler requestHandler,
                                      ILoRaDeviceClientConnectionManager connectionManager,
                                      LoRaDeviceCache deviceCache,
                                      NetworkServerConfiguration configuration,
                                      ILoRaDeviceClient loRaDeviceClient)
            : base(configuration ?? new NetworkServerConfiguration { GatewayID = MessageProcessorTestBase.ServerGatewayID },
                   requestHandler,
                   connectionManager,
                   deviceCache)
        {
            this.loRaDeviceClient = loRaDeviceClient;
        }

        public bool TryGetLoRaDevice(string devEUI, out LoRaDevice device) => this.deviceMap.TryGetValue(devEUI, out device);

        public void SetClient(string devEUI, ILoRaDeviceClient deviceClient) => this.deviceClientMap[devEUI] = deviceClient;

        public override ILoRaDeviceClient CreateDeviceClient(string eui, string primaryKey) =>
            this.deviceClientMap.TryGetValue(eui, out var deviceClientToAssign) ? deviceClientToAssign : this.loRaDeviceClient;
    }
}
