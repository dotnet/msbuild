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

        private string ExecuteHashTask(ITaskItem[] items)
        {
            var hashTask = new Hash
            {
                BuildEngine = new MockEngine(),
                ItemsToHash = items
            };

            Assert.True(hashTask.Execute());

            return hashTask.HashResult;
        }
    }
}
