// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
#if !CLR2COMPATIBILITY
using System.Collections.Concurrent;
#endif
using System.Text;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using Microsoft.Build.Shared;
using Microsoft.Build.Utilities;

namespace Microsoft.Build
{
    /// <summary>
    /// This class is used to selectively intern strings. It should be used at the point of new string creation.
    /// For example,
    ///
    ///     string interned = OpportunisticIntern.Intern(String.Join(",",someStrings));
    ///
    /// There are currently two underlying implementations. The new default one in WeakStringCacheInterner is based on weak GC handles.
    /// The legacy one in BucketedPrioritizedStringList is available only as an escape hatch by setting an environment variable.
    ///
    /// The legacy implementation uses heuristics to decide whether it will be efficient to intern a string or not. There is no
    /// guarantee that a string will intern.
    ///
    /// The thresholds and sizes were determined by experimentation to give the best number of bytes saved
    /// at reasonable elapsed time cost.
    ///
    /// The new implementation interns all strings but maintains only weak references so it doesn't keep the strings alive.
    /// </summary>
    internal sealed class OpportunisticIntern
    {
        /// <summary>
        /// Defines the interner interface as we currently implement more than one.
        /// </summary>
        private interface IInternerImplementation
        {
            /// <summary>
            /// Converts the given internable candidate to its string representation. Efficient implementions have side-effects
            /// of caching the results to end up with as few duplicates on the managed heap as practical.
            /// </summary>
            string InterningToString<T>(T candidate) where T : IInternable;

            /// <summary>
            /// Prints implementation specific interning statistics to the console.
            /// </summary>
            /// <param name="heading">A string identifying the interner in the output.</param>
            void ReportStatistics(string heading);
        }

        /// <summary>
        /// The singleton instance of OpportunisticIntern.
        /// </summary>
        private static OpportunisticIntern _instance = new OpportunisticIntern();
        internal static OpportunisticIntern Instance => _instance;

        private readonly bool _useLegacyInterner = Traits.Instance.UseLegacyStringInterner;
        private readonly bool _useSimpleConcurrency = Traits.Instance.UseSimpleInternConcurrency;

        /// <summary>
        /// The size of the small mru list.
        /// </summary>
        private readonly int _smallMruSize;

        /// <summary>
        /// The size of the large mru list.
        /// </summary>
        private readonly int _largeMruSize;

        /// <summary>
        /// The size of the huge mru list.
        /// </summary>
        private readonly int _hugeMruSize;

        /// <summary>
        /// The smallest size a string can be to be considered small.
        /// </summary>
        private readonly int _smallMruThreshold;

        /// <summary>
        /// The smallest size a string can be to be considered large.
        /// </summary>
        private readonly int _largeMruThreshold;

        /// <summary>
        /// The smallest size a string can be to be considered huge.
        /// </summary>
        private readonly int _hugeMruThreshold;

        /// <summary>
        /// The smallest size a string can be to be ginormous.
        /// 8K for large object heap.
        /// </summary>
        private readonly int _ginormousThreshold;

        /// <summary>
        /// The interner implementation in use.
        /// </summary>
        private IInternerImplementation _interner;

        #region Statistics
        /// <summary>
        /// What if Mru lists were infinitely long?
        /// </summary>
        private BucketedPrioritizedStringList _whatIfInfinite;

        /// <summary>
        /// What if we doubled the size of the Mru lists?
        /// </summary>
        private BucketedPrioritizedStringList _whatIfDoubled;

        /// <summary>
        /// What if we halved the size of the Mru lists?
        /// </summary>
        private BucketedPrioritizedStringList _whatIfHalved;

        /// <summary>
        /// What if the size of Mru lists was zero? (We still intern tiny strings in this case)
        /// </summary>
        private BucketedPrioritizedStringList _whatIfZero;
        #endregion

        private OpportunisticIntern()
        {
            _smallMruSize = AssignViaEnvironment("MSBUILDSMALLINTERNSIZE", 50);
            _largeMruSize = AssignViaEnvironment("MSBUILDLARGEINTERNSIZE", 100);
            _hugeMruSize = AssignViaEnvironment("MSBUILDHUGEINTERNSIZE", 100);
            _smallMruThreshold = AssignViaEnvironment("MSBUILDSMALLINTERNTHRESHOLD", 50);
            _largeMruThreshold = AssignViaEnvironment("MSBUILDLARGEINTERNTHRESHOLD", 70);
            _hugeMruThreshold = AssignViaEnvironment("MSBUILDHUGEINTERNTHRESHOLD", 200);
            _ginormousThreshold = AssignViaEnvironment("MSBUILDGINORMOUSINTERNTHRESHOLD", 8000);

            _interner = _useLegacyInterner
               ? (IInternerImplementation)new BucketedPrioritizedStringList(gatherStatistics: false, _smallMruSize, _largeMruSize, _hugeMruSize,
                    _smallMruThreshold, _largeMruThreshold, _hugeMruThreshold, _ginormousThreshold, _useSimpleConcurrency)
               : (IInternerImplementation)new WeakStringCacheInterner(gatherStatistics: false);
        }

