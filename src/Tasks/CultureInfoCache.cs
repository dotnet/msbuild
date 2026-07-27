// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Globalization;
#if NET
using System.Linq;
#else
using System.Collections.Generic;
#endif

#nullable disable

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
#if !NET
        private static readonly Lazy<HashSet<string>> ValidCultureNames = new Lazy<HashSet<string>>(() => InitializeValidCultureNames());
#endif

        // https://docs.microsoft.com/en-gb/windows/desktop/Intl/using-pseudo-locales-for-localization-testing
        // These pseudo-locales are available in versions of Windows from Vista and later.
        // However, from Windows 10, version 1803, they are not returned when enumerating the
        // installed cultures, even if the registry keys are set. Therefore, add them to the list manually.
        private static readonly string[] pseudoLocales = ["qps-ploc", "qps-ploca", "qps-plocm", "qps-Latn-x-sh"];

#if !NET
        private static HashSet<string> InitializeValidCultureNames()
        {
            HashSet<string> validCultureNames = new(StringComparer.OrdinalIgnoreCase);
            foreach (CultureInfo cultureName in CultureInfo.GetCultures(CultureTypes.AllCultures))
            {
                validCultureNames.Add(cultureName.Name);
            }

            // Account for pseudo-locales (see above)
            foreach (string pseudoLocale in pseudoLocales)
            {
                validCultureNames.Add(pseudoLocale);
            }

            return validCultureNames;
        }
#endif

        /// <summary>
        /// Determine if a culture string represents a valid <see cref="CultureInfo"/> instance.
        /// </summary>
        /// <param name="name">The culture name.</param>
        /// <returns>True if the culture is determined to be valid.</returns>
        internal static bool IsValidCultureString(string name)
        {
#if NET
            try
            {
                // GetCultureInfo throws if the culture doesn't exist
                CultureInfo.GetCultureInfo(name, predefinedOnly: true);
                return true;
            }
            catch
            {
                // Second attempt: try pseudolocales (see above)
                return pseudoLocales.Contains(name, StringComparer.OrdinalIgnoreCase);
            }
#else
            return ValidCultureNames.Value.Contains(name);
#endif
        }
    }
}
