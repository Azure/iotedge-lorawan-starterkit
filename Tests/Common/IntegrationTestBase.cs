// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Common
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Runtime.CompilerServices;

    public class IntegrationTestBase
    {
        protected IntegrationTestFixtureBase TestFixture { get; }

        public IntegrationTestBase(IntegrationTestFixtureBase testFixture)
        {
            TestFixture = testFixture;
        }

        protected static void Log(string value) => TestLogger.Log(value);

        // Logs starts of a test method call
        protected static void LogTestStart(TestDeviceInfo device, [CallerMemberName] string memberName = "")
        {
            Log($"[INFO] ** Starting {memberName} using device {device.DeviceID} **");
        }

        // Logs starts of a test method call
        protected static void LogTestStart(IEnumerable<TestDeviceInfo> devices, [CallerMemberName] string memberName = "")
        {
            var deviceIdList = string.Join(',', devices.Select(x => x.DeviceID));
            Log($"[INFO] ** Starting {memberName} using devices {deviceIdList} **");
        }

        // Logs starts of a test method call
        protected static void LogTestStart(TestDeviceInfo device, StationEui concentratorEui, [CallerMemberName] string memberName = "")
        {
            Log($"[INFO] ** Starting {memberName} using device {device.DeviceID} and concentrator {concentratorEui} **");
        }
    }
}
