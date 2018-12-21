using LoRaWan.Test.Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using Xunit;

namespace LoRaWan.IntegrationTest
{
    public class IntegrationTestBase : IClassFixture<IntegrationTestFixture>, IDisposable
    {
        private IntegrationTestFixture testFixture;
        protected IntegrationTestFixture TestFixture { get { return this.testFixture; } }

        private LoRaArduinoSerial arduinoDevice;
        protected LoRaArduinoSerial ArduinoDevice { get { return this.arduinoDevice; } }


        public IntegrationTestBase(IntegrationTestFixture testFixture)
        {
            this.testFixture = testFixture;
            this.arduinoDevice = testFixture.ArduinoDevice;
            this.TestFixture.ClearLogs();
        }


        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // Before starting a new test, wait 5 seconds to ensure serial port is not receiving dirty data
                    if (this.arduinoDevice != null)
                        this.arduinoDevice.WaitForIdleAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult();
                }
            
                disposedValue = true;
            }
        }


        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
            // TODO: uncomment the following line if the finalizer is overridden above.
            GC.SuppressFinalize(this);
        }
        #endregion

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
