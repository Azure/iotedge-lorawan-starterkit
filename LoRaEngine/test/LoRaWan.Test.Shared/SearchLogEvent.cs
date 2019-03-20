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
            if (!string.IsNullOrEmpty(rawMessage))
            {
                this.Message = rawMessage.Trim();
                if (rawMessage.StartsWith('['))
                {
                    var idxEnd = rawMessage.IndexOf(']');
                    if (idxEnd != -1)
                    {
                        if (rawMessage.Length >= idxEnd + 1)
                        {
                            this.SourceId = rawMessage.Substring(1, idxEnd - 1);
                            this.Message = rawMessage.Substring(idxEnd + 1).TrimStart();
                        }
                    }
                }
            }
        }

        public string Message { get; set; }

        public string SourceId { get; set; }
    }
}