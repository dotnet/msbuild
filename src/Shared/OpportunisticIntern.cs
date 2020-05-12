// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Text;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.Build.Shared;
using System.Linq;
using System.Globalization;

namespace Microsoft.Build
{
    /// <summary>
    /// This class is used to selectively intern strings. It should be used at the point of new string creation.
    /// For example,
    ///
    ///     string interned = OpportunisticIntern.Intern(String.Join(",",someStrings));
    ///
    /// </summary>
    internal static class OpportunisticIntern
    {
        /// <summary>
        /// Enumerates the possible interning results.
        /// </summary>
        private enum InternResult
        {
            MatchedHardcodedString,
            FoundInWeakStringCache,
            AddedToWeakStringCache,
            RejectedFromInterning
        }

        /// <summary>
        /// Manages the separate MRU lists.
        /// </summary>
        private static readonly WeakStringCache s_weakStringCache = new WeakStringCache();

        #region Statistics
        /// <summary>
        /// Whether or not to gather statistics
        /// </summary>
        private static bool s_gatherStatistics;

        /// <summary>
        /// Number of times interning worked.
        /// </summary>
        private static int s_hardcodedInternHits;

        /// <summary>
        /// Number of times interning didn't work.
        /// </summary>
        private static int s_regularInternHits;

        /// <summary>
        /// Number of times interning wasn't attempted.
        /// </summary>
        private static int s_regularInternMisses;

        /// <summary>
        /// Number of times interning wasn't attempted.
        /// </summary>
        private static int s_rejectedStrings;

        /// <summary>
        /// Total number of strings eliminated by interning.
        /// </summary>
        private static int s_internEliminatedStrings;

        /// <summary>
        /// Total number of chars eliminated across all strings.
        /// </summary>
        private static int s_internEliminatedChars;

        /// <summary>
        /// Strings which did not intern
        /// </summary>
        private static Dictionary<string, int> s_missedHardcodedStrings;

        #endregion

        /// <summary>
        /// Turn on statistics gathering.
        /// </summary>
        internal static void EnableStatisticsGathering()
        {
            s_missedHardcodedStrings = new Dictionary<string, int>();
            s_gatherStatistics = true;
        }

        /// <summary>
        /// Intern the given internable.
        /// </summary>
        internal static string InternableToString<T>(T candidate) where T : IInternable
        {
            string result = InterningToString(candidate);
#if DEBUG
            string expected = candidate.ExpensiveConvertToString();
            if (!String.Equals(result, expected))
            {
                ErrorUtilities.ThrowInternalError("Interned string {0} should have been {1}", result, expected);
            }
#endif
            return result;
        }

        /// <summary>
        /// Potentially Intern the given string builder.
        /// </summary>
        internal static string StringBuilderToString(StringBuilder candidate)
        {
            return InternableToString(new StringBuilderInternTarget(candidate));
        }

        /// <summary>
        /// Potentially Intern the given char array.
        /// </summary>
        internal static string CharArrayToString(char[] candidate, int count)
        {
            return InternableToString(new CharArrayInternTarget(candidate, count));
        }

        /// <summary>
        /// Potentially Intern the given char array.
        /// </summary>
        internal static string CharArrayToString(char[] candidate, int startIndex, int count)
        {
            return InternableToString(new CharArrayInternTarget(candidate, startIndex, count));
        }

        /// <summary>
        /// Potentially Intern the given string.
        /// </summary>
        /// <param name="candidate">The string to intern.</param>
        /// <returns>The interned string, or the same string if it could not be interned.</returns>
        internal static string InternStringIfPossible(string candidate)
        {
            return InternableToString(new StringInternTarget(candidate));
        }

        /// <summary>
        /// Intern the given internable.
        /// </summary>
        internal static string InterningToString<T>(T candidate) where T : IInternable
        {
            if (candidate.Length == 0)
            {
                // As in the case that a property or itemlist has evaluated to empty.
                return string.Empty;
            }

            if (s_gatherStatistics)
            {
                return InternWithStatistics(candidate);
            }
            else
            {
                TryIntern(candidate, out string result);
                return result;
            }
        }

