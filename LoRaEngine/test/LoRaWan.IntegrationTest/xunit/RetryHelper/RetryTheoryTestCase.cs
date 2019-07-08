// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.IntegrationTest.RetryHelper
{
    using System;
    using System.ComponentModel;
    using System.Threading;
    using System.Threading.Tasks;
    using Xunit.Abstractions;
    using Xunit.Sdk;

    [Serializable]
    public class RetryTheoryTestCase : XunitTheoryTestCase
    {
        private int maxRetries;

        [EditorBrowsable(EditorBrowsableState.Never)]
        [Obsolete("Called by the de-serializer", true)]
        public RetryTheoryTestCase()
        {
        }

        public RetryTheoryTestCase(IMessageSink diagnosticMessageSink, TestMethodDisplay testMethodDisplay, ITestMethod testMethod, int maxRetries)
            : base(diagnosticMessageSink, testMethodDisplay, testMethod)
        {
            this.maxRetries = maxRetries;
        }

        // This method is called by the xUnit test framework classes to run the test case. We will do the
        // loop here, forwarding on to the implementation in XunitTestCase to do the heavy lifting. We will
        // continue to re-run the test until the aggregator has an error (meaning that some internal error
        // condition happened), or the test runs without failure, or we've hit the maximum number of tries.
        public override async Task<RunSummary> RunAsync(
                                                IMessageSink diagnosticMessageSink,
                                                IMessageBus messageBus,
                                                object[] constructorArguments,
                                                ExceptionAggregator aggregator,
                                                CancellationTokenSource cancellationTokenSource)
        {
            return await RetryLogicWrapper.PerformRetry(base.RunAsync, this.maxRetries, diagnosticMessageSink, messageBus, constructorArguments, aggregator, cancellationTokenSource);
        }

        public override void Serialize(IXunitSerializationInfo data)
        {
            base.Serialize(data);

            data.AddValue("MaxRetries", this.maxRetries);
        }

        public override void Deserialize(IXunitSerializationInfo data)
        {
            base.Deserialize(data);

            this.maxRetries = data.GetValue<int>("MaxRetries");
        }
    }
}
