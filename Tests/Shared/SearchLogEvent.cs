// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Shared
{
    using System;

    public class SearchLogEvent
    {
        public SearchLogEvent()
        {
        }

        public SearchLogEvent(string rawMessage)
        {
            var parsedMessage = Parse(rawMessage);
            Message = parsedMessage.Message;
            SourceId = parsedMessage.SourceId;
        }

        internal static (string Message, string SourceId) Parse(string rawMessage)
        {
            string message = null;
            string sourceId = null;

            if (!string.IsNullOrEmpty(rawMessage))
            {
                message = rawMessage.Trim();
                if (message.StartsWith('['))
                {
                    var idxEnd = message.IndexOf(']', StringComparison.Ordinal);
                    if (idxEnd != -1)
                    {
                        if (message.Length >= idxEnd + 1)
                        {
                            sourceId = message[1..idxEnd];
                            message = message[(idxEnd + 1)..].TrimStart();
                        }
                    }
                }
            }

            return (message, sourceId);
        }

        public string Message { get; set; }

        public string SourceId { get; set; }
    }
}
