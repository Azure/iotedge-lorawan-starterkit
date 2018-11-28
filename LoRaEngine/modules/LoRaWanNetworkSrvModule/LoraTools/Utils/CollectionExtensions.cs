using System;
using System.Collections.Generic;
using System.Text;

namespace LoRaTools.Utils
{
    public  static class CollectionExtensions
    {     
        public static bool TryGetValueCaseInsensitive(this IDictionary<string, string> dict, string key, out string value)
        {
            value = null;
            if(dict==null){
                return false;
            }
            foreach (var kv in dict)
            {
                if (kv.Key.Equals(key, StringComparison.OrdinalIgnoreCase))
                {
                    value = kv.Value;
                    return true;
                }
            }

            return false;
        }
    }
}