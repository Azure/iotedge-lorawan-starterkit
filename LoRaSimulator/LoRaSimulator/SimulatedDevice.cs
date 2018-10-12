using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PacketManager;
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

        private byte[] _FCnt;

        public SimulatedDevice(string json)
        {
            try
            {
                LoRaDevice = new LoRaDevice(json);
                var additional = JsonConvert.DeserializeObject<JObject>(json);
                Interval = (int)additional["Interval"];
                FrmCntDown = (int)additional["FrmCntDown"];
                FrmCntUp = (int)additional["FrmCntUp"];
                _FCnt = BitConverter.GetBytes(FrmCntDown);
            }
            catch (Exception)
            {
                if (LoRaDevice == null)
                    LoRaDevice = new LoRaDevice();
            }
        }

        public string GetJoinRequest()
        {
            // TODO Laurent: add the join request creation here
            return "";
        }

        public string GetUnconfirmedDataUpMessage()
        {
            byte[] _mhbr = new byte[] { 0x40 };
            byte[] _devAddr = LoRaDevice.GetDevAddr();  //new byte[] { 0xAE, 0x13, 0x04, 0x26 };
            Array.Reverse(_devAddr);
            byte[] _FCtrl = new byte[] { 0x80 };
            // byte[] _FCnt = new byte[] { 0x00, 0x00 };
            _FCnt[0]++;
            byte[] _Fopts = null;
            byte[] _FPort = new byte[] { 0x01 };

            Random random = new Random();
            int temp = random.Next(-50, 70);
            byte[] _payload = Encoding.Default.GetBytes(temp.ToString());
            // 0 = uplink, 1 = downlink
            int direction = 0;
            LoRaPayloadStandardData standardData = new LoRaPayloadStandardData(_mhbr, _devAddr, _FCtrl, _FCnt, _Fopts, _FPort, _payload, direction);
            // Need to create Fops. If not, then MIC won't be correct
            standardData.Fopts = new byte[0];
            // First encrypt the data
            standardData.PerformEncryption(LoRaDevice.AppSKey); //"0A501524F8EA5FCBF9BDB5AD7D126F75");
            // Now we have the full package, create the MIC
            standardData.SetMic(LoRaDevice.NwkSKey); //"99D58493D1205B43EFF938F0F66C339E");

            return Convert.ToBase64String(standardData.ToMessage());

        }

        public void Dispose()
        {
            throw new NotImplementedException();
        }
    }
}
