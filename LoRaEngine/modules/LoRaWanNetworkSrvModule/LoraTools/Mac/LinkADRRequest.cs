// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaTools
{
    using System;
    using System.Collections.Generic;
    using LoRaTools.Utils;

    /// <summary>
    /// LinkAdrRequest Downstream
    /// </summary>
    public class LinkADRRequest : MacCommand
    {
        private readonly byte dataRateTXPower;
        private readonly ushort chMask;
        private readonly byte redundancy;

        public override int Length => 5;

        /// <summary>
        /// Initializes a new instance of the <see cref="LinkADRRequest"/> class.
        /// </summary>
        public LinkADRRequest(byte datarate, byte txPower, ushort chMask, byte chMaskCntl, byte nbTrans)
        {
            this.Cid = CidEnum.LinkADRCmd;
            this.dataRateTXPower = (byte)((datarate << 4) | txPower);
            this.chMask = chMask;
            // bit 7 is RFU
            this.redundancy = (byte)((byte)((chMaskCntl << 4) | nbTrans) & 0b01111111);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="LinkADRRequest"/> class.
        /// </summary>
        public LinkADRRequest(IDictionary<string, string> dictionary)
        {
            if (dictionary.TryGetValueCaseInsensitive("datarate", out string dataratestr) &&
                dictionary.TryGetValueCaseInsensitive("txpower", out string txpowerstr) &&
                dictionary.TryGetValueCaseInsensitive("chMask", out string chMaskstr) &&
                dictionary.TryGetValueCaseInsensitive("chMaskCntl", out string chMaskCntlstr) &&
                dictionary.TryGetValueCaseInsensitive("nbTrans", out string nbTransstr))
            {
                if (byte.TryParse(dataratestr, out byte datarate) &&
                    byte.TryParse(txpowerstr, out byte txPower) &&
                    ushort.TryParse(chMaskstr, out ushort chMask) &&
                    byte.TryParse(chMaskCntlstr, out byte chMaskCntl) &&
                    byte.TryParse(nbTransstr, out byte nbTrans))
                {
                    this.Cid = CidEnum.LinkADRCmd;
                    this.dataRateTXPower = (byte)((datarate << 4) | txPower);
                    this.chMask = chMask;
                    // bit 7 is RFU
                    this.redundancy = (byte)((byte)((chMaskCntl << 4) | nbTrans) & 0b01111111);
                }
                else
                {
                    throw new Exception("LinkADRRequest C2D properties must be in String Integer style");
                }
            }
            else
            {
                throw new Exception("LinkADRRequest C2D must have have the following message properties set : datarate, txpower, chMask, chMaskCntl, nbTrans");
            }
        }

        public override IEnumerable<byte> ToBytes()
        {
            List<byte> returnedBytes = new List<byte>();
            returnedBytes.Add((byte)this.Cid);
            returnedBytes.Add(this.dataRateTXPower);
            returnedBytes.Add((byte)this.chMask);
            returnedBytes.Add((byte)(this.chMask >> 8));
            returnedBytes.Add(this.redundancy);
            return returnedBytes;
        }

        public override string ToString()
        {
            return string.Empty;
        }
    }
}
