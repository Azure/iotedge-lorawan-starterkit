// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan
{
    using System;
    using System.Text;

    public class LoRaProcessingException : Exception
    {
        public LoRaProcessingErrorCode ErrorCode { get; }

        public LoRaProcessingException()
        { }

        public LoRaProcessingException(string message) : base(message)
        { }

        public LoRaProcessingException(string message, Exception innerException)
            : base(message, innerException)
        { }

        public LoRaProcessingException(string message, Exception innerException, LoRaProcessingErrorCode errorCode)
            : base(message, innerException)
        {
            ErrorCode = errorCode;
        }

        public LoRaProcessingException(string message, LoRaProcessingErrorCode errorCode)
            : base(message)
        {
            ErrorCode = errorCode;
        }

        public override string ToString()
        {
            var sb = new StringBuilder();
            _ = sb.Append(GetType()).Append(": ").AppendLine(Message)
                  .Append(nameof(ErrorCode)).Append(": ").Append(ErrorCode).AppendLine();

            if (InnerException != null)
            {
                _ = sb.Append(" ---> ").Append(InnerException).AppendLine()
                      .AppendLine("   --- End of inner exception stack trace ---");
            }

            if (StackTrace != null)
                _ = sb.AppendLine(StackTrace);

            return sb.ToString();
        }
    }

    public enum LoRaProcessingErrorCode
    {
        Default,
        TwinFetchFailed,
        InvalidDeviceConfiguration,
        DeviceClientCreationFailed,
        PayloadDecryptionFailed,
        DeviceInitializationFailed,
        InvalidDataRate,
        InvalidDataRateOffset,
        TagNotSet,
        InvalidFrequency,
        InvalidFormat,
        PayloadNotSet,
        RegionNotSet
    }
}
