// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
namespace LoRaWan.NetworkServer.Test
{
    using System;
    using System.Collections.Generic;
    using LoRaWan.NetworkServer;
    using Xunit;

    public class CaseInsensitiveEnvironmentVariablesTest
    {
        [Fact]
        public void Should_Return_Null_If_Not_Found()
        {
            var variables = new Dictionary<string, string>()
            {
            };

            var target = new CaseInsensitiveEnvironmentVariables(variables);
            var actual = target.GetEnvVar("NOT_EXISTS", null);
            Assert.Null(actual);
        }

        [Fact]
        public void Should_Return_StringEmpty_If_Not_Found()
        {
            var variables = new Dictionary<string, string>()
            {
            };

            var target = new CaseInsensitiveEnvironmentVariables(variables);
            var actual = target.GetEnvVar("NOT_EXISTS", string.Empty);
            Assert.NotNull(actual);
            Assert.Equal(0, actual.Length);
        }

        [Fact]
        public void Should_Find_String_If_Case_Matches()
        {
            var variables = new Dictionary<string, string>()
            {
                { "MYVAR", "VALUE" }
            };

            var target = new CaseInsensitiveEnvironmentVariables(variables);
            var actual = target.GetEnvVar("MYVAR", string.Empty);
            Assert.NotNull(actual);
            Assert.Equal("VALUE", actual);
        }

        [Fact]
        public void Should_Find_String_If_Case_Does_Not_Match()
        {
            var variables = new Dictionary<string, string>()
            {
                { "myvar", "VALUE" }
            };

            var target = new CaseInsensitiveEnvironmentVariables(variables);
            var actual = target.GetEnvVar("MYVAR", string.Empty);
            Assert.NotNull(actual);
            Assert.Equal("VALUE", actual);
        }

        [Fact]
        public void Should_Return_Default_Bool_Value_False_If_Not_Found()
        {
            var variables = new Dictionary<string, string>()
            {
            };

            var target = new CaseInsensitiveEnvironmentVariables(variables);
            var actual = target.GetEnvVar("MYVAR", false);
            Assert.False(actual);
        }

        [Fact]
        public void Should_Return_Default_Bool_Value_True_If_Not_Found()
        {
            var variables = new Dictionary<string, string>()
            {
            };

            var target = new CaseInsensitiveEnvironmentVariables(variables);
            var actual = target.GetEnvVar("MYVAR", true);
            Assert.True(actual);
        }

        [Fact]
        public void Should_Return_Default_Bool_Value_If_Cannot_Parse()
        {
            var variables = new Dictionary<string, string>()
            {
                { "MYVAR", "ABC" }
            };

            var target = new CaseInsensitiveEnvironmentVariables(variables);
            var actual = target.GetEnvVar("MYVAR", true);
            Assert.True(actual);
        }

        [Fact]
        public void Should_Find_Bool_If_Case_Matches()
        {
            var variables = new Dictionary<string, string>()
            {
                { "MYVAR", "true" }
            };

            var target = new CaseInsensitiveEnvironmentVariables(variables);
            var actual = target.GetEnvVar("MYVAR", false);
            Assert.True(actual);
        }

        [Fact]
        public void Should_Find_Bool_If_Case_Does_Not_Match()
        {
            var variables = new Dictionary<string, string>()
            {
                { "myvar", "true" }
            };

            var target = new CaseInsensitiveEnvironmentVariables(variables);
            var actual = target.GetEnvVar("MYVAR", false);
            Assert.True(actual);
        }

        [Fact]
        public void Should_Return_Default_Double_Value_True_If_Not_Found()
        {
            var variables = new Dictionary<string, string>()
            {
            };

            var target = new CaseInsensitiveEnvironmentVariables(variables);
            var actual = target.GetEnvVar("MYVAR", 10.0);
            Assert.Equal(10.0, actual);
        }

