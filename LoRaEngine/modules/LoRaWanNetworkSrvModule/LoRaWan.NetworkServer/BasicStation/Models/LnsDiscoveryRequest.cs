﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer.BasicStation.Models
{
    using System.Collections.Generic;
    using System.Text.Json.Serialization;

    class LnsDiscoveryRequest
    {
        [JsonPropertyName("router")]
        public string Router { get; set; }
    }
}
