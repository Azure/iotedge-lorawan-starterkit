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
            memoryPool.Memory.Span.Fill(0xbd);

            // Act
            var serializedResponse = updateResponseHeader.Serialize(memoryPool.Memory.Span);
            var serializedResponseSpan = serializedResponse.GetReader();

            // Assert
            Assert.Equal(14, serializedResponse.Length);
            Assert.True(serializedResponseSpan.ReadAll().All(b => b == 0));
            Assert.True(memoryPool.Memory.Span[serializedResponse.Length..].ToArray().All(b => b == 0xbd));

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
            memoryPool.Memory.Span.Fill(0xbd);

            // Act
            var serializedResponse = updateResponseHeader.Serialize(memoryPool.Memory.Span);
            var serializedResponseSpan = serializedResponse.GetReader();

            // Assert
            Assert.Equal(21, serializedResponse.Length);
            Assert.Equal(0, serializedResponseSpan.Read()); // no cupsUri
            Assert.Equal(0, serializedResponseSpan.Read()); // no tcUri
            Assert.Equal(0, serializedResponseSpan.ReadUInt16LittleEndian()); // no cupsCred
            Assert.Equal(0, serializedResponseSpan.ReadUInt16LittleEndian()); // no tcCred
            Assert.Equal((uint)signatureBytes.Length + 4, serializedResponseSpan.ReadUInt32LittleEndian());
            Assert.Equal(keyCRC, serializedResponseSpan.ReadUInt32LittleEndian());
            Assert.Equal(signatureBytes, serializedResponseSpan.Read(signatureBytes.Length));
            Assert.Equal(updateDataLength, serializedResponseSpan.ReadUInt32LittleEndian());
            Assert.True(memoryPool.Memory.Span[serializedResponse.Length..].ToArray().All(b => b == 0xbd));
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
            memoryPool.Memory.Span.Fill(0xbd);

            // Act
            var serializedResponse = updateResponseHeader.Serialize(memoryPool.Memory.Span);
            var serializedResponseSpan = serializedResponse.GetReader();

            // Assert
            Assert.Equal(36, serializedResponse.Length);
            Assert.Equal(updateUriString.Length, serializedResponseSpan.Read());
            Assert.Equal(updateUriString, serializedResponseSpan.ReadUtf8String(updateUriString.Length));
            Assert.Equal(0, serializedResponseSpan.Read()); //no tcUri
            Assert.Equal(0, serializedResponseSpan.ReadUInt16LittleEndian()); //no cupsCred
            Assert.Equal(0, serializedResponseSpan.ReadUInt16LittleEndian()); //no tcCred
            Assert.Equal(0U, serializedResponseSpan.ReadUInt32LittleEndian()); // no sig + keyCRC
            Assert.Equal(0U, serializedResponseSpan.ReadUInt32LittleEndian()); // no updData
            Assert.True(memoryPool.Memory.Span[serializedResponse.Length..].ToArray().All(b => b == 0xbd));
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
            memoryPool.Memory.Span.Fill(0xbd);

            // Act
            var serializedResponse = updateResponseHeader.Serialize(memoryPool.Memory.Span);
            var serializedResponseSpan = serializedResponse.GetReader();

            // Assert
            Assert.Equal(36, serializedResponse.Length);
            Assert.Equal(0, serializedResponseSpan.Read());
            Assert.Equal(updateUriString.Length, serializedResponseSpan.Read());
            Assert.Equal(updateUriString, serializedResponseSpan.ReadUtf8String(updateUriString.Length));
            Assert.Equal(0, serializedResponseSpan.ReadUInt16LittleEndian()); //no cupsCred
            Assert.Equal(0, serializedResponseSpan.ReadUInt16LittleEndian()); //no tcCred
            Assert.Equal(0U, serializedResponseSpan.ReadUInt32LittleEndian()); // no sig + keyCRC
            Assert.Equal(0U, serializedResponseSpan.ReadUInt32LittleEndian()); // no updData
            Assert.True(memoryPool.Memory.Span[serializedResponse.Length..].ToArray().All(b => b == 0xbd));
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
            memoryPool.Memory.Span.Fill(0xbd);

            // Act
            var serializedResponse = updateResponseHeader.Serialize(memoryPool.Memory.Span);
            var serializedResponseSpan = serializedResponse.GetReader();

            // Assert
            Assert.Equal(20, serializedResponse.Length);
            Assert.Equal(0, serializedResponseSpan.Read()); //no cupsUri
            Assert.Equal(0, serializedResponseSpan.Read()); //no tcUri
            Assert.Equal(credentialBytes.Length, serializedResponseSpan.ReadUInt16LittleEndian()); //cups cred length
            Assert.Equal(credentialBytes, serializedResponseSpan.Read(credentialBytes.Length)); //cups cred
            Assert.Equal(credentialBytes.Length, serializedResponseSpan.ReadUInt16LittleEndian()); //tc cred length
            Assert.Equal(credentialBytes, serializedResponseSpan.Read(credentialBytes.Length)); //tc cred
            Assert.Equal(0U, serializedResponseSpan.ReadUInt32LittleEndian()); // no sig + keyCRC
            Assert.Equal(0U, serializedResponseSpan.ReadUInt32LittleEndian()); // no updData
            Assert.True(memoryPool.Memory.Span[serializedResponse.Length..].ToArray().All(b => b == 0xbd));
        }
    }
}
