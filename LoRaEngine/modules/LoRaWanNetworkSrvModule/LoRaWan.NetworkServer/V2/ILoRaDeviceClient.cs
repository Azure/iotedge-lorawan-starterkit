//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//


using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Azure.Devices.Client;
using Microsoft.Azure.Devices.Shared;

namespace LoRaWan.NetworkServer.V2
{
    public interface ILoRaDeviceClient : IDisposable
    {
        Task<Twin> GetTwinAsync();
        Task SendEventAsync(string messageBody, Dictionary<string, string> properties);
        Task UpdateReportedPropertiesAsync(Dictionary<string, object> values);
        Task<Message> ReceiveAsync(TimeSpan timeout);
        Task<bool> CompleteAsync(Message cloudToDeviceMessage);
        Task<bool> AbandonAsync(Message cloudToDeviceMessage);
    }
}