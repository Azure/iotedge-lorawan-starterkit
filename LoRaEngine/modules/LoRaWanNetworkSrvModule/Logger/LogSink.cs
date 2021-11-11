// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

namespace LoRaWan
{
    using System;
    using System.Buffers;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Net.Sockets;
    using System.Text;
    using System.Threading;
    using System.Threading.Channels;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;

    public interface ILogSink
    {
        public LogLevel LogLevel { get; }
        public void Log(LogLevel logLevel, string message);
    }

    public class LogSink : ILogSink
    {
        public LogSink(LogLevel logLevel) => LogLevel = logLevel;

        public LogLevel LogLevel { get; }

        public virtual void Log(LogLevel logLevel, string message)
        {
            if (logLevel > LogLevel)
                return;
            CoreLog(logLevel, message);
        }

        protected virtual void CoreLog(LogLevel logLevel, string message) { /* Nop */ }
    }

    public static class NullLogSink
    {
        public static readonly ILogSink Instance = new LogSink(LogLevel.None);
    }

    public static class LogSinkExtensions
    {
        public static ILogSink And(this ILogSink first, ILogSink second) =>
            CompositeLogSink.Create(first, second);

        public static void LogAlways(this ILogSink logger, string message)
        {
            if (logger == null) throw new ArgumentNullException(nameof(logger));
            logger.Log(null, message, LogLevel.Critical);
        }

        public static void Log(this ILogSink logger, string message, LogLevel logLevel)
        {
            if (logger == null) throw new ArgumentNullException(nameof(logger));
            logger.Log(null, message, logLevel);
        }

        public static void Log(this ILogSink logger, string? deviceId, string message, LogLevel logLevel)
        {
            if (logger == null) throw new ArgumentNullException(nameof(logger));
            logger.Log(logLevel, deviceId is { Length: > 0 } ? $"{deviceId}: {message}" : message);
        }

        /// <summary>
        /// Use this if you want to serialize an object to JSON and
        /// append it to the message. The serialization will only take place
        /// if the logLevel is larger or equal to the configured level.
        /// </summary>
        public static void Log(this ILogSink logger, string deviceId, string message, object toJson, LogLevel logLevel)
        {
            if (logger == null) throw new ArgumentNullException(nameof(logger));

            if (logLevel < logger.LogLevel)
                return;

            var serializedObj = Newtonsoft.Json.JsonConvert.SerializeObject(toJson);
            logger.Log(deviceId, string.Concat(message, serializedObj), logLevel);
        }
    }

    public static class CompositeLogSink
    {
        public static ILogSink Create(ILogSink first, ILogSink second) =>
            new LogSink(first ?? throw new ArgumentNullException(nameof(first)),
                        second ?? throw new ArgumentNullException(nameof(second)));

        public static ILogSink? Choose(ILogSink? first, ILogSink? second) =>
            (first, second) switch
            {
                ({ } fst, null) => fst,
                (null, { } snd) => snd,
                ({ } fst, { } snd) => Create(fst, snd),
                _ => null
            };

        public static ILogSink Create(params ILogSink[] sinks) =>
            sinks.Length switch
            {
                0 => NullLogSink.Instance,
                1 => sinks[0],
                _ => sinks.Aggregate(Create)
            };

        private sealed class LogSink : ILogSink, IDisposable
        {
            private readonly ILogSink first;
            private readonly ILogSink second;

            public LogSink(ILogSink first, ILogSink second)
            {
                LogLevel = (LogLevel)Math.Min((int)first.LogLevel, (int)second.LogLevel);
                (this.first, this.second) = (first, second);
            }

            public LogLevel LogLevel { get; }

            public void Log(LogLevel logLevel, string message)
            {
                this.first.Log(logLevel, message);
                this.second.Log(logLevel, message);
            }

            public void Dispose()
            {
                (this.first as IDisposable)?.Dispose();
                (this.second as IDisposable)?.Dispose();
            }
        }
    }

    public sealed class ConsoleLogSink : LogSink
    {
        public ConsoleLogSink(LogLevel logLevel) : base(logLevel) { }

        protected override void CoreLog(LogLevel logLevel, string message)
        {
            var writer = logLevel == LogLevel.Error ? Console.Error : Console.Out;
            writer.WriteLine(FormattableString.Invariant($"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} {message}"));
        }
    }

