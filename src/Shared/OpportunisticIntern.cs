// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//-----------------------------------------------------------------------
// </copyright>
// <summary>Selectively intern strings.</summary>
//-----------------------------------------------------------------------

using System;
using System.Text;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using Microsoft.Build.Shared;

namespace Microsoft.Build
{
    /// <summary>
    /// This class is used to selectively intern strings. It should be used at the point of new string creation.
    /// For example,
    /// 
    ///     string interned = OpportunisticIntern.Intern(String.Join(",",someStrings));
    ///     
    /// This class uses heuristics to decide whether it will be efficient to intern a string or not. There is no
    /// guarantee that a string will intern.
    /// 
    /// The thresholds and sizes were determined by experimentation to give the best number of bytes saved
    /// at reasonable elapsed time cost.
    /// </summary>
    static internal class OpportunisticIntern
    {
        /// <summary>
        /// The size of the small mru list. 
        /// </summary>
        private static readonly int s_smallMruSize = AssignViaEnvironment("MSBUILDSMALLINTERNSIZE", 50);

        /// <summary>
        /// The size of the large mru list. 
        /// </summary>
        private static readonly int s_largeMruSize = AssignViaEnvironment("MSBUILDLARGEINTERNSIZE", 100);

        /// <summary>
        /// The size of the huge mru list. 
        /// </summary>
        private static readonly int s_hugeMruSize = AssignViaEnvironment("MSBUILDHUGEINTERNSIZE", 100);

        /// <summary>
        /// The smallest size a string can be to be considered small.
        /// </summary>
        private static readonly int s_smallMruThreshhold = AssignViaEnvironment("MSBUILDSMALLINTERNTHRESHOLD", 50);

        /// <summary>
        /// The smallest size a string can be to be considered large.
        /// </summary>
        private static readonly int s_largeMruThreshhold = AssignViaEnvironment("MSBUILDLARGEINTERNTHRESHOLD", 70);

        /// <summary>
        /// The smallest size a string can be to be considered huge.
        /// </summary>
        private static readonly int s_hugeMruThreshhold = AssignViaEnvironment("MSBUILDHUGEINTERNTHRESHOLD", 200);

        /// <summary>
        /// The smallest size a string can be to be ginormous.
        /// 8K for large object heap.
        /// </summary>
        private static readonly int s_ginormousThreshhold = AssignViaEnvironment("MSBUILDGINORMOUSINTERNTHRESHOLD", 8000);

        /// <summary>
        /// Manages the separate MRU lists.
        /// </summary>
        private static BucketedPrioritizedStringList s_si = new BucketedPrioritizedStringList(/*gatherStatistics*/ false, s_smallMruSize, s_largeMruSize, s_hugeMruSize, s_smallMruThreshhold, s_largeMruThreshhold, s_hugeMruThreshhold, s_ginormousThreshhold);

        #region Statistics
        /// <summary>
        /// What if Mru lists were infinitely long?
        /// </summary>
        private static BucketedPrioritizedStringList s_whatIfInfinite;

        /// <summary>
        /// What if we doubled the size of the Mru lists?
        /// </summary>
        private static BucketedPrioritizedStringList s_whatIfDoubled;

        /// <summary>
        /// What if we halved the size of the Mru lists?
        /// </summary>
        private static BucketedPrioritizedStringList s_whatIfHalved;

        /// <summary>
        /// What if the size of Mru lists was zero? (We still intern tiny strings in this case)
        /// </summary>
        private static BucketedPrioritizedStringList s_whatIfZero;
        #endregion

        #region IInternable
        /// <summary>
        /// Define the methods needed to intern something.
        /// </summary>
        internal interface IInternable
        {
            /// <summary>
            /// The length of the target.
            /// </summary>
            int Length { get; }

            /// <summary>
            /// Indexer into the target. Presumed to be fast.
            /// </summary>
            char this[int index]
            {
                get;
            }

            /// <summary>
            /// Convert target to string. Presumed to be slow (and will be called just once).
            /// </summary>
            string ExpensiveConvertToString();

            /// <summary>
            /// Compare target to string. Assumes lengths are equal.
            /// </summary>
            bool IsOrdinalEqualToStringOfSameLength(string other);

            /// <summary>
            /// Reference compare target to string. If target is non-string this should return false.
            /// </summary>
            bool ReferenceEquals(string other);
        }
        #endregion

