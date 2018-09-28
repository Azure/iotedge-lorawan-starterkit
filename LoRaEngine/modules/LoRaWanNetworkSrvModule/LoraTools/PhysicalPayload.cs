using LoRaWan;
using System;
using System.Collections.Generic;
using System.Text;

namespace LoRaTools
{
  
    public enum PhysicalIdentifier
    {
        PUSH_DATA, PUSH_ACK, PULL_DATA, PULL_RESP, PULL_ACK, TX_ACK
    }

    /// <summary>
    /// The Physical Payload wrapper
    /// </summary>
    public class PhysicalPayload
    {

        //case of inbound messages
        public PhysicalPayload(byte[] input)
        {

            protocolVersion = input[0];
            Array.Copy(input, 1, token, 0, 2);
            identifier = (PhysicalIdentifier)input[3];

            //PUSH_DATA That packet type is used by the gateway mainly to forward the RF packets received, and associated metadata, to the server
            if (identifier == PhysicalIdentifier.PUSH_DATA)
            {
                Array.Copy(input, 4, gatewayIdentifier, 0, 8);
                message = new byte[input.Length - 12];
                Array.Copy(input, 12, message, 0, input.Length - 12);
            }

            //PULL_DATA That packet type is used by the gateway to poll data from the server.
            if (identifier == PhysicalIdentifier.PULL_DATA)
            {
                Array.Copy(input, 4, gatewayIdentifier, 0, 8);
            }

            //TX_ACK That packet type is used by the gateway to send a feedback to the to inform if a downlink request has been accepted or rejected by the gateway.
            if (identifier == PhysicalIdentifier.TX_ACK)
            {
                Logger.Log($"Tx ack recieved from gateway", Logger.LoggingLevel.Info);
                Array.Copy(input, 4, gatewayIdentifier, 0, 8);
                if (input.Length - 12 > 0)
                {
                    message = new byte[input.Length - 12];
                    Array.Copy(input, 12, message, 0, input.Length - 12);
                }
            }
        }

        //downlink transmission
        public PhysicalPayload(byte[] _token, PhysicalIdentifier type, byte[] _message)
        {
            //0x01 PUSH_ACK That packet type is used by the server to acknowledge immediately all the PUSH_DATA packets received.
            //0x04 PULL_ACK That packet type is used by the server to confirm that the network route is open and that the server can send PULL_RESP packets at any time.
            if (type == PhysicalIdentifier.PUSH_ACK || type == PhysicalIdentifier.PULL_ACK)
            {
                token = _token;
                identifier = type;
            }

            //0x03 PULL_RESP That packet type is used by the server to send RF packets and  metadata that will have to be emitted by the gateway.
            if (type == PhysicalIdentifier.PULL_RESP)
            {
                token = _token;
                identifier = type;
                message = new byte[_message.Length];
                Array.Copy(_message, 0, message, 0, _message.Length);

            }

        }

        //1 byte
        public byte protocolVersion = 2;
        //1-2 bytes
        public byte[] token = new byte[2];
        //1 byte
        public PhysicalIdentifier identifier;
        //8 bytes
        public byte[] gatewayIdentifier = new byte[8];
        //0-unlimited
        public byte[] message;

        public byte[] GetMessage()
        {
            List<byte> returnList = new List<byte>();
            returnList.Add(protocolVersion);
            returnList.AddRange(token);
            returnList.Add((byte)identifier);
            if (identifier == PhysicalIdentifier.PULL_DATA ||
                identifier == PhysicalIdentifier.TX_ACK ||
                identifier == PhysicalIdentifier.PUSH_DATA
                )
                returnList.AddRange(gatewayIdentifier);
            if (message != null)
                returnList.AddRange(message);
            return returnList.ToArray();
        }
    }
    public class Txpk
    {
        public bool imme;
        public string data;
        public long tmst;
        public uint size;
        public double freq; //868
        public uint rfch;
        public string modu;
        public string datr;
        public string codr;
        public uint powe;
        public bool ipol;
    }

    public class Rxpk
    {
        public string time;
        public uint tmms;
        public uint tmst;
        public double freq; //868
        public uint chan;
        public uint rfch;
        public int stat;
        public string modu;
        public string datr;
        public string codr;
        public int rssi;
        public float lsnr;
        public uint size;
        public string data;

        /// <summary>
        /// Required Signal-to-noise ratio to demodulate a LoRa signal given a spread Factor
        /// Spreading Factor -> Required SNR
        /// taken from https://www.semtech.com/uploads/documents/DS_SX1276-7-8-9_W_APP_V5.pdf
        /// </summary>
        private Dictionary<int, double> SpreadFactorToSNR = new Dictionary<int, double>()
         {
            { 6,  -5 },
            { 7, -7.5 },
            {8,  -10 },
            {9, -12.5 },
            {10, -15 },
            {11, -17.5 },
            {12, -20 }
        };

        /// <summary>
        /// Get the modulation margin for MAC Commands LinkCheck
        /// </summary>
        /// <param name="input">the input physical rxpk from the packet</param>
        /// <returns></returns>
        public uint GetModulationMargin()
        {
            //required SNR:
            var requiredSNR = SpreadFactorToSNR[int.Parse(datr.Substring(datr.IndexOf("SF") + 2, datr.IndexOf("BW") - 1 - datr.IndexOf("SF") + 2))];
            //get the minimum
            uint margin = (uint)(lsnr - requiredSNR);
            if (margin < 0)
                margin = 0;
            return margin;
        }
    }


    #region PacketForwarder



    /// <summary>
    /// Base type of a Packet Forwarder message (lower level)
    /// </summary>
    public abstract class PktFwdMessage
    {
        PktFwdType pktFwdType;
    }


    enum PktFwdType
    {
        Downlink,
        Uplink
    }

    /// <summary>
    /// JSON of a Downlink message for the Packet forwarder.
    /// </summary>
    public class DownlinkPktFwdMessage : PktFwdMessage
    {
        public Txpk txpk;




        public DownlinkPktFwdMessage(string _data, string _datr = "SF12BW125", uint _rfch=0, double _freq = 869.525000, long _tmst = 0 )
        {
            var byteData = Convert.FromBase64String(_data);
            txpk = new Txpk()
            {
                imme = _tmst==0?true:false,
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
    }


    /// <summary>
    /// an uplink Json for the packet forwarder.
    /// </summary>
    public class UplinkPktFwdMessage : PktFwdMessage
    {
        public List<Rxpk> rxpk = new List<Rxpk>();
    }


    #endregion
}
