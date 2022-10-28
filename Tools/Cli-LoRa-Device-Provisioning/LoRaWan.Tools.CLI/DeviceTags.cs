// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tools.CLI
{
    internal static class DeviceTags
    {
        internal const string NetworkTagName = "network";
        internal const string RegionTagName = "lora_region";
        internal const string DeviceTypeTagName = "lora_device_type";

        internal static class DeviceTypes
        {
            internal const string Leaf = "leaf";
            internal const string NetworkServer = "network_server";
            internal const string BasicsStation = "basics_station";
            internal const string Concentrator = "concentrator";
        }

    }
}
