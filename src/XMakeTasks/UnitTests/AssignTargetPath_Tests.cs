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
    sealed public class AssignTargetPath_Tests
    {
        [Fact]
        public void Regress314791()
        {
            AssignTargetPath t = new AssignTargetPath();
            t.BuildEngine = new MockEngine();
            t.Files = new ITaskItem[] { new TaskItem(@"c:\bin2\abc.efg") };
            t.RootFolder = @"c:\bin";

            bool success = t.Execute();

            Assert.True(success);

            Assert.Equal(1, t.AssignedFiles.Length);
            Assert.Equal(@"c:\bin2\abc.efg", t.AssignedFiles[0].ItemSpec);
            Assert.Equal(@"abc.efg", t.AssignedFiles[0].GetMetadata("TargetPath"));
        }

        [Fact]
        public void AtConeRoot()
        {
            AssignTargetPath t = new AssignTargetPath();
            t.BuildEngine = new MockEngine();
            t.Files = new ITaskItem[] { new TaskItem(@"c:\f1\f2\file.txt") };
            t.RootFolder = @"c:\f1\f2";

            bool success = t.Execute();

            Assert.True(success);

            Assert.Equal(1, t.AssignedFiles.Length);
            Assert.Equal(@"file.txt", t.AssignedFiles[0].GetMetadata("TargetPath"));
        }

        [Fact]
        public void OutOfCone()
        {
            AssignTargetPath t = new AssignTargetPath();
            t.BuildEngine = new MockEngine();
            t.Files = new ITaskItem[] { new TaskItem(@"d:\f1\f2\f3\f4\file.txt") };
            t.RootFolder = @"c:\f1";

            bool success = t.Execute();

            Assert.True(success);

            Assert.Equal(1, t.AssignedFiles.Length);
            Assert.Equal("file.txt", t.AssignedFiles[0].GetMetadata("TargetPath"));
        }

        [Fact]
        public void InConeButAbsolute()
        {
            AssignTargetPath t = new AssignTargetPath();
            t.BuildEngine = new MockEngine();
            t.Files = new ITaskItem[] { new TaskItem(@"c:\f1\f2\f3\f4\file.txt") };
            t.RootFolder = @"c:\f1\f2";

            bool success = t.Execute();

            Assert.True(success);

            Assert.Equal(1, t.AssignedFiles.Length);
            Assert.Equal(@"f3\f4\file.txt", t.AssignedFiles[0].GetMetadata("TargetPath"));
        }
    }
}



