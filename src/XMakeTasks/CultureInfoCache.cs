// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;

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
        private static readonly ConcurrentDictionary<string, CacheEntry> s_cache = new ConcurrentDictionary<string, CacheEntry>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Gets a read-only cached <see cref="CultureInfo"/> for the specified name.
        /// </summary>
        /// <param name="name">The culture name.</param>
        /// <returns>
        /// A read-only cached <see cref="CultureInfo"/> or <c>null</c> if the culture is not valid.
        /// </returns>
        internal static CultureInfo GetCultureInfo(string name)
        {
            if (name == null)
            {
                return null;
            }

            PopulateCultures();

            var entry = s_cache.GetOrAdd(name, n =>
            {
                try
                {
                    return new CacheEntry(CultureInfo.ReadOnly(new CultureInfo(n)));
                }
                catch (CultureNotFoundException)
                {
                    return new CacheEntry(null);
                }
            });

            return entry.CultureInfo;
        }

        /// <summary>
        /// Determine if a culture string represents a valid <see cref="CultureInfo"/> instance.
        /// </summary>
        /// <param name="name">The culture name.</param>
        /// <returns>True if the culture is determined to be valid.</returns>
        internal static bool IsValidCultureString(string name)
        {
            return GetCultureInfo(name) != null;
        }

        /// <summary>
        /// Populate the cache with CultureInfo.GetCulture (if supported).
        /// </summary>
        private static void PopulateCultures()
        {
            if (s_cache.Count > 0) return;

#if FEATURE_CULTUREINFO_GETCULTURES
            var culturesToAdd = CultureInfo.GetCultures(CultureTypes.AllCultures);
#else
            // In CoreCLR we'll add at least the current culture to the cache, but populate the list as they're requested.
            var culturesToAdd = new[] {CultureInfo.CurrentCulture};
#endif

            foreach (var culture in culturesToAdd)
            {
                s_cache.GetOrAdd(culture.Name, n => new CacheEntry(culture));
            }
        }

        private class CacheEntry
        {
            public CacheEntry(CultureInfo cultureInfo)
            {
                CultureInfo = cultureInfo;
            }

            public CultureInfo CultureInfo { get; }
        }
    }
}

