// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Simulation.Models
{
    using Newtonsoft.Json;

    public sealed record RouterMessage
    {
        /// <summary>
        /// Should be simplified to property
        /// </summary>
        [JsonProperty("router")]
        public string Router { get; }

        public RouterMessage(string router)
        {
            this.Router = router;
        }
    }
}
