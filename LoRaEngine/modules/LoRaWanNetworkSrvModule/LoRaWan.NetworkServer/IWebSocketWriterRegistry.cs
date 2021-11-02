// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    public interface IWebSocketWriterRegistry<TKey, TMessage>
        where TKey : notnull
        where TMessage : notnull
    {
        IWebSocketWriter<TMessage> Deregister(TKey key);
        TKey[] Prune();
        WebSocketWriterHandle<TKey, TMessage> Register(TKey key, IWebSocketWriter<TMessage> socketWriter);
        Task RunPrunerAsync(CancellationToken cancellationToken);
        Task RunPrunerAsync(TimeSpan interval, CancellationToken cancellationToken);
        ValueTask SendAsync(TKey key, TMessage message, CancellationToken cancellationToken);
    }
}
