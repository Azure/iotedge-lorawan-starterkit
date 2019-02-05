// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoraKeysManagerFacade.Test
{
    using System.Collections.Generic;
    using LoRaWan.Shared;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Http.Internal;
    using Microsoft.Extensions.Primitives;
    using Xunit;

    public class HttpUtilitiesTest
    {
        [Fact]
        public void When_No_Version_Is_Defined_Returns_Version_02()
        {
            var request = new DefaultHttpRequest(new DefaultHttpContext())
            {
                Query = new QueryCollection(new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>())
            };

            var actual = HttpUtilities.GetRequestedVersion(request);
            Assert.NotNull(actual);
            Assert.Same(ApiVersion.Version_0_2_Or_Earlier, actual);
        }

        [Fact]
        public void When_Unknown_Version_Is_Requested_Returns_Unkown_Version()
        {
            var request = new DefaultHttpRequest(new DefaultHttpContext())
            {
                Query = new QueryCollection(new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>
                {
                    { ApiVersion.QueryStringParamName, "qwerty" },
                }),
            };

            var actual = HttpUtilities.GetRequestedVersion(request);
            Assert.NotNull(actual);
            Assert.False(actual.IsKnown);
            Assert.Equal("qwerty", actual.Version);
        }

        [Fact]
        public void When_Version_2018_12_16_Is_Requested_In_QueryString_Returns_It()
        {
            var request = new DefaultHttpRequest(new DefaultHttpContext())
            {
                Query = new QueryCollection(new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>
                {
                    { ApiVersion.QueryStringParamName, new StringValues("2018-12-16-preview") }
                }),
            };

            var actual = HttpUtilities.GetRequestedVersion(request);
            Assert.NotNull(actual);
            Assert.Same(ApiVersion.Version_2018_12_16_Preview, actual);
        }
    }
}
