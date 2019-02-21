// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaTools.ADR
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using LoRaTools.LoRaMessage;
    using LoRaTools.LoRaPhysical;

    /// <summary>
    /// An interface implementing LoRa ADR strategies.
    /// </summary>
    public interface ILoRaADRStrategy
    {
        (int txPower, int datarate) GetPowerAndDRConfiguration(float requiredSnr, int dataRate, double maxSnr, int currentTxPower, int minTxPowerIndex);

        int ComputeNbRepetion(int first, int last, int currentNbRep);
    }
}
