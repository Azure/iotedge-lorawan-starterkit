// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Unit.FacadeTests
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using LoraKeysManagerFacade;
    using LoraKeysManagerFacade.FunctionBundler;
    using LoRaTools.CommonAPI;
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
                (req) => new DeviceGetter(null, null).GetDevice(req, NullLogger.Instance),
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
    }
}
