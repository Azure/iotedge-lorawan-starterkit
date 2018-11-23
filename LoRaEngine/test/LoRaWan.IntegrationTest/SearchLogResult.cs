using System.Collections.Generic;

namespace LoRaWan.IntegrationTest
{
    public class SearchLogResult
    {
        // Indicates if the message was found
        public bool Found { get; }

        // Returns the contents of the log (to diagnose problems)
        public IReadOnlyCollection<string> Logs { get; }

        public SearchLogResult(bool found, HashSet<string> logs)
        {
            this.Found = found;
            this.Logs = logs;
        }
    }
    
}