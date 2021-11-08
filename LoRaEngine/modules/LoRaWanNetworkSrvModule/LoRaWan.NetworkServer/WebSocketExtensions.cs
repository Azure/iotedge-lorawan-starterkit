// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

namespace LoRaWan.NetworkServer
{
    using System;
    using System.Buffers;
    using System.Collections.Generic;
    using System.IO;
    using System.Net.WebSockets;
    using System.Threading;

    public static class WebSocketExtensions
    {
        /// <summary>
        /// Reads all text messages arriving on the socket until a message indicating that the
        /// socket is closed has been received.
        /// </summary>
        /// <exception cref="NotSupportedException">
        /// Thrown when a message type other than text is received.
        /// </exception>
        /// <remarks>
        /// The underlying socket is not closed.
        /// </remarks>
        public static IAsyncEnumerator<string> ReadTextMessages(this WebSocket socket, CancellationToken cancellationToken) =>
            socket.ReadTextMessages(MemoryPool<byte>.Shared, 1024, cancellationToken);

        /// <summary>
        /// Reads all text messages arriving on the socket until a message indicating that the
        /// socket is closed has been received. Additional arguments specify the memory pool and
        /// buffer size to use for receiving messages.
        /// </summary>
        /// <exception cref="NotSupportedException">
        /// Thrown when a message type other than text is received.
        /// </exception>
        /// <remarks>
        /// The underlying socket is not closed.
        /// </remarks>
        public static async IAsyncEnumerator<string> ReadTextMessages(this WebSocket socket,
                                                                      MemoryPool<byte> memoryPool, int minBufferSize,
                                                                      CancellationToken cancellationToken)
        {
            if (socket == null) throw new ArgumentNullException(nameof(socket));
            if (memoryPool == null) throw new ArgumentNullException(nameof(memoryPool));

            while (true)
            {
                ValueWebSocketReceiveResult result;
                using var buffer = memoryPool.Rent(minBufferSize);
                using var ms = new MemoryStream(buffer.Memory.Length);
                do
                {
                    result = await socket.ReceiveAsync(buffer.Memory, cancellationToken);
#pragma warning disable IDE0010 // Add missing cases (all are covered)
                    switch (result.MessageType)
#pragma warning restore IDE0010 // Add missing cases
                    {
                        case WebSocketMessageType.Close:
                            yield break;
                        case var type and not WebSocketMessageType.Text:
                            throw new NotSupportedException($"Invalid message type received: {type}");
                    }

                    ms.Write(buffer.Memory.Span[..result.Count]);
                }
                while (!result.EndOfMessage);

                ms.Position = 0;

                string input;
                using (var reader = new StreamReader(ms))
                    input = reader.ReadToEnd();
                yield return input;
            }
        }
    }
}
