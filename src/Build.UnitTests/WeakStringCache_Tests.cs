// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Text;
using System.IO;
using Microsoft.Build;
using Microsoft.Build.Shared;
using Xunit;
using Shouldly;
using System.Runtime.CompilerServices;
using System.Diagnostics;

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
            GC.Collect();

            callbackToRunWithTheStringAlive(cachedString);

            cachedString = _cache.GetOrCreateEntry(new StringInternTarget(testStringCopy), out cacheHit);
            cacheHit.ShouldBeTrue();
            cachedString.ShouldBeSameAs(testString);

            return hashCode;
        }

        /// <summary>
        /// Adds two strings that are known to have a hash code collision to the cache under test.
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private void AddTwoStringsWithHashCollision()
        {
            string firstCachedString = null;

            // Add strings to the cache using a non-inlinable method to make sure they're not reachable from a GC root.
            int hashCode1 = AddString("Random string ", "1", (string cachedString) =>
            {
                _cache.GetDebugInfo().ShouldBe(new WeakStringCache.DebugInfo()
                {
                    UsedBucketCount = 1,
                    UnusedBucketCount = 0,
                    LiveStringCount = 1,
                    CollectedStringCount = 0,
                    HashCollisionCount = 0
                });
                firstCachedString = cachedString;
            });

            int hashCode2 = AddString("Random string ", "14858396876", (string cachedString) =>
            {
                _cache.GetDebugInfo().ShouldBe(new WeakStringCache.DebugInfo()
                {
                    UsedBucketCount = 1,
                    UnusedBucketCount = 0,
                    LiveStringCount = 2,
                    CollectedStringCount = 0,
                    HashCollisionCount = 1
                });

                string firstCachedStringFromCache = _cache.GetOrCreateEntry(new StringInternTarget(firstCachedString), out bool cacheHit);
                cacheHit.ShouldBeTrue();
                firstCachedStringFromCache.ShouldBeSameAs(firstCachedString);
            });

            // The two string have been carefully chosen to have the same hash code.
            hashCode2.ShouldBe(hashCode1);
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
            GC.Collect();

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
        /// Same as RetainsStringUntilCollected but with two strings with the same hash code. Verifies that the bucket overflow area
        /// works correctly.
        /// </summary>
        [Fact]
        public void RetainsStringsWithHashCollisions()
        {
            AddTwoStringsWithHashCollision();

            // Trigger full GC.
            GC.Collect();

            // The bucket is still in the cache but it's unused now as the strings have been collected.
            _cache.GetDebugInfo().ShouldBe(new WeakStringCache.DebugInfo()
            {
                UsedBucketCount = 0,
                UnusedBucketCount = 1,
                LiveStringCount = 0,
                CollectedStringCount = 2,
                HashCollisionCount = 1
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
