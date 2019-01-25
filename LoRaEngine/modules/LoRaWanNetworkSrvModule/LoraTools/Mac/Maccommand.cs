using LoRaWan;
using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Extensions.Logging;

namespace LoRaTools
{
    public class MacCommandHolder
    {
        public List<GenericMACCommand> macCommand { get; set; }

        /// <summary>
        /// constructor for message from gateway (downstream)
        /// </summary>
        public MacCommandHolder()
        {
            macCommand = new List<GenericMACCommand>();
        }
        public MacCommandHolder(byte input)
        {
            macCommand = new List<GenericMACCommand>();

            CidEnum cid = (CidEnum)input;
            switch (cid)
            {
                case CidEnum.LinkCheckCmd:
                    LinkCheckCmd linkCheck = new LinkCheckCmd();
                    macCommand.Add(linkCheck);
                    break;
                case CidEnum.LinkADRCmd:
                    Logger.Log("mac command detected : LinkADRCmd", LogLevel.Information);
                    break;
                case CidEnum.DutyCycleCmd:
                    DutyCycleCmd dutyCycle = new DutyCycleCmd();
                    macCommand.Add(dutyCycle);
                    break;
                case CidEnum.RXParamCmd:
                    Logger.Log("mac command detected : RXParamCmd", LogLevel.Information);
                    break;
                case CidEnum.DevStatusCmd:
                    Logger.Log("mac command detected : DevStatusCmd", LogLevel.Information);
                    DevStatusCmd devStatus = new DevStatusCmd();
                    macCommand.Add(devStatus);
                    break;
                case CidEnum.NewChannelCmd:
                    //NewChannelReq newChannel = new NewChannelReq();
                    //macCommand.Add(newChannel);
                    break;
                case CidEnum.RXTimingCmd:
                    RXTimingSetupCmd rXTimingSetup = new RXTimingSetupCmd();
                    macCommand.Add(rXTimingSetup);
                    break;
            }
        }


        /// <summary>
        /// constructor for message received (upstream)
        /// </summary>
        /// <param name="input"></param>
        public MacCommandHolder(byte[] input)
        {
            int pointer = 0;
            macCommand = new List<GenericMACCommand>();

            while (pointer < input.Length)
            {
                CidEnum cid = (CidEnum)input[pointer];
                switch (cid)
                {
                    case CidEnum.LinkCheckCmd:
                        Logger.Log("mac command detected : LinkCheckCmd", LogLevel.Information);
                        LinkCheckCmd linkCheck = new LinkCheckCmd();
                        pointer += linkCheck.Length;
                        macCommand.Add(linkCheck);
                        break;
                    case CidEnum.LinkADRCmd:
                        Logger.Log("mac command detected : LinkADRCmd", LogLevel.Information);
                        break;
                    case CidEnum.DutyCycleCmd:
                        Logger.Log("mac command detected : DutyCycleCmd", LogLevel.Information);
                        DutyCycleCmd dutyCycle = new DutyCycleCmd();
                        pointer += dutyCycle.Length;
                        macCommand.Add(dutyCycle);
                        break;
                    case CidEnum.RXParamCmd:
                        Logger.Log("mac command detected : RXParamCmd", LogLevel.Information);
                        break;
                    case CidEnum.DevStatusCmd:
                        Logger.Log("mac command detected : DevStatusCmd", LogLevel.Information);
                        DevStatusCmd devStatus = new DevStatusCmd(input[pointer + 1], input[pointer + 2]);
                        pointer += devStatus.Length;
                        macCommand.Add(devStatus);
                        break;
                    case CidEnum.NewChannelCmd:
                        Logger.Log("mac command detected : NewChannelCmd", LogLevel.Information);
                        NewChannelAns newChannel = new NewChannelAns(Convert.ToBoolean(input[pointer + 1] & 1), Convert.ToBoolean(input[pointer + 1] & 2));
                        pointer += newChannel.Length;
                        macCommand.Add(newChannel);
                        break;
                    case CidEnum.RXTimingCmd:
                        Logger.Log("mac command detected : RXTimingCmd", LogLevel.Information);
                        RXTimingSetupCmd rXTimingSetup = new RXTimingSetupCmd();
                        pointer += rXTimingSetup.Length;
                        macCommand.Add(rXTimingSetup);
                        break;
                }
            }
        }
    }

    public enum CidEnum
    {
        Zero,
        One,
        LinkCheckCmd,
        LinkADRCmd,
        DutyCycleCmd,
        RXParamCmd,
        DevStatusCmd,
        NewChannelCmd,
        RXTimingCmd
    }
    public abstract class GenericMACCommand
    {
        /// <summary>
        /// cid number of 
        /// </summary>
        public CidEnum Cid { get; set; }

        public int Length { get; set; }

        public override abstract string ToString();

        /// <summary>
        /// create
        /// </summary>
        public GenericMACCommand()
        {
        }
        public CidEnum getMacType()
        {
            return Cid;
        }
        public abstract byte[] ToBytes();

    }

    /// <summary>
    /// LinkCheckReq Upstream & LinkCheckAns Downstream
    /// </summary>
    public class LinkCheckCmd : GenericMACCommand
    {
        uint Margin { get; set; }
        uint GwCnt { get; set; }

        /// <summary>
        /// Upstream Constructor
        /// </summary>
        public LinkCheckCmd()
        {
            Length = 1;
            Cid = CidEnum.LinkCheckCmd;
        }

