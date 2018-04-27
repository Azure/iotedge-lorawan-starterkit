using System;
using System.Collections.Generic;
using System.Text;

namespace PacketForwarder_JSON
{
    public class SX127xConf
    {
        public int freq { get; set; }
        public int spread_factor { get; set; }
        public int pin_nss { get; set; }
        public int pin_dio0 { get; set; }
        public int pin_rst { get; set; }
        public int pin_led1 { get; set; }
    }

    public class Server
    {
        public string address { get; set; }
        public int port { get; set; }
        public bool enabled { get; set; }
    }

    public class GatewayConf
    {
        public double ref_latitude { get; set; }
        public double ref_longitude { get; set; }
        public int ref_altitude { get; set; }
        public string name { get; set; }
        public string email { get; set; }
        public string desc { get; set; }
        public List<Server> servers { get; set; }
    }

    public class RootObject
    {
        public SX127xConf SX127x_conf { get; set; }
        public GatewayConf gateway_conf { get; set; }

    }
}
