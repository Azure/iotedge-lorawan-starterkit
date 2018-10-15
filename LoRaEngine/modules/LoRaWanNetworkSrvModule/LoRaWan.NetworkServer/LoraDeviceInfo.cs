//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//
using System;
using System.Collections.Generic;
using System.Text;

namespace LoRaWan.NetworkServer
{

    public class LoraDeviceInfo
    {
        public string DevAddr;
        public string DevEUI;
        public string AppKey;
        public string AppEUI;
        public string NwkSKey;
        public string AppSKey;
        public string PrimaryKey;
        public string AppNonce;
        public string DevNonce;
        public string NetId;
        public bool IsOurDevice = false;
        public bool IsJoinValid = false;
        public IoTHubConnector HubSender;
        public UInt16 FCntUp;
        public UInt16 FCntDown;
        public string GatewayID;
        public string SensorDecoder;
    }

}