        /// <summary>
        /// Assign an int from an environment variable. If its not present, use the default.
        /// </summary>
        static internal int AssignViaEnvironment(string env, int @default)
        {
            string threshhold = Environment.GetEnvironmentVariable(env);
            if (!String.IsNullOrEmpty(threshhold))
            {
                int result;
                if (Int32.TryParse(threshhold, out result))
                {
                    return result;
                }
            }

            return @default;
        }

        /// <summary>
        /// Turn on statistics gathering.
        /// </summary>
        internal static void EnableStatisticsGathering()
        {
            // Statistics include several 'what if' scenarios such as doubling the size of the MRU lists.
            s_si = new BucketedPrioritizedStringList(/*gatherStatistics*/ true, s_smallMruSize, s_largeMruSize, s_hugeMruSize, s_smallMruThreshhold, s_largeMruThreshhold, s_hugeMruThreshhold, s_ginormousThreshhold);
            s_whatIfInfinite = new BucketedPrioritizedStringList(/*gatherStatistics*/ true, Int32.MaxValue, Int32.MaxValue, Int32.MaxValue, s_smallMruThreshhold, s_largeMruThreshhold, s_hugeMruThreshhold, s_ginormousThreshhold);
            s_whatIfDoubled = new BucketedPrioritizedStringList(/*gatherStatistics*/ true, s_smallMruSize * 2, s_largeMruSize * 2, s_hugeMruSize * 2, s_smallMruThreshhold, s_largeMruThreshhold, s_hugeMruThreshhold, s_ginormousThreshhold);
            s_whatIfHalved = new BucketedPrioritizedStringList(/*gatherStatistics*/ true, s_smallMruSize / 2, s_largeMruSize / 2, s_hugeMruSize / 2, s_smallMruThreshhold, s_largeMruThreshhold, s_hugeMruThreshhold, s_ginormousThreshhold);
            s_whatIfZero = new BucketedPrioritizedStringList(/*gatherStatistics*/ true, 0, 0, 0, s_smallMruThreshhold, s_largeMruThreshhold, s_hugeMruThreshhold, s_ginormousThreshhold);
        }

