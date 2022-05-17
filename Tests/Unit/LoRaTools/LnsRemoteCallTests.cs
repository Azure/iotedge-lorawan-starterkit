// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

namespace LoRaWan.Tests.Unit.LoRaTools
{
    using System.Text.Json;
    using global::LoRaTools;
    using Xunit;

    public sealed class LnsRemoteCallTests
    {
        [Fact]
        public void Serialization_And_Deserialization_Preserves_Information()
        {
            // arrange
            var subject = new LnsRemoteCall(RemoteCallKind.CloudToDeviceMessage, "somepayload");

            // act
            var result = JsonSerializer.Deserialize<LnsRemoteCall>(JsonSerializer.Serialize(subject));

            // assert
            Assert.Equal(subject, result);
        }
    }
}
