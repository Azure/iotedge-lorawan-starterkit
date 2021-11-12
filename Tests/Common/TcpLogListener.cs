// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

namespace LoRaWan.Tests.Common
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Net;
    using System.Text;
    using System.Threading.Tasks;

    public sealed class TcpLogListener : IDisposable
    {
        private static readonly char[] NewLineChars = { '\r', '\n' };

        public static TcpLogListener Start(int port, bool dontLogToConsole = false)
        {
            var encoding = Encoding.UTF8;
            var lines = new ConcurrentQueue<string>();

            Task OnProcessLineAsync(string line)
            {
                line = line.TrimEnd(NewLineChars);
                lines.Enqueue(line);
                if (!dontLogToConsole)
                    TestLogger.Log($"[TCPLOG] {line}");
                return Task.CompletedTask;
            }

            var listener =
                SimpleTcpListener.Start(port, backlog: 1,
                                        (_, stream) => stream.ProcessLinesAsync(encoding, OnProcessLineAsync));

            try
            {
                var logListener = new TcpLogListener(listener, lines);
                TestLogger.Log($"*** TCP Log Listener created: {IPAddress.Any}:{port} ***");
                return logListener;
            }
            catch (Exception)
            {
                listener.Dispose();
                throw;
            }
        }

        private readonly SimpleTcpListener listener;
        private readonly ConcurrentQueue<string> events;

        private TcpLogListener(SimpleTcpListener listener, ConcurrentQueue<string> events)
        {
            this.listener = listener;
            this.events = events;
        }

        public IEnumerable<string> Events => this.events;

        public void ResetEvents()
        {
            TestLogger.Log($"*** Clearing udp logs (~{this.events.Count}) ***");
            this.events.Clear();
        }

        public void Dispose() => this.listener.Dispose();
    }
}
