// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoraKeysManagerFacade
{
    using System.Threading.Tasks;
    using LoRaTools;

    /// <summary>
    /// Interface for publisher interation.
    /// </summary>
    public interface IChannelPublisher
    {
        Task PublishAsync(string channel, LnsRemoteCall lnsRemoteCall);
    }
}
