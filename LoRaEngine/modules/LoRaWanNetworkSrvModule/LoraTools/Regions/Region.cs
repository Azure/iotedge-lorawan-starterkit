using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace LoRaTools.Regions
{
    public enum RegionEnum { EU, US }

    public class Region
    {
        public RegionEnum RegionEnum { get; set; }
        public byte LoRaSyncWord { get; private set; }
        public byte[] GFSKSyncWord { get; private set; }
        /// <summary>
        /// Datarate to configuration and max payload size (M)
        /// max application payload size N should be N= M-8 bytes.
        /// This is in case of absence of Fopts field.
        /// </summary>
        public Dictionary<uint, (string configuration, uint maxPyldSize)> DRtoConfiguration { get; set; } = new Dictionary<uint, (string, uint)>();
        /// <summary>
        /// By default MaxEIRP is considered to be +16dBm. 
        /// If the end-device cannot achieve 16dBm EIRP, the Max EIRP SHOULD be communicated to the network server using an out-of-band channel during the end-device commissioning process.
        /// </summary>
        public Dictionary<uint, string> TXPowertoMaxEIRP { get; set; } = new Dictionary<uint, string>();
        /// <summary>
        /// Table to the get receive windows Offsets.
        /// X = RX1DROffset Upstream DR
        /// Y = Downstream DR in RX1 slot
        /// </summary>
        public int[,] RX1DROffsetTable { get; set; }


        /// <summary>
        /// Default parameters for the RX2 receive Windows, This windows use a fix frequency and Data rate.
        /// </summary>
        public (double frequency, uint dr) RX2DefaultReceiveWindows { get; set; }

        /// <summary>
        /// Default first receive windows. [sec]
        /// </summary>
        public uint receive_delay1 { get; set; }
        /// <summary>
        /// Default second receive Windows. Should be receive_delay1+1 [sec].
        /// </summary>
        public uint receive_delay2 { get; set; }
        /// <summary>
        /// Default Join Accept Delay for first Join Accept Windows.[sec]
        /// </summary>
        public uint join_accept_delay1 { get; set; }
        /// <summary>
        /// Default Join Accept Delay for second Join Accept Windows. [sec]
        /// </summary>
        public uint join_accept_delay2 { get; set; }
        /// <summary>
        /// max fcnt gap between expected and received. [#frame]
        /// If this difference is greater than the value of MAX_FCNT_GAP then too many data frames have been lost then subsequent will be discarded
        /// </summary>
        public int max_fcnt_gap { get; set; }
        /// <summary>
        /// Number of uplink an end device can send without asking for an ADR acknowledgement request (set ADRACKReq bit to 1). [#frame]
        /// </summary>
        public uint adr_ack_limit { get; set; }
        /// <summary>
        /// Number of frames in which the network is required to respond to a ADRACKReq request. [#frame]
        /// If no response, during time select a lower data rate.
        /// </summary>
        public uint adr_adr_delay { get; set; }
        /// <summary>
        /// timeout for ack transmissiont, tuple with (min,max). Value should be a delay between min and max. [sec, sec]
        /// </summary>
        public (uint min, uint max) ack_timeout { get; set; }

        public Region(RegionEnum regionEnum, byte loRaSyncWord, byte[] gFSKSyncWord, (double frequency, uint datr) rX2DefaultReceiveWindows, uint receive_delay1, uint receive_delay2, uint join_accept_delay1, uint join_accept_delay2, int max_fcnt_gap, uint adr_ack_limit, uint adr_adr_delay, (uint min, uint max) ack_timeout)
        {
            this.RegionEnum = regionEnum;

            LoRaSyncWord = loRaSyncWord;
            GFSKSyncWord = gFSKSyncWord;

            RX2DefaultReceiveWindows = rX2DefaultReceiveWindows;
            this.receive_delay1 = receive_delay1;
            this.receive_delay2 = receive_delay2;
            this.join_accept_delay1 = join_accept_delay1;
            this.join_accept_delay2 = join_accept_delay2;
            this.max_fcnt_gap = max_fcnt_gap;
            this.adr_ack_limit = adr_ack_limit;
            this.adr_adr_delay = adr_adr_delay;
            this.ack_timeout = ack_timeout;
        }


        /// <summary>
        /// Implement correct logic to get the correct transmission frequency based on the region.
        /// </summary>
        /// <param name="upstreamChannel">the channel at which the message was transmitted</param>
        /// <returns></returns>
        public double GetDownstreamChannel(Rxpk upstreamChannel)
        {
            if (this.RegionEnum == RegionEnum.EU)
            {
                //in case of EU, you respond on same frequency as you sent data.
                return upstreamChannel.freq;
            }
            else if (this.RegionEnum == RegionEnum.US)
            {
                int channelNumber;
                if (upstreamChannel.datr == "SF8BW500") //==DR4
                {
                    channelNumber = 64 + (int)((upstreamChannel.freq - 903) / 1.6);
                }
                else //if not DR4 other encoding
                {
                    channelNumber = (int)((upstreamChannel.freq - 902.3) / 0.2);
                }

                return 923.3 + (channelNumber % 8) * 0.6;
            }
            return 0;
        }
     



       
    }
}
