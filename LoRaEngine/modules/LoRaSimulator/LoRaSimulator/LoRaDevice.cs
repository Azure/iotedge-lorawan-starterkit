namespace LoRaSimulator
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using Newtonsoft.Json;

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
        public bool IsJoined { get { return (this.DevAddr != string.Empty) ? true : false; } }

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
                this.DevAddr = ret.DevAddr;
                this.AppEUI = ret.AppEUI;
                this.DevEUI = ret.DevEUI;
                this.AppKey = ret.AppKey;
                this.NwkSKey = ret.NwkSKey;
                this.AppSKey = ret.AppSKey;
                this.AppNonce = ret.AppNonce;
                this.DevNonce = ret.DevNonce;
                this.NetId = ret.NetId;
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
        { return this.StringToByteArray(this.DevAddr); }

        public byte[] GetDevEUI()
        { return this.StringToByteArray(this.DevEUI); }

        public byte[] GetAppEUI()
        { return this.StringToByteArray(this.AppEUI); }

        public byte[] GetAppSKey()
        { return this.StringToByteArray(this.AppSKey); }

        public byte[] GetAppNonce()
        { return this.StringToByteArray(this.AppNonce); }

        public byte[] GetAppKey()
        { return this.StringToByteArray(this.AppKey); }

        public byte[] GetDevNonce()
        { return this.StringToByteArray(this.DevNonce); }

        public byte[] GetNetId()
        { return this.StringToByteArray(this.NetId); }
    }
}
