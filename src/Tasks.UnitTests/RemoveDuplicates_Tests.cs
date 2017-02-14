// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Reflection;
using Microsoft.Build.Framework;
using Microsoft.Build.Tasks;
using Microsoft.Build.Utilities;
using Xunit;

namespace Microsoft.Build.UnitTests
{
    sealed public class RemoveDuplicates_Tests
    {
        /// <summary>
        /// Pass one item in, get the same item back.
        /// </summary>
        [Fact]
        public void OneItemNop()
        {
            RemoveDuplicates t = new RemoveDuplicates();
            t.BuildEngine = new MockEngine();

            t.Inputs = new ITaskItem[] { new TaskItem("MyFile.txt") };

            bool success = t.Execute();
            Assert.True(success);
            Assert.Equal("MyFile.txt", t.Filtered[0].ItemSpec);
        }

        /// <summary>
        /// Pass in two of the same items.
        /// </summary>
        [Fact]
        public void TwoItemsTheSame()
        {
            RemoveDuplicates t = new RemoveDuplicates();
            t.BuildEngine = new MockEngine();

            t.Inputs = new ITaskItem[] { new TaskItem("MyFile.txt"), new TaskItem("MyFile.txt") };

            bool success = t.Execute();
            Assert.True(success);
            Assert.Equal("MyFile.txt", t.Filtered[0].ItemSpec);
        }

        /// <summary>
        /// Pass in two items that are different.
        /// </summary>
        [Fact]
        public void TwoItemsDifferent()
        {
            RemoveDuplicates t = new RemoveDuplicates();
            t.BuildEngine = new MockEngine();

            t.Inputs = new ITaskItem[] { new TaskItem("MyFile1.txt"), new TaskItem("MyFile2.txt") };

            bool success = t.Execute();
            Assert.True(success);
            Assert.Equal("MyFile1.txt", t.Filtered[0].ItemSpec);
            Assert.Equal("MyFile2.txt", t.Filtered[1].ItemSpec);
        }

        /// <summary>
        /// Case should not matter.
        /// </summary>
        [Fact]
        public void CaseInsensitive()
        {
            RemoveDuplicates t = new RemoveDuplicates();
            t.BuildEngine = new MockEngine();

            t.Inputs = new ITaskItem[] { new TaskItem("MyFile.txt"), new TaskItem("MyFIle.tXt") };

            bool success = t.Execute();
            Assert.True(success);
            Assert.Equal("MyFile.txt", t.Filtered[0].ItemSpec);
        }

        /// <summary>
        /// No inputs should result in zero-length outputs.
        /// </summary>
        [Fact]
        public void MissingInputs()
        {
            RemoveDuplicates t = new RemoveDuplicates();
            t.BuildEngine = new MockEngine();
            bool success = t.Execute();

            Assert.True(success);
            Assert.Equal(0, t.Filtered.Length);
        }
    }
}



