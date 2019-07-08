// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.IntegrationTest.RetryHelper
{
    using Xunit;
    using Xunit.Sdk;

    [XunitTestCaseDiscoverer("LoRaWan.IntegrationTest.RetryHelper.RetryTheoryTestDiscoverer", "LoRaWan.IntegrationTest")]
    public class RetryTheoryAttribute : TheoryAttribute
    {
        public RetryTheoryAttribute()
        {
        }

        public RetryTheoryAttribute(int maxRetries)
        {
            this.MaxRetries = maxRetries;
        }

        public int MaxRetries { get; set; } = 5;
    }
}