        [Fact]
        public void Should_Return_Default_Double_Value_If_Cannot_Parse()
        {
            var variables = new Dictionary<string, string>()
            {
                { "MYVAR", "ABC" }
            };

            var target = new CaseInsensitiveEnvironmentVariables(variables);
            var actual = target.GetEnvVar("MYVAR", 12.0);
            Assert.Equal(12.0, actual);
        }

        [Fact]
        public void Should_Find_Double_If_Case_Matches()
        {
            var variables = new Dictionary<string, string>()
            {
                { "MYVAR", "20" }
            };

            var target = new CaseInsensitiveEnvironmentVariables(variables);
            var actual = target.GetEnvVar("MYVAR", 0.0);
            Assert.Equal(20.0, actual);
        }

        [Fact]
        public void Should_Find_Double_If_Case_Does_Not_Match()
        {
            var variables = new Dictionary<string, string>()
            {
                { "myvar", "89.31" }
            };

            var target = new CaseInsensitiveEnvironmentVariables(variables);
            var actual = target.GetEnvVar("MYVAR", 0.0);
            Assert.Equal(89.31, actual);
        }

        [Fact]
        public void Should_Return_Default_Int_Value_True_If_Not_Found()
        {
            var variables = new Dictionary<string, string>()
            {
            };

            var target = new CaseInsensitiveEnvironmentVariables(variables);
            var actual = target.GetEnvVar("MYVAR", 10);
            Assert.Equal(10, actual);
        }

        [Fact]
        public void Should_Return_Default_Int_Value_If_Cannot_Parse()
        {
            var variables = new Dictionary<string, string>()
            {
                { "MYVAR", "ABC" }
            };

            var target = new CaseInsensitiveEnvironmentVariables(variables);
            var actual = target.GetEnvVar("MYVAR", 12);
            Assert.Equal(12, actual);
        }

        [Fact]
        public void Should_Find_Int_If_Case_Matches()
        {
            var variables = new Dictionary<string, string>()
            {
                { "MYVAR", "20" }
            };

            var target = new CaseInsensitiveEnvironmentVariables(variables);
            var actual = target.GetEnvVar("MYVAR", 0);
            Assert.Equal(20, actual);
        }

        [Fact]
        public void Should_Find_Int_If_Case_Does_Not_Match()
        {
            var variables = new Dictionary<string, string>()
            {
                { "myvar", "8931" }
            };

            var target = new CaseInsensitiveEnvironmentVariables(variables);
            var actual = target.GetEnvVar("MYVAR", 0);
            Assert.Equal(8931, actual);
        }

        [Fact]
        public void Should_Return_Default_Uint_Value_True_If_Not_Found()
        {
            var variables = new Dictionary<string, string>()
            {
            };

            var target = new CaseInsensitiveEnvironmentVariables(variables);
            var actual = target.GetEnvVar("MYVAR", 10u);
            Assert.Equal(10u, actual);
        }

        [Fact]
        public void Should_Return_Default_Uint_Value_If_Cannot_Parse()
        {
            var variables = new Dictionary<string, string>()
            {
                { "MYVAR", "ABC" }
            };

            var target = new CaseInsensitiveEnvironmentVariables(variables);
            var actual = target.GetEnvVar("MYVAR", 12u);
            Assert.Equal(12u, actual);
        }

        [Fact]
        public void Should_Find_Uint_If_Case_Matches()
        {
            var variables = new Dictionary<string, string>()
            {
                { "MYVAR", "20" }
            };

            var target = new CaseInsensitiveEnvironmentVariables(variables);
            var actual = target.GetEnvVar("MYVAR", 0u);
            Assert.Equal(20u, actual);
        }

        [Fact]
        public void Should_Find_Uint_If_Case_Does_Not_Match()
        {
            var variables = new Dictionary<string, string>()
            {
                { "myvar", "8931" }
            };

            var target = new CaseInsensitiveEnvironmentVariables(variables);
            var actual = target.GetEnvVar("MYVAR", 0u);
            Assert.Equal(8931u, actual);
        }
    }
}