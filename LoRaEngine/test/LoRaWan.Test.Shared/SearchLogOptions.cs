// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Test.Shared
{
    // Options to find iot hub message
    public class SearchLogOptions
    {
        public string Description { get; set; }

        public int? MaxAttempts { get; set; }

        // Defines if the not finding messages should be treated as errors
        public bool? TreatAsError { get; set; }

        /// <summary>
        /// Gets or sets the source id filter that is used to
        /// filter for a specific source (in multi gateway scenarios).
        /// This currently only support inclusion of the specified id and exlusion
        /// of anything that does not match that string
        /// </summary>
        public string SourceIdFilter { get; set; }

        public SearchLogOptions()
        {
        }

        public SearchLogOptions(string description)
        {
            this.Description = description;
        }
    }
}
