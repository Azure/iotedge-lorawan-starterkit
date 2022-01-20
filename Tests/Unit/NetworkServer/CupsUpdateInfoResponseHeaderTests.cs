// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Unit.NetworkServer
{
    using System;
    using System.Buffers;
    using System.Linq;
    using LoRaWan.NetworkServer.BasicsStation;
    using Xunit;

    public class CupsUpdateInfoResponseHeaderTests
    {
        private readonly string updateUriString = "https://localhost:1234";

        [Fact]
        public void Serialize_WithNoUpdates()
        {
            var updateResponseHeader = new CupsUpdateInfoResponseHeader
            {
                SignatureKeyCrc = 0,
                UpdateDataLength = 0,
            };

            using var memoryPool = MemoryPool<byte>.Shared.Rent(2048);

            // Act
            var serializedResponse = updateResponseHeader.Serialize(memoryPool.Memory.Span);

            // Assert
            Assert.True(serializedResponse.ToArray().All(b => b == 0));
        }

        [Fact]
        public void Serialize_WithFirmwareUpdates()
        {
            // setting up the twin in such a way that there are only firmware updates
            var signatureBytes = Convert.FromBase64String("ABCD");
            const uint keyCRC = 6789;
            const uint updateDataLength = 100;
            var updateResponseHeader = new CupsUpdateInfoResponseHeader
            {
                UpdateSignature = signatureBytes,
                SignatureKeyCrc = keyCRC,
                UpdateDataLength = updateDataLength,
            };

            using var memoryPool = MemoryPool<byte>.Shared.Rent(2048);

            // Act
            var serializedResponse = updateResponseHeader.Serialize(memoryPool.Memory.Span).GetReader();

            // Assert
            Assert.Equal(0, serializedResponse.Read()); // no cupsUri
            Assert.Equal(0, serializedResponse.Read()); // no tcUri
            Assert.Equal(0, serializedResponse.ReadUInt16LittleEndian()); // no cupsCred
            Assert.Equal(0, serializedResponse.ReadUInt16LittleEndian()); // no tcCred
            Assert.Equal((uint)signatureBytes.Length + 4, serializedResponse.ReadUInt32LittleEndian());
            Assert.Equal(keyCRC, serializedResponse.ReadUInt32LittleEndian());
            Assert.Equal(signatureBytes, serializedResponse.Read(signatureBytes.Length));
            Assert.Equal(updateDataLength, serializedResponse.ReadUInt32LittleEndian());
        }

        [Fact]
        public void Serialize_WithCupsUriUpdates()
        {
            // setting up the twin in such a way that there are only cups uri updates
            var updateResponseHeader = new CupsUpdateInfoResponseHeader
            {
                CupsUrl = new Uri(updateUriString)
            };

            using var memoryPool = MemoryPool<byte>.Shared.Rent(2048);

            // Act
            var serializedResponse = updateResponseHeader.Serialize(memoryPool.Memory.Span).GetReader();

            // Assert
            Assert.Equal(updateUriString.Length, serializedResponse.Read());
            Assert.Equal(updateUriString, serializedResponse.ReadUtf8String(updateUriString.Length));
            Assert.Equal(0, serializedResponse.Read()); //no tcUri
            Assert.Equal(0, serializedResponse.ReadUInt16LittleEndian()); //no cupsCred
            Assert.Equal(0, serializedResponse.ReadUInt16LittleEndian()); //no tcCred
            Assert.Equal(0U, serializedResponse.ReadUInt32LittleEndian()); // no sig + keyCRC
            Assert.Equal(0U, serializedResponse.ReadUInt32LittleEndian()); // no updData
        }

        [Fact]
        public void Serialize_WithTcUriUpdates()
        {
            // setting up the twin in such a way that there are only tc uri updates
            var updateResponseHeader = new CupsUpdateInfoResponseHeader
            {
                LnsUrl = new Uri(updateUriString)
            };

            using var memoryPool = MemoryPool<byte>.Shared.Rent(2048);

            // Act
            var serializedResponse = updateResponseHeader.Serialize(memoryPool.Memory.Span).GetReader();

            // Assert
            Assert.Equal(0, serializedResponse.Read());
            Assert.Equal(updateUriString.Length, serializedResponse.Read());
            Assert.Equal(0, serializedResponse.ReadUInt16LittleEndian()); //no cupsCred
            Assert.Equal(0, serializedResponse.ReadUInt16LittleEndian()); //no tcCred
            Assert.Equal(0U, serializedResponse.ReadUInt32LittleEndian()); // no sig + keyCRC
            Assert.Equal(0U, serializedResponse.ReadUInt32LittleEndian()); // no updData
        }

        [Fact]
        public void Serialize_WithCredentialUpdates()
        {
            // setting up the twin in such a way that there are both tc and cups credential updates
            var credentialBytes = new byte[] { 1, 2, 3 };
            var updateResponseHeader = new CupsUpdateInfoResponseHeader
            {
                CupsCredential = credentialBytes,
                LnsCredential = credentialBytes
            };

            using var memoryPool = MemoryPool<byte>.Shared.Rent(2048);

            // Act
            var serializedResponse = updateResponseHeader.Serialize(memoryPool.Memory.Span).GetReader();

            // Assert
            Assert.Equal(0, serializedResponse.Read()); //no cupsUri
            Assert.Equal(0, serializedResponse.Read()); //no tcUri
            Assert.Equal(credentialBytes.Length, serializedResponse.ReadUInt16LittleEndian()); //cups cred length
            Assert.Equal(credentialBytes, serializedResponse.Read(credentialBytes.Length)); //cups cred
            Assert.Equal(credentialBytes.Length, serializedResponse.ReadUInt16LittleEndian()); //tc cred length
            Assert.Equal(credentialBytes, serializedResponse.Read(credentialBytes.Length)); //tc cred
            Assert.Equal(0U, serializedResponse.ReadUInt32LittleEndian()); // no sig + keyCRC
            Assert.Equal(0U, serializedResponse.ReadUInt32LittleEndian()); // no updData
        }
    }
}
