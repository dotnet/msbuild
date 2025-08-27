// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;
using System.Threading;
using Microsoft.Build.Eventing;

namespace Microsoft.Build.Framework
{
    /// <summary>
    /// A StringBuilder lookalike that reuses its internal storage.
    /// </summary>
    /// <remarks>
    /// This class is being deprecated in favor of SpanBasedStringBuilder in StringTools. Avoid adding more uses.
    /// </remarks>
    internal sealed class ReuseableStringBuilder : IDisposable
    {
        /// <summary>
        /// Captured string builder.
        /// </summary>
        private StringBuilder? _borrowedBuilder;

        /// <summary>
        /// Capacity of borrowed string builder at the time of borrowing.
        /// </summary>
        private int _borrowedWithCapacity;

        /// <summary>
        /// Capacity to initialize the builder with.
        /// </summary>
        private int _capacity;

        /// <summary>
        /// Create a new builder, under the covers wrapping a reused one.
        /// </summary>
        internal ReuseableStringBuilder(int capacity = 16) // StringBuilder default is 16
        {
            // lazy initialization of the builder
            _capacity = capacity;
        }

        /// <summary>
        /// The length of the target.
        /// </summary>
        public int Length
        {
            get
            {
                return _borrowedBuilder?.Length ?? 0;
            }

            set
            {
                LazyPrepare();
                _borrowedBuilder.Length = value;
            }
        }

        /// <summary>
        /// Convert to a string.
        /// </summary>
        public override string ToString()
        {
            if (_borrowedBuilder == null)
            {
                return String.Empty;
            }

            return _borrowedBuilder.ToString();
        }

        /// <summary>
        /// Dispose, indicating you are done with this builder.
        /// </summary>
        public void Dispose()
        {
            if (_borrowedBuilder != null)
            {
                ReuseableStringBuilderFactory.Release(this);
                _borrowedBuilder = null;
                _capacity = -1;
            }
        }

        /// <summary>
        /// Append a character.
        /// </summary>
        internal ReuseableStringBuilder Append(char value)
        {
            LazyPrepare();
            _borrowedBuilder.Append(value);
            return this;
        }

        /// <summary>
        /// Append a string.
        /// </summary>
        internal ReuseableStringBuilder Append(string value)
        {
            LazyPrepare();
            _borrowedBuilder.Append(value);
            return this;
        }

        /// <summary>
        /// Append a substring.
        /// </summary>
        internal ReuseableStringBuilder Append(string value, int startIndex, int count)
        {
            LazyPrepare();
            _borrowedBuilder.Append(value, startIndex, count);
            return this;
        }

        /// <inheritdoc cref="StringBuilder.AppendFormat(IFormatProvider, string, object[])"/>
        internal ReuseableStringBuilder AppendFormat(
            CultureInfo currentCulture,
            string format,
            params object[] args)
        {
            LazyPrepare();
            _borrowedBuilder.AppendFormat(
                currentCulture,
                format,
                args);
            return this;
        }

        /// <inheritdoc cref="StringBuilder.AppendLine()"/>
        internal ReuseableStringBuilder AppendLine()
        {
            LazyPrepare();
            _borrowedBuilder.AppendLine();
            return this;
        }

        public ReuseableStringBuilder AppendSeparated(char separator, ICollection<string> strings)
        {
            LazyPrepare();

            var separatorsRemaining = strings.Count - 1;

            foreach (var s in strings)
            {
                _borrowedBuilder.Append(s);

                if (separatorsRemaining > 0)
                {
                    _borrowedBuilder.Append(separator);
                }

                separatorsRemaining--;
            }

            return this;
        }

        public ReuseableStringBuilder Clear()
        {
            LazyPrepare();
            _borrowedBuilder.Clear();
            return this;
        }

        /// <summary>
        /// Remove a substring.
        /// </summary>
        internal ReuseableStringBuilder Remove(int startIndex, int length)
        {
            LazyPrepare();
            _borrowedBuilder.Remove(startIndex, length);
            return this;
        }

        /// <summary>
        /// Grab a backing builder if necessary.
        /// </summary>
        [MemberNotNull(nameof(_borrowedBuilder))]
        private void LazyPrepare()
        {
            if (_borrowedBuilder == null)
            {
                FrameworkErrorUtilities.VerifyThrow(_capacity != -1, "Reusing after dispose");

                _borrowedBuilder = ReuseableStringBuilderFactory.Get(_capacity);
                _borrowedWithCapacity = _borrowedBuilder.Capacity;
            }
        }

