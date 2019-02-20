namespace LoRaSimulator
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using LoRaTools;
    using LoRaTools.LoRaMessage;
    using LoRaWan;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    class SimulatedDevice : IDisposable
    {
        public LoRaDevice LoRaDevice { get; internal set; }

        public int Interval { get; set; }

        public int FrmCntUp { get; set; }

        public int FrmCntDown { get; set; }

        private byte[] _fCnt = new byte[2];

        public PhysicalPayload LastPayload { get; set; }

        private bool isFirstJoinRequest = true;

        public int RandomInterval { get; set; }

        public int GroupRxpk { get; set; }

        public DateTimeOffset dt { get; set; }

        private ulong IncrementalData { get; set; }

        public SimulatedDevice(string json)
        {
            try
            {
                this.LoRaDevice = new LoRaDevice(json);
                var additional = JsonConvert.DeserializeObject<JObject>(json);
                this.Interval = this.GetProperty(additional,"Interval");
                this.FrmCntDown = this.GetProperty(additional, "FrmCntDown");
                this.FrmCntUp = this.GetProperty(additional, "FrmCntUp");
                this.RandomInterval = this.GetProperty(additional, "RandomInterval");
                this.GroupRxpk = this.GetProperty(additional, "GroupRxpk");
                //can store 32 bit but only 16 are sent
                var fcnt32 = BitConverter.GetBytes(this.FrmCntDown);
                this._fCnt[0] = fcnt32[0];
                this._fCnt[1] = fcnt32[1];
                this.IncrementalData = 0;
            }
            catch (Exception)
            {
                if (this.LoRaDevice == null)
                {
                    this.LoRaDevice = new LoRaDevice();
                }
            }
        }

        private int GetProperty(JObject additional, string propname)
        {
            var ret = additional[propname];
            if (ret == null)
            {
                return 0;
            }
            return ret.Value<int>();
        }

        public byte[] GetJoinRequest()
        {
            // create a join request
            var AppEUI = LoRaTools.Utils.ConversionHelper.ByteArrayToString(this.LoRaDevice.GetAppEUI());
            var DevEUI = LoRaTools.Utils.ConversionHelper.ByteArrayToString(this.LoRaDevice.GetDevEUI());
            byte[] DevNonce = new byte[2];
            if ((this.LoRaDevice.DevNonce == "") || (!this.isFirstJoinRequest))
            {
                Random random = new Random();
                // DevNonce[0] = 0xC8; DevNonce[1] = 0x86;
                random.NextBytes(DevNonce);
                this.LoRaDevice.DevNonce = BitConverter.ToString(DevNonce).Replace("-", "");
                Array.Reverse(DevNonce);
                this.isFirstJoinRequest = false;
            }
            else
            {
                DevNonce = this.LoRaDevice.GetDevNonce();
                Array.Reverse(DevNonce);
            }

            Logger.LogAlways(this.LoRaDevice.DevEUI, $"Join request sent DevNonce: {BitConverter.ToString(DevNonce).Replace("-","")}");
            var join = new LoRaPayloadJoinRequest(AppEUI, DevEUI, DevNonce);
            join.SetMic(this.LoRaDevice.AppKey);

            return join.GetByteMessage();
        }

        public byte[] GetUnconfirmedDataUpMessage()
        {
            byte[] mhbr = new byte[] { 0x40 };
            byte[] devAddr = this.LoRaDevice.GetDevAddr();
            Array.Reverse(devAddr);
            byte[] fCtrl = new byte[] { 0x80 };
            // byte[] _FCnt = new byte[] { 0x00, 0x00 };
            this._fCnt[0]++;
            List<MacCommand> fopts = null;
            byte[] fPort = new byte[] { 0x01 };
            // Creating a random number
            // Random random = new Random();
            // int temp = random.Next(-50, 70);
            this.IncrementalData++;
            Logger.LogAlways(this.LoRaDevice.DevEUI, $"Simulated data: {this.IncrementalData.ToString()}");
            byte[] payload = Encoding.ASCII.GetBytes(this.IncrementalData.ToString());
            Array.Reverse(payload);
            // 0 = uplink, 1 = downlink
            int direction = 0;
            LoRaPayloadData standardData = new LoRaPayloadData((LoRaMessageType)mhbr[0], devAddr, fCtrl, this._fCnt, fopts, fPort, payload, direction);
            // Need to create Fops. If not, then MIC won't be correct
            standardData.Fopts = new byte[0];
            // First encrypt the data
            standardData.PerformEncryption(this.LoRaDevice.AppSKey); 
            // "0A501524F8EA5FCBF9BDB5AD7D126F75");
            // Now we have the full package, create the MIC
            standardData.SetMic(this.LoRaDevice.NwkSKey); 
            // "99D58493D1205B43EFF938F0F66C339E");

            return standardData.GetByteMessage();

        }

        public void Dispose()
        {
            throw new NotImplementedException();
        }
    }
}
