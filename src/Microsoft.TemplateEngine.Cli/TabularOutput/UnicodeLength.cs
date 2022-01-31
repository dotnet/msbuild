// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.TemplateEngine.Cli.TabularOutput
{
    internal static class UnicodeLength
    {
        internal static int GetUnicodeLength(this string s)
        {
            int totalWidth = 0;
            for (int i = 0; i < s.Length; i++)
            {
                totalWidth += Wcwidth.UnicodeCalculator.GetWidth((int)s[i]);
            }
            return totalWidth;
        }
    }
}
