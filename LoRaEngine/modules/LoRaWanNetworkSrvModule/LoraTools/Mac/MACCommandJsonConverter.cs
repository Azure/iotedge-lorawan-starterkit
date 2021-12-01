// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaTools
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    /// <summary>
    /// Defines a <see cref="JsonConverter"/> capable of converting a JSON list of elements to concrete <see cref="MacCommand"/> objects.
    /// </summary>
    public class MacCommandJsonConverter : JsonConverter
    {
        public override bool CanRead => true;

        public override bool CanConvert(Type objectType)
        {
            return typeof(MacCommand).IsAssignableFrom(objectType);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            if (serializer is null) throw new ArgumentNullException(nameof(serializer));

            var item = JObject.Load(reader);
            var cidPropertyValue = item["cid"].Value<string>();
            if (string.IsNullOrEmpty(cidPropertyValue))
            {
                throw new JsonReaderException("Undefined mac command identifier");
            }

            if (Enum.TryParse<Cid>(cidPropertyValue, true, out var macCommandType))
            {
                switch (macCommandType)
                {
                    case Cid.DevStatusCmd:
                    {
                        var cmd = new DevStatusRequest();
                        serializer.Populate(item.CreateReader(), cmd);
                        return cmd;
                    }

                    case Cid.DutyCycleCmd:
                    {
                        var cmd = new DutyCycleRequest();
                        serializer.Populate(item.CreateReader(), cmd);
                        return cmd;
                    }

                    case Cid.NewChannelCmd:
                    {
                        var cmd = new NewChannelRequest();
                        serializer.Populate(item.CreateReader(), cmd);
                        return cmd;
                    }

                    case Cid.RXParamCmd:
                    {
                        var cmd = new RXParamSetupRequest();
                        serializer.Populate(item.CreateReader(), cmd);
                        return cmd;
                    }

                    case Cid.RXTimingCmd:
                    {
                        var cmd = new RXTimingSetupRequest();
                        serializer.Populate(item.CreateReader(), cmd);
                        return cmd;
                    }

                    case Cid.Zero:
                    case Cid.One:
                    case Cid.LinkCheckCmd:
                    case Cid.LinkADRCmd:
                    {
                        if (!IsValidLindADRCmd(item, out var errors))
                        {
                            throw new JsonReaderException(string.Format(CultureInfo.InvariantCulture, "Command {0} is invalid: {1}", item, string.Join(", ", errors)));
                        }
                        var cmd = new LinkADRRequest((ushort)item["datarate"], (ushort)item["txpower"], (ushort)item["chmask"], (byte)item["chmaskctl"], (byte)item["nbtrans"]);
                        serializer.Populate(item.CreateReader(), cmd);
                        return cmd;
                    }
                    default:
                        throw new JsonReaderException($"Unhandled command identifier: {macCommandType}");
                }
            }

            throw new JsonReaderException($"Unknown MAC command identifier: {cidPropertyValue}");
        }

        public override bool CanWrite => false;

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer) => throw new NotImplementedException();

        private static bool IsValidLindADRCmd(JObject item, out List<string> errorMsgs)
        {
            errorMsgs = new List<string>();
            var requiredProperties = new List<string> { "datarate", "txpower", "chmask", "chmaskctl", "nbtrans" };

            foreach (var property in requiredProperties)
            {
                if (!item.ContainsKey(property))
                    errorMsgs.Add($"Property '{property}' is missing");
            }

            return errorMsgs.Count == 0;
        }
    }
}
