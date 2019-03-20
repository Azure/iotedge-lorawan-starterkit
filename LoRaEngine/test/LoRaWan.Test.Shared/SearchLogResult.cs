// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Test.Shared
{
    using System.Collections.Generic;

    public class SearchLogResult
    {
        // Indicates if the message was found
        public bool Found { get; }

        // Returns the contents of the log (to diagnose problems)
        public IReadOnlyCollection<SearchLogEvent> Logs { get; }

        public SearchLogEvent MatchedEvent { get; set; }

        public string FoundLogResult { get; set; }

        public SearchLogResult(bool found, HashSet<SearchLogEvent> logs, string foundElement = null)
        {
            this.Found = found;
            this.Logs = logs;
            this.FoundLogResult = foundElement;
        }
    }
}