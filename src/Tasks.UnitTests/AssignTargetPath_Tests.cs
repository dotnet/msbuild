// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.Tasks;
using Microsoft.Build.Utilities;
using Shouldly;
using Xunit;

#nullable disable

namespace Microsoft.Build.UnitTests
{
    public sealed class AssignTargetPath_Tests
    {
        [Fact]
        public void Regress314791()
        {
            AssignTargetPath t = new AssignTargetPath();
            t.BuildEngine = new MockEngine();
            t.Files = new ITaskItem[]
                          { new TaskItem(NativeMethodsShared.IsWindows ? @"c:\bin2\abc.efg" : "/bin2/abc.efg") };
            t.RootFolder = NativeMethodsShared.IsWindows ? @"c:\bin" : "/bin";

            t.Execute().ShouldBeTrue();
            t.AssignedFiles.Length.ShouldBe(1);
            t.AssignedFiles[0].ItemSpec.ShouldBe(NativeMethodsShared.IsWindows ? @"c:\bin2\abc.efg" : "/bin2/abc.efg");
            t.AssignedFiles[0].GetMetadata("TargetPath").ShouldBe("abc.efg");
        }

        [Fact]
        public void AtConeRoot()
        {
            AssignTargetPath t = new AssignTargetPath();
            t.BuildEngine = new MockEngine();
            t.Files = new ITaskItem[]
                          { new TaskItem(NativeMethodsShared.IsWindows ? @"c:\f1\f2\file.txt" : "/f1/f2/file.txt") };
            t.RootFolder = NativeMethodsShared.IsWindows ? @"c:\f1\f2" : "/f1/f2";

            t.Execute().ShouldBeTrue();
            t.AssignedFiles.Length.ShouldBe(1);
            t.AssignedFiles[0].GetMetadata("TargetPath").ShouldBe("file.txt");
        }

        [Fact]
        public void OutOfCone()
        {
            AssignTargetPath t = new AssignTargetPath();
            t.BuildEngine = new MockEngine();
            t.Files = new ITaskItem[]
                          {
                              new TaskItem(
                                  NativeMethodsShared.IsWindows ? @"d:\f1\f2\f3\f4\file.txt" : "/f1/f2/f3/f4/file.txt")
                          };
            // Create a path that's outside of the cone create above. On Windows this is achieved by
            // changing the drive letter from d:\ to c:\ to make sure the result is out of the cone.
            // If not Windows, where there is no drive, this is dine by changes the root directory from
            // /f1 to /x1
            t.RootFolder = NativeMethodsShared.IsWindows ? @"c:\f1" : "/x1";

            t.Execute().ShouldBeTrue();
            t.AssignedFiles.Length.ShouldBe(1);
            t.AssignedFiles[0].GetMetadata("TargetPath").ShouldBe("file.txt");
        }

        [Fact]
        public void InConeButAbsolute()
        {
            AssignTargetPath t = new AssignTargetPath();
            t.BuildEngine = new MockEngine();
            t.Files = new ITaskItem[]
                          {
                              new TaskItem(
                                  NativeMethodsShared.IsWindows ? @"c:\f1\f2\f3\f4\file.txt" : "/f1/f2/f3/f4/file.txt")
                          };
            t.RootFolder = NativeMethodsShared.IsWindows ? @"c:\f1\f2" : "/f1/f2";

            t.Execute().ShouldBeTrue();
            t.AssignedFiles.Length.ShouldBe(1);
            t.AssignedFiles[0].GetMetadata("TargetPath").ShouldBe(NativeMethodsShared.IsWindows ? @"f3\f4\file.txt" : "f3/f4/file.txt");
        }

        [Theory]
        [InlineData("c:/fully/qualified/path.txt")]
        [InlineData("test/output/file.txt")]
        [InlineData(@"some\dir\to\file.txt")]
        [InlineData("file.txt")]
        [InlineData("file")]
        public void TargetPathAlreadySet(string targetPath)
        {
            AssignTargetPath t = new AssignTargetPath();
            t.BuildEngine = new MockEngine();
            Dictionary<string, string> metaData = new Dictionary<string, string>();
            metaData.Add("TargetPath", targetPath);
            metaData.Add("Link", "c:/foo/bar");
            t.Files = new ITaskItem[]
                          {
                              new TaskItem(
                                  itemSpec: NativeMethodsShared.IsWindows ? @"c:\f1\f2\file.txt" : "/f1/f2/file.txt",
                                  itemMetadata: metaData)
                          };
            t.RootFolder = NativeMethodsShared.IsWindows ? @"c:\f1\f2" : "/f1/f2";

            t.Execute().ShouldBeTrue();
            t.AssignedFiles.Length.ShouldBe(1);
            t.AssignedFiles[0].GetMetadata("TargetPath").ShouldBe(targetPath);
        }
    }
}
