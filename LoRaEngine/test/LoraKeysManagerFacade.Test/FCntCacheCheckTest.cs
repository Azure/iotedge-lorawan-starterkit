// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoraKeysManagerFacade.Test
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using LoRaWan.Shared;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Http.Internal;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Azure.WebJobs;
    using Microsoft.Extensions.Logging.Abstractions;
    using Xunit;

    public class FCntCacheCheckTest
    {
        [Fact]
        public async Task Version_2018_12_16_Preview_Returns_Bad_Request_If_Version_0_2_Is_Requested()
        {
            var request = new DefaultHttpRequest(new DefaultHttpContext())
            {
                Query = new QueryCollection(new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>
                {
                }),
            };

            var actual = FCntCacheCheck.Run(request, NullLogger.Instance, new ExecutionContext(), ApiVersion.Version_2018_12_16_Preview);
            Assert.NotNull(actual);
            Assert.IsType<BadRequestObjectResult>(actual);
            var badRequestResult = (BadRequestObjectResult)actual;

            Assert.Equal("Incompatible versions (requested: '0.2 or earlier', current: '2018-12-16-preview')", badRequestResult.Value.ToString());

            // Ensure current version is added to response
            Assert.Contains(ApiVersion.HttpHeaderName, request.HttpContext.Response.Headers);
            Assert.Equal("2018-12-16-preview", request.HttpContext.Response.Headers[ApiVersion.HttpHeaderName].FirstOrDefault());
        }

        [Fact]
        public async Task Version_2018_12_16_Preview_Returns_Bad_Request_If_Unknown_Version_Is_Requested()
        {
            var request = new DefaultHttpRequest(new DefaultHttpContext())
            {
                Query = new QueryCollection(new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>
                {
                    { ApiVersion.QueryStringParamName, "zyx" }
                }),
            };

            var actual = FCntCacheCheck.Run(request, NullLogger.Instance, new ExecutionContext(), ApiVersion.Version_2018_12_16_Preview);
            Assert.NotNull(actual);
            Assert.IsType<BadRequestObjectResult>(actual);
            var badRequestResult = (BadRequestObjectResult)actual;

            Assert.Equal("Incompatible versions (requested: 'zyx', current: '2018-12-16-preview')", badRequestResult.Value.ToString());

            // Ensure current version is added to response
            Assert.Contains(ApiVersion.HttpHeaderName, request.HttpContext.Response.Headers);
            Assert.Equal("2018-12-16-preview", request.HttpContext.Response.Headers[ApiVersion.HttpHeaderName].FirstOrDefault());
        }

        [Fact]
        public async Task Version_2018_12_16_Preview_Returns_Bad_Request_If_Version_2019_01_30_Preview_Is_Requested()
        {
            var request = new DefaultHttpRequest(new DefaultHttpContext())
            {
                Query = new QueryCollection(new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>
                {
                    { ApiVersion.QueryStringParamName, ApiVersion.Version_2019_01_30_Preview.Version }
                }),
            };

            var actual = FCntCacheCheck.Run(request, NullLogger.Instance, new ExecutionContext(), ApiVersion.Version_2018_12_16_Preview);
            Assert.NotNull(actual);
            Assert.IsType<BadRequestObjectResult>(actual);
            var badRequestResult = (BadRequestObjectResult)actual;

            Assert.Equal("Incompatible versions (requested: '2019-01-30-preview', current: '2018-12-16-preview')", badRequestResult.Value.ToString());

            // Ensure current version is added to response
            Assert.Contains(ApiVersion.HttpHeaderName, request.HttpContext.Response.Headers);
            Assert.Equal("2018-12-16-preview", request.HttpContext.Response.Headers[ApiVersion.HttpHeaderName].FirstOrDefault());
        }
    }
}
