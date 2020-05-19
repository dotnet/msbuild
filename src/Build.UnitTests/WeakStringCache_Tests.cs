// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;

using Shouldly;
using Xunit;

namespace Microsoft.Build.UnitTests
{
    public class WeakStringCache_Tests : IDisposable
    {
        /// <summary>
        /// The weak string cache under test.
        /// </summary>
        private WeakStringCache _cache = new WeakStringCache();

        public void Dispose()
        {
            _cache.Dispose();
        }

        /// <summary>
        /// Triggers full GC that reliably collects all objects without strong GC roots and also nulls out weak GC handles.
        /// </summary>
        /// <remarks>
        /// While a simple GC.Collect() would be enough when running on .NET Framework or .NET Core, the Mono runtime appears to have
        /// GC handles implemented differently and requires a bit more effort. We don't care when exactly our handles are nulled out
        /// as long as it eventually happens for otherwise unreachable strings.
        /// </remarks>
        private void RunGC()
        {
            for (int i = 0; i < 2; i++)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
            }
        }

        /// <summary>
        /// Adds a string to the cache under test.
        /// </summary>
        /// <param name="strPart1">Part one of the string (split to prevent runtime interning and unintended GC roots).</param>
        /// <param name="strPart2">Part two of the string (split to prevent runtime interning and unintended GC roots).</param>
        /// <param name="callbackToRunWithTheStringAlive">Callback to be invoked after the string has been added but before the strong GC ref is released.</param>
        /// <returns>The hash code of the string as calculated by WeakStringCache.</returns>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private int AddString(string strPart1, string strPart2, Action<string> callbackToRunWithTheStringAlive)
        {
            // Compose the string with SB so it doesn't get interned by the runtime.
            string testString = new StringBuilder(strPart1).Append(strPart2).ToString();
            StringInternTarget testStringTarget = new StringInternTarget(testString);

            int hashCode = WeakStringCache.GetInternableHashCode(testStringTarget);

            string cachedString = _cache.GetOrCreateEntry(testStringTarget, out bool cacheHit);
            cacheHit.ShouldBeFalse();
            cachedString.ShouldBeSameAs(testString);

            callbackToRunWithTheStringAlive(cachedString);

            // Verify that the string is really in the cache and the cache returns the interned instance.
            string testStringCopy = new StringBuilder(strPart1).Append(strPart2).ToString();
            cachedString = _cache.GetOrCreateEntry(new StringInternTarget(testStringCopy), out cacheHit);
            cacheHit.ShouldBeTrue();
            cachedString.ShouldBeSameAs(testString);

            // Trigger full GC and verify that nothing has changed since we're still keeping testString alive.
            RunGC();

            callbackToRunWithTheStringAlive(cachedString);

            cachedString = _cache.GetOrCreateEntry(new StringInternTarget(testStringCopy), out cacheHit);
            cacheHit.ShouldBeTrue();
            cachedString.ShouldBeSameAs(testString);

            return hashCode;
        }

        /// <summary>
        /// Adds strings that are known to have a hash code collision to the cache under test.
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private void AddStringsWithHashCollision(int numberOfStrings)
        {
            string[] cachedStrings = new string[numberOfStrings];
            int[] hashCodes = new int[numberOfStrings];

            for (int i = 0; i < numberOfStrings; i++)
            {
                string strPart2 = "1" + String.Concat(Enumerable.Repeat("4858396876", i));
                hashCodes[i] = AddString("Random string ", strPart2, (string cachedString) =>
                {
                    _cache.GetDebugInfo().ShouldBe(new WeakStringCache.DebugInfo()
                    {
                        UsedBucketCount = 1,
                        UnusedBucketCount = 0,
                        LiveStringCount = i + 1,
                        CollectedStringCount = 0,
                        HashCollisionCount = i
                    });
                    cachedStrings[i] = cachedString;

                    // All previously cached strings are still alive and retrievable.
                    for (int j = i - 1; j >= 0; j--)
                    {
                        string cachedStringFromCache = _cache.GetOrCreateEntry(new StringInternTarget(cachedStrings[j]), out bool cacheHit);
                        cacheHit.ShouldBeTrue();
                        cachedStringFromCache.ShouldBeSameAs(cachedStrings[j]);
                    }
                });

                if (i > 0)
                {
                    // The strings have been carefully constructed to have the same hash code.
                    hashCodes[i].ShouldBe(hashCodes[i - 1]);
                }
            }
        }

        /// <summary>
        /// Simple test case to verify that:
        /// 1. A string added to the cache stays in the cache as long as it's alive.
        /// 2. The string is no longer retrievable after all strong GC refs are gone.
        /// 3. The cache completely removes the bucket with the string after calling Scavenge on it.
        /// </summary>
        [Fact]
        public void RetainsStringUntilCollected()
        {
            // Add a string to the cache using a non-inlinable method to make sure it's not reachable from a GC root.
            AddString("Random string ", "test", (string cachedString) =>
            {
                _cache.GetDebugInfo().ShouldBe(new WeakStringCache.DebugInfo()
                {
                    UsedBucketCount = 1,
                    UnusedBucketCount = 0,
                    LiveStringCount = 1,
                    CollectedStringCount = 0,
                    HashCollisionCount = 0
                });
            });

            // Trigger full GC.
            RunGC();

            // The bucket is still in the cache but it's unused now as the string has been collected.
            _cache.GetDebugInfo().ShouldBe(new WeakStringCache.DebugInfo()
            {
                UsedBucketCount = 0,
                UnusedBucketCount = 1,
                LiveStringCount = 0,
                CollectedStringCount = 1,
                HashCollisionCount = 0
            });

            // Ask the cache to get rid of unused buckets.
            _cache.Scavenge();

            // The cache should be empty now.
            _cache.GetDebugInfo().ShouldBe(new WeakStringCache.DebugInfo()
            {
                UsedBucketCount = 0,
                UnusedBucketCount = 0,
                LiveStringCount = 0,
                CollectedStringCount = 0,
                HashCollisionCount = 0
            });
        }

        /// <summary>
        /// Same as RetainsStringUntilCollected but with multiple strings with the same hash code. Verifies that the bucket overflow area
        /// works correctly.
        /// </summary>
        [Fact]
        public void RetainsStringsWithHashCollisions()
        {
            // Add 3 strings.
            AddStringsWithHashCollision(3);

            // Trigger full GC.
            RunGC();

            // The bucket is still in the cache but it's unused now as the strings have been collected.
            _cache.GetDebugInfo().ShouldBe(new WeakStringCache.DebugInfo()
            {
                UsedBucketCount = 0,
                UnusedBucketCount = 1,
                LiveStringCount = 0,
                CollectedStringCount = 3,
                HashCollisionCount = 2
            });

            // Ask the cache to get rid of unused buckets.
            _cache.Scavenge();

            // The cache should be empty now.
            _cache.GetDebugInfo().ShouldBe(new WeakStringCache.DebugInfo()
            {
                UsedBucketCount = 0,
                UnusedBucketCount = 0,
                LiveStringCount = 0,
                CollectedStringCount = 0,
                HashCollisionCount = 0
            });
        }
    }
}
