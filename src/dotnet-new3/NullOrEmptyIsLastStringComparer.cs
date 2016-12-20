using System;
using System.Collections.Generic;

namespace dotnet_new3
{
    internal class NullOrEmptyIsLastStringComparer : IComparer<string>
    {
        public int Compare(string x, string y)
        {
            if(string.Equals(x, y, StringComparison.OrdinalIgnoreCase))
            {
                return 0;
            }

            if(string.IsNullOrEmpty(x) && string.IsNullOrEmpty(y))
            {
                return 0;
            }

            if (string.IsNullOrEmpty(y))
            {
                return 1;
            }

            if (string.IsNullOrEmpty(x))
            {
                return -1;
            }

            return 0;
        }
    }
}
