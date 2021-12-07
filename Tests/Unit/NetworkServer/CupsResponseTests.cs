// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Unit.NetworkServer
{
    using System;
    using System.Buffers.Binary;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using DotNetty.Common.Utilities;
    using global::LoRaTools.CommonAPI;
    using LoRaWan.NetworkServer.BasicsStation;
    using Xunit;

    public class CupsResponseTests
    {
        private readonly CupsUpdateInfoRequest cupsRequest;
        private readonly Func<StationEui, ConcentratorCredentialType, CancellationToken, Task<string>> credentialFetcher;
        private readonly byte[] CredentialBytes = new byte[] { 10, 11, 12, 13, 14 };

        public CupsResponseTests()
        {
            this.cupsRequest = new CupsUpdateInfoRequest(StationEui.Parse("aaaa:bbff:fecc:dddd"),
                                                         new Uri("https://localhost:5002"),
                                                         new Uri("wss://localhost:5001"),
                                                         12345,
                                                         12345);
            this.credentialFetcher = (eui, type, token) => Task.FromResult(Convert.ToBase64String(this.CredentialBytes));
        }

        [Fact]
        public async Task Serialize_WithNoUpdates()
        {
            // setting up the twin in such a way that there are no updates
            var cupsTwinInfo = new CupsTwinInfo(this.cupsRequest.CupsUri,
                                                this.cupsRequest.TcUri,
                                                this.cupsRequest.CupsCredentialsChecksum,
                                                this.cupsRequest.TcCredentialsChecksum);

            // Act
            var responseBytes = await new CupsResponse(this.cupsRequest, cupsTwinInfo, this.credentialFetcher).SerializeAsync(CancellationToken.None);

            // Assert
            Assert.True(responseBytes.All(b => b == 0));
        }

        [Fact]
        public async Task Serialize_WithCupsUriUpdates()
        {
            // setting up the twin in such a way that there are cups uri updates
            var anotherCupsUri = "https://anotheruri:5002";
            var cupsTwinInfo = new CupsTwinInfo(new Uri(anotherCupsUri),
                                                this.cupsRequest.TcUri,
                                                this.cupsRequest.CupsCredentialsChecksum,
                                                this.cupsRequest.TcCredentialsChecksum);

            // Act
            var responseBytes = await new CupsResponse(this.cupsRequest, cupsTwinInfo, this.credentialFetcher).SerializeAsync(CancellationToken.None);

            // Assert
            Assert.Equal(anotherCupsUri.Length, responseBytes[0]);
            Assert.Equal(anotherCupsUri, Encoding.UTF8.GetString(responseBytes.Slice(1, anotherCupsUri.Length)));
            // asserting all other bytes are 0 as there are no further updates
            Assert.True(responseBytes[(anotherCupsUri.Length + 1)..].All(b => b == 0));
        }


        [Fact]
        public async Task Serialize_WithTcUriUpdates()
        {
            // setting up the twin in such a way that there are tc uri updates
            var anotherTcUri = "wss://anotheruri:5001";
            var cupsTwinInfo = new CupsTwinInfo(this.cupsRequest.CupsUri,
                                                new Uri(anotherTcUri),
                                                this.cupsRequest.CupsCredentialsChecksum,
                                                this.cupsRequest.TcCredentialsChecksum);

            // Act
            var responseBytes = await new CupsResponse(this.cupsRequest, cupsTwinInfo, this.credentialFetcher).SerializeAsync(CancellationToken.None);

            // Assert
            Assert.Equal(anotherTcUri.Length, responseBytes[1]);
            Assert.Equal(anotherTcUri, Encoding.UTF8.GetString(responseBytes.Slice(2, anotherTcUri.Length)));
            // asserting other bytes are 0 as there are no further updates
            Assert.True(responseBytes[0] == 0);
            Assert.True(responseBytes[(anotherTcUri.Length + 2)..].All(b => b == 0));
        }

        [Fact]
        public async Task Serialize_WithCredentialUpdates()
        {
            // setting up the twin in such a way that there are credentials updates
            uint anotherChecksum = 56789;
            var cupsTwinInfo = new CupsTwinInfo(this.cupsRequest.CupsUri,
                                                this.cupsRequest.TcUri,
                                                anotherChecksum,
                                                anotherChecksum);

            // Act
            var responseBytes = await new CupsResponse(this.cupsRequest, cupsTwinInfo, this.credentialFetcher).SerializeAsync(CancellationToken.None);

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
        }
    }
}
