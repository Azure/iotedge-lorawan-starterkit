// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.IntegrationTest.RetryHelper
{
    using System.Collections.Generic;
    using Xunit.Abstractions;
    using Xunit.Sdk;

    public class DelayedMessageBus : IMessageBus
    {
        private readonly IMessageBus innerBus;
        private readonly IList<IMessageSinkMessage> messages = new List<IMessageSinkMessage>();

        public DelayedMessageBus(IMessageBus innerBus)
        {
            this.innerBus = innerBus;
        }

        public bool QueueMessage(IMessageSinkMessage message)
        {
            lock (this.messages)
            {
                this.messages.Add(message);
            }

            return true;
        }

        public void Complete()
        {
            for (var i = 0; i < this.messages.Count; i++)
            {
                this.innerBus.QueueMessage(this.messages[i]);
            }

            this.messages.Clear();
        }

        public void Dispose()
        {
            this.messages.Clear();
        }
    }
}
