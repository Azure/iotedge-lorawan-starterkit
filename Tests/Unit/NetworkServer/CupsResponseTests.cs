// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Unit.NetworkServer
{
    using System;
    using System.Buffers;
    using System.Buffers.Binary;
    using System.Collections.Immutable;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using DotNetty.Common.Utilities;
    using global::LoRaTools.CommonAPI;
    using LoRaWan.NetworkServer.BasicsStation;
    using Xunit;

    public class CupsResponseTests : IAsyncDisposable
    {
        private readonly CupsUpdateInfoRequest cupsRequest;
        private readonly Stream fwUpgradeStream;
        private readonly Func<StationEui, ConcentratorCredentialType, CancellationToken, Task<string>> credentialFetcher;
        private readonly Func<StationEui, CancellationToken, Task<(long?, Stream)>> fwUpgradeFetcher;
        private readonly byte[] FwUpgradeBytes = new byte[] { 1, 2, 3, 4, 5 };
        private readonly byte[] CredentialBytes = new byte[] { 10, 11, 12, 13, 14 };

        public CupsResponseTests()
        {
            this.cupsRequest = new CupsUpdateInfoRequest(StationEui.Parse("aaaa:bbff:fecc:dddd"),
                                                         new Uri("https://localhost:5002"),
                                                         new Uri("wss://localhost:5001"),
                                                         12345,
                                                         12345,
                                                         "1.0.0",
                                                         new[] { 6789U }.ToImmutableArray());
            this.fwUpgradeStream = new MemoryStream(this.FwUpgradeBytes);
            this.credentialFetcher = (eui, type, token) => Task.FromResult(Convert.ToBase64String(this.CredentialBytes));
            this.fwUpgradeFetcher = (eui, token) => Task.FromResult(((long?)FwUpgradeBytes.Length, this.fwUpgradeStream));
        }

        [Fact]
        public async Task Serialize_WithNoUpdates()
        {
            // setting up the twin in such a way that there are no updates
            var cupsTwinInfo = new CupsTwinInfo(this.cupsRequest.CupsUri,
                                                this.cupsRequest.TcUri,
                                                this.cupsRequest.CupsCredentialsChecksum,
                                                this.cupsRequest.TcCredentialsChecksum,
                                                string.Empty,
                                                string.Empty,
                                                this.cupsRequest.Package,
                                                this.cupsRequest.KeyChecksums.FirstOrDefault(),
                                                string.Empty);

            using var memoryPool = MemoryPool<byte>.Shared.Rent(2048);

            // Act
            var (r, _, _) = await new CupsResponse(this.cupsRequest, cupsTwinInfo, this.credentialFetcher, this.fwUpgradeFetcher).SerializeAsync(memoryPool.Memory, CancellationToken.None);

            // Assert
            Assert.True(r.ToArray().All(b => b == 0));
        }

        [Fact]
        public async Task Serialize_WithFirmwareUpdates()
        {
            // setting up the twin in such a way that there are only firmware updates
            var signature = "ABCD";
            var signatureBytes = Convert.FromBase64String(signature);
            var cupsTwinInfo = new CupsTwinInfo(this.cupsRequest.CupsUri,
                                                this.cupsRequest.TcUri,
                                                this.cupsRequest.CupsCredentialsChecksum,
                                                this.cupsRequest.TcCredentialsChecksum,
                                                string.Empty,
                                                string.Empty,
                                                "another",
                                                this.cupsRequest.KeyChecksums.FirstOrDefault(),
                                                signature);

            using var memoryPool = MemoryPool<byte>.Shared.Rent(2048);

            // Act
            var (r, fwl, fwb) = await new CupsResponse(this.cupsRequest, cupsTwinInfo, this.credentialFetcher, this.fwUpgradeFetcher).SerializeAsync(memoryPool.Memory, CancellationToken.None);

            var responseBytes = r.ToArray();
            // Assert
            Assert.Equal(signatureBytes.Length + 4, BinaryPrimitives.ReadInt32LittleEndian(responseBytes.AsSpan()[6..10]));
            using var tempPool = MemoryPool<byte>.Shared.Rent(100);
            BinaryPrimitives.WriteUInt32LittleEndian(tempPool.Memory.Span, this.cupsRequest.KeyChecksums.FirstOrDefault());
            Assert.Equal(tempPool.Memory.Span[..3].ToArray(), responseBytes[10..13]);
            Assert.Equal(signatureBytes, responseBytes[14..(14 + signatureBytes.Length)]);
            var fwLengthFieldStart = 14 + signatureBytes.Length;
            Assert.Equal(this.FwUpgradeBytes.Length, BinaryPrimitives.ReadInt32LittleEndian(responseBytes.AsSpan()[fwLengthFieldStart..(fwLengthFieldStart+4)]));
            Assert.Equal(this.FwUpgradeBytes.Length, fwl);
            var fwBytes = new byte[this.FwUpgradeBytes.Length];
            await fwb.WriteAsync(fwBytes);
            Assert.Equal(this.FwUpgradeBytes, fwBytes);
        }

        [Fact]
        public async Task Serialize_WithFirmwareUpdates_Fails_If_Missing_Checksum()
        {
            // setting up the twin in such a way that there are only firmware updates
            var signature = "ABCD";
            var cupsTwinInfo = new CupsTwinInfo(this.cupsRequest.CupsUri,
                                                this.cupsRequest.TcUri,
                                                this.cupsRequest.CupsCredentialsChecksum,
                                                this.cupsRequest.TcCredentialsChecksum,
                                                string.Empty,
                                                string.Empty,
                                                "another",
                                                0,
                                                signature);

            using var memoryPool = MemoryPool<byte>.Shared.Rent(2048);

            // Act
            await Assert.ThrowsAsync<InvalidOperationException>(async () => await new CupsResponse(this.cupsRequest, cupsTwinInfo, this.credentialFetcher, this.fwUpgradeFetcher).SerializeAsync(memoryPool.Memory, CancellationToken.None));
        }


        [Theory]
        [InlineData(0, false)]
        [InlineData(null, false)]
        [InlineData(long.MaxValue, false)]
        [InlineData(5, true)]
        public async Task Serialize_WithFirmwareUpdates_Fails_If_Invalid_Firmware(long? length, bool invalidStream)
        {
            // setting up the twin in such a way that there are only firmware updates
            var signature = "ABCD";
            var cupsTwinInfo = new CupsTwinInfo(this.cupsRequest.CupsUri,
                                                this.cupsRequest.TcUri,
                                                this.cupsRequest.CupsCredentialsChecksum,
                                                this.cupsRequest.TcCredentialsChecksum,
                                                string.Empty,
                                                string.Empty,
                                                "another",
                                                this.cupsRequest.KeyChecksums.FirstOrDefault(),
                                                signature);

            using var memoryPool = MemoryPool<byte>.Shared.Rent(2048);
            using var resp = (Stream)new MemoryStream(this.FwUpgradeBytes);
            Task<(long?, Stream)> InvalidLengthFirmwareUpdate(StationEui eui, CancellationToken token)
            {
                return Task.FromResult((length, invalidStream ? null : resp));
            }

            // Act
            await Assert.ThrowsAsync<InvalidOperationException>(async () => await new CupsResponse(this.cupsRequest, cupsTwinInfo, this.credentialFetcher, InvalidLengthFirmwareUpdate).SerializeAsync(memoryPool.Memory, CancellationToken.None));
        }

        [Fact]
        public async Task Serialize_WithCupsUriUpdates()
        {
            // setting up the twin in such a way that there are cups uri updates
            var anotherCupsUri = "https://anotheruri:5002";
            var cupsTwinInfo = new CupsTwinInfo(new Uri(anotherCupsUri),
                                                this.cupsRequest.TcUri,
                                                this.cupsRequest.CupsCredentialsChecksum,
                                                this.cupsRequest.TcCredentialsChecksum,
                                                string.Empty,
                                                string.Empty,
                                                this.cupsRequest.Package,
                                                this.cupsRequest.KeyChecksums.FirstOrDefault(),
                                                string.Empty);
            using var memoryPool = MemoryPool<byte>.Shared.Rent(2048);

            // Act
            var (r, fwl, fwb) = await new CupsResponse(this.cupsRequest, cupsTwinInfo, this.credentialFetcher, this.fwUpgradeFetcher).SerializeAsync(memoryPool.Memory, CancellationToken.None);

            var responseBytes = r.ToArray();

            // Assert
            Assert.Equal(anotherCupsUri.Length, responseBytes[0]);
            Assert.Equal(anotherCupsUri, Encoding.UTF8.GetString(responseBytes.Slice(1, anotherCupsUri.Length)));
            // asserting all other bytes are 0 as there are no further updates
            Assert.True(responseBytes[(anotherCupsUri.Length + 1)..].All(b => b == 0));
            Assert.Equal(0, fwl);
            Assert.Null(fwb);
        }


        [Fact]
        public async Task Serialize_WithTcUriUpdates()
        {
            // setting up the twin in such a way that there are tc uri updates
            var anotherTcUri = "wss://anotheruri:5001";
            var cupsTwinInfo = new CupsTwinInfo(this.cupsRequest.CupsUri,
                                                new Uri(anotherTcUri),
                                                this.cupsRequest.CupsCredentialsChecksum,
                                                this.cupsRequest.TcCredentialsChecksum,
                                                string.Empty,
                                                string.Empty,
                                                this.cupsRequest.Package,
                                                this.cupsRequest.KeyChecksums.FirstOrDefault(),
                                                string.Empty);
            using var memoryPool = MemoryPool<byte>.Shared.Rent(2048);

            // Act
            var (r, fwl, fwb) = await new CupsResponse(this.cupsRequest, cupsTwinInfo, this.credentialFetcher, this.fwUpgradeFetcher).SerializeAsync(memoryPool.Memory, CancellationToken.None);
            var responseBytes = r.ToArray();

            // Assert
            Assert.Equal(anotherTcUri.Length, responseBytes[1]);
            Assert.Equal(anotherTcUri, Encoding.UTF8.GetString(responseBytes.Slice(2, anotherTcUri.Length)));
            // asserting other bytes are 0 as there are no further updates
            Assert.True(responseBytes[0] == 0);
            Assert.True(responseBytes[(anotherTcUri.Length + 2)..].All(b => b == 0));
            Assert.Equal(0, fwl);
            Assert.Null(fwb);
        }

        [Fact]
        public async Task Serialize_WithCredentialUpdates()
        {
            // setting up the twin in such a way that there are credentials updates
            uint anotherChecksum = 56789;
            var cupsTwinInfo = new CupsTwinInfo(this.cupsRequest.CupsUri,
                                                this.cupsRequest.TcUri,
                                                anotherChecksum,
                                                anotherChecksum,
                                                string.Empty,
                                                string.Empty,
                                                this.cupsRequest.Package,
                                                this.cupsRequest.KeyChecksums.FirstOrDefault(),
                                                string.Empty);

            using var memoryPool = MemoryPool<byte>.Shared.Rent(2048);

            // Act
            var (r, fwl, fwb) = await new CupsResponse(this.cupsRequest, cupsTwinInfo, this.credentialFetcher, this.fwUpgradeFetcher).SerializeAsync(memoryPool.Memory, CancellationToken.None);
            var responseBytes = r.ToArray();

            // Assert
            // Cups Credentials
            // responseBytes[0] is 0 because no cups uri updates
            // responseBytes[1] is 0 because no tc uri updates
            // responseBytes[2] and responseBytes[3] contain the int16 in little endian for cups credential length
            // responseBytes[4..4+CredentialsLength-1] contain the cups credential bytes
            Assert.Equal(this.CredentialBytes.Length, BinaryPrimitives.ReadInt16LittleEndian(responseBytes.Slice(2, 2)));
            Assert.Equal(this.CredentialBytes, responseBytes.Slice(4, this.CredentialBytes.Length));
            // Tc Credentials
            // responseBytes[4+CredentialsLength] and responseBytes[4+CredentialsLength+1] contain the int16 in little endian for tc credential length
            // responseBytes[4+CredentialsLength+2..4+CredentialsLength+2-1] contain the tc credential bytes
            Assert.Equal(this.CredentialBytes.Length, BinaryPrimitives.ReadInt16LittleEndian(responseBytes.Slice(4 + this.CredentialBytes.Length, 2)));
            Assert.Equal(this.CredentialBytes, responseBytes.Slice(4 + this.CredentialBytes.Length + 2, this.CredentialBytes.Length));

            // asserting other fields are 0 as there are no further updates
            Assert.True(responseBytes[0] == 0);
            Assert.True(responseBytes[1] == 0);
            Assert.True(responseBytes[((2 * this.CredentialBytes.Length) + 6)..].All(b => b == 0));
            Assert.Equal(0, fwl);
            Assert.Null(fwb);
        }

        public async ValueTask DisposeAsync()
        {
            await fwUpgradeStream.DisposeAsync();

            GC.SuppressFinalize(this);
        }
    }
}
