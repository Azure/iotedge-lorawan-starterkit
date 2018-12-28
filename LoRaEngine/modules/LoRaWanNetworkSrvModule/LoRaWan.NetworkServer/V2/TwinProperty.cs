using System;
using System.Collections.Generic;
using System.Text;

namespace LoRaWan.NetworkServer.V2
{
    /// <summary>
    /// Twin properties of a <see cref="LoRaDevice"/>
    /// </summary>
    public static class TwinProperty
    {
        public const string AppSKey = "AppSKey";
        public const string NwkSKey = "NwkSKey";
        public const string DevAddr = "DevAddr";
        public const string DevNonce = "DevNonce";
        public const string AppEUI = "AppEUI";
        public const string AppKey = "AppKey";
        public const string GatewayID = "GatewayID";
        public const string SensorDecoder = "SensorDecoder";
        public const string FCntUp = "FCntUp";
        public const string FCntDown = "FCntDown";
    }
}
