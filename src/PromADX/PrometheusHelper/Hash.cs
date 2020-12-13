using System;

namespace PromADX.PrometheusHelper
{
    public static class Hash
    {
        public static long SDBMHash(string str)
        {
            long hash = 0;
            for (Int32 i = 0; i < str.Length; i++)
            {
                hash = str[i] + (hash << 6) + (hash << 16) - hash;
            }
            return hash;
        }
    }
}