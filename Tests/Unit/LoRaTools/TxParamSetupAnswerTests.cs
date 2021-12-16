// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Unit.LoRaTools
{
    using global::LoRaTools;
    using global::LoRaTools.Mac;
    using Xunit;

    public class TxParamSetupAnswerTests
    {
        [Fact]
        public void Length()
        {
            Assert.Equal(1, new TxParamSetupAnswer().Length);
        }

        [Fact]
        public void ToBytes()
        {
            Assert.Equal(new[] { (byte)Cid.TxParamSetupCmd }, new TxParamSetupAnswer().ToBytes());
        }

        [Fact]
        public void Cid_Success()
        {
            Assert.Equal(Cid.TxParamSetupCmd, new TxParamSetupAnswer().Cid);
        }
    }
}
