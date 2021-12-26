// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Unit.FacadeTests
{
    using System;
    using System.Linq;
    using System.Net.Http;
    using System.Security.Cryptography;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using global::LoraKeysManagerFacade.IoTCentralImp;
    using global::LoraKeysManagerFacade.IoTCentralImp.Definitions;
    using LoRaWan.Tests.Common;
    using Moq;
    using Moq.Protected;
    using Xunit;

    public class IoTCentralDeviceTwinPageResultTest : FunctionTestBase
    {
        private static DevAddr CreateDevAddr() => new DevAddr((uint)RandomNumberGenerator.GetInt32(int.MaxValue));

        [Fact]
        public async Task IoTCentral_Device_Twin_Pagination_Executed()
        {
            var deviceTemplateInfos = new DeviceTemplateInfo[] {
                new DeviceTemplateInfo
                {
                    ComponentName = "LoRa",
                    DeviceTempalteId = Guid.NewGuid().ToString()
                },
                new DeviceTemplateInfo
                {
                    ComponentName = "LoRa",
                    DeviceTempalteId = Guid.NewGuid().ToString()
                }
            };

            var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);

            handlerMock.Protected()
                .Setup("Dispose", ItExpr.IsAny<bool>());

            handlerMock
               .Protected()
               .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
               .ReturnsAsync((HttpRequestMessage req, CancellationToken token) =>
               {
                   var response = new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                   {
                       Content = new StringContent(
                            $"{{" +
                            $"    \"results\": [" +
                            $"        {{" +
                            $"              \"$id\":  \"vn1lwbzhic \"," +
                            $"              \"LoRa.DevAddr\":  \"{CreateDevAddr()}\"," +
                            $"              \"LoRa.NwkSKey\":  \"Sit quam itaque cum. \"," +
                            $"              \"LoRa.GatewayID\":  \"Qui voluptatem facere.\"" +
                            $"         }}" +
                            $"     ]",
                            Encoding.UTF8,
                            "application/json")
                   };

                   return response;
               })
               .Verifiable();

            using var mockHttpClient = new HttpClient(handlerMock.Object)
            {
                BaseAddress = new Uri("http://localhost.local/")
            };

            using var instance = new DeviceTwinPageResult(mockHttpClient, deviceTemplateInfos, "1.0", c => c.ComponentName);

            Assert.True(instance.HasMoreResults);
            var results = await instance.GetNextPageAsync();
            Assert.Single(results);
            Assert.True(results.All(x => x != null));
            handlerMock.Protected().Verify("SendAsync", Times.Exactly(1), ItExpr.Is<HttpRequestMessage>(c => c.RequestUri.LocalPath == "/api/query"), ItExpr.IsAny<CancellationToken>());

            Assert.True(instance.HasMoreResults);
            results = await instance.GetNextPageAsync();
            Assert.Single(results);
            Assert.True(results.All(x => x != null));
            handlerMock.Protected().Verify("SendAsync", Times.Exactly(2), ItExpr.Is<HttpRequestMessage>(c => c.RequestUri.LocalPath == "/api/query"), ItExpr.IsAny<CancellationToken>());

            Assert.False(instance.HasMoreResults);
        }
    }
}
