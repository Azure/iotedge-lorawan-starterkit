// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer.Common
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using Microsoft.Azure.Devices.Client;

    public class PhysicalClient
    {
#pragma warning disable SA1401 // Fields should be private
        protected readonly NetworkServerConfiguration configuration;
        protected readonly MessageDispatcher messageDispatcher;
        protected readonly LoRaDeviceAPIServiceBase loRaDeviceAPIService;
        protected readonly ILoRaDeviceRegistry loRaDeviceRegistry;
        protected ModuleClient ioTHubModuleClient;
        protected IClassCDeviceMessageSender classCMessageSender;
#pragma warning restore SA1401 // Fields should be private

        public PhysicalClient(
            NetworkServerConfiguration configuration,
            MessageDispatcher messageDispatcher,
            LoRaDeviceAPIServiceBase loRaDeviceAPIService,
            ILoRaDeviceRegistry loRaDeviceRegistry)
        {
            this.configuration = configuration;
            this.messageDispatcher = messageDispatcher;
            this.loRaDeviceAPIService = loRaDeviceAPIService;
            this.loRaDeviceRegistry = loRaDeviceRegistry;
        }
    }
}
