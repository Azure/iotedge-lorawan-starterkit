// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer.Test
{
    using System;
    using System.Collections.Generic;
    using System.Net.Http;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    class HttpMessageHandlerMock : HttpMessageHandler
    {
        private Func<HttpRequestMessage, HttpResponseMessage> handler;

        public HttpMessageHandlerMock SetupHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
        {
            this.handler = handler;
            return this;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var result = this.handler(request);
            return Task.FromResult(result);
        }
    }
}
