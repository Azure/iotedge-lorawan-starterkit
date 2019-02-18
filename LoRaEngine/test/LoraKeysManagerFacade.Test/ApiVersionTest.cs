﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoraKeysManagerFacade.Test
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using LoRaWan.Shared;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Http.Internal;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Azure.WebJobs;
    using Microsoft.Extensions.Logging.Abstractions;
    using Xunit;

    public class ApiVersionTest
    {
        [Theory]
        [InlineData(null)]
        [InlineData("zyx")]
        [InlineData("2019-01-30-preview")]
        public async Task LatestVersion_Returns_Bad_Request_If_InvalidVersion_Requested(string requestVersion)
        {
            const string MissingVersionToken = "0.2 or earlier";

            var dummyExecContext = new ExecutionContext();
            var apiCalls = new Func<HttpRequest, Task<IActionResult>>[]
            {
                (req) => DeviceGetter.GetDevice(req, NullLogger.Instance, dummyExecContext),
                (req) => Task.Run(() => FCntCacheCheck.NextFCntDownInvoke(req, NullLogger.Instance, dummyExecContext)),
                (req) => Task.Run(() => DuplicateMsgCacheCheck.DuplicateMsgCheck(req, NullLogger.Instance, dummyExecContext))
            };

            foreach (var apiCall in apiCalls)
            {
                var request = new DefaultHttpRequest(new DefaultHttpContext());

                if (!string.IsNullOrEmpty(requestVersion))
                {
                    request.Query = new QueryCollection(new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>
                                                        {
                                                            { ApiVersion.QueryStringParamName, requestVersion }
                                                        });
                }

                var actual = await apiCall(request);
                Assert.NotNull(actual);
                Assert.IsType<BadRequestObjectResult>(actual);
                var badRequestResult = (BadRequestObjectResult)actual;

                var versionToken = !string.IsNullOrEmpty(requestVersion) ? requestVersion : MissingVersionToken;

                Assert.Equal($"Incompatible versions (requested: '{versionToken}', current: '{ApiVersion.LatestVersion.Version}')", ((Exception)badRequestResult.Value).Message);

                // Ensure current version is added to response
                Assert.Contains(ApiVersion.HttpHeaderName, request.HttpContext.Response.Headers);
                Assert.Equal(ApiVersion.LatestVersion.Version, request.HttpContext.Response.Headers[ApiVersion.HttpHeaderName].FirstOrDefault());
            }
        }

        [Fact]
        public void Version_02_Should_Be_Older_As_All()
        {
            Assert.True(ApiVersion.Version_0_2_Or_Earlier < ApiVersion.Version_2018_12_16_Preview);
            Assert.True(ApiVersion.Version_0_2_Or_Earlier < ApiVersion.Version_2019_01_30_Preview);
        }

        [Fact]
        public void Version_2019_Should_Be_Newer_As_All()
        {
            Assert.True(ApiVersion.Version_2019_01_30_Preview > ApiVersion.Version_2018_12_16_Preview);
            Assert.True(ApiVersion.Version_2019_01_30_Preview > ApiVersion.Version_0_2_Or_Earlier);
        }

        [Fact]
        public void Empty_String_Should_Parse_To_Version_02()
        {
            var actual = ApiVersion.Parse(string.Empty);
            Assert.Same(actual, ApiVersion.Version_0_2_Or_Earlier);
            Assert.Equal(actual, ApiVersion.Version_0_2_Or_Earlier);
        }

        [Fact]
        public void Parse_Null_String_Should_Return_Unkown_Version()
        {
            var actual = ApiVersion.Parse(null);
            Assert.False(actual.IsKnown);
        }

        [Fact]
        public void Parse_Unknown_Version_String_Should_Return_Unkown_Version()
        {
            var actual = ApiVersion.Parse("qwerty");
            Assert.False(actual.IsKnown);
            Assert.Equal("qwerty", actual.Version);
        }

        [Fact]
        public void Parse_Version_2018_12_16_Preview_Should_Return_Version()
        {
            var actual = ApiVersion.Parse("2018-12-16-preview");
            Assert.True(actual.IsKnown);
            Assert.Equal(actual, ApiVersion.Version_2018_12_16_Preview);
            Assert.Same(actual, ApiVersion.Version_2018_12_16_Preview);
        }

        [Fact]
        public void Parse_Version_2019_01_30_Preview_Should_Return_Version()
        {
            var actual = ApiVersion.Parse("2019-01-30-preview");
            Assert.True(actual.IsKnown);
            Assert.Equal(actual, ApiVersion.Version_2019_01_30_Preview);
            Assert.Same(actual, ApiVersion.Version_2019_01_30_Preview);
        }

        [Fact]
        public void Version_0_2_Is_Not_Compatible_With_Newer_Versions()
        {
            Assert.False(ApiVersion.Version_0_2_Or_Earlier.SupportsVersion(ApiVersion.Version_2018_12_16_Preview));
            Assert.False(ApiVersion.Version_0_2_Or_Earlier.SupportsVersion(ApiVersion.Version_2019_01_30_Preview));
        }

        [Fact]
        public void Version_2018_12_16_Preview_Is_Not_Compatible_With_0_2()
        {
            Assert.False(ApiVersion.Version_2018_12_16_Preview.SupportsVersion(ApiVersion.Version_0_2_Or_Earlier));
        }

        [Fact]
        public void Version_2018_12_16_Preview_Is_Not_Compatible_With_Version_2019_01_30_Preview()
        {
            Assert.False(ApiVersion.Version_2018_12_16_Preview.SupportsVersion(ApiVersion.Version_2019_01_30_Preview));
        }

        [Fact]
        public void Version_2019_01_30_Preview_Is_Compatible_With_2018_12_16_Preview()
        {
            Assert.True(ApiVersion.Version_2019_01_30_Preview.SupportsVersion(ApiVersion.Version_2018_12_16_Preview));
        }

        [Fact]
        public void Version_2019_01_30_Preview_Is_Not_Compatible_With_0_2_Or_Earlier()
        {
            Assert.False(ApiVersion.Version_2019_01_30_Preview.SupportsVersion(ApiVersion.Version_0_2_Or_Earlier));
        }
    }
}
