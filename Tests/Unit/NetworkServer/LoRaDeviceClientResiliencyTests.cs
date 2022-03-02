// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

namespace LoRaWan.Tests.Unit.NetworkServer
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Common;
    using LoRaWan.NetworkServer;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Shared;
    using Microsoft.Extensions.Logging;
    using Moq;
    using Moq.Language;
    using Xunit;

    /// <summary>
    /// Tests the client returned by <see cref="LoRaDeviceClientExtensions.AddResiliency"/>.
    /// </summary>
    public sealed class LoRaDeviceClientResiliencyTests : IAsyncDisposable
    {
        private readonly Mock<ILoRaDeviceClient> originalMock;
        private readonly Mock<ILogger> loggerMock = new();
        private readonly ILoRaDeviceClient subject;
        private readonly CancellationTokenSource cancellationTokenSource = new();

        public LoRaDeviceClientResiliencyTests()
        {
            this.originalMock = new Mock<ILoRaDeviceClient>();
            var loggerFactoryMock = new Mock<ILoggerFactory>();
            loggerFactoryMock.Setup(x => x.CreateLogger(It.IsAny<string>())).Returns(this.loggerMock.Object);
            this.subject = this.originalMock.Object.AddResiliency(loggerFactoryMock.Object);
        }

        public async ValueTask DisposeAsync()
        {
            this.cancellationTokenSource.Dispose();
            await this.subject.DisposeAsync();
        }

        private CancellationToken CancellationToken => this.cancellationTokenSource.Token;

        [Fact]
        public void AddResiliency_Returns_Same_For_Successive_Call()
        {
            var result = this.subject.AddResiliency(null);

            Assert.Same(this.subject, result);
        }

        [Fact]
        public void Client_Provides_Identity()
        {
            var identityProvider = Assert.IsAssignableFrom<IIdentityProvider<ILoRaDeviceClient>>(this.subject);

            Assert.Same(this.originalMock.Object, identityProvider.Identity);
        }

        [Fact]
        public void EnsureConnected_Invokes_Original_Client()
        {
            this.originalMock.Setup(x => x.EnsureConnected()).Returns(true);

            var result = this.subject.EnsureConnected();

            Assert.True(result);
        }

        [Theory]
        [MemberData(nameof(RetriedExceptionsData))]
        public void EnsureConnected_Is_Not_Resilient(Exception exception)
        {
            this.originalMock.Setup(x => x.EnsureConnected()).Throws(exception);

            var ex = Assert.Throws(exception.GetType(), () => _ = this.subject.EnsureConnected());

            Assert.Same(exception, ex);
            this.originalMock.Verify(x => x.EnsureConnected(), Times.Once);
        }

        [Fact]
        public async Task DisconnectAsync_Invokes_Original_Client()
        {
            await this.subject.DisconnectAsync(CancellationToken);

            this.originalMock.Verify(x => x.DisconnectAsync(CancellationToken), Times.Once);
        }

        private static readonly IEnumerable<Exception> RetriedExceptions = new []
        {
            new InvalidOperationException("This operation is only allowed using a successfully authenticated context. " + "" +
                                          "This sentence in the error message shouldn't matter."),
            new ObjectDisposedException("<object>")
        };

        public static readonly TheoryData<Exception> RetriedExceptionsData = TheoryDataFactory.From(RetriedExceptions);

        [Theory]
        [MemberData(nameof(RetriedExceptionsData))]
        public async Task DisconnectAsync_Is_Not_Resilient(Exception exception)
        {
            this.originalMock.Setup(x => x.DisconnectAsync(CancellationToken)).Throws(exception);

            var ex = await Assert.ThrowsAsync(exception.GetType(), () => this.subject.DisconnectAsync(CancellationToken));

            Assert.Same(exception, ex);
            this.originalMock.Verify(x => x.DisconnectAsync(CancellationToken), Times.Once);
        }

        [Fact]
        public async Task DisposeAsync_Invokes_Original_Client()
        {
            await this.subject.DisposeAsync();

            this.originalMock.Verify(x => x.DisposeAsync(), Times.Once);
        }

        [Theory]
        [MemberData(nameof(RetriedExceptionsData))]
        public async Task DisposeAsync_Is_Not_Resilient(Exception exception)
        {
            this.originalMock.Setup(x => x.DisposeAsync()).Throws(exception);

            var ex = await Assert.ThrowsAsync(exception.GetType(), () => this.subject.DisposeAsync().AsTask());

            Assert.Same(exception, ex);
            this.originalMock.Verify(x => x.DisposeAsync(), Times.Once);
        }

        public interface IOperationTestCase
        {
            IOperationSequentialResultSetup Setup(Mock<ILoRaDeviceClient> mock);
            Task InvokeAsync(ILoRaDeviceClient subject);
            void Verify(Mock<ILoRaDeviceClient> mock, Times times);
        }

        public interface IOperationSequentialResultSetup
        {
            IOperationSequentialResultSetup Succeed();
            IOperationSequentialResultSetup Fail(Exception exception);
        }

        private abstract class OperationTestCase<T> : IOperationTestCase, IOperationSequentialResultSetup, IDisposable
        {
            private readonly T result;
            private readonly CancellationTokenSource cancellationTokenSource = new();
            private ISetupSequentialResult<Task<T>>? setupSequentialResult;

            protected OperationTestCase(T result) => this.result = result;

            protected CancellationToken CancellationToken => this.cancellationTokenSource.Token;

            private ISetupSequentialResult<Task<T>> SetupSequentialResult => this.setupSequentialResult ?? throw new InvalidOperationException();

            protected abstract ISetupSequentialResult<Task<T>> SetupCore(Mock<ILoRaDeviceClient> mock);

            public virtual IOperationSequentialResultSetup Setup(Mock<ILoRaDeviceClient> mock)
            {
                this.setupSequentialResult = SetupCore(mock);
                return this;
            }

            public abstract Task InvokeAsync(ILoRaDeviceClient subject);
            public abstract void Verify(Mock<ILoRaDeviceClient> mock, Times times);

            protected virtual void Dispose(bool disposing)
            {
                if (disposing)
                {
                    this.cancellationTokenSource.Dispose();
                    if (this.result is IDisposable disposable)
                        disposable.Dispose();
                }
            }

            public void Dispose()
            {
                Dispose(true);
                GC.SuppressFinalize(this);
            }

            IOperationSequentialResultSetup IOperationSequentialResultSetup.Succeed()
            {
                SetupSequentialResult.ReturnsAsync(this.result);
                return this;
            }

            IOperationSequentialResultSetup IOperationSequentialResultSetup.Fail(Exception exception)
            {
                SetupSequentialResult.Throws(exception);
                return this;
            }
        }

        private sealed class GetTwinAsyncTestCase : OperationTestCase<Twin>
        {
            public GetTwinAsyncTestCase(Twin result) : base(result) { }

            protected override ISetupSequentialResult<Task<Twin>> SetupCore(Mock<ILoRaDeviceClient> mock) => mock.SetupSequence(x => x.GetTwinAsync(CancellationToken));
            public override Task InvokeAsync(ILoRaDeviceClient subject) => subject.GetTwinAsync(CancellationToken);
            public override void Verify(Mock<ILoRaDeviceClient> mock, Times times) => mock.Verify(x => x.GetTwinAsync(CancellationToken), times);
        }

        private sealed class SendEventAsyncTestCase : OperationTestCase<bool>
        {
            private readonly LoRaDeviceTelemetry telemetry;
            private readonly Dictionary<string, string> properties;

            public SendEventAsyncTestCase(LoRaDeviceTelemetry telemetry, Dictionary<string, string> properties, bool result) : base(result)
            {
                this.telemetry = telemetry;
                this.properties = properties;
            }

            protected override ISetupSequentialResult<Task<bool>> SetupCore(Mock<ILoRaDeviceClient> mock) => mock.SetupSequence(x => x.SendEventAsync(this.telemetry, this.properties));
            public override Task InvokeAsync(ILoRaDeviceClient subject) => subject.SendEventAsync(this.telemetry, this.properties);
            public override void Verify(Mock<ILoRaDeviceClient> mock, Times times) => mock.Verify(x => x.SendEventAsync(this.telemetry, this.properties), times);
        }

        private sealed class UpdateReportedPropertiesTestCase : OperationTestCase<bool>
        {
            private readonly TwinCollection twinCollection;

            public UpdateReportedPropertiesTestCase(TwinCollection twinCollection, bool result) : base(result) =>
                this.twinCollection = twinCollection;

            protected override ISetupSequentialResult<Task<bool>> SetupCore(Mock<ILoRaDeviceClient> mock) => mock.SetupSequence(x => x.UpdateReportedPropertiesAsync(this.twinCollection, CancellationToken));
            public override Task InvokeAsync(ILoRaDeviceClient subject) => subject.UpdateReportedPropertiesAsync(this.twinCollection, CancellationToken);
            public override void Verify(Mock<ILoRaDeviceClient> mock, Times times) => mock.Verify(x => x.UpdateReportedPropertiesAsync(this.twinCollection, CancellationToken), times);
        }

        private sealed class ReceiveTestCase : OperationTestCase<Message>
        {
            private readonly TimeSpan timeout;

            public ReceiveTestCase(TimeSpan timeout, Message result) : base(result) =>
                this.timeout = timeout;

            protected override ISetupSequentialResult<Task<Message>> SetupCore(Mock<ILoRaDeviceClient> mock) => mock.SetupSequence(x => x.ReceiveAsync(this.timeout));
            public override Task InvokeAsync(ILoRaDeviceClient subject) => subject.ReceiveAsync(this.timeout);
            public override void Verify(Mock<ILoRaDeviceClient> mock, Times times) => mock.Verify(x => x.ReceiveAsync(this.timeout), times);
        }

        private sealed class CompleteTestCase : OperationTestCase<bool>
        {
            private readonly Message message;

            public CompleteTestCase(Message message, bool result) : base(result) => this.message = message;

            protected override ISetupSequentialResult<Task<bool>> SetupCore(Mock<ILoRaDeviceClient> mock) => mock.SetupSequence(x => x.CompleteAsync(this.message));
            public override Task InvokeAsync(ILoRaDeviceClient subject) => subject.CompleteAsync(this.message);
            public override void Verify(Mock<ILoRaDeviceClient> mock, Times times) => mock.Verify(x => x.CompleteAsync(this.message), times);
        }

        private sealed class AbandonTestCase : OperationTestCase<bool>
        {
            private readonly Message message;

            public AbandonTestCase(Message message, bool result) : base(result) => this.message = message;

            protected override ISetupSequentialResult<Task<bool>> SetupCore(Mock<ILoRaDeviceClient> mock) => mock.SetupSequence(x => x.AbandonAsync(this.message));
            public override Task InvokeAsync(ILoRaDeviceClient subject) => subject.AbandonAsync(this.message);
            public override void Verify(Mock<ILoRaDeviceClient> mock, Times times) => mock.Verify(x => x.AbandonAsync(this.message), times);
        }

        private sealed class RejectTestCase : OperationTestCase<bool>
        {
            private readonly Message message;

            public RejectTestCase(Message message, bool result) : base(result) => this.message = message;

            protected override ISetupSequentialResult<Task<bool>> SetupCore(Mock<ILoRaDeviceClient> mock) => mock.SetupSequence(x => x.RejectAsync(this.message));
            public override Task InvokeAsync(ILoRaDeviceClient subject) => subject.RejectAsync(this.message);
            public override void Verify(Mock<ILoRaDeviceClient> mock, Times times) => mock.Verify(x => x.RejectAsync(this.message), times);
        }

        private static IEnumerable<IOperationTestCase> OperationTestCases()
        {
            using var message = new Message();
            yield return new GetTwinAsyncTestCase(new Twin());
            yield return new SendEventAsyncTestCase(new LoRaDeviceTelemetry(), new Dictionary<string, string>(), true);
            yield return new UpdateReportedPropertiesTestCase(new TwinCollection(), true);
            yield return new ReceiveTestCase(TimeSpan.FromSeconds(5), message);
            yield return new CompleteTestCase(message, true);
            yield return new AbandonTestCase(message, true);
            yield return new RejectTestCase(message, true);
        }

        public static TheoryData<IOperationTestCase> OperationsTestData() =>
            TheoryDataFactory.From(OperationTestCases());

        public static TheoryData<IOperationTestCase, Exception> ResiliencyTestData() =>
            TheoryDataFactory.From(from re in RetriedExceptions
                                   from tc in OperationTestCases()
                                   select (tc, re));

        [Theory]
        [MemberData(nameof(OperationsTestData))]
        public async Task Successful_Operation(IOperationTestCase testCase)
        {
            testCase.Setup(this.originalMock).Succeed();

            await testCase.InvokeAsync(this.subject);

            testCase.Verify(this.originalMock, Times.Exactly(1));
            this.originalMock.Verify(x => x.EnsureConnected(), Times.Exactly(1));
            this.originalMock.Verify(x => x.DisconnectAsync(It.IsAny<CancellationToken>()), Times.Never);
        }

        [Theory]
        [MemberData(nameof(ResiliencyTestData))]
        public async Task Unsuccessful_Operation_Retries_On_Expected_Errors(IOperationTestCase testCase, Exception exception)
        {
            testCase.Setup(this.originalMock)
                    .Fail(exception)
                    .Fail(exception)
                    .Fail(exception);

            var ex = await Assert.ThrowsAsync(exception.GetType(), () => testCase.InvokeAsync(this.subject));

            Assert.Same(exception, ex);
            testCase.Verify(this.originalMock, Times.Exactly(3));
            this.originalMock.Verify(x => x.EnsureConnected(), Times.Exactly(3));
            this.originalMock.Verify(x => x.DisconnectAsync(CancellationToken.None), Times.Exactly(3));
        }

        [Theory]
        [MemberData(nameof(OperationsTestData))]
        public async Task Unsuccessful_Operation(IOperationTestCase testCase)
        {
            var exception = new InvalidOperationException();
            testCase.Setup(this.originalMock).Fail(exception);

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => testCase.InvokeAsync(this.subject));

            Assert.Same(exception, ex);
            testCase.Verify(this.originalMock, Times.Exactly(1));
            this.originalMock.Verify(x => x.EnsureConnected(), Times.Exactly(1));
            this.originalMock.Verify(x => x.DisconnectAsync(It.IsAny<CancellationToken>()), Times.Never);
        }

        [Theory]
        [MemberData(nameof(ResiliencyTestData))]
        public async Task Unstable_Operation_Retries_On_Expected_Errors(IOperationTestCase testCase, Exception exception)
        {
            testCase.Setup(this.originalMock)
                    .Fail(exception)
                    .Fail(exception)
                    .Succeed();

            await testCase.InvokeAsync(this.subject);

            testCase.Verify(this.originalMock, Times.Exactly(3));
            this.originalMock.Verify(x => x.EnsureConnected(), Times.Exactly(3));
            this.originalMock.Verify(x => x.DisconnectAsync(CancellationToken.None), Times.Exactly(2));
        }
    }
}