        /// <summary>
        /// Intern the given internable.
        /// </summary>
        internal static string InternableToString(IInternable candidate)
        {
            if (s_whatIfInfinite != null)
            {
                s_whatIfInfinite.InterningToString(candidate);
                s_whatIfDoubled.InterningToString(candidate);
                s_whatIfHalved.InterningToString(candidate);
                s_whatIfZero.InterningToString(candidate);
            }

            string result = s_si.InterningToString(candidate);
#if _DEBUG
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
        /// Report statistics about interning. Don't call unless GatherStatistics has been called beforehand.
        /// </summary>
        internal static void ReportStatistics()
        {
            s_si.ReportStatistics("Main");
            s_whatIfInfinite.ReportStatistics("if Infinite");
            s_whatIfDoubled.ReportStatistics("if Doubled");
            s_whatIfHalved.ReportStatistics("if Halved");
            s_whatIfZero.ReportStatistics("if Zero");
            Console.WriteLine(" * Even for MRU size of zero there will still be some intern hits because of the tiny ");
            Console.WriteLine("   string matching (eg. 'true')");
        }

        #region IInternable Implementations
        /// <summary>
        /// A wrapper over StringBuilder.
        /// </summary>
        internal struct StringBuilderInternTarget : IInternable
        {
            /// <summary>
            /// The held StringBuilder
            /// </summary>
            private StringBuilder _target;

            /// <summary>
            /// Pointless comment about constructor.
            /// </summary>
            internal StringBuilderInternTarget(StringBuilder target)
            {
                _target = target;
            }

            /// <summary>
            /// The length of the target.
            /// </summary>
            public int Length
            {
                get
                {
                    return _target.Length;
                }
            }

            /// <summary>
            /// Indexer into the target. Presumed to be fast.
            /// </summary>
            public char this[int index]
            {
                get
                {
                    return _target[index];
                }
            }

            /// <summary>
            /// Never reference equals to string.
            /// </summary>
            public bool ReferenceEquals(string other)
            {
                return false;
            }

            /// <summary>
            /// Convert target to string. Presumed to be slow (and will be called just once).
            /// </summary>
            public string ExpensiveConvertToString()
            {
                // PERF NOTE: This will be an allocation hot-spot because the StringBuilder is finally determined to
                // not be internable. There is still only one conversion of StringBuilder into string it has just 
                // moved into this single spot.
                return _target.ToString();
            }

            /// <summary>
            /// Compare target to string. Assumes lengths are equal.
            /// </summary>
            public bool IsOrdinalEqualToStringOfSameLength(string other)
            {
#if DEBUG
                ErrorUtilities.VerifyThrow(other.Length == _target.Length, "should be same length");
#endif
                int length = _target.Length;

                // Backwards because the end of the string is (by observation of Australian Government build) more likely to be different earlier in the loop.
                // For example, C:\project1, C:\project2
                for (int i = length - 1; i >= 0; --i)
                {
                    if (_target[i] != other[i])
                    {
                        return false;
                    }
                }

                return true;
            }

            /// <summary>
            /// Don't use this function. Use ExpensiveConvertToString
            /// </summary>
            public override string ToString()
            {
                throw new InvalidOperationException();
            }
        }

        /// <summary>
        /// A wrapper over char[].
        /// </summary>
        internal struct CharArrayInternTarget : IInternable
        {
            /// <summary>
            /// Start index for the string
            /// </summary>
            private int _startIndex;

            /// <summary>
            /// Number of characters.
            /// </summary>
            private int _count;

            /// <summary>
            /// The held array
            /// </summary>
            private char[] _target;

            /// <summary>
            /// Pointless comment about constructor.
            /// </summary>
            internal CharArrayInternTarget(char[] target, int count)
                : this(target, 0, count)
            {
            }

            /// <summary>
            /// Pointless comment about constructor.
            /// </summary>
            internal CharArrayInternTarget(char[] target, int startIndex, int count)
            {
#if DEBUG
                if (startIndex + count > target.Length)
                {
                    ErrorUtilities.ThrowInternalError("wrong length");
                }
#endif
                _target = target;
                _startIndex = startIndex;
                _count = count;
            }

            /// <summary>
            /// The length of the target.
            /// </summary>
            public int Length
            {
                get
                {
                    return _count;
                }
            }

            /// <summary>
            /// Indexer into the target. Presumed to be fast.
            /// </summary>
            public char this[int index]
            {
                get
                {
                    if (index > _startIndex + _count - 1 || index < 0)
                    {
                        ErrorUtilities.ThrowInternalError("past end");
                    }

                    return _target[index + _startIndex];
                }
            }

            /// <summary>
            /// Convert target to string. Presumed to be slow (and will be called just once).
            /// </summary>
            public bool ReferenceEquals(string other)
            {
                return false;
            }

            /// <summary>
            /// Convert target to string. Presumed to be slow (and will be called just once).
            /// </summary>
            public string ExpensiveConvertToString()
            {
                // PERF NOTE: This will be an allocation hot-spot because the char[] is finally determined to
                // not be internable. There is still only one conversion of char[] into string it has just 
                // moved into this single spot.
                return new String(_target, _startIndex, _count);
            }

            /// <summary>
            /// Compare target to string. Assumes lengths are equal.
            /// </summary>
            public bool IsOrdinalEqualToStringOfSameLength(string other)
            {
#if DEBUG
                ErrorUtilities.VerifyThrow(other.Length == this.Length, "should be same length");
#endif
                // Backwards because the end of the string is (by observation of Australian Government build) more likely to be different earlier in the loop.
                // For example, C:\project1, C:\project2
                for (int i = _count - 1; i >= 0; --i)
                {
                    if (_target[i + _startIndex] != other[i])
                    {
                        return false;
                    }
                }

                return true;
            }

            /// <summary>
            /// Don't use this function. Use ExpensiveConvertToString
            /// </summary>
            public override string ToString()
            {
                throw new InvalidOperationException();
            }
        }

        /// <summary>
        /// Wrapper over a string.
        /// </summary>
        internal struct StringInternTarget : IInternable
        {
            /// <summary>
            /// Stores the wrapped string.
            /// </summary>
            private string _target;

            /// <summary>
            /// Constructor of the class
            /// </summary>
            /// <param name="target">The string to wrap</param>
            internal StringInternTarget(string target)
            {
                ErrorUtilities.VerifyThrowArgumentLength(target, "target");
                _target = target;
            }

            /// <summary>
            /// Gets the length of the target string.
            /// </summary>
            public int Length
            {
                get { return _target.Length; }
            }

            /// <summary>
            /// Gets the n character in the target string.
            /// </summary>
            /// <param name="index">Index of the character to gather.</param>
            /// <returns>The character in the position marked by index.</returns>
            public char this[int index]
            {
                get { return _target[index]; }
            }

            /// <summary>
            /// Returs the target which is already a string.
            /// </summary>
            /// <returns>The target string.</returns>
            public string ExpensiveConvertToString()
            {
                return _target;
            }

            /// <summary>
            /// Compare if the target string is equal to the given string.
            /// </summary>
            /// <param name="other">The string to compare with the target.</param>
            /// <returns>True if the strings are equal, false otherwise.</returns>
            public bool IsOrdinalEqualToStringOfSameLength(string other)
            {
                return _target.Equals(other, StringComparison.Ordinal);
            }

            /// <summary>
            /// Verifies if the reference of the target string is the same of the given string.
            /// </summary>
            /// <param name="other">The string reference to compare to.</param>
            /// <returns>True if both references are equal, false otherwise.</returns>
            public bool ReferenceEquals(string other)
            {
                return Object.ReferenceEquals(_target, other);
            }
        }

        #endregion

        /// <summary>
        /// Manages a set of mru lists that hold strings in varying size ranges.
        /// </summary>
        private class BucketedPrioritizedStringList
        {
            /// <summary>
            /// The small string Mru list.
            /// </summary>
            private PrioritizedStringList _smallMru;

            /// <summary>
            /// The large string Mru list.
            /// </summary>
            private PrioritizedStringList _largeMru;

            /// <summary>
            /// The huge string Mru list.
            /// </summary>
            private PrioritizedStringList _hugeMru;

            /// <summary>
            /// Three most recently used strings over 8K.
            /// </summary>
            private LinkedList<WeakReference> _ginormous = new LinkedList<WeakReference>();

            /// <summary>
            /// The smallest size a string can be to be considered small.
            /// </summary>
            private int _smallMruThreshhold;

            /// <summary>
            /// The smallest size a string can be to be considered large.
            /// </summary>
            private int _largeMruThreshhold;

            /// <summary>
            /// The smallest size a string can be to be considered huge.
            /// </summary>
            private int _hugeMruThreshhold;

            /// <summary>
            /// The smallest size a string can be to be ginormous.
            /// </summary>
            private int _ginormousThreshhold;

            #region Statistics
            /// <summary>
            /// Whether or not to gather statistics
            /// </summary>
            private bool _gatherStatistics = false;

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
            private bool _dontTrack;

            /// <summary>
            /// The time spent interning.
            /// </summary>
            private Stopwatch _stopwatch;

            /// <summary>
            /// Strings which did not intern
            /// </summary>
            private Dictionary<string, int> _missedStrings;

            /// <summary>
            /// Strings which we didn't attempt to intern
            /// </summary>
            private Dictionary<string, int> _rejectedStrings;

            /// <summary>
            /// Number of ginormous strings to keep
            /// By observation of Auto7, there are about three variations of the huge solution config blob
            /// There aren't really any other strings of this size, but make it 10 to be sure. (There will barely be any misses)
            /// </summary>
            private int _ginormousSize = 10;

            #endregion

            /// <summary>
            /// Construct.
            /// </summary>
            internal BucketedPrioritizedStringList(bool gatherStatistics, int smallMruSize, int largeMruSize, int hugeMruSize, int smallMruThreshhold, int largeMruThreshhold, int hugeMruThreshhold, int ginormousThreshhold)
            {
                if (smallMruSize == 0 && largeMruSize == 0 && hugeMruSize == 0)
                {
                    _dontTrack = true;
                }

                _smallMru = new PrioritizedStringList(smallMruSize);
                _largeMru = new PrioritizedStringList(largeMruSize);
                _hugeMru = new PrioritizedStringList(hugeMruSize);
                _smallMruThreshhold = smallMruThreshhold;
                _largeMruThreshhold = largeMruThreshhold;
                _hugeMruThreshhold = hugeMruThreshhold;
                _ginormousThreshhold = ginormousThreshhold;

                for (int i = 0; i < _ginormousSize; i++)
                {
                    _ginormous.AddFirst(new WeakReference(String.Empty));
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
            internal string InterningToString(IInternable candidate)
            {
                if (candidate.Length == 0)
                {
                    // As in the case that a property or itemlist has evaluated to empty.
                    return String.Empty;
                }

                if (_gatherStatistics)
                {
                    return InternWithStatistics(candidate);
                }
                else
                {
                    string result;
                    TryIntern(candidate, out result);
                    return result;
                }
            }

            /// <summary>
            /// Report statistics to the console.
            /// </summary>
            internal void ReportStatistics(string heading)
            {
                string title = "Opportunistic Intern (" + heading + ")";
                Console.WriteLine("\n{0}{1}{0}", new String('=', 41 - (title.Length / 2)), title);
                Console.WriteLine("||{0,50}|{1,20:N0}|{2,8}|", "Intern Hits", _internHits, "hits");
                Console.WriteLine("||{0,50}|{1,20:N0}|{2,8}|", "Intern Misses", _internMisses, "misses");
                Console.WriteLine("||{0,50}|{1,20:N0}|{2,8}|", "Intern Rejects (as shorter than " + s_smallMruThreshhold + " bytes)", _internRejects, "rejects");
                Console.WriteLine("||{0,50}|{1,20:N0}|{2,8}|", "Eliminated Strings*", _internEliminatedStrings, "strings");
                Console.WriteLine("||{0,50}|{1,20:N0}|{2,8}|", "Eliminated Chars", _internEliminatedChars, "chars");
                Console.WriteLine("||{0,50}|{1,20:N0}|{2,8}|", "Estimated Eliminated Bytes", _internEliminatedChars * 2, "bytes");
                Console.WriteLine("Elimination assumes that strings provided were unique objects.");
                Console.WriteLine("|---------------------------------------------------------------------------------|");
                KeyValuePair<int, int> held = _smallMru.Statistics();
                Console.WriteLine("||{0,50}|{1,20:N0}|{2,8}|", "Small Strings MRU Size", s_smallMruSize, "strings");
                Console.WriteLine("||{0,50}|{1,20:N0}|{2,8}|", "Small Strings (>=" + _smallMruThreshhold + " chars) Held", held.Key, "strings");
                Console.WriteLine("||{0,50}|{1,20:N0}|{2,8}|", "Small Estimated Bytes Held", held.Value * 2, "bytes");
                Console.WriteLine("|---------------------------------------------------------------------------------|");
                held = _largeMru.Statistics();
                Console.WriteLine("||{0,50}|{1,20:N0}|{2,8}|", "Large Strings MRU Size", s_largeMruSize, "strings");
                Console.WriteLine("||{0,50}|{1,20:N0}|{2,8}|", "Large Strings  (>=" + _largeMruThreshhold + " chars) Held", held.Key, "strings");
                Console.WriteLine("||{0,50}|{1,20:N0}|{2,8}|", "Large Estimated Bytes Held", held.Value * 2, "bytes");
                Console.WriteLine("|---------------------------------------------------------------------------------|");
                held = _hugeMru.Statistics();
                Console.WriteLine("||{0,50}|{1,20:N0}|{2,8}|", "Huge Strings MRU Size", s_hugeMruSize, "strings");
                Console.WriteLine("||{0,50}|{1,20:N0}|{2,8}|", "Huge Strings  (>=" + _hugeMruThreshhold + " chars) Held", held.Key, "strings");
                Console.WriteLine("||{0,50}|{1,20:N0}|{2,8}|", "Huge Estimated Bytes Held", held.Value * 2, "bytes");
                Console.WriteLine("|---------------------------------------------------------------------------------|");
                Console.WriteLine("||{0,50}|{1,20:N0}|{2,8}|", "Ginormous Strings MRU Size", _ginormousSize, "strings");
                Console.WriteLine("||{0,50}|{1,20:N0}|{2,8}|", "Ginormous (>=" + _ginormousThreshhold + " chars)  Hits", _ginormousHits, "hits");
                Console.WriteLine("||{0,50}|{1,20:N0}|{2,8}|", "Ginormous Misses", _ginormousMisses, "misses");
                Console.WriteLine("||{0,50}|{1,20:N0}|{2,8}|", "Ginormous Chars Saved", _ginormousCharsSaved, "chars");
                Console.WriteLine("|---------------------------------------------------------------------------------|");

                // There's no point in reporting the ginormous string because it will have evaporated by now.
                Console.WriteLine("||{0,50}|{1,20:N0}|{2,8}|", "Time Spent Interning", _stopwatch.ElapsedMilliseconds, "ms");
                Console.WriteLine("{0}{0}", new String('=', 41));

                var topMissingString =
                    _missedStrings
                    .OrderByDescending(kv => kv.Value * kv.Key.Length)
                    .Take(15)
                    .Where(kv => kv.Value > 1)
                    .Select(kv => String.Format(CultureInfo.InvariantCulture, "({1} instances x each {2} chars = {3}KB wasted)\n{0}", kv.Key, kv.Value, kv.Key.Length, (kv.Value - 1) * kv.Key.Length * 2 / 1024));

                Console.WriteLine("##########Top Missed Strings:  \n{0} ", String.Join("\n==============\n", topMissingString.ToArray()));
                Console.WriteLine();

                var topRejectedString =
                    _rejectedStrings
                    .OrderByDescending(kv => kv.Value * kv.Key.Length)
                    .Take(15)
                    .Where(kv => kv.Value > 1)
                    .Select(kv => String.Format(CultureInfo.InvariantCulture, "({1} instances x each {2} chars = {3}KB wasted)\n{0}", kv.Key, kv.Value, kv.Key.Length, (kv.Value - 1) * kv.Key.Length * 2 / 1024));

                Console.WriteLine("##########Top Rejected Strings: \n{0} ", String.Join("\n==============\n", topRejectedString.ToArray()));
            }

            /// <summary>
            /// Try to intern the string. 
            /// Return true if an interned value could be returned.
            /// Return false if it was added to the intern list, but wasn't there already.
            /// Return null if it didn't meet the length criteria for any of the buckets.
            /// </summary>
            private bool? TryIntern(IInternable candidate, out string interned)
            {
                int length = candidate.Length;

                // First, try the hard coded intern strings.
                // Each of the hard-coded small strings below showed up in a profile run with considerable duplication in memory.
                if (!_dontTrack)
                {
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
                        if (candidate[0] == 'T')
                        {
                            if (candidate[1] == 'R' && candidate[2] == 'U' && candidate[3] == 'E')
                            {
                                interned = "TRUE";
                                return true;
                            }

                            if (candidate[1] == 'r' && candidate[2] == 'u' && candidate[3] == 'e')
                            {
                                interned = "True";
                                return true;
                            }
                        }

                        if (candidate[0] == 'C' && candidate[1] == 'o' && candidate[2] == 'p' && candidate[3] == 'y')
                        {
                            interned = "Copy";
                            return true;
                        }

                        if (candidate[0] == 't' && candidate[1] == 'r' && candidate[2] == 'u' && candidate[3] == 'e')
                        {
                            interned = "true";
                            return true;
                        }

                        if (candidate[0] == 'v' && candidate[1] == '4' && candidate[2] == '.' && candidate[3] == '0')
                        {
                            interned = "v4.0";
                            return true;
                        }
                    }
                    else if (length == 5)
                    {
                        if (candidate[0] == 'F' && candidate[1] == 'A' && candidate[2] == 'L' && candidate[3] == 'S' && candidate[4] == 'E')
                        {
                            interned = "FALSE";
                            return true;
                        }

                        if (candidate[0] == 'f' && candidate[1] == 'a' && candidate[2] == 'l' && candidate[3] == 's' && candidate[4] == 'e')
                        {
                            interned = "false";
                            return true;
                        }

                        if (candidate[0] == 'D' && candidate[1] == 'e' && candidate[2] == 'b' && candidate[3] == 'u' && candidate[4] == 'g')
                        {
                            interned = "Debug";
                            return true;
                        }

                        if (candidate[0] == 'B' && candidate[1] == 'u' && candidate[2] == 'i' && candidate[3] == 'l' && candidate[4] == 'd')
                        {
                            interned = "Build";
                            return true;
                        }

                        if (candidate[0] == 'W' && candidate[1] == 'i' && candidate[2] == 'n' && candidate[3] == '3' && candidate[4] == '2')
                        {
                            interned = "Win32";
                            return true;
                        }
                    }
                    else if (length == 6)
                    {
                        if (candidate[0] == '\'' && candidate[1] == '\'' && candidate[2] == '!' && candidate[3] == '=' && candidate[4] == '\'' && candidate[5] == '\'')
                        {
                            interned = "''!=''";
                            return true;
                        }

                        if (candidate[0] == 'A' && candidate[1] == 'n' && candidate[2] == 'y' && candidate[3] == 'C' && candidate[4] == 'P' && candidate[5] == 'U')
                        {
                            interned = "AnyCPU";
                            return true;
                        }
                    }
                    else if (length == 7)
                    {
                        if (candidate[0] == 'L' && candidate[1] == 'i' && candidate[2] == 'b' && candidate[3] == 'r' && candidate[4] == 'a' && candidate[5] == 'r' && candidate[6] == 'y')
                        {
                            interned = "Library";
                            return true;
                        }

                        if (candidate[0] == 'M' && candidate[1] == 'S' && candidate[2] == 'B' && candidate[3] == 'u' && candidate[4] == 'i' && candidate[5] == 'l' && candidate[6] == 'd')
                        {
                            interned = "MSBuild";
                            return true;
                        }

                        if (candidate[0] == 'R' && candidate[1] == 'e' && candidate[2] == 'l' && candidate[3] == 'e' && candidate[4] == 'a' && candidate[5] == 's' && candidate[6] == 'e')
                        {
                            interned = "Release";
                            return true;
                        }
                    }
                    else if (length == 24)
                    {
                        if (candidate[0] == 'R' && candidate[1] == 'e' && candidate[2] == 's' && candidate[3] == 'o' && candidate[4] == 'l' && candidate[5] == 'v' && candidate[6] == 'e')
                        {
                            if (candidate[7] == 'A' && candidate[8] == 's' && candidate[9] == 's' && candidate[10] == 'e' && candidate[11] == 'm' && candidate[12] == 'b' && candidate[13] == 'l' && candidate[14] == 'y')
                            {
                                if (candidate[15] == 'R' && candidate[16] == 'e' && candidate[17] == 'f' && candidate[18] == 'e' && candidate[19] == 'r' && candidate[20] == 'e' && candidate[21] == 'n' && candidate[22] == 'c' && candidate[23] == 'e')
                                {
                                    interned = "ResolveAssemblyReference";
                                    return true;
                                }
                            }
                        }
                    }
                    else if (length > _ginormousThreshhold)
                    {
                        lock (_ginormous)
                        {
                            LinkedListNode<WeakReference> current = _ginormous.First;

                            while (current != null)
                            {
                                string last = current.Value.Target as string;
                                if (last != null && last.Length == candidate.Length && candidate.IsOrdinalEqualToStringOfSameLength(last))
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

                            var lastNode = _ginormous.Last;
                            _ginormous.RemoveLast();
                            _ginormous.AddFirst(lastNode);
                            lastNode.Value.Target = interned;

                            return false;
                        }
                    }
                    else if (length >= _hugeMruThreshhold)
                    {
                        lock (_hugeMru)
                        {
                            return _hugeMru.TryGet(candidate, out interned);
                        }
                    }
                    else if (length >= _largeMruThreshhold)
                    {
                        lock (_largeMru)
                        {
                            return _largeMru.TryGet(candidate, out interned);
                        }
                    }
                    else if (length >= _smallMruThreshhold)
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
            private string InternWithStatistics(IInternable candidate)
            {
                string result;
                _stopwatch.Start();
                bool? interned = TryIntern(candidate, out result);
                _stopwatch.Stop();

                if (interned.HasValue && !interned.Value)
                {
                    // Could not intern.
                    _internMisses++;

                    int priorCount = 0;
                    _missedStrings.TryGetValue(result, out priorCount);
                    _missedStrings[result] = priorCount + 1;

                    return result;
                }
                else if (interned == null)
                {
                    // Decided not to attempt interning
                    _internRejects++;

                    int priorCount = 0;
                    _rejectedStrings.TryGetValue(result, out priorCount);
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
                internal bool TryGet(IInternable candidate, out string interned)
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
                            if (candidate.IsOrdinalEqualToStringOfSameLength(head.Value))
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
                        _mru = new Node(candidate.ExpensiveConvertToString());
                        _mru.Next = old;

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
                    internal string Value { get; private set; }
                }
            }
        }
    }
}
