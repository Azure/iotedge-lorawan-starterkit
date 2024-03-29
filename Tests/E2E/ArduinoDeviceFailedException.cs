// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.E2E
{
    using System;
    using System.Runtime.Serialization;

    [Serializable]
    public class ArduinoDeviceFailedException : Exception
    {
        public ArduinoDeviceFailedException()
        {
        }

        public ArduinoDeviceFailedException(string message)
            : base(message)
        {
        }

        public ArduinoDeviceFailedException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        protected ArduinoDeviceFailedException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }
    }
}
