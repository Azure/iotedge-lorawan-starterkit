using LoRaTools;
using LoRaTools.LoRaMessage;
using LoRaTools.LoRaPhysical;
using LoRaTools.Utils;
using LoRaWan;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace LoRaWan.IntegrationTest
{
    class SimulatedDevice
    {
        public TestDeviceInfo LoRaDevice { get; internal set; }
        public int FrmCntUp { get; set; }
        public int FrmCntDown { get; set; }

        private byte[] fCnt = new byte[2];
        public PhysicalPayload LastPayload { get; set; }
        public string DevNonce { get; private set; }
        public bool IsJoined => !string.IsNullOrEmpty(LoRaDevice.DevAddr);
        public string NetId { get; internal set; }
        public string AppNonce { get; internal set; }

        
        SemaphoreSlim joinFinished;

        private bool isFirstJoinRequest = true;
        public SimulatedDevice(TestDeviceInfo testDeviceInfo, int frmCntDown = 0, int frmCntUp = 0)
        {
            this.LoRaDevice = testDeviceInfo;

            FrmCntDown = frmCntDown;
            FrmCntUp = frmCntUp;
        
            //can store 32 bit but only 16 are sent
            var fcnt32 = BitConverter.GetBytes(FrmCntDown);
            fCnt[0] = fcnt32[0];
            fCnt[1] = fcnt32[1];

            if (!this.IsJoined)
                this.joinFinished = new SemaphoreSlim(0);
        }

        byte[] CreateJoinRequest()
        {
            //create a join request
            byte[] AppEUI = ConversionHelper.StringToByteArray(LoRaDevice.AppEUI);
            Array.Reverse(AppEUI);
            byte[] DevEUI = ConversionHelper.StringToByteArray(LoRaDevice.DeviceID);
            Array.Reverse(DevEUI);

            byte[] devNonce = new byte[2];
            if ((string.IsNullOrEmpty(this.DevNonce)) || (!isFirstJoinRequest))
            {                               
                Random random = new Random();
                // DevNonce[0] = 0xC8; DevNonce[1] = 0x86;
                random.NextBytes(devNonce);   
                this.DevNonce = BitConverter.ToString(devNonce).Replace("-", "");
                Array.Reverse(devNonce);
                isFirstJoinRequest = false;
            }
            else
            {
                devNonce = ConversionHelper.StringToByteArray(this.DevNonce);
                Array.Reverse(devNonce);
            }

            TestLogger.Log($"[{LoRaDevice.DeviceID}] Join request sent DevNonce: {BitConverter.ToString(devNonce).Replace("-","")} / {this.DevNonce}");
            var join = new LoRaPayloadJoinRequest(AppEUI, DevEUI, devNonce);
            join.SetMic(this.LoRaDevice.AppKey);
            
            return join.GetByteMessage();
        }

        byte[] CreateUnconfirmedDataUpMessage(string data, byte fport = 1)
        {
            byte[] mhbr = new byte[] { 0x40 };
            byte[] devAddr = ConversionHelper.StringToByteArray(LoRaDevice.DevAddr);
            Array.Reverse(devAddr);
            byte[] fCtrl = new byte[] { 0x80 };
            // byte[] _FCnt = new byte[] { 0x00, 0x00 };
            fCnt[0]++;
            byte[] fopts = null;
            byte[] fPort = new byte[] { fport };           
            TestLogger.Log($"{LoRaDevice.DeviceID}: Simulated data: {data}");
            byte[] payload = Encoding.UTF8.GetBytes(data);
            Array.Reverse(payload);
            // 0 = uplink, 1 = downlink
            int direction = 0;
            LoRaPayloadData standardData = new LoRaPayloadData((LoRaPayloadData.MType)mhbr[0], devAddr, fCtrl, fCnt, fopts, fPort, payload, direction);
            // Need to create Fops. If not, then MIC won't be correct
            standardData.Fopts = new byte[0];
            // First encrypt the data
            standardData.PerformEncryption(LoRaDevice.AppSKey); //"0A501524F8EA5FCBF9BDB5AD7D126F75");
            // Now we have the full package, create the MIC
            standardData.SetMic(LoRaDevice.NwkSKey); //"99D58493D1205B43EFF938F0F66C339E");

            return standardData.GetByteMessage();

        }

        // Sends unconfirmed message
        public async Task SendUnconfirmedMessageAsync(SimulatedPacketForwarder simulatedPacketForwarder, string payload)
        {
            var token = await RandomTokenGenerator.GetTokenAsync();
            this.LastPayload = new PhysicalPayload(token, PhysicalIdentifier.PUSH_DATA, null);
            var header = this.LastPayload.GetSyncHeader(simulatedPacketForwarder.MacAddress);

            var unconfirmedMessage = this.CreateUnconfirmedDataUpMessage(payload);
            await simulatedPacketForwarder.SendAsync(header, unconfirmedMessage);

            TestLogger.Log($"[{this.LoRaDevice.DeviceID}] Unconfirmed data: {BitConverter.ToString(header).Replace("-", "")} {payload}");

            //TestLogger.Log($"[{this.LoRaDevice.DevAddr}] Sending data: {BitConverter.ToString(header).Replace("-", "")}{Encoding.UTF8.GetString(gatewayInfo)}");
        }

        // Performs join
        public async Task<bool> JoinAsync(SimulatedPacketForwarder packetForwarder, int timeoutInMs = 30 * 1000)
        {
            if (this.IsJoined)
                return true;

            var token = await RandomTokenGenerator.GetTokenAsync();
            this.LastPayload = new PhysicalPayload(token, PhysicalIdentifier.PUSH_DATA, null);
            var header = this.LastPayload.GetSyncHeader(packetForwarder.MacAddress);

            var joinRequest = this.CreateJoinRequest();
            var joinCompleted = new SemaphoreSlim(0);
            packetForwarder.SubscribeOnce(token, PhysicalIdentifier.PULL_RESP, (response) => {
                var txpk=  Txpk.CreateTxpk(response, this.LoRaDevice.AppKey);
                // handle join
                var loraMessage = new LoRaMessageWrapper(txpk, this.LoRaDevice.AppKey);
                if (loraMessage.LoRaMessageType == LoRaMessageType.JoinAccept)
                {
                    this.HandleJoinAccept(loraMessage);
                    joinCompleted.Release();
                }
            });

            await packetForwarder.SendAsync(header, joinRequest);
            TestLogger.Log($"[{this.LoRaDevice.DeviceID}] Join request: {BitConverter.ToString(header).Replace("-", "")}");

            return await joinCompleted.WaitAsync(timeoutInMs);
        }


        void HandleJoinAccept(LoRaMessageWrapper loraMessage)
        {
            var payload = (LoRaPayloadJoinAccept)loraMessage.LoRaPayloadMessage;
            // TODO Need to check if the time is not passed 

            // Calculate the keys
            var netid = payload.NetID.ToArray();
            Array.Reverse(netid);
            var appNonce = payload.AppNonce.ToArray();
            Array.Reverse(appNonce);
            var devNonce = ConversionHelper.StringToByteArray(this.DevNonce);
            Array.Reverse(devNonce);
            var deviceAppKey = ConversionHelper.StringToByteArray(this.LoRaDevice.AppKey);
            var appSKey = payload.CalculateKey(LoRaPayload.KeyType.AppSKey, appNonce, netid, devNonce, deviceAppKey);
            this.LoRaDevice.AppSKey = BitConverter.ToString(appSKey).Replace("-", "");
            var nwkSKey = payload.CalculateKey(LoRaPayload.KeyType.NwkSKey, appNonce, netid, devNonce, deviceAppKey);
            this.LoRaDevice.NwkSKey = BitConverter.ToString(nwkSKey).Replace("-", "");
            this.NetId = BitConverter.ToString(netid).Replace("-", "");
            this.AppNonce = BitConverter.ToString(appNonce).Replace("-", "");
            var devAdd = payload.DevAddr;
            //Array.Reverse(devAdd);
            this.LoRaDevice.DevAddr = BitConverter.ToString(devAdd.ToArray()).Replace("-", "");
        }
    }
}
