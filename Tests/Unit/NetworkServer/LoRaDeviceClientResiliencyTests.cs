// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

namespace LoRaWan.Tests.Unit.NetworkServer
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
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

        private record struct LogEntry(string Message, Exception? Exception);

        public LoRaDeviceClientResiliencyTests()
        {
            this.originalMock = new Mock<ILoRaDeviceClient>();
            var loggerFactoryMock = new Mock<ILoggerFactory>();
            loggerFactoryMock.Setup(x => x.CreateLogger(It.IsAny<string>())).Returns(this.loggerMock.Object);
            this.subject = this.originalMock.Object.AddResiliency(loggerFactoryMock.Object);
        }

        private IEnumerable<LogEntry> LogEntries =>
            from e in this.loggerMock.GetLogInvocations()
            where e.LogLevel is LogLevel.Debug
            select new LogEntry(e.Message, e.Exception);

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
        [MemberData(nameof(GetRetriedExceptionsTestData))]
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

        private static IEnumerable<Exception> GetRetriedExceptions() => new[]
        {
            new InvalidOperationException("This operation is only allowed using a successfully authenticated context. " + "" +
                                          "This sentence in the error message shouldn't matter."),
            new ObjectDisposedException("<object>")
        };

        public static TheoryData<Exception> GetRetriedExceptionsTestData() => TheoryDataFactory.From(GetRetriedExceptions());

        [Theory]
        [MemberData(nameof(GetRetriedExceptionsTestData))]
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
        [MemberData(nameof(GetRetriedExceptionsTestData))]
        public async Task DisposeAsync_Is_Not_Resilient(Exception exception)
        {
            this.originalMock.Setup(x => x.DisposeAsync()).Throws(exception);

            var ex = await Assert.ThrowsAsync(exception.GetType(), () => this.subject.DisposeAsync().AsTask());

            Assert.Same(exception, ex);
            this.originalMock.Verify(x => x.DisposeAsync(), Times.Once);
        }

        public interface IOperationTestHelper
        {
            string Name { get; }
            IOperationSequentialResultSetup SetupSequence(Mock<ILoRaDeviceClient> mock);
            Task InvokeAsync(ILoRaDeviceClient subject);
            void Verify(Mock<ILoRaDeviceClient> mock, Times times);
        }

        public interface IOperationSequentialResultSetup
        {
            IOperationSequentialResultSetup Succeed();
            IOperationSequentialResultSetup Fail(Exception exception);
        }

        private abstract class OperationTestHelper<T> : IOperationTestHelper, IOperationSequentialResultSetup, IDisposable
        {
            private readonly T result;
            private readonly CancellationTokenSource cancellationTokenSource = new();
            private ISetupSequentialResult<Task<T>>? setupSequentialResult;

            protected OperationTestHelper(T result) => this.result = result;

            public virtual string Name => GetType().Name[..^"TestHelper".Length];

            protected CancellationToken CancellationToken => this.cancellationTokenSource.Token;

            private ISetupSequentialResult<Task<T>> SetupSequentialResult => this.setupSequentialResult ?? throw new InvalidOperationException();

            protected abstract ISetupSequentialResult<Task<T>> SetupSequenceCore(Mock<ILoRaDeviceClient> mock);

            public virtual IOperationSequentialResultSetup SetupSequence(Mock<ILoRaDeviceClient> mock)
            {
                this.setupSequentialResult = SetupSequenceCore(mock);
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

        private sealed class GetTwinAsyncTestHelper : OperationTestHelper<Twin>
        {
            public GetTwinAsyncTestHelper(Twin result) : base(result) { }

            protected override ISetupSequentialResult<Task<Twin>> SetupSequenceCore(Mock<ILoRaDeviceClient> mock) => mock.SetupSequence(x => x.GetTwinAsync(CancellationToken));
            public override Task InvokeAsync(ILoRaDeviceClient subject) => subject.GetTwinAsync(CancellationToken);
            public override void Verify(Mock<ILoRaDeviceClient> mock, Times times) => mock.Verify(x => x.GetTwinAsync(CancellationToken), times);
        }

        private sealed class SendEventAsyncTestHelper : OperationTestHelper<bool>
        {
            private readonly LoRaDeviceTelemetry telemetry;
            private readonly Dictionary<string, string> properties;

            public SendEventAsyncTestHelper(LoRaDeviceTelemetry telemetry, Dictionary<string, string> properties, bool result) : base(result)
            {
                this.telemetry = telemetry;
                this.properties = properties;
            }

            protected override ISetupSequentialResult<Task<bool>> SetupSequenceCore(Mock<ILoRaDeviceClient> mock) => mock.SetupSequence(x => x.SendEventAsync(this.telemetry, this.properties));
            public override Task InvokeAsync(ILoRaDeviceClient subject) => subject.SendEventAsync(this.telemetry, this.properties);
            public override void Verify(Mock<ILoRaDeviceClient> mock, Times times) => mock.Verify(x => x.SendEventAsync(this.telemetry, this.properties), times);
        }

        private sealed class UpdateReportedPropertiesAsyncTestHelper : OperationTestHelper<bool>
        {
            private readonly TwinCollection twinCollection;

            public UpdateReportedPropertiesAsyncTestHelper(TwinCollection twinCollection, bool result) : base(result) =>
                this.twinCollection = twinCollection;

            protected override ISetupSequentialResult<Task<bool>> SetupSequenceCore(Mock<ILoRaDeviceClient> mock) => mock.SetupSequence(x => x.UpdateReportedPropertiesAsync(this.twinCollection, CancellationToken));
            public override Task InvokeAsync(ILoRaDeviceClient subject) => subject.UpdateReportedPropertiesAsync(this.twinCollection, CancellationToken);
            public override void Verify(Mock<ILoRaDeviceClient> mock, Times times) => mock.Verify(x => x.UpdateReportedPropertiesAsync(this.twinCollection, CancellationToken), times);
        }

        private sealed class ReceiveAsyncTestHelper : OperationTestHelper<Message>
        {
            private readonly TimeSpan timeout;

            public ReceiveAsyncTestHelper(TimeSpan timeout, Message result) : base(result) =>
                this.timeout = timeout;

            protected override ISetupSequentialResult<Task<Message>> SetupSequenceCore(Mock<ILoRaDeviceClient> mock) => mock.SetupSequence(x => x.ReceiveAsync(this.timeout));
            public override Task InvokeAsync(ILoRaDeviceClient subject) => subject.ReceiveAsync(this.timeout);
            public override void Verify(Mock<ILoRaDeviceClient> mock, Times times) => mock.Verify(x => x.ReceiveAsync(this.timeout), times);
        }

        private sealed class CompleteAsyncTestHelper : OperationTestHelper<bool>
        {
            private readonly Message message;

            public CompleteAsyncTestHelper(Message message, bool result) : base(result) => this.message = message;

            protected override ISetupSequentialResult<Task<bool>> SetupSequenceCore(Mock<ILoRaDeviceClient> mock) => mock.SetupSequence(x => x.CompleteAsync(this.message));
            public override Task InvokeAsync(ILoRaDeviceClient subject) => subject.CompleteAsync(this.message);
            public override void Verify(Mock<ILoRaDeviceClient> mock, Times times) => mock.Verify(x => x.CompleteAsync(this.message), times);
        }

        private sealed class AbandonAsyncTestHelper : OperationTestHelper<bool>
        {
            private readonly Message message;

            public AbandonAsyncTestHelper(Message message, bool result) : base(result) => this.message = message;

            protected override ISetupSequentialResult<Task<bool>> SetupSequenceCore(Mock<ILoRaDeviceClient> mock) => mock.SetupSequence(x => x.AbandonAsync(this.message));
            public override Task InvokeAsync(ILoRaDeviceClient subject) => subject.AbandonAsync(this.message);
            public override void Verify(Mock<ILoRaDeviceClient> mock, Times times) => mock.Verify(x => x.AbandonAsync(this.message), times);
        }

        private sealed class RejectAsyncTestHelper : OperationTestHelper<bool>
        {
            private readonly Message message;

            public RejectAsyncTestHelper(Message message, bool result) : base(result) => this.message = message;

            protected override ISetupSequentialResult<Task<bool>> SetupSequenceCore(Mock<ILoRaDeviceClient> mock) => mock.SetupSequence(x => x.RejectAsync(this.message));
            public override Task InvokeAsync(ILoRaDeviceClient subject) => subject.RejectAsync(this.message);
            public override void Verify(Mock<ILoRaDeviceClient> mock, Times times) => mock.Verify(x => x.RejectAsync(this.message), times);
        }

        private static IEnumerable<IOperationTestHelper> GetOperationTestHelpers()
        {
            using var message = new Message();
            yield return new GetTwinAsyncTestHelper(new Twin());
            yield return new SendEventAsyncTestHelper(new LoRaDeviceTelemetry(), new Dictionary<string, string>(), true);
            yield return new UpdateReportedPropertiesAsyncTestHelper(new TwinCollection(), true);
            yield return new ReceiveAsyncTestHelper(TimeSpan.FromSeconds(5), message);
            yield return new CompleteAsyncTestHelper(message, true);
            yield return new AbandonAsyncTestHelper(message, true);
            yield return new RejectAsyncTestHelper(message, true);
        }

        public static TheoryData<IOperationTestHelper> GetOperationsTestData() =>
            TheoryDataFactory.From(GetOperationTestHelpers());

        public static TheoryData<IOperationTestHelper, Exception> GetResiliencyTestData() =>
            TheoryDataFactory.From(from tc in GetOperationTestHelpers()
                                   from re in GetRetriedExceptions()
                                   select (tc, re));

        [Theory]
        [MemberData(nameof(GetOperationsTestData))]
        public async Task Operation_Is_Not_Retried_When_Successful(IOperationTestHelper operation)
        {
            operation.SetupSequence(this.originalMock).Succeed();

            await operation.InvokeAsync(this.subject);

            operation.Verify(this.originalMock, Times.Exactly(1));
            this.originalMock.Verify(x => x.EnsureConnected(), Times.Exactly(1));
            this.originalMock.Verify(x => x.DisconnectAsync(It.IsAny<CancellationToken>()), Times.Never);
            Assert.Empty(LogEntries);
        }

        [Theory]
        [MemberData(nameof(GetResiliencyTestData))]
        public async Task Operation_Is_Retried_On_Expected_Errors_Until_Retry_Limit_Is_Exhausted(IOperationTestHelper operation, Exception exception)
        {
            operation.SetupSequence(this.originalMock)
                     .Fail(exception)
                     .Fail(exception)
                     .Fail(exception);

            var ex = await Assert.ThrowsAsync(exception.GetType(), () => operation.InvokeAsync(this.subject));

            Assert.Same(exception, ex);
            operation.Verify(this.originalMock, Times.Exactly(3));
            this.originalMock.Verify(x => x.EnsureConnected(), Times.Exactly(3));
            this.originalMock.Verify(x => x.DisconnectAsync(CancellationToken.None), Times.Exactly(3));
            Assert.Equal(CreateExpectedLog(3, operation.Name, exception), LogEntries);
        }

        [Theory]
        [MemberData(nameof(GetOperationsTestData))]
        public async Task Operation_Is_Not_Retried_On_Unexpected_Errors(IOperationTestHelper operation)
        {
            var exception = new InvalidOperationException();
            operation.SetupSequence(this.originalMock).Fail(exception);

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => operation.InvokeAsync(this.subject));

            Assert.Same(exception, ex);
            operation.Verify(this.originalMock, Times.Exactly(1));
            this.originalMock.Verify(x => x.EnsureConnected(), Times.Exactly(1));
            this.originalMock.Verify(x => x.DisconnectAsync(It.IsAny<CancellationToken>()), Times.Never);
            Assert.Empty(LogEntries);
        }

        [Theory]
        [MemberData(nameof(GetResiliencyTestData))]
        public async Task Operation_Succeeds_Eventually_If_Retried_Within_Retry_Limit(IOperationTestHelper operation, Exception exception)
        {
            operation.SetupSequence(this.originalMock)
                     .Fail(exception)
                     .Fail(exception)
                     .Succeed();

            await operation.InvokeAsync(this.subject);

            operation.Verify(this.originalMock, Times.Exactly(3));
            this.originalMock.Verify(x => x.EnsureConnected(), Times.Exactly(3));
            this.originalMock.Verify(x => x.DisconnectAsync(CancellationToken.None), Times.Exactly(2));
            Assert.Equal(CreateExpectedLog(2, operation.Name, exception), LogEntries);
        }

        private static IEnumerable<LogEntry> CreateExpectedLog(int count, string name, Exception exception) =>
            from a in Enumerable.Range(1, count)
            select a.ToString(CultureInfo.InvariantCulture) into a
            select new LogEntry($@"Device client operation ""{name}"" (attempt {a}/3) failed due to error: {exception.Message}", exception);
    }
}
