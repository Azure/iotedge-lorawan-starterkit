// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Unit.FacadeTests
{
    using System;
    using System.Linq;
    using System.Net.Http;
    using System.Text;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using LoraKeysManagerFacade.IoTCentralImp;
    using LoraKeysManagerFacade.IoTCentralImp.Definitions;
    using LoRaWan.Tests.Common;
    using Moq;
    using Moq.Protected;
    using Xunit;

    public class IoTCentralDeviceTwinPageResultTest : FunctionTestBase
    {
        [Fact]
        public async Task IoTCentral_Device_Twin_Pagination_Executed()
        {
            var devices = Enumerable.Range(0, 7)
                                  .Select(c => new Device { Id = NewUniqueEUI64() });

            var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);

            var pageIndex = 0;
            var pageSize = 5;

            handlerMock
               .Protected()
               .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
               .ReturnsAsync((HttpRequestMessage req, CancellationToken token) =>
               {
                   var response = new HttpResponseMessage(System.Net.HttpStatusCode.OK);
                   StringContent content = null;

                   if (req.RequestUri.LocalPath.Equals($"/api/devices", StringComparison.OrdinalIgnoreCase) || req.RequestUri.LocalPath.Equals($"/api/devices/next", StringComparison.OrdinalIgnoreCase))
                   {
                       var pageDevices = devices.Skip(pageIndex * pageSize).Take(pageSize);
                       var result = new DeviceCollectionResult
                       {
                           Value = pageDevices,
                           NextLink = (pageIndex * pageSize) + pageDevices.Count() >= devices.Count() ? null : "/api/devices/next"
                       };

                       content = new StringContent(JsonSerializer.Serialize(result), Encoding.UTF8, "application/json");
                       pageIndex++;
                   }

                   if (req.RequestUri.LocalPath.StartsWith($"/api/devices", StringComparison.OrdinalIgnoreCase) && req.RequestUri.LocalPath.EndsWith($"/properties", StringComparison.OrdinalIgnoreCase))
                   {
                       content = new StringContent(JsonSerializer.Serialize(new DesiredProperties()), Encoding.UTF8, "application/json");
                   }

                   response.Content = content ?? new StringContent(string.Empty);

                   return response;
               })
               .Verifiable();

#pragma warning disable CA2000 // Dispose objects before losing scope
            var mockHttpClient = new HttpClient(handlerMock.Object)
            {
                BaseAddress = new Uri("http://localhost.local/")
            };
#pragma warning restore CA2000 // Dispose objects before losing scope

            var instance = new DeviceTwinPageResult(mockHttpClient, "1.0", c => true);

            Assert.True(instance.HasMoreResults);
            var results = await instance.GetNextPageAsync();
            Assert.Equal(1, pageIndex);
            Assert.Equal(5, results.Count());
            Assert.True(results.All(x => x != null));
            handlerMock.Protected().Verify("SendAsync", Times.Exactly(1), ItExpr.Is<HttpRequestMessage>(c => c.RequestUri.LocalPath == "/api/devices" || c.RequestUri.LocalPath == "/api/devices/next"), ItExpr.IsAny<CancellationToken>());
            handlerMock.Protected().Verify("SendAsync", Times.Exactly(5), ItExpr.Is<HttpRequestMessage>(c => c.RequestUri.LocalPath.EndsWith("/properties", StringComparison.OrdinalIgnoreCase)), ItExpr.IsAny<CancellationToken>());

            Assert.True(instance.HasMoreResults);
            results = await instance.GetNextPageAsync();
            Assert.Equal(2, pageIndex);
            Assert.Equal(2, results.Count());
            Assert.True(results.All(x => x != null));
            handlerMock.Protected().Verify("SendAsync", Times.Exactly(2), ItExpr.Is<HttpRequestMessage>(c => c.RequestUri.LocalPath == "/api/devices" || c.RequestUri.LocalPath == "/api/devices/next"), ItExpr.IsAny<CancellationToken>());
            handlerMock.Protected().Verify("SendAsync", Times.Exactly(7), ItExpr.Is<HttpRequestMessage>(c => c.RequestUri.LocalPath.EndsWith("/properties", StringComparison.OrdinalIgnoreCase)), ItExpr.IsAny<CancellationToken>());

            Assert.False(instance.HasMoreResults);
        }
    }
}