    public sealed class TcpLogSink : ILogSink, IDisposable
    {
        private static readonly TimeSpan ConnectionRetryInterval = TimeSpan.FromSeconds(10);

        private readonly ILogger? logger;
        private readonly IPEndPoint serverEndpoint;
        private readonly Func<string, string>? formatter;
        private readonly CancellationTokenSource cancellationTokenSource = new();
        private readonly Channel<string> channel = Channel.CreateBounded<string>(new BoundedChannelOptions(1000)
        {
            SingleReader = true,
            FullMode = BoundedChannelFullMode.DropOldest,
        });

        public static TcpLogSink Start(IPEndPoint serverEndPoint, LogLevel logLevel,
                                       Func<string, string>? formatter = null,
                                       ILogger<TcpLogSink>? logger = null)
        {
            var sink = new TcpLogSink(serverEndPoint, logLevel, formatter, logger);
            _ = Task.Run(() => sink.SendAllLogMessagesAsync(sink.cancellationTokenSource.Token));
            return sink;
        }

        private TcpLogSink(IPEndPoint serverEndpoint, LogLevel logLevel,
                           Func<string, string>? formatter,
                           ILogger? logger)
        {
            this.serverEndpoint = serverEndpoint;
            LogLevel = logLevel;
            this.formatter = formatter;
            this.logger = logger;
        }

        public void Dispose()
        {
            try
            {
                this.cancellationTokenSource.Cancel();
            }
            catch (AggregateException ex)
            {
                // "AggregateException" is thrown and contains all the exceptions thrown by the
                // registered callbacks on the associated "CancellationToken". However,
                // "IDisposable.Dispose" methods are not expected to throw exceptions thus it is
                // simply ignore here.

                Debug.WriteLine(ex);
            }

            this.cancellationTokenSource.Dispose();
        }

        private async Task SendAllLogMessagesAsync(CancellationToken cancellationToken)
        {
            var encoding = Encoding.UTF8;
            var buffers = ArrayPool<byte>.Shared;
            var client = new TcpClient();

            try
            {
                await foreach (var message in this.channel.Reader.ReadAllAsync(cancellationToken))
                {
                    const int attempts = 10;
                    for (var attempt = 1; attempt <= attempts; attempt++)
                    {
                        try
                        {
                            if (!client.Connected)
                            {
                                this.logger?.LogDebug("Connecting to log server at: " + this.serverEndpoint);
                                await client.ConnectAsync(this.serverEndpoint.Address, this.serverEndpoint.Port);
                            }

                            var size = encoding.GetByteCount(message) + 2;
                            var buffer = buffers.Rent(size);
                            var bi = encoding.GetBytes(message, buffer);
                            buffer[bi++] = 13; // CR
                            buffer[bi] = 10;   // LF

                            try
                            {
                                await client.GetStream().WriteAsync(buffer, cancellationToken);
                                break;
                            }
                            finally
                            {
                                buffers.Return(buffer);
                            }
                        }
                        catch (SocketException ex)
                        {
                            this.logger?.LogError(ex, "Error writing to the logging socket.");
                            client.Dispose();
                            client = new TcpClient();
                            this.logger?.LogError(ex, $"Waiting (delay = {ConnectionRetryInterval}) before retrying to connecting to logging server.");
                            await Task.Delay(ConnectionRetryInterval, cancellationToken);
                        }
                    }
                }
            }
            finally
            {
                client.Dispose();
            }
        }

        public LogLevel LogLevel { get; }

        private static readonly char[] NewLineChars = { '\n', '\r' };

        public void Log(LogLevel logLevel, string message)
        {
            if (message == null) throw new ArgumentNullException(nameof(message));

            message = this.formatter?.Invoke(message) ?? message;

            if (message.IndexOfAny(NewLineChars) >= 0)
            {
                foreach (var line in SplitIntoLines(message))
                    _ = this.channel.Writer.TryWrite(line);
            }
            else
            {
                _ = this.channel.Writer.TryWrite(message);
            }

            static IEnumerable<string> SplitIntoLines(string input)
            {
                using var reader = new StringReader(input);
                while (reader.ReadLine() is { } line)
                    yield return line;
            }
        }
    }
}
