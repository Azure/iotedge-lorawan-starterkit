// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

namespace LoRaWan.NetworkServer.BasicsStation.Processors
{
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Http;

    public interface ICupsProtocolMessageProcessor
    {
        public Task HandleUpdateInfoAsync(HttpContext httpContext, CancellationToken token);
    }
}
