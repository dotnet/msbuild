// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Concurrent;

namespace Microsoft.Build.Framework
{
    /// <summary>
    ///     Caching 'Last Write File Utc' times for Immutable files <see cref="FileClassifier" />.
    /// </summary>
    /// <remarks>
    ///     Cache is add only. It does not updates already existing cached items.
    /// </remarks>
    internal class ImmutableFilesTimestampCache
    {
        private readonly ConcurrentDictionary<string, DateTime> _cache = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        ///     Shared singleton instance
        /// </summary>
        public static ImmutableFilesTimestampCache Shared { get; } = new();

        /// <summary>
        ///     Try get 'Last Write File Utc' time of particular file.
        /// </summary>
        /// <returns><see langword="true" /> if record exists</returns>
        public bool TryGetValue(string fullPath, out DateTime lastModified) => _cache.TryGetValue(fullPath, out lastModified);

        /// <summary>
        ///     Try Add 'Last Write File Utc' time of particular file into cache.
        /// </summary>
        public void TryAdd(string fullPath, DateTime lastModified) => _cache.TryAdd(fullPath, lastModified);
    }
}
