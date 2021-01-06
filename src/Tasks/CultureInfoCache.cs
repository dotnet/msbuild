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
        // See https://docs.microsoft.com/en-us/dotnet/api/system.globalization.cultureinfo#custom-cultures
        private const int LocaleCustomUnspecified = 0x1000;

        /// <summary>
        /// Determine if a culture string represents a valid <see cref="CultureInfo"/> instance.
        /// </summary>
        /// <param name="name">The culture name.</param>
        /// <returns>True if the culture is determined to be valid.</returns>
        internal static bool IsValidCultureString(string name)
        {
            try
            {
                var culture = CultureInfo.GetCultureInfo(name);
                return culture.LCID != LocaleCustomUnspecified || culture.ThreeLetterISOLanguageName.Length == 3;
            }
            catch (CultureNotFoundException)
            {
                return false;
            }
        }
    }
}
