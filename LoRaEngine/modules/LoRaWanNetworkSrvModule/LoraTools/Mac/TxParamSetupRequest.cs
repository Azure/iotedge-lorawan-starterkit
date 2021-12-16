// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaTools.Mac
{
    using System;
    using System.Collections.Generic;
    using LoRaTools.Regions;

    public class TxParamSetupRequest : MacCommand
    {
        private readonly DwellTimeSetting dwellTimeSetting;

        public TxParamSetupRequest(DwellTimeSetting dwellTimeSetting)
        {
            this.dwellTimeSetting = dwellTimeSetting ?? throw new ArgumentNullException(nameof(dwellTimeSetting));
            if (dwellTimeSetting.MaxEirp > 15) throw new ArgumentException(null, nameof(dwellTimeSetting));
            Cid = Cid.TxParamSetupCmd;
        }

        public override int Length { get; } = sizeof(byte) + 1;

        public override IEnumerable<byte> ToBytes() =>
            new[]
            {
                (byte)((byte)this.dwellTimeSetting.MaxEirp
                       | (this.dwellTimeSetting.UplinkDwellTime ? 0b0001_0000 : 0)
                       | (this.dwellTimeSetting.DownlinkDwellTime ? 0b0010_0000 : 0)),
                (byte)Cid.TxParamSetupCmd
            };

        public override string ToString() =>
            $"Type: {Cid} Request, {this.dwellTimeSetting}";
    }
}
