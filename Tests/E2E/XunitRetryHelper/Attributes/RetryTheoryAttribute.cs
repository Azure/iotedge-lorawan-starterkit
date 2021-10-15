// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace XunitRetryHelper
{
    using Xunit;
    using Xunit.Sdk;

    [XunitTestCaseDiscoverer("XunitRetryHelper.RetryTheoryTestDiscoverer", "LoRaWan.Tests.E2E")]
    public class RetryTheoryAttribute : TheoryAttribute
    {
        public RetryTheoryAttribute()
        {
        }

        public RetryTheoryAttribute(int maxRetries)
        {
            MaxRetries = maxRetries;
        }

        public int MaxRetries { get; } = Constants.DefaultMaxRetries;
    }
}
