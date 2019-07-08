// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Test.Shared
{
    public class SearchLogEvent
    {
        public SearchLogEvent()
        {
        }

        public SearchLogEvent(string rawMessage)
        {
            var parsedMessage = Parse(rawMessage);
            this.Message = parsedMessage.Message;
            this.SourceId = parsedMessage.SourceId;
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
                    var idxEnd = message.IndexOf(']');
                    if (idxEnd != -1)
                    {
                        if (message.Length >= idxEnd + 1)
                        {
                            sourceId = message.Substring(1, idxEnd - 1);
                            message = message.Substring(idxEnd + 1).TrimStart();
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