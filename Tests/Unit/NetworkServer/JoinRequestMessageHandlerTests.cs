// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Unit.NetworkServer
{
    using System.Diagnostics.Metrics;
    using System.Linq;
    using System.Threading.Tasks;
    using LoRaWan.NetworkServer;
    using LoRaWan.Tests.Common;
    using Microsoft.Extensions.Logging.Abstractions;
    using Moq;
    using Xunit;

    public sealed class JoinRequestMessageHandlerTests
    {
        private const string MetricNamespace = nameof(JoinRequestMessageHandlerTests);

        [Theory]
        [InlineData(ConcentratorDeduplicationResult.Duplicate, 0)]
        [InlineData(ConcentratorDeduplicationResult.DuplicateDueToResubmission, 0)]
        [InlineData(ConcentratorDeduplicationResult.SoftDuplicateDueToDeduplicationStrategy, 0)]
        [InlineData(ConcentratorDeduplicationResult.NotDuplicate, 1)]
        public async Task Increases_Join_Request_Counter_If_Not_Duplicate(ConcentratorDeduplicationResult deduplicationResult, int joinRequestCount)
        {
            // arrange
            using var subject = Setup(deduplicationResult);
            using var request = WaitableLoRaRequest.CreateWaitableRequest(new SimulatedDevice(TestDeviceInfo.CreateOTAADevice(1)).CreateJoinRequest());
            using var metricListener = new TestMetricListener(MetricNamespace);
            metricListener.Start();

            // act
            await subject.Value.ProcessJoinRequestAsync(request);

            // assert
            Assert.Equal(joinRequestCount, metricListener.RecordedMetrics.Count(m => m.Instrument.Name == MetricRegistry.JoinRequests.Name && m.Value == 1));
        }

        private static DisposableValue<JoinRequestMessageHandler> Setup(ConcentratorDeduplicationResult deduplicationResult)
        {
            var deduplicationMock = new Mock<IConcentratorDeduplication>();
            _ = deduplicationMock.Setup(d => d.CheckDuplicateJoin(It.IsAny<LoRaRequest>())).Returns(deduplicationResult);
#pragma warning disable CA2000 // Dispose objects before losing scope (dispose handled in DisposableValue)
            var meter = new Meter(MetricNamespace);
#pragma warning restore CA2000 // Dispose objects before losing scope
            return new DisposableValue<JoinRequestMessageHandler>(
                new JoinRequestMessageHandler(new NetworkServerConfiguration(),
                                              deduplicationMock.Object,
                                              Mock.Of<ILoRaDeviceRegistry>(),
                                              NullLogger<JoinRequestMessageHandler>.Instance,
                                              Mock.Of<LoRaDeviceAPIServiceBase>(),
                                              meter),
                meter);
        }
    }
}
