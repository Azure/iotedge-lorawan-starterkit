using LoRaTools.Regions;
using System;
using System.Collections.Generic;
using System.Text;

namespace LoRaTools.LoRaPhysical
{
    [Obsolete("This class will be faded out in the next versions, please use DownlinkPktFwdMessage or UplinkPktFwdMessage instead.")]
    public class PktFwdMessageAdapter
    {
        public List<Rxpk> Rxpks { get; set; }
        public Txpk Txpk { get; set; }


    }
    /// <summary>
    /// Base type of a Packet Forwarder message (lower level)
    /// </summary>
    public abstract class PktFwdMessage
    {
        public abstract PktFwdMessageAdapter GetPktFwdMessage();
        enum PktFwdType
        {
            Downlink,
            Uplink
        }
    }

    /// <summary>
    /// JSON of a Downlink message for the Packet forwarder.
    /// </summary>
    public class DownlinkPktFwdMessage : PktFwdMessage
    {
        public Txpk txpk;

        public DownlinkPktFwdMessage()
        {

        }

        [Obsolete("This constructor will be faded out at message processor refactory")]
        public DownlinkPktFwdMessage(string data, string datr = "SF12BW125", uint rfch = 0, double freq = 869.525000, long tmst = 0)
        {
            var byteData = Convert.FromBase64String(data);
            txpk = new Txpk()
            {
                imme = tmst == 0 ? true : false,
                tmst = tmst,
                data = data,
                size = (uint)byteData.Length,
                freq = freq,
                rfch = rfch,
                modu = "LORA",
                datr = datr,
                codr = "4/5",
                //TODO put 14 for EU
                powe = 14,
                ipol = true

            };
        }

        /// <summary>
        /// This method is used in case of a response to a upstream message.
        /// </summary>
        /// <param name="LoraMessage">the serialized LoRa Message.</param>
        /// <returns>DownlinkPktFwdMessage object ready to be sent</returns>
        public DownlinkPktFwdMessage(byte[] loRaData, string datr, double freq, long tmst = 0)
        {          
            txpk = new Txpk()
            {
                imme = tmst == 0 ? true : false,
                tmst = tmst,
                data = Convert.ToBase64String(loRaData),
                size = (uint)loRaData.Length,
                freq = freq,
                //TODO check this,
                rfch = 0,
                modu = "LORA",
                datr = datr,
                codr = "4/5",
                //TODO put 14 for EU               
                powe = 14,
                ipol = true
            };
        }


        public override PktFwdMessageAdapter GetPktFwdMessage()
        {
            PktFwdMessageAdapter pktFwdMessageAdapter = new PktFwdMessageAdapter();
            pktFwdMessageAdapter.Txpk = this.txpk;
            return pktFwdMessageAdapter;
        }
    }
    /// <summary>
    /// an uplink Json for the packet forwarder.
    /// </summary>
    public class UplinkPktFwdMessage : PktFwdMessage
    {
        public List<Rxpk> rxpk = new List<Rxpk>();

        public UplinkPktFwdMessage(Rxpk rxpkInput){
            rxpk= new List<Rxpk>(){
                rxpkInput
            };
        }


        /// <summary>
        /// This method is used in case of a request to a upstream message.
        /// </summary>
        /// <param name="LoraMessage">the serialized LoRa Message.</param>
        /// <returns>UplinkPktFwdMessage object ready to be sent</returns>
        public UplinkPktFwdMessage(byte[] loRaData, string datr, double freq, uint tmst = 0)
        {
            // This is a new ctor, must be validated by MIK
            rxpk = new List<Rxpk>() {
                new Rxpk()
                {                   
                    tmst = tmst,
                    data = Convert.ToBase64String(loRaData),
                    size = (uint)loRaData.Length,
                    freq = freq,
                    //TODO check this,
                    rfch = 1,
                    modu = "LORA",
                    datr = datr,
                    codr = "4/5"
                }
            };            
        }

        public override PktFwdMessageAdapter GetPktFwdMessage()
        {
            PktFwdMessageAdapter pktFwdMessageAdapter = new PktFwdMessageAdapter();
            pktFwdMessageAdapter.Rxpks = this.rxpk;
            return pktFwdMessageAdapter;
        }
    }
}