        /// <summary>
        /// Recreates the singleton instance based on the current environment (test only).
        /// </summary>
        internal static void ResetForTests()
        {
            Debug.Assert(BuildEnvironmentHelper.Instance.RunningTests);
            _instance = new OpportunisticIntern();
        }

        /// <summary>
        /// Assign an int from an environment variable. If its not present, use the default.
        /// </summary>
        private int AssignViaEnvironment(string env, int @default)
        {
            string threshold = Environment.GetEnvironmentVariable(env);
            if (!string.IsNullOrEmpty(threshold))
            {
                if (int.TryParse(threshold, out int result))
                {
                    return result;
                }
            }

            return @default;
        }

        /// <summary>
        /// Turn on statistics gathering.
        /// </summary>
        internal void EnableStatisticsGathering()
        {
            if (_useLegacyInterner)
            {
                // Statistics include several 'what if' scenarios such as doubling the size of the MRU lists.
                _interner = new BucketedPrioritizedStringList(gatherStatistics: true, _smallMruSize, _largeMruSize, _hugeMruSize, _smallMruThreshold, _largeMruThreshold, _hugeMruThreshold, _ginormousThreshold, _useSimpleConcurrency);
                _whatIfInfinite = new BucketedPrioritizedStringList(gatherStatistics: true, int.MaxValue, int.MaxValue, int.MaxValue, _smallMruThreshold, _largeMruThreshold, _hugeMruThreshold, _ginormousThreshold, _useSimpleConcurrency);
                _whatIfDoubled = new BucketedPrioritizedStringList(gatherStatistics: true, _smallMruSize * 2, _largeMruSize * 2, _hugeMruSize * 2, _smallMruThreshold, _largeMruThreshold, _hugeMruThreshold, _ginormousThreshold, _useSimpleConcurrency);
                _whatIfHalved = new BucketedPrioritizedStringList(gatherStatistics: true, _smallMruSize / 2, _largeMruSize / 2, _hugeMruSize / 2, _smallMruThreshold, _largeMruThreshold, _hugeMruThreshold, _ginormousThreshold, _useSimpleConcurrency);
                _whatIfZero = new BucketedPrioritizedStringList(gatherStatistics: true, 0, 0, 0, _smallMruThreshold, _largeMruThreshold, _hugeMruThreshold, _ginormousThreshold, _useSimpleConcurrency);
            }
            else
            {
                _interner = new WeakStringCacheInterner(gatherStatistics: true);
            }
        }

        /// <summary>
        /// Intern the given internable.
        /// </summary>
        internal static string InternableToString<T>(T candidate) where T : IInternable
        {
            return Instance.InternableToStringImpl(candidate);
        }

        /// <summary>
        /// Potentially Intern the given string builder.
        /// </summary>
        internal static string StringBuilderToString(StringBuilder candidate)
        {
            return Instance.InternableToStringImpl(new StringBuilderInternTarget(candidate));
        }

        /// <summary>
        /// Potentially Intern the given char array.
        /// </summary>
        internal static string CharArrayToString(char[] candidate, int count)
        {
            return Instance.InternableToStringImpl(new CharArrayInternTarget(candidate, count));
        }

        /// <summary>
        /// Potentially Intern the given char array.
        /// </summary>
        internal static string CharArrayToString(char[] candidate, int startIndex, int count)
        {
            return Instance.InternableToStringImpl(new CharArrayInternTarget(candidate, startIndex, count));
        }

        /// <summary>
        /// Potentially Intern the given string.
        /// </summary>
        /// <param name="candidate">The string to intern.</param>
        /// <returns>The interned string, or the same string if it could not be interned.</returns>
        internal static string InternStringIfPossible(string candidate)
        {
            return Instance.InternableToStringImpl(new StringInternTarget(candidate));
        }