        /// <summary>
        /// Downstream Constructor
        /// </summary>
        /// <param name="_margin"></param>
        /// <param name="_gwCount"></param>
        public LinkCheckCmd(uint _margin, uint _gwCnt)
        {
            Length = 3;
            Cid = CidEnum.LinkCheckCmd;
            Margin = _margin;
            GwCnt = _gwCnt;
        }

        public override byte[] ToBytes()
        {
            byte[] returnedBytes = new byte[Length];
            returnedBytes[0] = (byte)Cid;
            returnedBytes[1] = BitConverter.GetBytes(Margin)[0];
            returnedBytes[2] = BitConverter.GetBytes(GwCnt)[0];
            return returnedBytes;
        }

        public override string ToString()
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// LinkAdrReq & LinkAdrAns TODO REGION SPECIFIC
    /// </summary>
    public class LinkADRCmd : GenericMACCommand
    {

        byte DataRate_TXPower { get; set; }
        byte[] ChMask = new byte[2];
        byte Redondancy { get; set; }

        public override byte[] ToBytes()
        {
            throw new NotImplementedException();
        }

        public override string ToString()
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// DutyCycleAns Upstream & DutyCycleReq Downstream
    /// </summary>
    public class DutyCycleCmd : GenericMACCommand
    {
        uint DutyCyclePL { get; set; }



        //Downstream message
        public DutyCycleCmd(uint _dutyCyclePL)
        {
            Length = 2;
            Cid = CidEnum.DutyCycleCmd;
            DutyCyclePL = _dutyCyclePL;
        }

        public DutyCycleCmd()
        {
            Length = 1;
            Cid = CidEnum.DutyCycleCmd;
        }

        public override byte[] ToBytes()
        {
            throw new NotImplementedException();
        }

        public override string ToString()
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// RXParamSetupReq & RXParamSetupAns TODO Region specific
    /// </summary>
    public class RXParamSetupCmd : GenericMACCommand
    {
        byte DLSettings { get; set; }
        byte[] Frequency = new byte[3];

        public override byte[] ToBytes()
        {
            throw new NotImplementedException();
        }

        public override string ToString()
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// DevStatusAns Upstream & DevStatusReq Downstream
    /// </summary>
    public class DevStatusCmd : GenericMACCommand
    {
        public uint Battery { get; set; }
        public int Margin { get; set; }

        /// <summary>
        /// Upstream Constructor
        /// </summary>
        public DevStatusCmd()
        {
            Length = 1;
            Cid = CidEnum.DevStatusCmd;
        }

        public override string ToString()
        {

            return String.Format("Battery Level : {0}, Margin : {1}", Battery, Margin);
        }

        /// <summary>
        /// Upstream constructor
        /// </summary>
        /// <param name="_battery"></param>
        /// <param name="_margin"></param>
        public DevStatusCmd(uint _battery, int _margin)
        {
            Length = 3;
            Battery = _battery;
            Margin = _margin;
            Cid = CidEnum.DevStatusCmd;
        }

        public override byte[] ToBytes()
        {
            byte[] returnedBytes = new byte[Length];
            returnedBytes[0] = (byte)Cid;
            return returnedBytes;
        }
    }

    /// <summary>
    /// NewChannelReq & NewChannelAns TODO REGION SPECIFIC
    /// </summary>
    public abstract class NewChannelCmd : GenericMACCommand
    {
    }

    /// <summary>
    /// Both ways
    /// </summary>
    public class NewChannelReq : NewChannelCmd
    {
        uint ChIndex { get; set; }
        uint Freq { get; set; }
        uint MaxDR { get; set; }
        uint MinDR { get; set; }

        public NewChannelReq(uint _chIndex, uint _freq, uint _maxDr, uint _minDr)
        {
            Length = 4;
            ChIndex = _chIndex;
            Freq = _freq;
            MaxDR = _maxDr;
            MinDR = _minDr;
            Cid = CidEnum.NewChannelCmd;

        }

        public override byte[] ToBytes()
        {
            throw new NotImplementedException();
        }

        public override string ToString()
        {
            throw new NotImplementedException();
        }
    }
    public class NewChannelAns : NewChannelCmd
    {
        bool DRRangeOk { get; set; }
        bool ChanFreqOk { get; set; }

        public NewChannelAns(bool _drRangeOk, bool _chanFreqOk)
        {
            Length = 2;
            DRRangeOk = _drRangeOk;
            ChanFreqOk = _chanFreqOk;
            Cid = CidEnum.NewChannelCmd;


        }

        public override byte[] ToBytes()
        {
            throw new NotImplementedException();
        }

        public override string ToString()
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// RXTimingSetupAns Upstream & RXTimingSetupReq Downstream
    /// </summary>
    public class RXTimingSetupCmd : GenericMACCommand
    {
        uint Delay { get; set; }

        public RXTimingSetupCmd(uint _delay)
        {
            Length = 2;
            Cid = CidEnum.RXTimingCmd;
            Delay = _delay;
        }

        public RXTimingSetupCmd()
        {
            Length = 1;
            Cid = CidEnum.RXTimingCmd;
        }

        public override byte[] ToBytes()
        {
            throw new NotImplementedException();
        }

        public override string ToString()
        {
            throw new NotImplementedException();
        }
    }

}
