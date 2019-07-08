// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.IntegrationTest.RetryHelper
{
    using Xunit;
    using Xunit.Sdk;

    [XunitTestCaseDiscoverer("LoRaWan.IntegrationTest.RetryHelper.RetryFactDiscoverer", "LoRaWan.IntegrationTest")]
    public class RetryFactAttribute : FactAttribute
    {
        public RetryFactAttribute()
        {
        }

        public RetryFactAttribute(int maxRetries)
        {
            this.MaxRetries = maxRetries;
        }

        public int MaxRetries { get; set; } = 5;
    }
}
