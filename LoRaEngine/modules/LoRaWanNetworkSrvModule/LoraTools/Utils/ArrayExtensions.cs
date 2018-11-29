using System;
using System.Collections.Generic;
using System.Text;

namespace LoRaTools.Utils
{
    public static class ArrayExtensions
    {
        public static T[] RangeSubset<T>(this T[] array, int startIndex, int length)
        {
            T[] subset = new T[length];
            Array.Copy(array, startIndex, subset, 0, length);
            return subset;
        }
    }
}