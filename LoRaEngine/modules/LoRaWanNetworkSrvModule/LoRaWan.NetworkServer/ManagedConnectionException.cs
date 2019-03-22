// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer
{
    using System;
    using System.Runtime.Serialization;

    /// <summary>
    /// Exception raised if there is a problem with a managed device connection
    /// </summary>
    [Serializable]
    public class ManagedConnectionException : Exception
    {
        public ManagedConnectionException()
        {
        }

        public ManagedConnectionException(string message)
            : base(message)
        {
        }

        public ManagedConnectionException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        protected ManagedConnectionException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }
    }
}