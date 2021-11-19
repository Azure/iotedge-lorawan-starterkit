// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

namespace LoRaWan.Tests.Common
{
    using System;
    using System.Buffers;
    using System.IO;
    using System.IO.Pipelines;
    using System.Text;
    using System.Threading.Tasks;

    public static class StreamExtensions
    {
        /// <summary>
        /// Reads lines terminated by LF from the stream and delivers them to a function for
        /// processing.
        /// </summary>
        /// <remarks>
        /// If the last line is not terminated by LF then it is not delivered to
        /// <paramref name="processor"/>.
        /// </remarks>
        public static async Task ProcessLinesAsync(this Stream stream, Encoding encoding, Func<string, Task> processor)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            if (encoding == null) throw new ArgumentNullException(nameof(encoding));
            if (processor == null) throw new ArgumentNullException(nameof(processor));

            var reader = PipeReader.Create(stream);
            var arrays = ArrayPool<byte>.Shared;

            while (true)
            {
                var result = await reader.ReadAsync().ConfigureAwait(false);
                var buffer = result.Buffer;

                while (TryReadLine(ref buffer, out var line))
                    await processor(ReadLine(line)).ConfigureAwait(false);

                reader.AdvanceTo(buffer.Start, buffer.End);

                if (result.IsCompleted) // are we there yet?
                    break;
            }

            await reader.CompleteAsync().ConfigureAwait(false);

            static bool TryReadLine(ref ReadOnlySequence<byte> buffer, out ReadOnlySequence<byte> line)
            {
                if (buffer.PositionOf((byte)'\n') is { } position)
                {
                    position = buffer.GetPosition(1, position);
                    line = buffer.Slice(0, position); // include the LF
                    buffer = buffer.Slice(position);  // skip the LF
                    return true;
                }

                line = default;
                return false;
            }

            string ReadLine(in ReadOnlySequence<byte> sequence)
            {
                if (sequence.IsEmpty)
                    return string.Empty;

                if (sequence.IsSingleSegment)
                    return encoding.GetString(sequence.First.Span);

                var length = checked((int)sequence.Length);
                var array = (byte[]?)null;
                var bytes = length <= 128 ? stackalloc byte[length] : array = arrays.Rent(length);

                var reader = new SequenceReader<byte>(sequence);
                _ = reader.TryCopyTo(bytes);
                bytes = bytes[..length];

                if (array is { } someArray)
                {
                    try
                    {
                        return encoding.GetString(bytes);
                    }
                    finally
                    {
                        arrays.Return(someArray);
                    }
                }
                else
                {
                    return encoding.GetString(bytes);
                }
            }
        }
    }
}
