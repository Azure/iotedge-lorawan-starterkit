// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaTools
{
    using System;
    using System.Collections.Generic;
    using LoRaWan;
    using Microsoft.Extensions.Logging;

    public class MacCommandHolder
    {
        public List<GenericMACCommand> MacCommand { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="MacCommandHolder"/> class.
        /// constructor for message from gateway (downstream)
        /// </summary>
        public MacCommandHolder()
        {
            this.MacCommand = new List<GenericMACCommand>();
        }

        public MacCommandHolder(byte input)
        {
            this.MacCommand = new List<GenericMACCommand>();

            CidEnum cid = (CidEnum)input;
            switch (cid)
            {
                case CidEnum.LinkCheckCmd:
                    LinkCheckCmd linkCheck = new LinkCheckCmd();
                    this.MacCommand.Add(linkCheck);
                    break;
                case CidEnum.LinkADRCmd:
                    Logger.Log("mac command detected : LinkADRCmd", LogLevel.Information);
                    break;
                case CidEnum.DutyCycleCmd:
                    DutyCycleCmd dutyCycle = new DutyCycleCmd();
                    this.MacCommand.Add(dutyCycle);
                    break;
                case CidEnum.RXParamCmd:
                    Logger.Log("mac command detected : RXParamCmd", LogLevel.Information);
                    break;
                case CidEnum.DevStatusCmd:
                    Logger.Log("mac command detected : DevStatusCmd", LogLevel.Information);
                    DevStatusCmd devStatus = new DevStatusCmd();
                    this.MacCommand.Add(devStatus);
                    break;
                case CidEnum.NewChannelCmd:
                    // NewChannelReq newChannel = new NewChannelReq();
                    // macCommand.Add(newChannel);
                    break;
                case CidEnum.RXTimingCmd:
                    RXTimingSetupCmd rXTimingSetup = new RXTimingSetupCmd();
                    this.MacCommand.Add(rXTimingSetup);
                    break;
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="MacCommandHolder"/> class.
        /// constructor for message received (upstream)
        /// </summary>
        public MacCommandHolder(byte[] input)
        {
            int pointer = 0;
            this.MacCommand = new List<GenericMACCommand>();

            while (pointer < input.Length)
            {
                CidEnum cid = (CidEnum)input[pointer];
                switch (cid)
                {
                    case CidEnum.LinkCheckCmd:
                        Logger.Log("mac command detected : LinkCheckCmd", LogLevel.Information);
                        LinkCheckCmd linkCheck = new LinkCheckCmd();
                        pointer += linkCheck.Length;
                        this.MacCommand.Add(linkCheck);
                        break;
                    case CidEnum.LinkADRCmd:
                        Logger.Log("mac command detected : LinkADRCmd", LogLevel.Information);
                        break;
                    case CidEnum.DutyCycleCmd:
                        Logger.Log("mac command detected : DutyCycleCmd", LogLevel.Information);
                        DutyCycleCmd dutyCycle = new DutyCycleCmd();
                        pointer += dutyCycle.Length;
                        this.MacCommand.Add(dutyCycle);
                        break;
                    case CidEnum.RXParamCmd:
                        Logger.Log("mac command detected : RXParamCmd", LogLevel.Information);
                        break;
                    case CidEnum.DevStatusCmd:
                        Logger.Log("mac command detected : DevStatusCmd", LogLevel.Information);
                        DevStatusCmd devStatus = new DevStatusCmd(input[pointer + 1], input[pointer + 2]);
                        pointer += devStatus.Length;
                        this.MacCommand.Add(devStatus);
                        break;
                    case CidEnum.NewChannelCmd:
                        Logger.Log("mac command detected : NewChannelCmd", LogLevel.Information);
                        NewChannelAns newChannel = new NewChannelAns(Convert.ToBoolean(input[pointer + 1] & 1), Convert.ToBoolean(input[pointer + 1] & 2));
                        pointer += newChannel.Length;
                        this.MacCommand.Add(newChannel);
                        break;
                    case CidEnum.RXTimingCmd:
                        Logger.Log("mac command detected : RXTimingCmd", LogLevel.Information);
                        RXTimingSetupCmd rXTimingSetup = new RXTimingSetupCmd();
                        pointer += rXTimingSetup.Length;
                        this.MacCommand.Add(rXTimingSetup);
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
}
