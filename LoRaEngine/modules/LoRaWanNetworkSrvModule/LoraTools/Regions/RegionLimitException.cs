// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaTools.Regions
{
    using System;

    /// <summary>
    /// Exception representing invalid region parameters.
    /// </summary>
    public class RegionLimitException : Exception
    {
        public RegionLimitExceptionType RegionLimitExceptionType { get; set; }

        public RegionLimitException(string message, RegionLimitExceptionType regionMappingExceptionType)
            : base(message)
        {
            RegionLimitExceptionType = regionMappingExceptionType;
        }

        public RegionLimitException()
        {
        }

        public RegionLimitException(string message)
            : base(message)
        {
        }

        public RegionLimitException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
