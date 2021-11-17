// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

namespace LoRaWan.NetworkServer
{
    using System;
    using System.Runtime.Serialization;

    [Serializable]
    public class LoRaNetworkServerException : Exception
    {
        public LoRaNetworkServerException() : this(null) { }
        public LoRaNetworkServerException(string? message) : this(message, null) { }
        public LoRaNetworkServerException(string? message, Exception? inner) : base(message, inner) { }

        protected LoRaNetworkServerException(SerializationInfo info,
                                          StreamingContext context)
            : base(info, context) { }
    }
}
