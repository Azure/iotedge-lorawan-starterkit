// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer.BasicsStation.JsonHandlers
{
    using System.Linq;
    using System.Text.Json;

    public static class LnsData
    {
        internal static readonly IJsonReader<LnsMessageType> MessageTypeReader =
            JsonReader.Object(
                JsonReader.Property("msgtype",
                    from s in JsonReader.String()
                    select s switch
                    {
                        "version"       => LnsMessageType.Version,
                        "router_config" => LnsMessageType.RouterConfig,
                        "jreq"          => LnsMessageType.JoinRequest,
                        "updf"          => LnsMessageType.UplinkDataFrame,
                        "dntxed"        => LnsMessageType.TransmitConfirmation,
                        "dnmsg"         => LnsMessageType.DownlinkMessage,
                        var type => throw new JsonException("Invalid or unsupported message type: " + type)
                    }));

        /*
            {
                "msgtype"   : "version"
                "station"   : STRING
                "firmware"  : STRING
                "package"   : STRING
                "model"     : STRING
                "protocol"  : INT
                "features"  : STRING
            }
         */

        // We are deliberately ignoring firmware/package/model/protocol/features as these are not strictly needed at this stage of implementation
        // TODO Tests for this method are missing (waiting for more usefulness of it)

        internal static readonly IJsonReader<string> VersionMessageReader =
            JsonReader.Object(JsonReader.Property("station", JsonReader.String()));
    }
}
