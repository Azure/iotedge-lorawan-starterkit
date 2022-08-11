// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Unit.IoTHubImpl
{
    using System;
    using global::LoRaTools.IoTHubImpl;
    using Microsoft.Azure.Devices.Shared;
    using Moq;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;
    using Xunit;

    public class IoTHubTwinPropertiesTests
    {
        [Fact]
        public void VersionAccessorTest()
        {
            // Arrange
            var mockCollection = new TwinCollection(/*lang=json,strict*/ "{\"$version\":123}");

            var instance = new IoTHubTwinProperties(mockCollection);

            // Act
            var result = instance.Version;

            // Asset
            Assert.Equal(123, result);
        }

        [Fact]
        public void GetLastUpdatedTest()
        {
            // Arrange
            var expected = DateTime.Now;
            var mockCollection = new TwinCollection(JObject.Parse(/*lang=json,strict*/ "{\"$version\":123}"),
                                                    JObject.Parse(/*lang=json,strict*/ $"{{\"$lastUpdated\":\"{expected:o}\",\"$lastUpdatedVersion\":123}}"));

            var instance = new IoTHubTwinProperties(mockCollection);

            // Act
            var result = instance.GetLastUpdated();

            // Asset
            Assert.Equal(expected, result);
        }

        [Fact]
        public void GetMetadataTest()
        {
            // Arrange
            var expected = DateTime.Now;
            var mockCollection = new TwinCollection(JObject.Parse(/*lang=json,strict*/ "{\"$version\":123}"),
                                                    JObject.Parse(/*lang=json,strict*/ $"{{\"$lastUpdated\":\"{expected:o}\",\"$lastUpdatedVersion\":123}}"));

            var instance = new IoTHubTwinProperties(mockCollection);

            // Act
            var result = instance.GetMetadata();

            // Asset
            Assert.Equal(123, result.LastUpdatedVersion);
            Assert.Equal(expected, result.LastUpdated);
        }

        [Theory]
        [InlineData(/*lang=json,strict*/ "{\"searchProperty\": \"aaa\"}", true)]
        [InlineData(/*lang=json,strict*/ "{\"other\": \"aaa\"}", false)]
        public void ContainsKeyTest(string json, bool expected)
        {
            // Arrange
            var mockCollection = new TwinCollection(json);

            var instance = new IoTHubTwinProperties(mockCollection);

            // Act
            var result = instance.ContainsKey("searchProperty");

            // Asset
            Assert.Equal(expected, result);
        }
    }
}
