// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Unit.LoraKeysManagerFacade
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using global::LoraKeysManagerFacade;
    using global::LoraKeysManagerFacade.FunctionBundler;
    using global::LoRaTools.CommonAPI;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Azure.WebJobs;
    using Microsoft.Extensions.Logging.Abstractions;
    using Xunit;

    public class ApiValidationTest
    {
        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("1A88d")]
        [InlineData(".")]
        [InlineData("0000000000000000")]
        public async Task DevEUI_Validation(string devEUI)
        {
            var dummyExecContext = new ExecutionContext();
            var apiCalls = new Func<HttpRequest, Task<IActionResult>>[]
            {
                (req) => Task.Run(() => new FCntCacheCheck(null).NextFCntDownInvoke(req, NullLogger.Instance)),
                (req) => Task.Run(() => new FunctionBundlerFunction(Array.Empty<IFunctionBundlerExecutionItem>()).FunctionBundler(req, NullLogger.Instance, string.Empty))
            };

            foreach (var apiCall in apiCalls)
            {
                var request = new DefaultHttpContext().Request;
                request.Query = new QueryCollection(new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>
                    {
                        { ApiVersion.QueryStringParamName, ApiVersion.LatestVersion.Name },
                        { "DevEUI", devEUI }
                    });

                await Assert.ThrowsAsync<ArgumentException>(async () => await apiCall(request));
            }
        }

        [Theory]
        [InlineData("", false)]
        [InlineData("   ", true)]
        [InlineData("1A88d", true)]
        [InlineData(".", true)]
        [InlineData("0000000000000000", true)]
        public async Task DevEUI_Validation_DeviceGetter(string devEUI, bool throws)
        {
            var request = new DefaultHttpContext().Request;
            request.Query = new QueryCollection(new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>
            {
                { ApiVersion.QueryStringParamName, ApiVersion.LatestVersion.Name },
                { "DevEUI", devEUI }
            });

            var act = () => new DeviceGetter(null, null).GetDevice(request, NullLogger.Instance);

            if (!throws)
                Assert.IsType<BadRequestObjectResult>(await act());
            else
                await Assert.ThrowsAsync<ArgumentException>(act);
        }
    }
}