        /// <summary>
        /// Report statistics to the console.
        /// </summary>
        internal static void ReportStatistics()
        {
            string title = "Opportunistic Intern";
            Console.WriteLine("\n{0}{1}{0}", new string('=', 41 - (title.Length / 2)), title);
            Console.WriteLine("||{0,50}|{1,20:N0}|{2,8}|", "Hardcoded Hits", s_hardcodedInternHits, "hits");
            Console.WriteLine("||{0,50}|{1,20:N0}|{2,8}|", "Hardcoded Rejects", s_rejectedStrings, "rejects");
            Console.WriteLine("||{0,50}|{1,20:N0}|{2,8}|", "WeakStringCache Hits", s_regularInternHits, "hits");
            Console.WriteLine("||{0,50}|{1,20:N0}|{2,8}|", "WeakStringCache Misses", s_regularInternMisses, "misses");
            Console.WriteLine("||{0,50}|{1,20:N0}|{2,8}|", "Eliminated Strings*", s_internEliminatedStrings, "strings");
            Console.WriteLine("||{0,50}|{1,20:N0}|{2,8}|", "Eliminated Chars", s_internEliminatedChars, "chars");
            Console.WriteLine("||{0,50}|{1,20:N0}|{2,8}|", "Estimated Eliminated Bytes", s_internEliminatedChars * 2, "bytes");
            Console.WriteLine("Elimination assumes that strings provided were unique objects.");
            Console.WriteLine("|---------------------------------------------------------------------------------|");

            IEnumerable<string> topMissingHardcodedString =
                s_missedHardcodedStrings
                .OrderByDescending(kv => kv.Value * kv.Key.Length)
                .Take(15)
                .Where(kv => kv.Value > 1)
                .Select(kv => string.Format(CultureInfo.InvariantCulture, "({1} instances x each {2} chars)\n{0}", kv.Key, kv.Value, kv.Key.Length));

            Console.WriteLine("##########Top Missing Hardcoded Strings:  \n{0} ", string.Join("\n==============\n", topMissingHardcodedString.ToArray()));
            Console.WriteLine();

            WeakStringCache.DebugInfo debugInfo = s_weakStringCache.GetDebugInfo();
            Console.WriteLine("WeakStringCache statistics:");
            Console.WriteLine("Bucket count used/unused/total    = {0}/{1}/{2}", debugInfo.UsedBucketCount, debugInfo.UnusedBucketCount, debugInfo.UsedBucketCount + debugInfo.UnusedBucketCount);
            Console.WriteLine("String count live/collected/total = {0}/{1}/{2}", debugInfo.LiveStringCount, debugInfo.CollectedStringCount, debugInfo.LiveStringCount + debugInfo.CollectedStringCount);
            Console.WriteLine("Hash collisions                   = {0}", debugInfo.HashCollisionCount);
        }

        private static bool TryInternHardcodedString<T>(T candidate, string str, ref string interned) where T : IInternable
        {
            Debug.Assert(candidate.Length == str.Length);

            if (candidate.StartsWithStringByOrdinalComparison(str))
            {
                interned = str;
                return true;
            }
            return false;
        }

