// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer
{
    using System;
    using System.Collections.Generic;
    using Microsoft.Extensions.Logging;

    // Helper class to log process operations
    internal sealed class ProcessLogger : IDisposable
    {
        LoRaOperationTimeWatcher timeWatcher;
        string devEUI;
        ReadOnlyMemory<byte> devAddr;

        public LogLevel LogLevel { get; set; }

        internal ProcessLogger(LoRaOperationTimeWatcher timeWatcher)
        {
            this.LogLevel = LogLevel.Information;
            this.timeWatcher = timeWatcher;
            this.LogLevel = LogLevel.Information;
        }

        internal ProcessLogger(LoRaOperationTimeWatcher timeWatcher, ReadOnlyMemory<byte> devAddr)
        {
            this.LogLevel = LogLevel.Information;
            this.timeWatcher = timeWatcher;
            this.devAddr = devAddr;
            this.LogLevel = LogLevel.Information;
        }

        internal void SetDevAddr(ReadOnlyMemory<byte> value) => this.devAddr = value;

        internal void SetDevEUI(string value) => this.devEUI = value;

        public void Dispose()
        {
            Logger.Log(this.devEUI ?? LoRaTools.Utils.ConversionHelper.ByteArrayToString(this.devAddr), $"processing time: {this.timeWatcher.GetElapsedTime()}", this.LogLevel);
            GC.SuppressFinalize(this);
        }
    }
}