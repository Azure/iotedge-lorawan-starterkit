using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace LoRaSimulator
{
    public class LoRaDevice
    {
        // Used when the device is actually joined
        public string DevAddr { get; set; }
        // Next 3 ones used for OTAA
        public string DevEUI { get; set; }
        public string AppKey { get; set; }
        public string AppEUI { get; set; }
        // Keys are needed for any joined request
        public string NwkSKey { get; set; }
        public string AppSKey { get; set; }
        // AppNonce and DevNonce uniquement pour join request
        // Nonce are used to make sure we are who we are
        public string AppNonce { get; set; }
        public string DevNonce { get; set; }
        // the NetworkId. Always 001 for tests
        public string NetId { get; set; }
        // Are we joined?
        public bool IsJoined { get { return (DevAddr != "") ? true : false; } }

        public LoRaDevice()
        {
            // Nothing to do in the default constructor
        }
        // Constructor to populate properties thru a json
        public LoRaDevice(string json)
        {
            try
            {
                var ret = JsonConvert.DeserializeObject<LoRaDevice>(json);
                DevAddr = ret.DevAddr;
                AppEUI = ret.AppEUI;
                DevEUI = ret.DevEUI;
                AppKey = ret.AppKey;
                NwkSKey = ret.NwkSKey;
                AppSKey = ret.AppSKey;
                AppNonce = ret.AppNonce;
                DevNonce = ret.DevNonce;
                NetId = ret.NetId;
            }
            catch (Exception)
            {
                throw;
            }
            
        }

        //get the correct byte arrays for all properties
        private byte[] StringToByteArray(string hex)
        {
            return Enumerable.Range(0, hex.Length)
                             .Where(x => x % 2 == 0)
                             .Select(x => Convert.ToByte(hex.Substring(x, 2), 16))
                             .ToArray();
        }

        public byte[] GetDevAddr()
        { return StringToByteArray(DevAddr); }
        public byte[] GetDevEUI()
        { return StringToByteArray(DevEUI); }
        public byte[] GetAppEUI()
        { return StringToByteArray(AppEUI); }
        public byte[] GetAppSKey()
        { return StringToByteArray(AppSKey); }
        public byte[] GetAppNonce()
        { return StringToByteArray(AppNonce); }
        public byte[] GetAppKey()
        { return StringToByteArray(AppKey); }
        public byte[] GetDevNonce()
        { return StringToByteArray(DevNonce); }
        public byte[] GetNetId()
        { return StringToByteArray(NetId); }
    }
}