        /// <summary>
        /// Try to intern the string.
        /// The return value indicates the how the string was interned (if at all).
        /// </summary>
        private static InternResult TryIntern<T>(T candidate, out string interned) where T : IInternable
        {
            int length = candidate.Length;
            interned = null;

            // First, try the hard coded intern strings.
            // Each of the hard-coded small strings below showed up in a profile run with considerable duplication in memory.
            if (length == 2)
            {
                if (candidate[1] == '#')
                {
                    if (candidate[0] == 'C')
                    {
                        interned = "C#";
                        return InternResult.MatchedHardcodedString;
                    }

                    if (candidate[0] == 'F')
                    {
                        interned = "F#";
                        return InternResult.MatchedHardcodedString;
                    }
                }

                if (candidate[0] == 'V' && candidate[1] == 'B')
                {
                    interned = "VB";
                    return InternResult.MatchedHardcodedString;
                }
            }
            else if (length == 4)
            {
                if (TryInternHardcodedString(candidate, "TRUE", ref interned) ||
                    TryInternHardcodedString(candidate, "True", ref interned) ||
                    TryInternHardcodedString(candidate, "Copy", ref interned) ||
                    TryInternHardcodedString(candidate, "true", ref interned) ||
                    TryInternHardcodedString(candidate, "v4.0", ref interned))
                {
                    return InternResult.MatchedHardcodedString;
                }
            }
            else if (length == 5)
            {
                if (TryInternHardcodedString(candidate, "FALSE", ref interned) ||
                    TryInternHardcodedString(candidate, "false", ref interned) ||
                    TryInternHardcodedString(candidate, "Debug", ref interned) ||
                    TryInternHardcodedString(candidate, "Build", ref interned) ||
                    TryInternHardcodedString(candidate, "Win32", ref interned))
                {
                    return InternResult.MatchedHardcodedString;
                }
            }
            else if (length == 6)
            {
                if (TryInternHardcodedString(candidate, "''!=''", ref interned) ||
                    TryInternHardcodedString(candidate, "AnyCPU", ref interned))
                {
                    return InternResult.MatchedHardcodedString;
                }
            }
            else if (length == 7)
            {
                if (TryInternHardcodedString(candidate, "Library", ref interned) ||
                    TryInternHardcodedString(candidate, "MSBuild", ref interned) ||
                    TryInternHardcodedString(candidate, "Release", ref interned))
                {
                    return InternResult.MatchedHardcodedString;
                }
            }
            // see Microsoft.Build.BackEnd.BuildRequestConfiguration.CreateUniqueGlobalProperty
            else if (length > MSBuildConstants.MSBuildDummyGlobalPropertyHeader.Length &&
                    candidate.StartsWithStringByOrdinalComparison(MSBuildConstants.MSBuildDummyGlobalPropertyHeader))
            {
                // don't want to leak unique strings into the cache
                interned = candidate.ExpensiveConvertToString();
                return InternResult.RejectedFromInterning;
            }
            else if (length == 24)
            {
                if (TryInternHardcodedString(candidate, "ResolveAssemblyReference", ref interned))
                {
                    return InternResult.MatchedHardcodedString;
                }
            }

            interned = s_weakStringCache.GetOrCreateEntry(candidate, out bool cacheHit);
            return cacheHit ? InternResult.FoundInWeakStringCache : InternResult.AddedToWeakStringCache;
        }

        /// <summary>
        /// Version of Intern that gathers statistics
        /// </summary>
        private static string InternWithStatistics<T>(T candidate) where T : IInternable
        {
            lock (s_missedHardcodedStrings)
            {
                InternResult internResult = TryIntern(candidate, out string result);

                switch (internResult)
                {
                    case InternResult.MatchedHardcodedString:
                        s_hardcodedInternHits++;
                        break;
                    case InternResult.FoundInWeakStringCache:
                        s_regularInternHits++;
                        break;
                    case InternResult.AddedToWeakStringCache:
                        s_regularInternMisses++;
                        break;
                    case InternResult.RejectedFromInterning:
                        s_rejectedStrings++;
                        break;
                }

                if (internResult != InternResult.MatchedHardcodedString && internResult != InternResult.RejectedFromInterning)
                {
                    s_missedHardcodedStrings.TryGetValue(result, out int priorCount);
                    s_missedHardcodedStrings[result] = priorCount + 1;
                }

                if (!candidate.ReferenceEquals(result))
                {
                    // Reference changed so 'candidate' is now released and should save memory.
                    s_internEliminatedStrings++;
                    s_internEliminatedChars += candidate.Length;
                }

                return result;
            }
        }
    }
}
