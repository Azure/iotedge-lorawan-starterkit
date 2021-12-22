// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Simulation.Models
{
    using System.Text.Json;
    using Newtonsoft.Json;

    public class RouterMessage
    {
        /// <summary>
        /// Should be simplified to property
        /// </summary>
        [JsonProperty("router")]
#pragma warning disable CA1051 // Do not declare visible instance fields
        public string Router { get; set; }
#pragma warning restore CA1051 // Do not declare visible instance fields

        public RouterMessage(string router)
        {
            this.Router = router;
        }
    }
}
