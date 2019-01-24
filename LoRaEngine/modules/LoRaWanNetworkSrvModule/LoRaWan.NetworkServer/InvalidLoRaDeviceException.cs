// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer
{
    using System;
    using System.Runtime.Serialization;

    /// <summary>
    /// Defines a error loading a LoRaDevice
    /// </summary>
    [Serializable]
    public class InvalidLoRaDeviceException : Exception
    {
        public InvalidLoRaDeviceException()
        {
        }

        public InvalidLoRaDeviceException(string message)
            : base(message)
        {
        }

        public InvalidLoRaDeviceException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        protected InvalidLoRaDeviceException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }
    }
}