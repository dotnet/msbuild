// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

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