        /// <summary>
        /// A utility class that mediates access to a shared string builder.
        /// </summary>
        /// <remarks>
        /// If this shared builder is highly contended, this class could add
        /// a second one and try both in turn.
        /// </remarks>
        private static class ReuseableStringBuilderFactory
        {
            /// <summary>
            /// Made up limit beyond which we won't share the builder
            /// because we could otherwise hold a huge builder indefinitely.
            /// This was picked empirically to save at least 95% of allocated data size.
            /// This constant has to be exactly 2^n (power of 2) where n = 4 ... 32 as GC is optimized to work with such block sizes.
            /// Same approach is used in ArrayPool or RecyclableMemoryStream so having same uniform allocation sizes will
            ///   reduce likelihood of heaps fragmentation.
            /// </summary>
            /// <remarks>
            /// In order to collect and analyze ETW ReusableStringBuilderFactory events developer could follow these steps:
            ///   - With compiled as Debug capture events by perfview; example: "perfview collect /NoGui /OnlyProviders=*Microsoft-Build"
            ///   - Open Events view and filter for ReusableStringBuilderFactory and pick ReusableStringBuilderFactory/Stop
            ///   - Display columns: returning length, type
            ///   - Set MaxRet limit to 1_000_000
            ///   - Right click and Open View in Excel
            ///   - Use Excel data analytic tools to extract required data from it. I recommend to use
            ///       Pivot Table/Chart with
            ///         filter: type=[return-se,discarder];
            ///         rows: returningLength grouped (right click and Group... into sufficient size bins)
            ///         value: sum of returningLength
            /// </remarks>
            /// <remarks>
            /// This constant might looks huge, but rather than lowering this constant,
            /// we shall focus on eliminating code which requires creating such huge strings.
            /// </remarks>
            private const int MaxBuilderSizeBytes = 2 * 1024 * 1024; // ~1M chars
            private const int MaxBuilderSizeCapacity = MaxBuilderSizeBytes / sizeof(char);

            /// <summary>
            /// The shared builder.
            /// </summary>
            private static StringBuilder? s_sharedBuilder;

#if DEBUG && ASSERT_BALANCE
            /// <summary>
            /// Balance between calling Get and Release.
            /// Shall be always 0 as Get and 1 at Release.
            /// </summary>
            private static int s_getVsReleaseBalance;
#endif

            /// <summary>
            /// Obtains a string builder which may or may not already
            /// have been used.
            /// Never returns null.
            /// </summary>
            internal static StringBuilder Get(int capacity)
            {
#if DEBUG && ASSERT_BALANCE
                int balance = Interlocked.Increment(ref s_getVsReleaseBalance);
                Debug.Assert(balance == 1, "Unbalanced Get vs Release. Either forgotten Release or used from multiple threads concurrently.");
#endif

                StringBuilder? returned = Interlocked.Exchange(ref s_sharedBuilder, null);

                if (returned == null)
                {
                    // Currently loaned out so return a new one with capacity in given bracket.
                    // If user wants bigger capacity than maximum capacity, respect it.
                    returned = new StringBuilder(SelectBracketedCapacity(capacity));
#if DEBUG
                    MSBuildEventSource.Log.ReusableStringBuilderFactoryStart(hash: returned.GetHashCode(), newCapacity: capacity, oldCapacity: 0, type: "miss");
#endif
                }
                else if (returned.Capacity < capacity)
                {
                    // It's essential we guarantee the capacity because this
                    // may be used as a buffer to a PInvoke call.
                    int newCapacity = SelectBracketedCapacity(capacity);
#if DEBUG
                    MSBuildEventSource.Log.ReusableStringBuilderFactoryStart(hash: returned.GetHashCode(), newCapacity: newCapacity, oldCapacity: returned.Capacity, type: "miss-need-bigger");
#endif
                    // Let the current StringBuilder be collected and create new with bracketed capacity. This way it allocates only char[newCapacity]
                    //   otherwise it would allocate char[new_capacity_of_last_chunk] (in set_Capacity) and char[newCapacity] (in Clear).
                    returned = new StringBuilder(newCapacity);
                }
                else
                {
#if DEBUG
                    MSBuildEventSource.Log.ReusableStringBuilderFactoryStart(hash: returned.GetHashCode(), newCapacity: capacity, oldCapacity: returned.Capacity, type: "hit");
#endif
                }

                return returned;
            }

