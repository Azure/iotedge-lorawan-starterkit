//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//
using LoRaWan.Shared;
using System;
using System.Collections.Generic;
using System.Text;
using Xunit;

namespace LoraKeysManagerFacade.Test
{
    public class ApiVersionTest
    {
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
        public void Version_2018_12_16_Preview_Is_Compatible_With_0_2()
        {
            Assert.True(ApiVersion.Version_2018_12_16_Preview.SupportsVersion(ApiVersion.Version_0_2_Or_Earlier));
        }

        [Fact]
        public void Version_2018_12_16_Preview_Is_Not_Compatible_With_Version_2019_01_30_Preview()
        {
            Assert.False(ApiVersion.Version_2018_12_16_Preview.SupportsVersion(ApiVersion.Version_2019_01_30_Preview));
        }

        [Fact]
        public void Version_2019_01_30_Preview_Is_Compatible_With_Older_Versions()
        {
            Assert.True(ApiVersion.Version_2019_01_30_Preview.SupportsVersion(ApiVersion.Version_0_2_Or_Earlier));
            Assert.True(ApiVersion.Version_2019_01_30_Preview.SupportsVersion(ApiVersion.Version_2018_12_16_Preview));
        }
    }
}
