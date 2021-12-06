// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaTools
{
    using System;
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
                        if (!item.TryGetValue("datarate", StringComparison.InvariantCultureIgnoreCase, out var datarate))
                            throw new JsonReaderException("Property 'dataRate' is missing");
                        if (!item.TryGetValue("txpower", StringComparison.InvariantCultureIgnoreCase, out var txpower))
                            throw new JsonReaderException("Property 'txPower' is missing");
                        if (!item.TryGetValue("chmask", StringComparison.InvariantCultureIgnoreCase, out var chmask))
                            throw new JsonReaderException("Property 'chMask' is missing");
                        if (!item.TryGetValue("chmaskcntl", StringComparison.InvariantCultureIgnoreCase, out var chmaskcntl))
                            throw new JsonReaderException("Property 'chMaskCntl' is missing");
                        if (!item.TryGetValue("nbrep", StringComparison.InvariantCultureIgnoreCase, out var nbrep))
                            throw new JsonReaderException("Property 'nbRep' is missing");

                        var cmd = new LinkADRRequest((ushort)datarate, (ushort)txpower, (ushort)chmask, (byte)chmaskcntl, (byte)nbrep);
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
    }
}