            /// <summary>
            /// Returns the shared builder for the next caller to use.
            /// ** CALLERS, DO NOT USE THE BUILDER AFTER RELEASING IT HERE! **
            /// </summary>
            internal static void Release(ReuseableStringBuilder returning)
            {
#if DEBUG && ASSERT_BALANCE // Please define ASSERT_BALANCE if you need to analyze where we have cross thread competing usage of ReuseableStringBuilder
                int balance = Interlocked.Decrement(ref s_getVsReleaseBalance);
                Debug.Assert(balance == 0, "Unbalanced Get vs Release. Either forgotten Release or used from multiple threads concurrently.");
#endif
                FrameworkErrorUtilities.VerifyThrowInternalNull(returning._borrowedBuilder, nameof(returning._borrowedBuilder));

                StringBuilder returningBuilder = returning._borrowedBuilder!;
                int returningLength = returningBuilder.Length;

                // It's possible for someone to cause the builder to
                // enlarge to such an extent that this static field
                // would be a leak. To avoid that, only accept
                // the builder if it's no more than a certain size.
                //
                // If some code has a bug and forgets to return their builder
                // (or we refuse it here because it's too big) the next user will
                // get given a new one, and then return it soon after.
                // So the shared builder will be "replaced".
                if (returningBuilder.Capacity > MaxBuilderSizeCapacity)
                {
                    // In order to free memory usage by huge string builder, do not pool this one and let it be collected.
#if DEBUG
                    MSBuildEventSource.Log.ReusableStringBuilderFactoryStop(hash: returningBuilder.GetHashCode(), returningCapacity: returningBuilder.Capacity, returningLength: returningLength, type: "discard");
#endif
                }
                else
                {
                    if (returningBuilder.Capacity != returning._borrowedWithCapacity)
                    {
                        Debug.Assert(returningBuilder.Capacity > returning._borrowedWithCapacity, "Capacity can only increase");

                        // This builder used more than pre-allocated capacity bracket.
                        // Let this builder be collected and put new builder, with reflecting bracket capacity, into the pool.
                        // If we would just return this builder into pool as is, it would allocated new array[capacity] anyway (current implementation of returningBuilder.Clear() does it)
                        //   and that could lead to unpredictable amount of LOH allocations and eventual LOH fragmentation.
                        // Below implementation has predictable max Log2(MaxBuilderSizeBytes) string builder array re-allocations during whole process lifetime - unless MaxBuilderSizeCapacity is reached frequently.
                        int newCapacity = SelectBracketedCapacity(returningBuilder.Capacity);
                        returningBuilder = new StringBuilder(newCapacity);
                    }

                    returningBuilder.Clear(); // Clear before pooling

                    var oldSharedBuilder = Interlocked.Exchange(ref s_sharedBuilder, returningBuilder);
                    if (oldSharedBuilder != null)
                    {
                        // This can identify improper usage from multiple thread or bug in code - Get was reentered before Release.
                        // User of ReuseableStringBuilder has to make sure that calling method call stacks do not also use ReuseableStringBuilder.
                        // Look at stack traces of ETW events which contains reported string builder hashes.
                        MSBuildEventSource.Log.ReusableStringBuilderFactoryUnbalanced(oldHash: oldSharedBuilder.GetHashCode(), newHash: returningBuilder.GetHashCode());
                    }
#if DEBUG
                    MSBuildEventSource.Log.ReusableStringBuilderFactoryStop(hash: returningBuilder.GetHashCode(), returningCapacity: returningBuilder.Capacity, returningLength: returningLength, type: returning._borrowedBuilder != returningBuilder ? "return-new" : "return");
#endif
                }

                // Ensure ReuseableStringBuilder can no longer use _borrowedBuilder
                returning._borrowedBuilder = null;
            }

            private static int SelectBracketedCapacity(int requiredCapacity)
            {
                const int minimumCapacity = 0x100; // 256 characters, 512 bytes

                if (requiredCapacity <= minimumCapacity)
                {
                    return minimumCapacity;
                }

                // If user wants bigger capacity than maximum respect it as it could be used as buffer in P/Invoke.
                if (requiredCapacity >= MaxBuilderSizeCapacity)
                {
                    return requiredCapacity;
                }

                // Find next power of two http://graphics.stanford.edu/~seander/bithacks.html#RoundUpPowerOf2
                int v = requiredCapacity;

                v--;
                v |= v >> 1;
                v |= v >> 2;
                v |= v >> 4;
                v |= v >> 8;
                v |= v >> 16;
                v++;

                return v;
            }
        }
    }
}
