// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaTools
{
    using System;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    /// <summary>
    /// Defines a <see cref="JsonConverter"/> capable of converting a JSON list of elements to concrete <see cref="MacCommand"/> objects
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
            JObject item = JObject.Load(reader);
            var cidPropertyValue = item["cid"].Value<string>();
            if (string.IsNullOrEmpty(cidPropertyValue))
            {
                throw new JsonReaderException("Undefined mac command identifier");
            }

            if (Enum.TryParse<CidEnum>(cidPropertyValue, true, out var macCommandType))
            {
                switch (macCommandType)
                {
                    case CidEnum.DevStatusCmd:
                    {
                        var cmd = new DevStatusRequest();
                        serializer.Populate(item.CreateReader(), cmd);
                        return cmd;
                    }

                    case CidEnum.DutyCycleCmd:
                    {
                        var cmd = new DutyCycleRequest();
                        serializer.Populate(item.CreateReader(), cmd);
                        return cmd;
                    }

                    case CidEnum.NewChannelCmd:
                    {
                        var cmd = new NewChannelRequest();
                        serializer.Populate(item.CreateReader(), cmd);
                        return cmd;
                    }

                    case CidEnum.RXParamCmd:
                    {
                        var cmd = new RXParamSetupRequest();
                        serializer.Populate(item.CreateReader(), cmd);
                        return cmd;
                    }

                    case CidEnum.RXTimingCmd:
                    {
                        var cmd = new RXTimingSetupRequest();
                        serializer.Populate(item.CreateReader(), cmd);
                        return cmd;
                    }
                }
            }

            throw new JsonReaderException($"Unkown MAC command identifier: {cidPropertyValue}");
        }

        public override bool CanWrite => false;

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer) => throw new NotImplementedException();
    }
}
