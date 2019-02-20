// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaTools
{
    using System;
    using System.Collections.Generic;
    using LoRaTools.Mac;
    using LoRaTools.Utils;
    using LoRaWan;
    using Microsoft.Extensions.Logging;
    using Newtonsoft.Json;

    public abstract class MacCommand
    {
        /// <summary>
        /// Gets or sets cid number of
        /// </summary>
        [JsonProperty("cid")]
        public CidEnum Cid { get; set; }

        public abstract int Length { get; }

        public override abstract string ToString();

        /// <summary>
        /// Initializes a new instance of the <see cref="MacCommand"/> class.
        /// create
        /// </summary>
        public MacCommand(ReadOnlySpan<byte> input)
        {
            if (input.Length < this.Length)
            {
                throw new MacCommandException("The Mac Command was not well formed, aborting mac command processing");
            }
        }

        public MacCommand()
        {
        }

        public abstract IEnumerable<byte> ToBytes();

        /// <summary>
        /// Create a Mac Command from a C2D Message
        /// </summary>
        /// <param name="cidType">CidType of the MAC command</param>
        /// <param name="properties">additional property to add</param>
        /// <returns>the Mac command</returns>
        public static MacCommand CreateMacCommandFromC2DMessage(string cidType, IDictionary<string, string> properties)
        {
            if (!Enum.TryParse(cidType, true, out CidEnum cid))
            {
                throw new MacCommandException("Could not parse the cid enum");
            }

            MacCommand macCommand = null;
            switch (cid)
            {
                case CidEnum.LinkCheckCmd:
                    if (properties.TryGetValueCaseInsensitive("margin", out string marginStr) &&
                        properties.TryGetValueCaseInsensitive("gatewayCount", out string gatewayCountStr))
                    {
                        if (uint.TryParse(marginStr, out uint margin) && uint.TryParse(gatewayCountStr, out uint gatewayCount))
                        macCommand = new LinkCheckAnswer(margin, gatewayCount);
                        break;
                    }

                    throw new MacCommandException("Could not parse margin or gatewayCount argument");
                case CidEnum.LinkADRCmd:
                    macCommand = new LinkADRRequest(properties);
                    break;
                case CidEnum.DutyCycleCmd:
                    // macCommand = new DutyCycleRequest();
                    throw new NotImplementedException("DutyCycleCmd MAC command is not yet supported");
                case CidEnum.RXParamCmd:
                    // macCommand = new RXParamSetupRequest();
                    throw new NotImplementedException("RXParamSetupRequest MAC command is not yet supported");
                case CidEnum.DevStatusCmd:
                    macCommand = new DevStatusRequest();
                    break;
                case CidEnum.NewChannelCmd:
                    throw new NotImplementedException("NewChannelCmd MAC command is not yet supported");
                case CidEnum.RXTimingCmd:
                    throw new NotImplementedException("RXTimingCmd MAC command is not yet supported");
            }

            return macCommand;
        }

        /// <summary>
        /// Create a List of Mac commands based on a sequence of bytes.
        /// </summary>
        public static List<MacCommand> CreateMacCommandFromBytes(string deviceId, ReadOnlyMemory<byte> input)
        {
            int pointer = 0;
            var macCommands = new List<MacCommand>(3);

            while (pointer < input.Length)
            {
                try
                {
                    CidEnum cid = (CidEnum)input.Span[pointer];
                    switch (cid)
                    {
                        case CidEnum.LinkCheckCmd:
                            LinkCheckRequest linkCheck = new LinkCheckRequest();
                            pointer += linkCheck.Length;
                            macCommands.Add(linkCheck);
                            break;
                        case CidEnum.LinkADRCmd:
                            var linkAdrAnswer = new LinkADRAnswer(input.Span.Slice(pointer));
                            pointer += linkAdrAnswer.Length;
                            macCommands.Add(linkAdrAnswer);
                            break;
                        case CidEnum.DutyCycleCmd:
                            var dutyCycle = new DutyCycleAnswer();
                            pointer += dutyCycle.Length;
                            macCommands.Add(dutyCycle);
                            break;
                        case CidEnum.RXParamCmd:
                            var rxParamSetup = new RXParamSetupAnswer(input.Span.Slice(pointer));
                            pointer += rxParamSetup.Length;
                            macCommands.Add(rxParamSetup);
                            break;
                        case CidEnum.DevStatusCmd:
                            // Added this case to enable unit testing
                            if (input.Length == 1)
                            {
                                var devStatusRequest = new DevStatusRequest();
                                pointer += devStatusRequest.Length;
                                macCommands.Add(devStatusRequest);
                            }
                            else
                            {
                                DevStatusAnswer devStatus = new DevStatusAnswer(input.Span.Slice(pointer));
                                pointer += devStatus.Length;
                                macCommands.Add(devStatus);
                            }

                            break;
                        case CidEnum.NewChannelCmd:
                            NewChannelAnswer newChannel = new NewChannelAnswer(input.Span.Slice(pointer));
                            pointer += newChannel.Length;
                            macCommands.Add(newChannel);
                            break;
                        case CidEnum.RXTimingCmd:
                            RXTimingSetupAnswer rxTimingSetup = new RXTimingSetupAnswer();
                            pointer += rxTimingSetup.Length;
                            macCommands.Add(rxTimingSetup);
                            break;
                        default:
                            Logger.Log($"A transmitted Mac Command value ${input.Span[pointer]} was not from a supported type. Aborting Mac Command processing", LogLevel.Error);
                            return null;
                    }

                    MacCommand addedMacCommand = macCommands[macCommands.Count - 1];
                    Logger.Log(deviceId, $"{addedMacCommand.Cid} mac command detected in upstream payload: {addedMacCommand.ToString()}", LogLevel.Debug);
                }
                catch (MacCommandException ex)
                {
                    Logger.Log(deviceId, ex.ToString(), LogLevel.Error);
                }
            }

            return macCommands;
        }
    }
}
