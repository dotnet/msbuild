// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.TemplateEngine.Cli
{
    internal class NullOrEmptyIsLastStringComparer : IComparer<string>
    {
        public int Compare(string? x, string? y)
        {
            if (string.Equals(x, y, StringComparison.OrdinalIgnoreCase))
            {
                return 0;
            }

            if (string.IsNullOrEmpty(x) && string.IsNullOrEmpty(y))
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