        /// <summary>
        /// Intern the given internable.
        /// </summary>
        private string InternableToStringImpl<T>(T candidate) where T : IInternable
        {
            if (candidate.Length == 0)
            {
                // As in the case that a property or itemlist has evaluated to empty.
                return string.Empty;
            }

            if (_whatIfInfinite != null)
            {
                _whatIfInfinite.InterningToString(candidate);
                _whatIfDoubled.InterningToString(candidate);
                _whatIfHalved.InterningToString(candidate);
                _whatIfZero.InterningToString(candidate);
            }

            string result = _interner.InterningToString(candidate);
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
        /// Report statistics about interning. Don't call unless GatherStatistics has been called beforehand.
        /// </summary>
        internal void ReportStatistics()
        {
            _interner.ReportStatistics("Main");
            if (_useLegacyInterner)
            {
                _whatIfInfinite.ReportStatistics("if Infinite");
                _whatIfDoubled.ReportStatistics("if Doubled");
                _whatIfHalved.ReportStatistics("if Halved");
                _whatIfZero.ReportStatistics("if Zero");
                Console.WriteLine(" * Even for MRU size of zero there will still be some intern hits because of the tiny ");
                Console.WriteLine("   string matching (eg. 'true')");
            }
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
        /// Try to match the candidate with small number of hardcoded interned string literals.
        /// The return value indicates how the string was interned (if at all).
        /// </summary>
        /// <returns>
        /// True if the candidate matched a hardcoded literal, null if it matched a "do not intern" string, false otherwise.
        /// </returns>
        private static bool? TryMatchHardcodedStrings<T>(T candidate, out string interned) where T : IInternable
        {
            int length = candidate.Length;
            interned = null;

            // Each of the hard-coded small strings below showed up in a profile run with considerable duplication in memory.
            if (length == 2)
            {
                if (candidate[1] == '#')
                {
                    if (candidate[0] == 'C')
                    {
                        interned = "C#";
                        return true;
                    }

                    if (candidate[0] == 'F')
                    {
                        interned = "F#";
                        return true;
                    }
                }

                if (candidate[0] == 'V' && candidate[1] == 'B')
                {
                    interned = "VB";
                    return true;
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
                    return true;
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
                    return true;
                }
            }
            else if (length == 6)
            {
                if (TryInternHardcodedString(candidate, "''!=''", ref interned) ||
                    TryInternHardcodedString(candidate, "AnyCPU", ref interned))
                {
                    return true;
                }
            }
            else if (length == 7)
            {
                if (TryInternHardcodedString(candidate, "Library", ref interned) ||
                    TryInternHardcodedString(candidate, "MSBuild", ref interned) ||
                    TryInternHardcodedString(candidate, "Release", ref interned))
                {
                    return true;
                }
            }
            // see Microsoft.Build.BackEnd.BuildRequestConfiguration.CreateUniqueGlobalProperty
            else if (length > MSBuildConstants.MSBuildDummyGlobalPropertyHeader.Length &&
                    candidate.StartsWithStringByOrdinalComparison(MSBuildConstants.MSBuildDummyGlobalPropertyHeader))
            {
                // don't want to leak unique strings into the cache
                interned = candidate.ExpensiveConvertToString();
                return null;
            }
            else if (length == 24)
            {
                if (TryInternHardcodedString(candidate, "ResolveAssemblyReference", ref interned))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Implements interning based on a WeakStringCache (new implementation).
        /// </summary>
        private class WeakStringCacheInterner : IInternerImplementation
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
            /// The cache to keep strings in.
            /// </summary>
            private readonly WeakStringCache _weakStringCache = new WeakStringCache();

#region Statistics
            /// <summary>
            /// Whether or not to gather statistics.
            /// </summary>
            private readonly bool _gatherStatistics;

            /// <summary>
            /// Number of times interning with hardcoded string literals worked.
            /// </summary>
            private int _hardcodedInternHits;

            /// <summary>
            /// Number of times the regular interning path found the string in the cache.
            /// </summary>
            private int _regularInternHits;

            /// <summary>
            /// Number of times the regular interning path added the string to the cache.
            /// </summary>
            private int _regularInternMisses;

            /// <summary>
            /// Number of times interning wasn't attempted.
            /// </summary>
            private int _rejectedStrings;

            /// <summary>
            /// Total number of strings eliminated by interning.
            /// </summary>
            private int _internEliminatedStrings;

            /// <summary>
            /// Total number of chars eliminated across all strings.
            /// </summary>
            private int _internEliminatedChars;

            /// <summary>
            /// Maps strings that went though the regular (i.e. not hardcoded) interning path to the number of times they have been
            /// seen. The higher the number the better the payoff if the string had been hardcoded.
            /// </summary>
            private Dictionary<string, int> _missedHardcodedStrings;

#endregion

            public WeakStringCacheInterner(bool gatherStatistics)
            {
                if (gatherStatistics)
                {
                    _missedHardcodedStrings = new Dictionary<string, int>();
                }
                _gatherStatistics = gatherStatistics;
            }

            /// <summary>
            /// Intern the given internable.
            /// </summary>
            public string InterningToString<T>(T candidate) where T : IInternable
            {
                if (_gatherStatistics)
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
            public void ReportStatistics(string heading)
            {
                string title = "Opportunistic Intern (" + heading + ")";
                Console.WriteLine("\n{0}{1}{0}", new string('=', 41 - (title.Length / 2)), title);
                Console.WriteLine("||{0,50}|{1,20:N0}|{2,8}|", "Hardcoded Hits", _hardcodedInternHits, "hits");
                Console.WriteLine("||{0,50}|{1,20:N0}|{2,8}|", "Hardcoded Rejects", _rejectedStrings, "rejects");
                Console.WriteLine("||{0,50}|{1,20:N0}|{2,8}|", "WeakStringCache Hits", _regularInternHits, "hits");
                Console.WriteLine("||{0,50}|{1,20:N0}|{2,8}|", "WeakStringCache Misses", _regularInternMisses, "misses");
                Console.WriteLine("||{0,50}|{1,20:N0}|{2,8}|", "Eliminated Strings*", _internEliminatedStrings, "strings");
                Console.WriteLine("||{0,50}|{1,20:N0}|{2,8}|", "Eliminated Chars", _internEliminatedChars, "chars");
                Console.WriteLine("||{0,50}|{1,20:N0}|{2,8}|", "Estimated Eliminated Bytes", _internEliminatedChars * 2, "bytes");
                Console.WriteLine("Elimination assumes that strings provided were unique objects.");
                Console.WriteLine("|---------------------------------------------------------------------------------|");

                IEnumerable<string> topMissingHardcodedString =
                    _missedHardcodedStrings
                    .OrderByDescending(kv => kv.Value * kv.Key.Length)
                    .Take(15)
                    .Where(kv => kv.Value > 1)
                    .Select(kv => string.Format(CultureInfo.InvariantCulture, "({1} instances x each {2} chars)\n{0}", kv.Key, kv.Value, kv.Key.Length));

                Console.WriteLine("##########Top Missing Hardcoded Strings:  \n{0} ", string.Join("\n==============\n", topMissingHardcodedString.ToArray()));
                Console.WriteLine();

                WeakStringCache.DebugInfo debugInfo = _weakStringCache.GetDebugInfo();
                Console.WriteLine("WeakStringCache statistics:");
                Console.WriteLine("String count live/collected/total = {0}/{1}/{2}", debugInfo.LiveStringCount, debugInfo.CollectedStringCount, debugInfo.LiveStringCount + debugInfo.CollectedStringCount);
            }

            /// <summary>
            /// Try to intern the string.
            /// The return value indicates the how the string was interned (if at all).
            /// </summary>
            private InternResult TryIntern<T>(T candidate, out string interned) where T : IInternable
            {
                // First, try the hard coded intern strings.
                bool? hardcodedMatchResult = TryMatchHardcodedStrings(candidate, out interned);
                if (hardcodedMatchResult != false)
                {
                    // Either matched a hardcoded string or is explicitly not to be interned.
                    return hardcodedMatchResult.HasValue ? InternResult.MatchedHardcodedString : InternResult.RejectedFromInterning;
                }

                interned = _weakStringCache.GetOrCreateEntry(candidate, out bool cacheHit);
                return cacheHit ? InternResult.FoundInWeakStringCache : InternResult.AddedToWeakStringCache;
            }

            /// <summary>
            /// Version of Intern that gathers statistics
            /// </summary>
            private string InternWithStatistics<T>(T candidate) where T : IInternable
            {
                lock (_missedHardcodedStrings)
                {
                    InternResult internResult = TryIntern(candidate, out string result);

                    switch (internResult)
                    {
                        case InternResult.MatchedHardcodedString:
                            _hardcodedInternHits++;
                            break;
                        case InternResult.FoundInWeakStringCache:
                            _regularInternHits++;
                            break;
                        case InternResult.AddedToWeakStringCache:
                            _regularInternMisses++;
                            break;
                        case InternResult.RejectedFromInterning:
                            _rejectedStrings++;
                            break;
                    }

                    if (internResult != InternResult.MatchedHardcodedString && internResult != InternResult.RejectedFromInterning)
                    {
                        _missedHardcodedStrings.TryGetValue(result, out int priorCount);
                        _missedHardcodedStrings[result] = priorCount + 1;
                    }

                    if (!candidate.ReferenceEquals(result))
                    {
                        // Reference changed so 'candidate' is now released and should save memory.
                        _internEliminatedStrings++;
                        _internEliminatedChars += candidate.Length;
                    }

                    return result;
                }
            }
        }

        /// <summary>
        /// Manages a set of mru lists that hold strings in varying size ranges (legacy implementation).
        /// </summary>
        private class BucketedPrioritizedStringList : IInternerImplementation
        {
            /// <summary>
            /// The small string Mru list.
            /// </summary>
            private readonly PrioritizedStringList _smallMru;

            /// <summary>
            /// The large string Mru list.
            /// </summary>
            private readonly PrioritizedStringList _largeMru;

            /// <summary>
            /// The huge string Mru list.
            /// </summary>
            private readonly PrioritizedStringList _hugeMru;

            /// <summary>
            /// Three most recently used strings over 8K.
            /// </summary>
            private readonly LinkedList<WeakReference> _ginormous = new LinkedList<WeakReference>();

            /// <summary>
            /// The smallest size a string can be to be considered small.
            /// </summary>
            private readonly int _smallMruThreshold;

            /// <summary>
            /// The smallest size a string can be to be considered large.
            /// </summary>
            private readonly int _largeMruThreshold;

            /// <summary>
            /// The smallest size a string can be to be considered huge.
            /// </summary>
            private readonly int _hugeMruThreshold;

            /// <summary>
            /// The smallest size a string can be to be ginormous.
            /// </summary>
            private readonly int _ginormousThreshold;

            private readonly bool _useSimpleConcurrency;

#if !CLR2COMPATIBILITY
            // ConcurrentDictionary starts with capacity 31 but we're usually adding far more than that. Make a better first capacity guess to reduce
            // ConcurrentDictionary having to take all internal locks to upgrade its bucket list. Note that the number should be prime per the
            // comments on the code at https://referencesource.microsoft.com/#mscorlib/system/Collections/Concurrent/ConcurrentDictionary.cs,122 
            // Also note default lock count is NativeMethodsShared.GetLogicalCoreCount() from the same code.
            private const int InitialCapacity = 2053;
            private readonly ConcurrentDictionary<string, string> _internedStrings = new ConcurrentDictionary<string, string>(NativeMethodsShared.GetLogicalCoreCount(), InitialCapacity, StringComparer.Ordinal);
#endif

#region Statistics
            /// <summary>
            /// Whether or not to gather statistics
            /// </summary>
            private readonly bool _gatherStatistics;

            /// <summary>
            /// Number of times interning worked.
            /// </summary>
            private int _internHits;

            /// <summary>
            /// Number of times interning didn't work.
            /// </summary>
            private int _internMisses;

            /// <summary>
            /// Number of times interning wasn't attempted.
            /// </summary>
            private int _internRejects;

            /// <summary>
            /// Total number of strings eliminated by interning.
            /// </summary>
            private int _internEliminatedStrings;

            /// <summary>
            /// Total number of chars eliminated across all strings.
            /// </summary>
            private int _internEliminatedChars;

            /// <summary>
            /// Number of times the ginourmous string hit.
            /// </summary>
            private int _ginormousHits;

            /// <summary>
            /// Number of times the ginourmous string missed.
            /// </summary>
            private int _ginormousMisses;

            /// <summary>
            /// Chars interned for ginormous range.
            /// </summary>
            private int _ginormousCharsSaved;

            /// <summary>
            /// Whether or not to track ginormous strings.
            /// </summary>
            private readonly bool _dontTrack;

            /// <summary>
            /// The time spent interning.
            /// </summary>
            private readonly Stopwatch _stopwatch;

            /// <summary>
            /// Strings which did not intern
            /// </summary>
            private readonly Dictionary<string, int> _missedStrings;

            /// <summary>
            /// Strings which we didn't attempt to intern
            /// </summary>
            private readonly Dictionary<string, int> _rejectedStrings;

            /// <summary>
            /// Number of ginormous strings to keep
            /// By observation of Auto7, there are about three variations of the huge solution config blob
            /// There aren't really any other strings of this size, but make it 10 to be sure. (There will barely be any misses)
            /// </summary>
            private const int GinormousSize = 10;

#endregion

            /// <summary>
            /// Construct.
            /// </summary>
            internal BucketedPrioritizedStringList(bool gatherStatistics, int smallMruSize, int largeMruSize, int hugeMruSize, int smallMruThreshold, int largeMruThreshold, int hugeMruThreshold, int ginormousThreshold, bool useSimpleConcurrency)
            {
                if (smallMruSize == 0 && largeMruSize == 0 && hugeMruSize == 0)
                {
                    _dontTrack = true;
                }

                _smallMru = new PrioritizedStringList(smallMruSize);
                _largeMru = new PrioritizedStringList(largeMruSize);
                _hugeMru = new PrioritizedStringList(hugeMruSize);
                _smallMruThreshold = smallMruThreshold;
                _largeMruThreshold = largeMruThreshold;
                _hugeMruThreshold = hugeMruThreshold;
                _ginormousThreshold = ginormousThreshold;
                _useSimpleConcurrency = useSimpleConcurrency;

                for (int i = 0; i < GinormousSize; i++)
                {
                    _ginormous.AddFirst(new WeakReference(string.Empty));
                }

                _gatherStatistics = gatherStatistics;
                if (gatherStatistics)
                {
                    _stopwatch = new Stopwatch();
                    _missedStrings = new Dictionary<string, int>(StringComparer.Ordinal);
                    _rejectedStrings = new Dictionary<string, int>(StringComparer.Ordinal);
                }
            }

            /// <summary>
            /// Intern the given internable.
            /// </summary>
            public string InterningToString<T>(T candidate) where T : IInternable
            {
                if (_gatherStatistics)
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
            public void ReportStatistics(string heading)
            {
                string title = "Opportunistic Intern (" + heading + ")";
                Console.WriteLine("\n{0}{1}{0}", new string('=', 41 - (title.Length / 2)), title);
                Console.WriteLine("||{0,50}|{1,20:N0}|{2,8}|", "Intern Hits", _internHits, "hits");
                Console.WriteLine("||{0,50}|{1,20:N0}|{2,8}|", "Intern Misses", _internMisses, "misses");
                Console.WriteLine("||{0,50}|{1,20:N0}|{2,8}|", "Intern Rejects (as shorter than " + _smallMruThreshold + " bytes)", _internRejects, "rejects");
                Console.WriteLine("||{0,50}|{1,20:N0}|{2,8}|", "Eliminated Strings*", _internEliminatedStrings, "strings");
                Console.WriteLine("||{0,50}|{1,20:N0}|{2,8}|", "Eliminated Chars", _internEliminatedChars, "chars");
                Console.WriteLine("||{0,50}|{1,20:N0}|{2,8}|", "Estimated Eliminated Bytes", _internEliminatedChars * 2, "bytes");
                Console.WriteLine("Elimination assumes that strings provided were unique objects.");
                Console.WriteLine("|---------------------------------------------------------------------------------|");
                KeyValuePair<int, int> held = _smallMru.Statistics();
                Console.WriteLine("||{0,50}|{1,20:N0}|{2,8}|", "Small Strings MRU Size", Instance._smallMruSize, "strings");
                Console.WriteLine("||{0,50}|{1,20:N0}|{2,8}|", "Small Strings (>=" + _smallMruThreshold + " chars) Held", held.Key, "strings");
                Console.WriteLine("||{0,50}|{1,20:N0}|{2,8}|", "Small Estimated Bytes Held", held.Value * 2, "bytes");
                Console.WriteLine("|---------------------------------------------------------------------------------|");
                held = _largeMru.Statistics();
                Console.WriteLine("||{0,50}|{1,20:N0}|{2,8}|", "Large Strings MRU Size", Instance._largeMruSize, "strings");
                Console.WriteLine("||{0,50}|{1,20:N0}|{2,8}|", "Large Strings  (>=" + _largeMruThreshold + " chars) Held", held.Key, "strings");
                Console.WriteLine("||{0,50}|{1,20:N0}|{2,8}|", "Large Estimated Bytes Held", held.Value * 2, "bytes");
                Console.WriteLine("|---------------------------------------------------------------------------------|");
                held = _hugeMru.Statistics();
                Console.WriteLine("||{0,50}|{1,20:N0}|{2,8}|", "Huge Strings MRU Size", Instance._hugeMruSize, "strings");
                Console.WriteLine("||{0,50}|{1,20:N0}|{2,8}|", "Huge Strings  (>=" + _hugeMruThreshold + " chars) Held", held.Key, "strings");
                Console.WriteLine("||{0,50}|{1,20:N0}|{2,8}|", "Huge Estimated Bytes Held", held.Value * 2, "bytes");
                Console.WriteLine("|---------------------------------------------------------------------------------|");
                Console.WriteLine("||{0,50}|{1,20:N0}|{2,8}|", "Ginormous Strings MRU Size", GinormousSize, "strings");
                Console.WriteLine("||{0,50}|{1,20:N0}|{2,8}|", "Ginormous (>=" + _ginormousThreshold + " chars)  Hits", _ginormousHits, "hits");
                Console.WriteLine("||{0,50}|{1,20:N0}|{2,8}|", "Ginormous Misses", _ginormousMisses, "misses");
                Console.WriteLine("||{0,50}|{1,20:N0}|{2,8}|", "Ginormous Chars Saved", _ginormousCharsSaved, "chars");
                Console.WriteLine("|---------------------------------------------------------------------------------|");

                // There's no point in reporting the ginormous string because it will have evaporated by now.
                Console.WriteLine("||{0,50}|{1,20:N0}|{2,8}|", "Time Spent Interning", _stopwatch.ElapsedMilliseconds, "ms");
                Console.WriteLine("{0}{0}", new string('=', 41));

                IEnumerable<string> topMissingString =
                    _missedStrings
                    .OrderByDescending(kv => kv.Value * kv.Key.Length)
                    .Take(15)
                    .Where(kv => kv.Value > 1)
                    .Select(kv => string.Format(CultureInfo.InvariantCulture, "({1} instances x each {2} chars = {3}KB wasted)\n{0}", kv.Key, kv.Value, kv.Key.Length, (kv.Value - 1) * kv.Key.Length * 2 / 1024));

                Console.WriteLine("##########Top Missed Strings:  \n{0} ", string.Join("\n==============\n", topMissingString.ToArray()));
                Console.WriteLine();

                IEnumerable<string> topRejectedString =
                    _rejectedStrings
                    .OrderByDescending(kv => kv.Value * kv.Key.Length)
                    .Take(15)
                    .Where(kv => kv.Value > 1)
                    .Select(kv => string.Format(CultureInfo.InvariantCulture, "({1} instances x each {2} chars = {3}KB wasted)\n{0}", kv.Key, kv.Value, kv.Key.Length, (kv.Value - 1) * kv.Key.Length * 2 / 1024));

                Console.WriteLine("##########Top Rejected Strings: \n{0} ", string.Join("\n==============\n", topRejectedString.ToArray()));
            }

            /// <summary>
            /// Try to intern the string.
            /// Return true if an interned value could be returned.
            /// Return false if it was added to the intern list, but wasn't there already.
            /// Return null if it didn't meet the length criteria for any of the buckets. Interning was rejected
            /// </summary>
            private bool? TryIntern<T>(T candidate, out string interned) where T : IInternable
            {
                int length = candidate.Length;
                interned = null;

                // First, try the hard coded intern strings.
                // Each of the hard-coded small strings below showed up in a profile run with considerable duplication in memory.
                if (!_dontTrack)
                {
                    bool? hardcodedMatchResult = TryMatchHardcodedStrings(candidate, out interned);
                    if (hardcodedMatchResult != false)
                    {
                        // Either matched a hardcoded string or is explicitly not to be interned.
                        return hardcodedMatchResult;
                    }

                    if (length > _ginormousThreshold)
                    {
                        lock (_ginormous)
                        {
                            LinkedListNode<WeakReference> current = _ginormous.First;

                            while (current != null)
                            {
                                if (current.Value.Target is string last && last.Length == candidate.Length && candidate.StartsWithStringByOrdinalComparison(last))
                                {
                                    interned = last;
                                    _ginormousHits++;
                                    _ginormousCharsSaved += last.Length;

                                    _ginormous.Remove(current);
                                    _ginormous.AddFirst(current);

                                    return true;
                                }

                                current = current.Next;
                            }

                            _ginormousMisses++;
                            interned = candidate.ExpensiveConvertToString();

                            LinkedListNode<WeakReference> lastNode = _ginormous.Last;
                            _ginormous.RemoveLast();
                            _ginormous.AddFirst(lastNode);
                            lastNode.Value.Target = interned;

                            return false;
                        }
                    }
#if !CLR2COMPATIBILITY
                    else if (_useSimpleConcurrency)
                    {
                        var stringified = candidate.ExpensiveConvertToString();
                        interned = _internedStrings.GetOrAdd(stringified, stringified);
                        return true;
                    }
#endif
                    else if (length >= _hugeMruThreshold)
                    {
                        lock (_hugeMru)
                        {
                            return _hugeMru.TryGet(candidate, out interned);
                        }
                    }
                    else if (length >= _largeMruThreshold)
                    {
                        lock (_largeMru)
                        {
                            return _largeMru.TryGet(candidate, out interned);
                        }
                    }
                    else if (length >= _smallMruThreshold)
                    {
                        lock (_smallMru)
                        {
                            return _smallMru.TryGet(candidate, out interned);
                        }
                    }
                }

                interned = candidate.ExpensiveConvertToString();
                return null;
            }

            /// <summary>
            /// Version of Intern that gathers statistics
            /// </summary>
            private string InternWithStatistics<T>(T candidate) where T : IInternable
            {
                lock (_missedStrings)
                {
                    _stopwatch.Start();
                    bool? interned = TryIntern(candidate, out string result);
                    _stopwatch.Stop();

                    if (interned.HasValue && !interned.Value)
                    {
                        // Could not intern.
                        _internMisses++;

                        _missedStrings.TryGetValue(result, out int priorCount);
                        _missedStrings[result] = priorCount + 1;

                        return result;
                    }
                    else if (interned == null)
                    {
                        // Decided not to attempt interning
                        _internRejects++;

                        _rejectedStrings.TryGetValue(result, out int priorCount);
                        _rejectedStrings[result] = priorCount + 1;

                        return result;
                    }

                    _internHits++;
                    if (!candidate.ReferenceEquals(result))
                    {
                        // Reference changed so 'candidate' is now released and should save memory.
                        _internEliminatedStrings++;
                        _internEliminatedChars += candidate.Length;
                    }

                    return result;
                }
            }

            /// <summary>
            /// A singly linked list of strings where the most recently accessed string is at the top.
            /// Size expands up to a fixed number of strings.
            /// </summary>
            private class PrioritizedStringList
            {
                /// <summary>
                /// Maximum size of the mru list.
                /// </summary>
                private readonly int _size;

                /// <summary>
                /// Head of the mru list.
                /// </summary>
                private Node _mru;

                /// <summary>
                /// Construct an Mru list with a fixed maximum size.
                /// </summary>
                internal PrioritizedStringList(int size)
                {
                    _size = size;
                }

                /// <summary>
                /// Try to get one element from the list. Upon leaving the function 'candidate' will be at the head of the Mru list.
                /// This function is not thread-safe.
                /// </summary>
                internal bool TryGet<T>(T candidate, out string interned) where T : IInternable
                {
                    if (_size == 0)
                    {
                        interned = candidate.ExpensiveConvertToString();
                        return false;
                    }

                    int length = candidate.Length;
                    Node secondPrior = null;
                    Node prior = null;
                    Node head = _mru;
                    bool found = false;
                    int itemCount = 0;

                    while (head != null && !found)
                    {
                        if (head.Value.Length == length)
                        {
                            if (candidate.StartsWithStringByOrdinalComparison(head.Value))
                            {
                                found = true;
                            }
                        }

                        if (!found)
                        {
                            secondPrior = prior;
                            prior = head;
                            head = head.Next;
                        }

                        itemCount++;
                    }

                    if (found)
                    {
                        // Move it to the top and return the interned version.
                        if (prior != null)
                        {
                            if (!candidate.ReferenceEquals(head.Value))
                            {
                                // Wasn't at the top already, so move it there.
                                prior.Next = head.Next;
                                head.Next = _mru;
                                _mru = head;
                                interned = _mru.Value;
                                return true;
                            }
                            else
                            {
                                // But don't move it up if there is reference equality so that multiple calls to Intern don't redundantly emphasize a string.
                                interned = head.Value;
                                return true;
                            }
                        }
                        else
                        {
                            // Found the item in the top spot. No need to move anything.
                            interned = _mru.Value;
                            return true;
                        }
                    }
                    else
                    {
                        // Not found. Create a new entry and place it at the top.
                        Node old = _mru;
                        _mru = new Node(candidate.ExpensiveConvertToString()) { Next = old };

                        // Cache miss. Use this opportunity to discard any element over the max size.
                        if (itemCount >= _size && secondPrior != null)
                        {
                            secondPrior.Next = null;
                        }

                        interned = _mru.Value;
                        return false;
                    }
                }

                /// <summary>
                /// Returns the number of strings held and the total number of chars held.
                /// </summary>
                internal KeyValuePair<int, int> Statistics()
                {
                    Node head = _mru;
                    int chars = 0;
                    int strings = 0;
                    while (head != null)
                    {
                        chars += head.Value.Length;
                        strings++;
                        head = head.Next;
                    }

                    return new KeyValuePair<int, int>(strings, chars);
                }

                /// <summary>
                /// Singly linked list node.
                /// </summary>
                private class Node
                {
                    /// <summary>
                    /// Construct a Node
                    /// </summary>
                    internal Node(string value)
                    {
                        Value = value;
                    }

                    /// <summary>
                    /// The next node in the list.
                    /// </summary>
                    internal Node Next { get; set; }

                    /// <summary>
                    /// The held string.
                    /// </summary>
                    internal string Value { get; }
                }
            }
        }
    }
}
