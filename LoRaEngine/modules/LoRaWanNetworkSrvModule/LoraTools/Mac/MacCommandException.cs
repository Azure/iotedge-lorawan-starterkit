// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaTools.Mac
{
    using System;
    using System.Collections.Generic;
    using System.Runtime.Serialization;
    using System.Text;

    [Serializable]
    public class MacCommandException : Exception
    {
        public MacCommandException(string message)
            : base(message)
        {
        }

        public MacCommandException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        protected MacCommandException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }
    }
}
