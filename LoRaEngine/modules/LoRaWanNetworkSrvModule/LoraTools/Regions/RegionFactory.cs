using LoRaTools.LoRaPhysical;
using LoRaTools.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace LoRaTools.Regions
{
    public static class RegionFactory
    {
        public static Region CurrentRegion
        {
            get; set;
        }

        public static Region Create(Rxpk rxpk)
        {
            //EU863-870
            if (rxpk.freq < 870 && rxpk.freq > 863)
            {
                return CurrentRegion = Region.EU;
            }//US902-928
            else if(rxpk.freq<=928 && rxpk.freq >= 902)
            {
                return CurrentRegion = Region.US;
            }

            return null;
        }   
    }
}
