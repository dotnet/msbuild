// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Tasks;
using Microsoft.Build.Utilities;

#nullable disable

namespace Microsoft.Build.UnitTests
{
    [TestClass]
    public sealed class RemoveDuplicates_Tests
    {
        /// <summary>
        /// Pass one item in, get the same item back.
        /// </summary>
        [MSBuildTestMethod]
        public void OneItemNop()
        {
            var t = new RemoveDuplicates();
            t.BuildEngine = new MockEngine();

            t.Inputs = new[] { new TaskItem("MyFile.txt") };

            bool success = t.Execute();
            Assert.IsTrue(success);
            Assert.ContainsSingle(t.Filtered);
            Assert.AreEqual("MyFile.txt", t.Filtered[0].ItemSpec);
            Assert.IsFalse(t.HadAnyDuplicates);
        }

        /// <summary>
        /// Pass in two of the same items.
        /// </summary>
        [MSBuildTestMethod]
        public void TwoItemsTheSame()
        {
            var t = new RemoveDuplicates();
            t.BuildEngine = new MockEngine();

            t.Inputs = new[] { new TaskItem("MyFile.txt"), new TaskItem("MyFile.txt") };

            bool success = t.Execute();
            Assert.IsTrue(success);
            Assert.ContainsSingle(t.Filtered);
            Assert.AreEqual("MyFile.txt", t.Filtered[0].ItemSpec);
            Assert.IsTrue(t.HadAnyDuplicates);
        }

        /// <summary>
        /// Item order preserved
        /// </summary>
        [MSBuildTestMethod]
        public void OrderPreservedNoDups()
        {
            var t = new RemoveDuplicates();
            t.BuildEngine = new MockEngine();

            // intentionally not sorted to catch an invalid implementation that sorts before
            // de-duping.
            t.Inputs = new[]
            {
                new TaskItem("MyFile2.txt"),
                new TaskItem("MyFile1.txt"),
                new TaskItem("MyFile3.txt")
            };

            bool success = t.Execute();
            Assert.IsTrue(success);
            Assert.AreEqual(3, t.Filtered.Length);
            Assert.AreEqual("MyFile2.txt", t.Filtered[0].ItemSpec);
            Assert.AreEqual("MyFile1.txt", t.Filtered[1].ItemSpec);
            Assert.AreEqual("MyFile3.txt", t.Filtered[2].ItemSpec);
        }

        /// <summary>
        /// Item order preserved, keeping the first items seen when there are duplicates.
        /// </summary>
        [MSBuildTestMethod]
        public void OrderPreservedDups()
        {
            var t = new RemoveDuplicates();
            t.BuildEngine = new MockEngine();

            t.Inputs = new[]
            {
                new TaskItem("MyFile2.txt"),
                new TaskItem("MyFile1.txt"),
                new TaskItem("MyFile2.txt"),
                new TaskItem("MyFile3.txt"),
                new TaskItem("MyFile1.txt")
            };

            bool success = t.Execute();
            Assert.IsTrue(success);
            Assert.AreEqual(3, t.Filtered.Length);
            Assert.AreEqual("MyFile2.txt", t.Filtered[0].ItemSpec);
            Assert.AreEqual("MyFile1.txt", t.Filtered[1].ItemSpec);
            Assert.AreEqual("MyFile3.txt", t.Filtered[2].ItemSpec);
        }

        /// <summary>
        /// Pass in two items that are different.
        /// </summary>
        [MSBuildTestMethod]
        public void TwoItemsDifferent()
        {
            var t = new RemoveDuplicates();
            t.BuildEngine = new MockEngine();

            t.Inputs = new[] { new TaskItem("MyFile1.txt"), new TaskItem("MyFile2.txt") };

            bool success = t.Execute();
            Assert.IsTrue(success);
            Assert.AreEqual(2, t.Filtered.Length);
            Assert.AreEqual("MyFile1.txt", t.Filtered[0].ItemSpec);
            Assert.AreEqual("MyFile2.txt", t.Filtered[1].ItemSpec);
            Assert.IsFalse(t.HadAnyDuplicates);
        }

        /// <summary>
        /// Case should not matter.
        /// </summary>
        [MSBuildTestMethod]
        public void CaseInsensitive()
        {
            var t = new RemoveDuplicates();
            t.BuildEngine = new MockEngine();

            t.Inputs = new[] { new TaskItem("MyFile.txt"), new TaskItem("MyFIle.tXt") };

            bool success = t.Execute();
            Assert.IsTrue(success);
            Assert.ContainsSingle(t.Filtered);
            Assert.AreEqual("MyFile.txt", t.Filtered[0].ItemSpec);
            Assert.IsTrue(t.HadAnyDuplicates);
        }

        /// <summary>
        /// No inputs should result in zero-length outputs.
        /// </summary>
        [MSBuildTestMethod]
        public void MissingInputs()
        {
            var t = new RemoveDuplicates();
            t.BuildEngine = new MockEngine();
            bool success = t.Execute();

            Assert.IsTrue(success);
            Assert.IsEmpty(t.Filtered);
            Assert.IsFalse(t.HadAnyDuplicates);
        }
    }
}
