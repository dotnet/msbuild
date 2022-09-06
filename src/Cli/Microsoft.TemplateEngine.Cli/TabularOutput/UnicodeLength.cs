// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

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
