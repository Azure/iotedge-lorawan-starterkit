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
        [InlineData("0000:0000:0000:0000")]
        [InlineData("FFFF:FFFF:FFFF:FFFF")]
        public async Task DevEUI_Validation(string devEUI)
        {
            var dummyExecContext = new ExecutionContext();
            var apiCalls = new Func<HttpRequest, Task<IActionResult>>[]
            {
                (req) => Task.Run(() => new FCntCacheCheck(null, NullLogger<FCntCacheCheck>.Instance).NextFCntDownInvoke(req)),
                (req) => Task.Run(() => new FunctionBundlerFunction(Array.Empty<IFunctionBundlerExecutionItem>(), NullLogger<FunctionBundlerFunction>.Instance).FunctionBundler(req, string.Empty)),
                (req) => new DeviceGetter(null, null, NullLogger<DeviceGetter>.Instance).GetDevice(req)
            };

            foreach (var apiCall in apiCalls)
            {
                var request = new DefaultHttpContext().Request;
                request.Query = new QueryCollection(new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>
                    {
                        { ApiVersion.QueryStringParamName, ApiVersion.LatestVersion.Name },
                        { "DevEUI", devEUI }
                    });

                _ = Assert.IsType<BadRequestObjectResult>(await apiCall(request));
            }
        }
    }
}
