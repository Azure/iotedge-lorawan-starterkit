using System;
using System.Collections.Generic;
using System.Text;

namespace LoRaTools.LoRaPhysical
{
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
        PktFwdType pktFwdType;

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

        public DownlinkPktFwdMessage(string _data, string _datr = "SF12BW125", uint _rfch = 0, double _freq = 869.525000, long _tmst = 0)
        {
            var byteData = Convert.FromBase64String(_data);
            txpk = new Txpk()
            {
                imme = _tmst == 0 ? true : false,
                tmst = _tmst,
                data = _data,
                size = (uint)byteData.Length,
                freq = _freq,
                rfch = _rfch,
                modu = "LORA",
                datr = _datr,
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

        public override PktFwdMessageAdapter GetPktFwdMessage()
        {
            PktFwdMessageAdapter pktFwdMessageAdapter = new PktFwdMessageAdapter();
            pktFwdMessageAdapter.Rxpks = this.rxpk;
            return pktFwdMessageAdapter;
        }
    }
}
