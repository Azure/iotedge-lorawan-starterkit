// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoraKeysManagerFacade.Test
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using System.Threading.Tasks;

    public class FunctionTestBase
    {
        private const int EUI64BitStringLength = 16;
        private const int EUI32BitStringLength = 8;
        private static readonly char[] ValidChars = new char[] { '1', '2', '3', '4', '5', '6', '7', '8', '9', '0', 'A', 'B', 'C', 'D', 'E', 'F' };
        private static readonly List<string> UsedEUI64 = new List<string>();
        private static readonly List<string> UsedEUI32 = new List<string>();

        private static readonly Random Rnd = new Random(Environment.TickCount);
        private static readonly object RndLock = new object();

        protected static string NewUniqueEUI64()
        {
            var sb = new StringBuilder(EUI64BitStringLength);

            for (var i = 0; i < EUI64BitStringLength; i++)
            {
                int n = 0;
                lock (RndLock)
                {
                    n = Rnd.Next(0, ValidChars.Length);
                }

                sb.Append(ValidChars[n]);
            }

            var result = sb.ToString();

            lock (UsedEUI64)
            {
                if (!UsedEUI64.Contains(result))
                {
                    UsedEUI64.Add(result);
                    return result;
                }
            }

            return NewUniqueEUI64();
        }

        protected static string NewUniqueEUI32()
        {
            var sb = new StringBuilder(EUI32BitStringLength);

            for (var i = 0; i < EUI32BitStringLength; i++)
            {
                int n = 0;
                lock (RndLock)
                {
                    n = Rnd.Next(0, ValidChars.Length);
                }

                sb.Append(ValidChars[n]);
            }

            var result = sb.ToString();

            lock (UsedEUI32)
            {
                if (!UsedEUI32.Contains(result))
                {
                    UsedEUI32.Add(result);
                    return result;
                }
            }

            return NewUniqueEUI32();
        }

        protected static LoRaADRRequest CreateStandardADRRequest(string gatewayId, float snr = -10)
        {
            var req = StandardADRRequest;
            req.GatewayId = gatewayId;
            req.RequiredSnr = snr;
            return req;
        }

        private static LoRaADRRequest StandardADRRequest
        {
            get
            {
                return new LoRaADRRequest
                {
                    DataRate = 1,
                    FCntUp = 1,
                    RequiredSnr = -10,
                    FCntDown = 1,
                    MinTxPowerIndex = 4,
                    PerformADRCalculation = true
                };
            }
        }
    }
}