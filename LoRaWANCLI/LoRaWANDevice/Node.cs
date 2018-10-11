using System;
using System.Collections.Generic;
using System.Text;

namespace LoRaWANDevice
{
    public partial class Node
    {
        public NodeActivationMethod ActivationType { get; set; }
        public char Class { get; set; }
        public string DevEui { get; set; }
        public string AppEui { get; set; }
        public string AppKey { get; set; }
        public string DeviceAddr { get; set; }
        public string AppSkey { get; set; }
        public string NwkSKey { get; set; }
        public string GatewayId { get; set; }
        public string SensorDecoder { get; set; }
        public int RX1 { get; set; }
        public int RX2 { get; set; }
        public int AdrMin { get; set; }
        public int AdrMax { get; set; }
        public int AdrFix { get; set; } // assumption of data type
        public bool AdrEnabled { get; set; }
        public int DutyCycle { get; set; }

        public Node()
        {
            GatewayId = "";
            SensorDecoder = "";

            AdrEnabled = false;
            AdrFix = 0;
            AdrMax = 0;
            AdrMin = 0;
            RX1 = 1;
            RX2 = 1;
            DutyCycle = 1;
        }


    }
}
