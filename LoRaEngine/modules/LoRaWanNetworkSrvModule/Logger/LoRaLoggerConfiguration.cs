// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

namespace Logger
{
    using Microsoft.Extensions.Logging;

    public sealed class LoRaLoggerConfiguration
    {
        public LogLevel LogLevel { get; set; }
        public EventId EventId { get; set; }
        public bool UseScopes { get; set; } = true;
    }
}
