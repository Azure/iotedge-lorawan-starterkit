// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoraKeysManagerFacade.Test
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using LoraKeysManagerFacade.FunctionBundler;
    using LoRaWan.Shared;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Http.Internal;
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
                (req) => DeviceGetter.GetDevice(req, NullLogger.Instance, dummyExecContext),
                (req) => Task.Run(() => FCntCacheCheck.NextFCntDownInvoke(req, NullLogger.Instance, dummyExecContext)),
                (req) => Task.Run(() => DuplicateMsgCacheCheck.DuplicateMsgCheck(req, NullLogger.Instance, dummyExecContext, devEUI)),
                (req) => Task.Run(() => LoRaADRFunction.ADRFunctionImpl(req, NullLogger.Instance, dummyExecContext, devEUI)),
                (req) => Task.Run(() => FunctionBundler.FunctionBundlerImpl(req, NullLogger.Instance, dummyExecContext, devEUI))
            };

            foreach (var apiCall in apiCalls)
            {
                var request = new DefaultHttpRequest(new DefaultHttpContext())
                {
                    Query = new QueryCollection(new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>
                                                        {
                                                            { ApiVersion.QueryStringParamName, ApiVersion.LatestVersion.Name },
                                                            { "DevEUI", devEUI }
                                                        })
                };

                await Assert.ThrowsAsync<ArgumentException>(async () => await apiCall(request));
            }
        }
    }
}
