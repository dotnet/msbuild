// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Globalization;

namespace Microsoft.Build;

internal static class Int32Extensions
{
    extension(int)
    {
        /// <inheritdoc cref="int.TryParse(string, out int)"/>
        public static bool TryParseInvariant(string s, out int result)
            => int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out result);
    }
}
