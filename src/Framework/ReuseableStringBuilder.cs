﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
#define ASSERT_BALANCE

using System;
using System.Collections.Generic;
using System.Diagnostics;
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
        private StringBuilder _borrowedBuilder;

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
            _capacity = capacity;

            // lazy initialization of the builder
        }

        /// <summary>
        /// The length of the target.
        /// </summary>
        public int Length
        {
            get { return (_borrowedBuilder == null) ? 0 : _borrowedBuilder.Length; }
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
        void IDisposable.Dispose()
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
            /// This constant has to exactly 2^n (power of 2) where n = 4 ... 32
            /// </summary>
            /// <remarks>
            /// This constant might looks huge, but rather that lowering this constant,
            ///   we shall focus on eliminating of code which requires to create such huge strings.
            /// </remarks>
            private const int MaxBuilderSizeBytes = 2 * 1024 * 1024; // ~1M chars
            private const int MaxBuilderSizeCapacity = MaxBuilderSizeBytes / 2;

            private static readonly IReadOnlyList<int> s_capacityBrackets;

            static ReuseableStringBuilderFactory()
            {
                var brackets = new List<int>();

                int bytes = 0x200; // Minimal capacity is 256 (512 bytes) as this was, according to captured traces, mean required capacity
                while (bytes <= MaxBuilderSizeBytes)
                {
                    // Allocation of arrays is optimized in byte[bytes] => bytes = 2^n.
                    // StringBuilder allocates chars[capacity] and each char is 2 bytes so lets have capacity brackets computed as `bytes/2` 
                    brackets.Add(bytes/2); 
                    bytes <<= 1;
                }
                Debug.Assert((bytes >> 1) == MaxBuilderSizeBytes, "MaxBuilderSizeBytes has to be 2^n (power of 2)");

                s_capacityBrackets = brackets;
            }

            /// <summary>
            /// The shared builder.
            /// </summary>
            private static StringBuilder s_sharedBuilder;

#if DEBUG
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

                var returned = Interlocked.Exchange(ref s_sharedBuilder, null);

                if (returned == null)
                {
                    // Currently loaned out so return a new one with capacity in given bracket.
                    // If user wants bigger capacity that maximum capacity respect it.
                    returned = new StringBuilder(SelectBracketedCapacity(capacity));
#if DEBUG
                    MSBuildEventSource.Log.ReusableStringBuilderFactoryStart(hash: returned.GetHashCode(), newCapacity:capacity, oldCapacity:0, type:"miss");
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
                    returned = new StringBuilder(SelectBracketedCapacity(newCapacity));
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
#if DEBUG && ASSERT_BALANCE
                int balance = Interlocked.Decrement(ref s_getVsReleaseBalance);
                Debug.Assert(balance == 0, "Unbalanced Get vs Release. Either forgotten Release or used from multiple threads concurrently.");
#endif

                StringBuilder returningBuilder = returning._borrowedBuilder;
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
                    // In order to free memory usage by huge string builder, do not pull this one and let it be collected.
#if DEBUG
                    MSBuildEventSource.Log.ReusableStringBuilderFactoryStop(hash: returningBuilder.GetHashCode(), returningCapacity: returningBuilder.Capacity, returningLength: returningLength, type: "discard");
#endif
                }
                else
                {
                    if (returningBuilder.Capacity != returning._borrowedWithCapacity)
                    {
                        Debug.Assert(returningBuilder.Capacity > returning._borrowedWithCapacity, "Capacity can only increase");

                        // This builder used more that pre-allocated capacity bracket.
                        // Let this builder be collected and put new builder, with reflecting bracket capacity, into the pool.
                        // If we would just return this builder into pool as is, it would allocated new array[capacity] anyway (current implementation of returningBuilder.Clear() does it)
                        //   and that could lead to unpredictable amount of LOH allocations and eventual LOH fragmentation.
                        // Bellow implementation have predictable max Log2(MaxBuilderSizeBytes) string builder array re-allocations during whole process lifetime - unless MaxBuilderSizeCapacity is reached frequently.
                        int newCapacity = SelectBracketedCapacity(returningBuilder.Capacity);
                        returningBuilder = new StringBuilder(newCapacity);
                    }

                    returningBuilder.Clear(); // Clear before pooling

                    var oldSharedBuilder = Interlocked.Exchange(ref s_sharedBuilder, returningBuilder);
                    if (oldSharedBuilder != null)
                    {
#if DEBUG
                        // This can identify in-proper usage from multiple thread or bug in code - Get was reentered before Release.
                        // User of ReuseableStringBuilder has to make sure that calling method call stacks do not also use ReuseableStringBuilder.
                        // Look at stack traces of ETW events which contains reported string builder hashes.
                        MSBuildEventSource.Log.ReusableStringBuilderFactoryReplace(oldHash: oldSharedBuilder.GetHashCode(), newHash: returningBuilder.GetHashCode());
#endif
                    }
#if DEBUG
                    MSBuildEventSource.Log.ReusableStringBuilderFactoryStop(hash: returningBuilder.GetHashCode(), returningCapacity: returningBuilder.Capacity, returningLength: returningLength, type: returning._borrowedBuilder != returningBuilder ? "return-new" : "return");
#endif
                }
            }

            private static int SelectBracketedCapacity(int requiredCapacity)
            {
                foreach (int bracket in s_capacityBrackets)
                {
                    if (requiredCapacity <= bracket)
                    {
                        return bracket;
                    }
                }

                // If user wants bigger capacity than maximum respect it as it could be used as buffer in P/Invoke.
                return requiredCapacity;
            }
        }
    }
}
