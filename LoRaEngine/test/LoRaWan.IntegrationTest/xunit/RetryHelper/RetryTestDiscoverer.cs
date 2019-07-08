// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.IntegrationTest.RetryHelper
{
    using System;
    using System.Collections.Generic;
    using Xunit.Abstractions;
    using Xunit.Sdk;

    public class RetryTestDiscoverer : IXunitTestCaseDiscoverer
    {
        readonly IMessageSink diagnosticMessageSink;

        public RetryTestDiscoverer(IMessageSink diagnosticMessageSink)
        {
            this.diagnosticMessageSink = diagnosticMessageSink;
        }

        public IEnumerable<IXunitTestCase> Discover(ITestFrameworkDiscoveryOptions discoveryOptions, ITestMethod testMethod, IAttributeInfo factAttribute)
        {
            var maxRetries = Math.Max(1, factAttribute.GetNamedArgument<int>("MaxRetries"));
            yield return new RetryTestCase(this.diagnosticMessageSink, discoveryOptions.MethodDisplayOrDefault(), testMethod, maxRetries);
        }
    }
}
