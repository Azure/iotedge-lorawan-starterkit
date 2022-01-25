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
        private const string UpdateUriString = "https://localhost:1234";

        [Fact]
        public void Serialize_WithNoUpdates()
        {
            var updateResponseHeader = new CupsUpdateInfoResponseHeader
            {
                SignatureKeyCrc = 0,
                UpdateDataLength = 0,
            };

            using var memoryRental = MemoryPool<byte>.Shared.Rent(2048);
            memoryRental.Memory.Span.Fill(0xbd);

            // Act
            var serializedResponse = updateResponseHeader.Serialize(memoryRental.Memory.Span);
            var serializedResponseSpanReader = serializedResponse.GetReader();

            // Assert
            Assert.Equal(14, serializedResponse.Length);
            Assert.True(serializedResponseSpanReader.ReadAll().All(b => b == 0));
            Assert.True(memoryRental.Memory.Span[serializedResponse.Length..].ToArray().All(b => b == 0xbd));

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

            using var memoryRental = MemoryPool<byte>.Shared.Rent(2048);
            memoryRental.Memory.Span.Fill(0xbd);

            // Act
            var serializedResponse = updateResponseHeader.Serialize(memoryRental.Memory.Span);
            var serializedResponseSpanReader = serializedResponse.GetReader();

            // Assert
            Assert.Equal(21, serializedResponse.Length);
            Assert.Equal(0, serializedResponseSpanReader.Read()); // no cupsUri
            Assert.Equal(0, serializedResponseSpanReader.Read()); // no tcUri
            Assert.Equal(0, serializedResponseSpanReader.ReadUInt16LittleEndian()); // no cupsCred
            Assert.Equal(0, serializedResponseSpanReader.ReadUInt16LittleEndian()); // no tcCred
            Assert.Equal((uint)signatureBytes.Length + 4, serializedResponseSpanReader.ReadUInt32LittleEndian());
            Assert.Equal(keyCRC, serializedResponseSpanReader.ReadUInt32LittleEndian());
            Assert.Equal(signatureBytes, serializedResponseSpanReader.Read(signatureBytes.Length));
            Assert.Equal(updateDataLength, serializedResponseSpanReader.ReadUInt32LittleEndian());
            Assert.True(memoryRental.Memory.Span[serializedResponse.Length..].ToArray().All(b => b == 0xbd));
        }

        [Fact]
        public void Serialize_WithCupsUriUpdates()
        {
            // setting up the twin in such a way that there are only cups uri updates
            var updateResponseHeader = new CupsUpdateInfoResponseHeader
            {
                CupsUrl = new Uri(UpdateUriString)
            };

            using var memoryRental = MemoryPool<byte>.Shared.Rent(2048);
            memoryRental.Memory.Span.Fill(0xbd);

            // Act
            var serializedResponse = updateResponseHeader.Serialize(memoryRental.Memory.Span);
            var serializedResponseSpanReader = serializedResponse.GetReader();

            // Assert
            Assert.Equal(36, serializedResponse.Length);
            Assert.Equal(UpdateUriString.Length, serializedResponseSpanReader.Read());
            Assert.Equal(UpdateUriString, serializedResponseSpanReader.ReadUtf8String(UpdateUriString.Length));
            Assert.Equal(0, serializedResponseSpanReader.Read()); //no tcUri
            Assert.Equal(0, serializedResponseSpanReader.ReadUInt16LittleEndian()); //no cupsCred
            Assert.Equal(0, serializedResponseSpanReader.ReadUInt16LittleEndian()); //no tcCred
            Assert.Equal(0U, serializedResponseSpanReader.ReadUInt32LittleEndian()); // no sig + keyCRC
            Assert.Equal(0U, serializedResponseSpanReader.ReadUInt32LittleEndian()); // no updData
            Assert.True(memoryRental.Memory.Span[serializedResponse.Length..].ToArray().All(b => b == 0xbd));
        }

        [Fact]
        public void Serialize_WithTcUriUpdates()
        {
            // setting up the twin in such a way that there are only tc uri updates
            var updateResponseHeader = new CupsUpdateInfoResponseHeader
            {
                LnsUrl = new Uri(UpdateUriString)
            };

            using var memoryRental = MemoryPool<byte>.Shared.Rent(2048);
            memoryRental.Memory.Span.Fill(0xbd);

            // Act
            var serializedResponse = updateResponseHeader.Serialize(memoryRental.Memory.Span);
            var serializedResponseSpanReader = serializedResponse.GetReader();

            // Assert
            Assert.Equal(36, serializedResponse.Length);
            Assert.Equal(0, serializedResponseSpanReader.Read());
            Assert.Equal(UpdateUriString.Length, serializedResponseSpanReader.Read());
            Assert.Equal(UpdateUriString, serializedResponseSpanReader.ReadUtf8String(UpdateUriString.Length));
            Assert.Equal(0, serializedResponseSpanReader.ReadUInt16LittleEndian()); //no cupsCred
            Assert.Equal(0, serializedResponseSpanReader.ReadUInt16LittleEndian()); //no tcCred
            Assert.Equal(0U, serializedResponseSpanReader.ReadUInt32LittleEndian()); // no sig + keyCRC
            Assert.Equal(0U, serializedResponseSpanReader.ReadUInt32LittleEndian()); // no updData
            Assert.True(memoryRental.Memory.Span[serializedResponse.Length..].ToArray().All(b => b == 0xbd));
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

            using var memoryRental = MemoryPool<byte>.Shared.Rent(2048);
            memoryRental.Memory.Span.Fill(0xbd);

            // Act
            var serializedResponse = updateResponseHeader.Serialize(memoryRental.Memory.Span);
            var serializedResponseSpanReader = serializedResponse.GetReader();

            // Assert
            Assert.Equal(20, serializedResponse.Length);
            Assert.Equal(0, serializedResponseSpanReader.Read()); //no cupsUri
            Assert.Equal(0, serializedResponseSpanReader.Read()); //no tcUri
            Assert.Equal(credentialBytes.Length, serializedResponseSpanReader.ReadUInt16LittleEndian()); //cups cred length
            Assert.Equal(credentialBytes, serializedResponseSpanReader.Read(credentialBytes.Length)); //cups cred
            Assert.Equal(credentialBytes.Length, serializedResponseSpanReader.ReadUInt16LittleEndian()); //tc cred length
            Assert.Equal(credentialBytes, serializedResponseSpanReader.Read(credentialBytes.Length)); //tc cred
            Assert.Equal(0U, serializedResponseSpanReader.ReadUInt32LittleEndian()); // no sig + keyCRC
            Assert.Equal(0U, serializedResponseSpanReader.ReadUInt32LittleEndian()); // no updData
            Assert.True(memoryRental.Memory.Span[serializedResponse.Length..].ToArray().All(b => b == 0xbd));
        }
    }
}
