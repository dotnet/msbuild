using Microsoft.Build.Framework;
using Microsoft.Build.UnitTests;
using Microsoft.Build.Utilities;
using Xunit;

namespace Microsoft.Build.Tasks.UnitTests
{
    public class Hash_Tests
    {
        [Fact]
        public void HashTaskTest()
        {
            // This hash was pre-computed. If the implementation changes it may need to be adjusted.
            var expectedHash = "5593e2db83ac26117cd95ed8917f09b02a02e2a0";

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
            var emptyItemHash = ExecuteHashTask(new ITaskItem[] {new TaskItem("")});
            Assert.False(string.IsNullOrWhiteSpace(emptyItemHash));
            Assert.NotEmpty(emptyItemHash);

            // Hash should be null for null ItemsToHash or array of length 0
            var nullItemsHash = ExecuteHashTask(null);
            Assert.Null(nullItemsHash);

            var zeroLengthItemsHash = ExecuteHashTask(new ITaskItem[0]);
            Assert.Null(zeroLengthItemsHash);
        }

        [Fact]
        public void HashTaskLargeInputCountTest()
        {
            // This hash was pre-computed. If the implementation changes it may need to be adjusted.
            var expectedHash = "8a996bbcb5e481981c2fba7ac408e20d0b4360a5";

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
            var expectedHash = "0509142dd3d3a733f30a52a0eec37cd727d46122";

            string[] array = new string[1000];
            for (int i = 0; i < array.Length; i++)
            {
                array[i] = $"Item{i}";
            }
            ITaskItem[] itemsToHash = new ITaskItem[] { new TaskItem(string.Join("", array)) };

            var actualHash = ExecuteHashTask(itemsToHash);
            Assert.Equal(expectedHash, actualHash);
        }

        [Fact]
        public void HashTaskLargeInputCountAndSizeTest()
        {
            // This hash was pre-computed. If the implementation changes it may need to be adjusted.
            var expectedHash = "be23ae7b09f1e14fa5a17de87ddae2c3ec62f967";

            int inputSize = 1000;
            string[][] array = new string[inputSize][];
            for (int i = 0; i < array.Length; i++)
            {
                array[i] = new string[inputSize];
                for (int j = 0; j < array[i].Length; j++)
                {
                    array[i][j] = $"Item{i}{j}";
                }
            }

            ITaskItem[] itemsToHash = new ITaskItem[inputSize];
            for (int i = 0; i < itemsToHash.Length; i++)
            {
                itemsToHash[i] = new TaskItem(string.Join("", array[i]));
            }

            var actualHash = ExecuteHashTask(itemsToHash);

            Assert.Equal(expectedHash, actualHash);
        }

#pragma warning disable CA5350
        [Fact]
        public void HashTaskDifferentInputSizesTest()
        {
            int maxInputSize = 2000;
            string input = "";
            using (var sha1 = System.Security.Cryptography.SHA1.Create())
            {
                using (var stringBuilder = new ReuseableStringBuilder(sha1.HashSize))
                {
                    for (int i = 0; i < maxInputSize; i++)
                    {
                        input += "a";
                        ITaskItem[] itemsToHash = new ITaskItem[] { new TaskItem(input) };

                        var actualHash = ExecuteHashTask(itemsToHash);

                        byte[] hash = sha1.ComputeHash(System.Text.Encoding.UTF8.GetBytes(input+'\u2028'));
                        stringBuilder.Clear();
                        foreach (var b in hash)
                        {
                            stringBuilder.Append(b.ToString("x2"));
                        }
                        string expectedHash = stringBuilder.ToString();

                        Assert.Equal(expectedHash, actualHash);
                    }
                }
            }

        }
#pragma warning restore CA5350

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

        private string ExecuteHashTask(ITaskItem[] items, bool ignoreCase = false)
        {
            var hashTask = new Hash
            {
                BuildEngine = new MockEngine(),
                ItemsToHash = items,
                IgnoreCase = ignoreCase
            };

            Assert.True(hashTask.Execute());

            return hashTask.HashResult;
        }

    }
}
