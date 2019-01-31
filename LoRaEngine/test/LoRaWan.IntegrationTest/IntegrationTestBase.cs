using LoRaWan.Test.Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using Xunit;

namespace LoRaWan.IntegrationTest
{

    public class IntegrationTestBase
    {
        private IntegrationTestFixtureBase testFixture;
        protected IntegrationTestFixtureBase TestFixture { get { return this.testFixture; } }

        public IntegrationTestBase(IntegrationTestFixtureBase testFixture)
        {
            this.testFixture = testFixture;
        }

        protected void Log(string value) =>TestLogger.Log(value);

        // Logs starts of a test method call
        protected void LogTestStart(TestDeviceInfo device, [CallerMemberName] string memberName = "")
        {
            Log($"[INFO] ** Starting {memberName} using device {device.DeviceID} **");
        }

        // Logs starts of a test method call
        protected void LogTestStart(IEnumerable<TestDeviceInfo> devices, [CallerMemberName] string memberName = "")
        {
            var deviceIdList = string.Join(',', devices.Select(x => x.DeviceID));
            Log($"[INFO] ** Starting {memberName} using devices {deviceIdList} **");
        }
    }
}
