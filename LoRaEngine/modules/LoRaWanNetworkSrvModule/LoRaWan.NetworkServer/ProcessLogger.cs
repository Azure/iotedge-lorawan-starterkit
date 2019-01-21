//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//
using System;
using System.Collections.Generic;

namespace LoRaWan.NetworkServer
{
    // Helper class to log process operations
    internal sealed class ProcessLogger : IDisposable
    {
        LoRaOperationTimeWatcher timeWatcher;        
        string devEUI;
        ReadOnlyMemory<byte> devAddr;
        internal ProcessLogger(LoRaOperationTimeWatcher timeWatcher)
        {
            this.timeWatcher = timeWatcher;
        }

        internal ProcessLogger(LoRaOperationTimeWatcher timeWatcher, ReadOnlyMemory<byte> devAddr)
        {
            this.timeWatcher = timeWatcher;
            this.devAddr = devAddr;
        }



        internal void SetDevAddr(ReadOnlyMemory<byte> value) => devAddr = value;
        internal void SetDevEUI(string value) => devEUI = value;

        public void Dispose()
        {
            Logger.Log(devEUI ?? LoRaTools.Utils.ConversionHelper.ByteArrayToString(devAddr), $"processing time: {timeWatcher.GetElapsedTime()}", Logger.LoggingLevel.Info);
            GC.SuppressFinalize(this);

        }
    }
}