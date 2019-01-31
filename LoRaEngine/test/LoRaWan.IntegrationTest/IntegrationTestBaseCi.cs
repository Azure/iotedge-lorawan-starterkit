// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.IntegrationTest
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Runtime.CompilerServices;
    using System.Text;
    using LoRaWan.Test.Shared;
    using Xunit;

    public class IntegrationTestBaseCi : IntegrationTestBase, IClassFixture<IntegrationTestFixtureCi>, IDisposable
    {
        protected IntegrationTestFixtureCi TestFixtureCi
        {
            get { return (IntegrationTestFixtureCi)this.TestFixture; }
        }

        private LoRaArduinoSerial arduinoDevice;

        protected LoRaArduinoSerial ArduinoDevice
        {
            get { return this.arduinoDevice; }
        }

        public IntegrationTestBaseCi(IntegrationTestFixtureCi testFixture)
            : base(testFixture)
        {
            this.arduinoDevice = testFixture.ArduinoDevice;
        }

        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!this.disposedValue)
            {
                if (disposing)
                {
                    // Before starting a new test, wait 5 seconds to ensure serial port is not receiving dirty data
                    if (this.arduinoDevice != null)
                        this.arduinoDevice.WaitForIdleAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult();
                }

                this.disposedValue = true;
            }
        }

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            this.Dispose(true);
            // TODO: uncomment the following line if the finalizer is overridden above.
            GC.SuppressFinalize(this);
        }
    }
}
