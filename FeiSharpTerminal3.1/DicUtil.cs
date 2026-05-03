using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
namespace FeiSharpTerminal3._1
{
    static class DicUtil
    {
        public static void NewAdd<TKey, TValue>(this Dictionary<TKey, TValue> dictionary, TKey key, TValue value)
        {
            try
            {
                dictionary.Add(key, value);
            }
            catch
            {
                dictionary[key] = value;
            }
        }
    }
}
