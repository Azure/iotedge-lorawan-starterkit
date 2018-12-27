using LoRaWan.NetworkServer;
using LoRaWan.Test.Shared;
using Microsoft.Azure.Devices.Client;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace LoRaWanNetworkServer.Test
{
    public class TestLoRaDeviceAdapter : ILoRaDevice
    {
        private readonly SimulatedDevice simulatedDevice;

        public TestLoRaDeviceAdapter(SimulatedDevice simulatedDevice)
        {
            this.simulatedDevice = simulatedDevice;
        }

        public string DevEUI => this.simulatedDevice.LoRaDevice.DeviceID;

        public string GatewayID => this.simulatedDevice.LoRaDevice.GatewayID;

        int fcntUp;
        public int FcntUp
        {
            get { return this.fcntUp; }
        }

        public string AppSKey => this.simulatedDevice.LoRaDevice.AppSKey;

        public int? ReceiveDelay1 { get; set; } = null;

        public int? ReceiveDelay2 { get; set; } = null;

        public bool AlwaysUseSecondWindow { get; set; }
        public Task AbandonCloudToDeviceMessageAsync(Message additionalMsg) => Task.FromResult(0);

        public Task CompleteCloudToDeviceMessageAsync(Message c2dMsg) => Task.FromResult(0);

        public Dictionary<string, object> GetTwinProperties()
        {
            return new Dictionary<string, object>()
            {
                {  "FcntDown", this.fcntDown },
                { "FcntUp", this.fcntUp },
            };
        }

        int fcntDown = 0;
        public int FcntDown { get { return this.fcntDown; } }

        public string NwkSKey => this.simulatedDevice.LoRaDevice.NwkSKey;

        public int IncrementFcntDown(int value) => Interlocked.Add(ref this.fcntDown, value);

        // TODO: fix this
        public bool IsABP() => !string.IsNullOrEmpty(this.AppSKey);


        public bool IsABPRelaxedFrameCounter() => true;

        public Task<Message> ReceiveCloudToDeviceAsync(TimeSpan waitTime) => Task.FromResult<Message>(null);

        public void SetFcntDown(int value)
        {
            Interlocked.Exchange(ref this.fcntDown, value);
        }

        public void SetFcntUp(int value)
        {
            Interlocked.Exchange(ref this.fcntUp, value);
        }

        public virtual Task UpdateTwinAsync(object twinProperties) => Task.FromResult(0);

        public bool WasNotJustReadFromCache() => false;

        public virtual Task SendEventAsync(Message message) => Task.FromResult(0);
    }
}
