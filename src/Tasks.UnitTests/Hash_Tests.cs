// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.UnitTests;
using Microsoft.Build.Utilities;
using Xunit;

#nullable disable

namespace Microsoft.Build.Tasks.UnitTests
{
    public class Hash_Tests
    {
        [Fact]
        public void HashTaskTest()
        {
            // This hash was pre-computed. If the implementation changes it may need to be adjusted.
            var expectedHash = "3a9e94b896536fdab1343db5038239847e2db371f27e6ac9b5e3e6ea4aa2f2bf";

            var actualHash = ExecuteHashTask(new ITaskItem[]
            {
                new TaskItem("Item1"), new TaskItem("Item2"), new TaskItem("Item3")
            });
            Assert.Equal(expectedHash, actualHash);

            // Try again to ensure the same hash
            var actualHash2 = ExecuteHashTask(new ITaskItem[]
            {
                new TaskItem("Item1"), new TaskItem("Item2"), new TaskItem("Item3")
            });
            Assert.Equal(expectedHash, actualHash2);
        }

        [Fact]
        public void HashTaskEmptyInputTest()
        {
            // Hash should be valid for empty item
            var emptyItemHash = ExecuteHashTask(new ITaskItem[] { new TaskItem("") });
            Assert.False(string.IsNullOrWhiteSpace(emptyItemHash));
            Assert.NotEmpty(emptyItemHash);

            // Hash should be null for null ItemsToHash or array of length 0
            var nullItemsHash = ExecuteHashTask(null);
            Assert.Null(nullItemsHash);

            var zeroLengthItemsHash = ExecuteHashTask(System.Array.Empty<ITaskItem>());
            Assert.Null(zeroLengthItemsHash);
        }

        [Fact]
        public void HashTaskLargeInputCountTest()
        {
            // This hash was pre-computed. If the implementation changes it may need to be adjusted.
            var expectedHash = "ae8799dfc1f81c50b08d28ac138e25958947895c8563c8fce080ceb5cb44db6f";

            ITaskItem[] itemsToHash = new ITaskItem[1000];
            for (int i = 0; i < itemsToHash.Length; i++)
            {
                itemsToHash[i] = new TaskItem($"Item{i}");
            }

            var actualHash = ExecuteHashTask(itemsToHash);
            Assert.Equal(expectedHash, actualHash);
        }

        [Fact]
        public void HashTaskLargeInputSizeTest()
        {
            // This hash was pre-computed. If the implementation changes it may need to be adjusted.
            var expectedHash = "48a3fdf5cb1afc679497a418015edc85e571282bb70691d7a64f2ab2e32d5dbf";

            string[] array = new string[1000];
            for (int i = 0; i < array.Length; i++)
            {
                array[i] = $"Item{i}";
            }
            ITaskItem[] itemsToHash = new ITaskItem[] { new TaskItem(string.Join("", array)) };

            var actualHash = ExecuteHashTask(itemsToHash);
            Assert.Equal(expectedHash, actualHash);
        }

        // This test verifies that hash computes correctly for various numbers of characters.
        // We would like to process edge of the buffer use cases regardless on the size of the buffer.
        [Fact]
        public void HashTaskDifferentInputSizesTest()
        {
            int maxInputSize = 2000;
            MockEngine mockEngine = new();

            var hashGroups =
                Enumerable.Range(0, maxInputSize)
                    .Select(cnt => new string('a', cnt))
                    .Select(GetHash)
                    .GroupBy(h => h)
                    .Where(g => g.Count() > 1)
                    .Select(g => g.Key);
            // none of the hashes should repeat
            Assert.Empty(hashGroups);

            string GetHash(string input)
            {
                Hash hashTask = new()
                {
                    BuildEngine = mockEngine,
                    ItemsToHash = new ITaskItem[] { new TaskItem(input) },
                    IgnoreCase = false
                };
                Assert.True(hashTask.Execute());
                return hashTask.HashResult;
            }
        }

        [Fact]
        public void HashTaskIgnoreCaseTest()
        {
            var uppercaseHash =
                ExecuteHashTask(new ITaskItem[]
                    {
                        new TaskItem("ITEM1"),
                        new TaskItem("ITEM2"),
                        new TaskItem("ITEM3")
                    },
                    true);
            var mixedcaseHash =
                ExecuteHashTask(new ITaskItem[]
                    {
                        new TaskItem("Item1"),
                        new TaskItem("iTEm2"),
                        new TaskItem("iteM3")
                    },
                    true);
            var lowercaseHash =
                ExecuteHashTask(new ITaskItem[]
                    {
                        new TaskItem("item1"),
                        new TaskItem("item2"),
                        new TaskItem("item3")
                    },
                    true);
            Assert.Equal(uppercaseHash, lowercaseHash);
            Assert.Equal(uppercaseHash, mixedcaseHash);
            Assert.Equal(mixedcaseHash, lowercaseHash);
        }

        [Fact]
        public void HashTaskPathMapMakesHashLocationIndependent()
        {
            // Identical inputs built under two different roots hash differently without a path map...
            ITaskItem[] underRootA = [new TaskItem(@"C:\rootA\src\a.cs"), new TaskItem(@"C:\rootA\obj\ref.dll")];
            ITaskItem[] underRootB = [new TaskItem(@"C:\rootB\src\a.cs"), new TaskItem(@"C:\rootB\obj\ref.dll")];

            Assert.NotEqual(ExecuteHashTask(underRootA), ExecuteHashTask(underRootB));

            // ...but converge once each root is mapped to the same deterministic prefix.
            string hashA = ExecuteHashTask(underRootA, pathMap: @"C:\rootA\=/_/");
            string hashB = ExecuteHashTask(underRootB, pathMap: @"C:\rootB\=/_/");
            Assert.Equal(hashA, hashB);
        }

        [Fact]
        public void HashTaskEmptyPathMapIsUnchanged()
        {
            // An empty path map must not alter the legacy hash, so enabling the feature is opt-in.
            ITaskItem[] items = [new TaskItem("Item1"), new TaskItem("Item2")];
            Assert.Equal(ExecuteHashTask(items), ExecuteHashTask(items, pathMap: ""));
            Assert.Equal(ExecuteHashTask(items), ExecuteHashTask(items, pathMap: null));
        }

        private string ExecuteHashTask(ITaskItem[] items, bool ignoreCase = false, string pathMap = null)
        {
            var hashTask = new Hash
            {
                BuildEngine = new MockEngine(),
                ItemsToHash = items,
                IgnoreCase = ignoreCase,
                PathMap = pathMap
            };

            Assert.True(hashTask.Execute());

            return hashTask.HashResult;
        }
    }
}
