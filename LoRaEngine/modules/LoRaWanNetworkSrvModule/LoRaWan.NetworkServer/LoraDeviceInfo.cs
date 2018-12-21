//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace LoRaWan.NetworkServer
{

    public class LoraDeviceInfo
    {
        public string DevAddr { get; set; }
        public string DevEUI{ get; set; }
    public string AppKey{ get; set; }
public string AppEUI{ get; set; }
        public string NwkSKey { get; set; }
        public string AppSKey { get; set; }
        public string PrimaryKey { get; set; }
        public string AppNonce { get; set; }
        public string DevNonce { get; set; }
        public string NetId { get; set; }
        public bool IsOurDevice = false;
        public bool IsJoinValid = false;
        public IoTHubConnector HubSender;
        public UInt16 FCntUp;
        public UInt16 FCntDown;
        public string GatewayID { get; set; }
        public string SensorDecoder { get; set; }


        public LoraDeviceInfo()
        {
        }

        internal Task UpdateTwinAsync(object p)
        {
            throw new NotImplementedException();
        }

        internal object GetTwinProperties()
        {
            throw new NotImplementedException();
        }
    }


}
