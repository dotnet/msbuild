// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Globalization;

namespace Microsoft.Build.Tasks
{
    /// <summary>
    /// Provides read-only cached instances of <see cref="CultureInfo"/>.
    /// <remarks>
    /// Original source:
    /// https://raw.githubusercontent.com/aspnet/Localization/dev/src/Microsoft.Framework.Globalization.CultureInfoCache/CultureInfoCache.cs
    /// </remarks>
    /// </summary>
    internal static class CultureInfoCache
    {
        /// <summary>
        /// Determine if a culture string represents a valid <see cref="CultureInfo"/> instance.
        /// </summary>
        /// <param name="name">The culture name.</param>
        /// <returns>True if the culture is determined to be valid.</returns>
        internal static bool IsValidCultureString(string name)
        {
            try
            {
                _ = CultureInfo.GetCultureInfo(name);
                return true;
            }
            catch (CultureNotFoundException)
            {
                return false;
            }
        }
    }
}
