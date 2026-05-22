// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Build.Collections;
using Xunit;

namespace Microsoft.Build.UnitTests.OM.Collections
{
    public class ConcurrentWeakValueDictionary_Tests
    {
        private sealed class Box
        {
            public Box(string s) { Value = s; }
            public string Value { get; }
        }

        [Fact]
        public void TryGetValue_ReturnsLiveValue()
        {
            var d = new ConcurrentWeakValueDictionary<string, Box>();
            var b = new Box("hello");
            d["k"] = b;

            Assert.True(d.TryGetValue("k", out Box found));
            Assert.Same(b, found);
            GC.KeepAlive(b);
        }

        [Fact]
        public void TryGetValue_MissingKey_ReturnsFalse()
        {
            var d = new ConcurrentWeakValueDictionary<string, Box>();
            Assert.False(d.TryGetValue("absent", out Box found));
            Assert.Null(found);
        }

        [Fact]
        public void TryRemove_ByKey_RemovesEntry()
        {
            var d = new ConcurrentWeakValueDictionary<string, Box>();
            var b = new Box("v");
            d["k"] = b;

            Assert.True(d.TryRemove("k"));
            Assert.False(d.TryGetValue("k", out _));
            GC.KeepAlive(b);
        }

        [Fact]
        public void TryRemoveWithExpectedValue_RemovesOnlyOnMatch()
        {
            var d = new ConcurrentWeakValueDictionary<string, Box>();
            var b1 = new Box("v1");
            var b2 = new Box("v2");
            d["k"] = b1;

            // Mismatched expected value must not remove the entry.
            Assert.False(d.TryRemove("k", b2));
            Assert.True(d.TryGetValue("k", out Box stillThere));
            Assert.Same(b1, stillThere);

            // Matching expected value removes it.
            Assert.True(d.TryRemove("k", b1));
            Assert.False(d.TryGetValue("k", out _));

            GC.KeepAlive(b1);
            GC.KeepAlive(b2);
        }

        [Fact]
        public void Set_OverwritesExistingEntry()
        {
            var d = new ConcurrentWeakValueDictionary<string, Box>();
            var first = new Box("first");
            var second = new Box("second");

            d["k"] = first;
            d["k"] = second;

            Assert.True(d.TryGetValue("k", out Box found));
            Assert.Same(second, found);
            GC.KeepAlive(first);
            GC.KeepAlive(second);
        }

        [Fact]
        public void Enumerator_SkipsDeadEntries()
        {
            var d = new ConcurrentWeakValueDictionary<string, Box>();
            var live = new Box("live");
            d["live"] = live;

            // Make sure a dead entry exists and the live one survives.
            ForceDeadEntry(d, "dead");
            GC.KeepAlive(live);

            var keysSeen = new HashSet<string>();
            foreach (KeyValuePair<string, Box> kvp in d)
            {
                keysSeen.Add(kvp.Key);
                Assert.NotNull(kvp.Value);
            }

            Assert.Contains("live", keysSeen);
            Assert.DoesNotContain("dead", keysSeen);
        }

        [Fact]
        public void TryGetValue_OpportunisticallyRemovesDeadEntry()
        {
            var d = new ConcurrentWeakValueDictionary<string, Box>();
            ForceDeadEntry(d, "dead");

            int before = d.Count;
            Assert.False(d.TryGetValue("dead", out _));
            Assert.True(d.Count < before, "Dead entry should be opportunistically removed by TryGetValue.");
        }

        [Fact]
        public void Scavenge_RemovesAllDeadEntries()
        {
            var d = new ConcurrentWeakValueDictionary<string, Box>();
            var alive = new Box("alive");
            d["alive"] = alive;

            for (int i = 0; i < 20; i++)
            {
                ForceDeadEntry(d, "dead-" + i);
            }

            int removed = d.Scavenge();
            Assert.True(removed >= 1, $"Expected at least one dead entry to be removed; got {removed}.");
            Assert.True(d.TryGetValue("alive", out _));

            GC.KeepAlive(alive);
        }

        [Fact]
        public void Clear_RemovesEverything()
        {
            var d = new ConcurrentWeakValueDictionary<string, Box>();
            var b = new Box("v");
            d["a"] = b;
            d["b"] = b;

            d.Clear();

            Assert.Equal(0, d.Count);
            Assert.False(d.TryGetValue("a", out _));
            Assert.False(d.TryGetValue("b", out _));

            GC.KeepAlive(b);
        }

        [Fact]
        public void ConcurrentReadersAndWriters_DoNotCorruptState()
        {
            var d = new ConcurrentWeakValueDictionary<int, Box>();
            const int keys = 200;
            var holders = new Box[keys];
            for (int i = 0; i < keys; i++)
            {
                holders[i] = new Box(i.ToString());
                d[i] = holders[i];
            }

            using var start = new ManualResetEventSlim(false);
            using var stop = new ManualResetEventSlim(false);

            Task[] readers = new Task[8];
            for (int t = 0; t < readers.Length; t++)
            {
                readers[t] = Task.Run(() =>
                {
                    start.Wait();
                    while (!stop.IsSet)
                    {
                        for (int i = 0; i < keys; i++)
                        {
                            if (d.TryGetValue(i, out Box b))
                            {
                                Assert.Same(holders[i], b);
                            }
                        }
                    }
                });
            }

            Task writer = Task.Run(() =>
            {
                start.Wait();
                while (!stop.IsSet)
                {
                    for (int i = 0; i < keys; i++)
                    {
                        d[i] = holders[i];
                    }
                }
            });

            start.Set();
            Thread.Sleep(TimeSpan.FromMilliseconds(800));
            stop.Set();
            Task.WaitAll(readers);
            writer.Wait();

            GC.KeepAlive(holders);
        }

        /// <summary>
        /// Inserts a key whose value is immediately eligible for collection, and forces a GC so
        /// the slot becomes a tombstone. The inserted Box is intentionally not rooted.
        /// </summary>
        private static void ForceDeadEntry(ConcurrentWeakValueDictionary<string, Box> d, string key)
        {
            // Local scope so the temporary is not kept alive by a local on this stack frame.
            AddTransient(d, key);
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }

        private static void AddTransient(ConcurrentWeakValueDictionary<string, Box> d, string key)
        {
            d[key] = new Box("transient-" + key);
        }
    }
}
