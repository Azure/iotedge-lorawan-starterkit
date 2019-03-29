// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaTools.ADR
{
    using System;
    using System.Runtime.Serialization;

    [Serializable]
    internal class ADRException : Exception
    {
        public ADRException()
        {
        }

        public ADRException(string message)
            : base(message)
        {
        }

        public ADRException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        protected ADRException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }
    }
}