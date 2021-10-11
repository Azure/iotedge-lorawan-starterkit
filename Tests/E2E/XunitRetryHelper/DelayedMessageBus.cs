// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace XunitRetryHelper
{
    using System.Collections.Generic;
    using Xunit.Abstractions;
    using Xunit.Sdk;

    public class DelayedMessageBus : IMessageBus
    {
        private readonly IMessageBus innerBus;
        private readonly IList<IMessageSinkMessage> delayedMessages = new List<IMessageSinkMessage>();

        public DelayedMessageBus(IMessageBus innerBus)
        {
            this.innerBus = innerBus;
        }

        public bool QueueMessage(IMessageSinkMessage message)
        {
            lock (this.delayedMessages)
            {
                this.delayedMessages.Add(message);
            }

            return true;
        }

        public void Complete()
        {
            lock (this.delayedMessages)
            {
                for (var i = 0; i < this.delayedMessages.Count; i++)
                {
                    this.innerBus.QueueMessage(this.delayedMessages[i]);
                }

                this.delayedMessages.Clear();
            }
        }

        public TestFailed GetLastFailure()
        {
            lock (this.delayedMessages)
            {
                for (var i = this.delayedMessages.Count - 1; i > 0; i--)
                {
                    if (this.delayedMessages[i] is TestFailed failed)
                    {
                        return failed;
                    }
                }
            }

            return null;
        }

        public void Dispose()
        {
            lock (this.delayedMessages)
            {
                this.delayedMessages.Clear();
            }
        }
    }
}
