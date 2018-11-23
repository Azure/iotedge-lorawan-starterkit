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
