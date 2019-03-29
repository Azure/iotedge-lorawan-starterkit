// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer.Test
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using LoRaTools.CommonAPI;
    using LoRaWan.NetworkServer;
    using LoRaWan.Test.Shared;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Shared;
    using Microsoft.Extensions.Caching.Memory;
    using Newtonsoft.Json;

    internal static class TestUtils
    {
        internal static LoRaDevice CreateFromSimulatedDevice(
            SimulatedDevice simulatedDevice,
            ILoRaDeviceClient loRaDeviceClient,
            DefaultLoRaDataRequestHandler requestHandler = null,
            ILoRaDeviceClientConnectionManager connectionManager = null)
        {
            var result = new LoRaDevice(simulatedDevice.LoRaDevice.DevAddr, simulatedDevice.LoRaDevice.DeviceID, connectionManager ?? new SingleDeviceConnectionManager(loRaDeviceClient))
            {
                AppEUI = simulatedDevice.LoRaDevice.AppEUI,
                AppKey = simulatedDevice.LoRaDevice.AppKey,
                SensorDecoder = simulatedDevice.LoRaDevice.SensorDecoder,
                AppSKey = simulatedDevice.LoRaDevice.AppSKey,
                NwkSKey = simulatedDevice.LoRaDevice.NwkSKey,
                GatewayID = simulatedDevice.LoRaDevice.GatewayID,
                IsOurDevice = true,
                ClassType = (simulatedDevice.ClassType == 'C' || simulatedDevice.ClassType == 'c') ? LoRaDeviceClassType.C : LoRaDeviceClassType.A,
            };

            result.SetFcntDown(simulatedDevice.FrmCntDown);
            result.SetFcntUp(simulatedDevice.FrmCntUp);
            result.AcceptFrameCountChanges();

            if (requestHandler != null)
                result.SetRequestHandler(requestHandler);

            return result;
        }

        internal static Twin CreateTwin(Dictionary<string, object> desired = null, Dictionary<string, object> reported = null)
        {
            var twin = new Twin();
            if (desired != null)
            {
                foreach (var kv in desired)
                {
                    twin.Properties.Desired[kv.Key] = kv.Value;
                }
            }

            if (reported != null)
            {
                foreach (var kv in reported)
                {
                    twin.Properties.Reported[kv.Key] = kv.Value;
                }
            }

            return twin;
        }

        public static Twin CreateABPTwin(
            this SimulatedDevice simulatedDevice,
            Dictionary<string, object> desiredProperties = null,
            Dictionary<string, object> reportedProperties = null)
        {
            var finalDesiredProperties = new Dictionary<string, object>
                {
                    { TwinProperty.DevAddr, simulatedDevice.DevAddr },
                    { TwinProperty.AppSKey, simulatedDevice.AppSKey },
                    { TwinProperty.NwkSKey, simulatedDevice.NwkSKey },
                    { TwinProperty.GatewayID, simulatedDevice.LoRaDevice.GatewayID },
                    { TwinProperty.SensorDecoder, simulatedDevice.LoRaDevice.SensorDecoder },
                    { TwinProperty.ClassType, simulatedDevice.ClassType.ToString() },
                };

            if (desiredProperties != null)
            {
                foreach (var kv in desiredProperties)
                {
                    finalDesiredProperties[kv.Key] = kv.Value;
                }
            }

            var finalReportedProperties = new Dictionary<string, object>
            {
                { TwinProperty.FCntDown, simulatedDevice.FrmCntDown },
                { TwinProperty.FCntUp, simulatedDevice.FrmCntUp }
            };

            if (reportedProperties != null)
            {
                foreach (var kv in reportedProperties)
                {
                    finalReportedProperties[kv.Key] = kv.Value;
                }
            }

            return CreateTwin(desired: finalDesiredProperties, reported: finalReportedProperties);
        }

        internal static Twin CreateOTAATwin(
            this SimulatedDevice simulatedDevice,
            Dictionary<string, object> desiredProperties = null,
            Dictionary<string, object> reportedProperties = null)
        {
            var finalDesiredProperties = new Dictionary<string, object>
                {
                    { TwinProperty.AppEUI, simulatedDevice.AppEUI },
                    { TwinProperty.AppKey, simulatedDevice.AppKey },
                    { TwinProperty.GatewayID, simulatedDevice.LoRaDevice.GatewayID },
                    { TwinProperty.SensorDecoder, simulatedDevice.LoRaDevice.SensorDecoder },
                    { TwinProperty.ClassType, simulatedDevice.ClassType.ToString() },
                };

            if (desiredProperties != null)
            {
                foreach (var kv in desiredProperties)
                {
                    finalDesiredProperties[kv.Key] = kv.Value;
                }
            }

            var finalReportedProperties = new Dictionary<string, object>
            {
                { TwinProperty.DevAddr, simulatedDevice.DevAddr },
                { TwinProperty.AppSKey, simulatedDevice.AppSKey },
                { TwinProperty.NwkSKey, simulatedDevice.NwkSKey },
                { TwinProperty.DevNonce, simulatedDevice.DevNonce },
                { TwinProperty.NetID, simulatedDevice.NetId },
                { TwinProperty.FCntDown, simulatedDevice.FrmCntDown },
                { TwinProperty.FCntUp, simulatedDevice.FrmCntUp }
            };

            if (reportedProperties != null)
            {
               foreach (var kv in reportedProperties)
                {
                    finalReportedProperties[kv.Key] = kv.Value;
                }
            }

            return CreateTwin(desired: finalDesiredProperties, reported: finalReportedProperties);
        }

        /// <summary>
        /// Helper to create a <see cref="Message"/> from a <see cref="LoRaCloudToDeviceMessage"/>
        /// </summary>
        public static Message CreateMessage(this LoRaCloudToDeviceMessage loRaMessage)
        {
            var message = new Message(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(loRaMessage)))
            {
                ContentType = "application/json",
            };

            return message;
        }

        public static string GeneratePayload(string allowedChars, int length)
        {
            Random random = new Random();

            char[] chars = new char[length];
            int setLength = allowedChars.Length;

            for (int i = 0; i < length; ++i)
            {
                chars[i] = allowedChars[random.Next(setLength)];
            }

            return new string(chars, 0, length);
        }

        /// <summary>
        /// Gets the time span delay necessary to make the request be answered in 2nd receive window
        /// </summary>
        public static TimeSpan GetStartTimeOffsetForSecondWindow()
        {
            return TimeSpan.FromMilliseconds(1000 - LoRaOperationTimeWatcher.ExpectedTimeToPackageAndSendMessage.TotalMilliseconds + 1);
        }

        /// <summary>
        /// Helper method for testing
        /// </summary>
        public static LoRaDeviceClientConnectionManager CreateConnectionManager() => new LoRaDeviceClientConnectionManager(new MemoryCache(new MemoryCacheOptions()));
    }
}