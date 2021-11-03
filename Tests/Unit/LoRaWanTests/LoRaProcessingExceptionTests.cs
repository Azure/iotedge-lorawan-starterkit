// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Unit.LoRaWanTests
{
    using System.Text.RegularExpressions;
    using Xunit;

    public class LoRaProcessingExceptionTests
    {
        [Fact]
        public void ToString_Success()
        {
            // arrange
            const string message = "Device configuration not found.";
            var ex = new LoRaProcessingException(message, LoRaProcessingErrorCode.InvalidDeviceConfiguration);

            // act
            var result = ex.ToString();

            // assert
            static string RemoveWhitespace(string input) => Regex.Replace(input, @"\s", "");
            var expected = @"LoRaWan.LoRaProcessingException: Device configuration not found.
                             ErrorCode: InvalidDeviceConfiguration";
            Assert.Equal(RemoveWhitespace(expected), RemoveWhitespace(result));
        }
    }
}
