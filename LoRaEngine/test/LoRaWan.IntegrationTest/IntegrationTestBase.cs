using System;
using System.Collections.Generic;
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
            this.arduinoDevice = LoRaArduinoSerial.CreateFromPort(this.TestFixture.Configuration.LeafDeviceSerialPort);
            this.TestFixture.ClearNetworkServerModuleLog();
        }


        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    this.arduinoDevice?.Dispose();
                    this.arduinoDevice = null;
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
    }
}
