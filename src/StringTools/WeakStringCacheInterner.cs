// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Microsoft.NET.StringTools
{
    /// <summary>
    /// Implements interning based on a WeakStringCache.
    /// </summary>
    internal class WeakStringCacheInterner : IDisposable
    {
        /// <summary>
        /// Enumerates the possible interning results.
        /// </summary>
        private enum InternResult
        {
            FoundInWeakStringCache,
            AddedToWeakStringCache,
        }

        internal static WeakStringCacheInterner Instance = new WeakStringCacheInterner();

        /// <summary>
        /// The cache to keep strings in.
        /// </summary>
        private readonly WeakStringCache _weakStringCache = new WeakStringCache();

        #region Statistics
        /// <summary>
        /// Number of times the regular interning path found the string in the cache.
        /// </summary>
        private int _regularInternHits;

        /// <summary>
        /// Number of times the regular interning path added the string to the cache.
        /// </summary>
        private int _regularInternMisses;

        /// <summary>
        /// Total number of strings eliminated by interning.
        /// </summary>
        private int _internEliminatedStrings;

        /// <summary>
        /// Total number of chars eliminated across all strings.
        /// </summary>
        private int _internEliminatedChars;

        /// <summary>
        /// Maps strings that went though the interning path to the number of times they have been
        /// seen. The higher the number the better the payoff of interning. Null if statistics
        /// gathering has not been enabled.
        /// </summary>
        private Dictionary<string, int>? _internCallCountsByString;

        #endregion

        /// <summary>
        /// Try to intern the string.
        /// The return value indicates the how the string was interned.
        /// </summary>
        private InternResult Intern(ref InternableString candidate, out string interned)
        {
            interned = _weakStringCache.GetOrCreateEntry(ref candidate, out bool cacheHit);
            return cacheHit ? InternResult.FoundInWeakStringCache : InternResult.AddedToWeakStringCache;
        }

        /// <summary>
        /// WeakIntern the given InternableString.
        /// </summary>
        public string InternableToString(ref InternableString candidate)
        {
            if (candidate.Length == 0)
            {
                return string.Empty;
            }

            InternResult resultForStatistics = Intern(ref candidate, out string internedString);
#if DEBUG
            string expectedString = candidate.ExpensiveConvertToString();
            if (!String.Equals(internedString, expectedString))
            {
                throw new InvalidOperationException($"Interned string {internedString} should have been {expectedString}");
            }
#endif

            if (_internCallCountsByString != null)
            {
                lock (_internCallCountsByString)
                {
                    switch (resultForStatistics)
                    {
                        case InternResult.FoundInWeakStringCache:
                            _regularInternHits++;
                            break;
                        case InternResult.AddedToWeakStringCache:
                            _regularInternMisses++;
                            break;
                    }

                    _internCallCountsByString.TryGetValue(internedString, out int priorCount);
                    _internCallCountsByString[internedString] = priorCount + 1;

                    if (!candidate.ReferenceEquals(internedString))
                    {
                        // Reference changed so 'candidate' is now released and should save memory.
                        _internEliminatedStrings++;
                        _internEliminatedChars += candidate.Length;
                    }
                }
            }

            return internedString;
        }

        /// <summary>
        ///
        /// </summary>
        public void EnableStatistics()
        {
            _internCallCountsByString = new Dictionary<string, int>();
        }

        /// <summary>
        /// Returns a string with human-readable statistics.
        /// </summary>
        public string FormatStatistics()
        {
            StringBuilder result = new StringBuilder(1024);

            string title = "Opportunistic Intern";

            if (_internCallCountsByString != null)
            {
                result.AppendLine($"\n{new string('=', 41 - (title.Length / 2))}{title}{new string('=', 41 - (title.Length / 2))}");
                result.AppendLine($"||{"WeakStringCache Hits",50}|{_regularInternHits,20:N0}|{"hits",8}|");
                result.AppendLine($"||{"WeakStringCache Misses",50}|{_regularInternMisses,20:N0}|{"misses",8}|");
                result.AppendLine($"||{"Eliminated Strings*",50}|{_internEliminatedStrings,20:N0}|{"strings",8}|");
                result.AppendLine($"||{"Eliminated Chars",50}|{_internEliminatedChars,20:N0}|{"chars",8}|");
                result.AppendLine($"||{"Estimated Eliminated Bytes",50}|{_internEliminatedChars * 2,20:N0}|{"bytes",8}|");
                result.AppendLine("Elimination assumes that strings provided were unique objects.");
                result.AppendLine("|---------------------------------------------------------------------------------|");

                IEnumerable<string> topInternedStrings =
                    _internCallCountsByString
                    .OrderByDescending(kv => kv.Value * kv.Key.Length)
                    .Where(kv => kv.Value > 1)
                    .Take(15)
                    .Select(kv => string.Format(CultureInfo.InvariantCulture, "({1} instances x each {2} chars)\n{0}", kv.Key, kv.Value, kv.Key.Length));

                result.AppendLine(string.Format("##########Top Top Interned Strings:  \n{0} ", string.Join("\n==============\n", topInternedStrings.ToArray())));
                result.AppendLine();

                WeakStringCache.DebugInfo debugInfo = _weakStringCache.GetDebugInfo();
                result.AppendLine("WeakStringCache statistics:");
                result.AppendLine($"String count live/collected/total = {debugInfo.LiveStringCount}/{debugInfo.CollectedStringCount}/{debugInfo.LiveStringCount + debugInfo.CollectedStringCount}");
            }
            else
            {
                result.Append(title);
                result.AppendLine(" - EnableStatisticsGathering() has not been called");
            }

            return result.ToString();
        }

        /// <summary>
        /// Releases all strings from the underlying intern table.
        /// </summary>
        public void Dispose()
        {
            _weakStringCache.Dispose();
        }
    }
}
