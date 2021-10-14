// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace XunitRetryHelper
{
    using Xunit;
    using Xunit.Sdk;

    [XunitTestCaseDiscoverer("XunitRetryHelper.RetryFactTestDiscoverer", "XunitRetryHelper")]
    public class RetryFactAttribute : FactAttribute
    {
        public RetryFactAttribute()
        {
        }

        public RetryFactAttribute(int maxRetries)
        {
            this.MaxRetries = maxRetries;
        }

        public int MaxRetries { get; } = Constants.DefaultMaxRetries;
    }
}
