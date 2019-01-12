using LoRaTools;
using LoRaTools.LoRaMessage;
using LoRaWan;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Text;

namespace LoRaSimulator
{
    class SimulatedDevice : IDisposable
    {
        public LoRaDevice LoRaDevice { get; internal set; }
        public int Interval { get; set; }
        public int FrmCntUp { get; set; }
        public int FrmCntDown { get; set; }

        private byte[] _FCnt = new byte[2];
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
                LoRaDevice = new LoRaDevice(json);
                var additional = JsonConvert.DeserializeObject<JObject>(json);
                Interval = GetProperty(additional,"Interval");
                FrmCntDown = GetProperty(additional, "FrmCntDown");
                FrmCntUp = GetProperty(additional, "FrmCntUp");
                RandomInterval = GetProperty(additional, "RandomInterval");
                GroupRxpk = GetProperty(additional, "GroupRxpk");
                //can store 32 bit but only 16 are sent
                var fcnt32 = BitConverter.GetBytes(FrmCntDown);
                _FCnt[0] = fcnt32[0];
                _FCnt[1] = fcnt32[1];
                IncrementalData = 0;
            }
            catch (Exception)
            {
                if (LoRaDevice == null)
                    LoRaDevice = new LoRaDevice();
            }
        }

        private int GetProperty(JObject additional, string propname)
        {
            var ret = additional[propname];
            if (ret == null)
                return 0;
            return ret.Value<int>();
        }

        public byte[] GetJoinRequest()
        {
            //create a join request
            byte[] AppEUI = LoRaDevice.GetAppEUI();
            Array.Reverse(AppEUI);
            byte[] DevEUI = LoRaDevice.GetDevEUI();
            Array.Reverse(DevEUI);

            byte[] DevNonce = new byte[2];
            if ((LoRaDevice.DevNonce == "") || (!isFirstJoinRequest))
            {                               
                Random random = new Random();
                // DevNonce[0] = 0xC8; DevNonce[1] = 0x86;
                random.NextBytes(DevNonce);                
                LoRaDevice.DevNonce = BitConverter.ToString(DevNonce).Replace("-", "");
                Array.Reverse(DevNonce);
                isFirstJoinRequest = false;
            }
            else
            {
                DevNonce = LoRaDevice.GetDevNonce();
                Array.Reverse(DevNonce);
            }

            Logger.Log(LoRaDevice.DevEUI, $"Join request sent DevNonce: {BitConverter.ToString(DevNonce).Replace("-","")}", Logger.LoggingLevel.Always);
            var join = new LoRaPayloadJoinRequest(AppEUI, DevEUI, DevNonce);
            join.SetMic(LoRaDevice.AppKey);
            
            return join.GetByteMessage();
        }

        public byte[] GetUnconfirmedDataUpMessage()
        {
            byte[] _mhbr = new byte[] { 0x40 };
            byte[] _devAddr = LoRaDevice.GetDevAddr();
            Array.Reverse(_devAddr);
            byte[] _FCtrl = new byte[] { 0x80 };
            // byte[] _FCnt = new byte[] { 0x00, 0x00 };
            _FCnt[0]++;
            byte[] _Fopts = null;
            byte[] _FPort = new byte[] { 0x01 };
            // Creating a random number
            //Random random = new Random();
            //int temp = random.Next(-50, 70);
            IncrementalData++;
            Logger.Log(LoRaDevice.DevEUI, $"Simulated data: {IncrementalData.ToString()}", Logger.LoggingLevel.Always);
            byte[] _payload = Encoding.ASCII.GetBytes(IncrementalData.ToString());
            Array.Reverse(_payload);
            // 0 = uplink, 1 = downlink
            int direction = 0;
            LoRaPayloadData standardData = new LoRaPayloadData((LoRaMessageType)_mhbr[0], _devAddr, _FCtrl, _FCnt, _Fopts, _FPort, _payload, direction);
            // Need to create Fops. If not, then MIC won't be correct
            standardData.Fopts = new byte[0];
            // First encrypt the data
            standardData.PerformEncryption(LoRaDevice.AppSKey); //"0A501524F8EA5FCBF9BDB5AD7D126F75");
            // Now we have the full package, create the MIC
            standardData.SetMic(LoRaDevice.NwkSKey); //"99D58493D1205B43EFF938F0F66C339E");

            return standardData.GetByteMessage();

        }

        public void Dispose()
        {
            throw new NotImplementedException();
        }
    }
}
