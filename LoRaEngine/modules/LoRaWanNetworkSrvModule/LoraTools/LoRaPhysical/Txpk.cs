using System;
using System.Collections.Generic;
using System.Text;

namespace LoRaTools.LoRaPhysical
{
    public class Txpk
    {
        public bool imme;
        public string data;
        public long tmst;
        public uint size;
        public double freq; 
        public uint rfch;
        public string modu;
        public string datr;
        public string codr;
        public uint powe;
        public bool ipol;
    }
}
