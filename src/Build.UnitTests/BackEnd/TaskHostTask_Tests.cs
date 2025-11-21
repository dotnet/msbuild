// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using Microsoft.Build.BackEnd;
using Microsoft.Build.Shared;
using Shouldly;
using Xunit;

namespace Microsoft.Build.UnitTests.BackEnd
{
    public class TaskHostTask_Tests
    {
        [Fact]
        public void GetStartupDirectory_ReturnsProjectDirectory_WhenMultiThreadedAndProjectFileExists()
        {
            string projectFile = Path.Combine("C:", "MyProject", "Project.csproj");
            string expectedDirectory = Path.GetDirectoryName(projectFile)!;
            
            string result = TaskHostTask.GetStartupDirectory(projectFile, true);
            
            result.ShouldBe(expectedDirectory);
        }

        [Fact]
        public void GetStartupDirectory_ReturnsCurrentDirectory_WhenNotMultiThreaded()
        {
            string projectFile = Path.Combine("C:", "MyProject", "Project.csproj");
            string currentDirectory = NativeMethodsShared.GetCurrentDirectory();
            
            string result = TaskHostTask.GetStartupDirectory(projectFile, false);
            
            result.ShouldBe(currentDirectory);
        }

        [Fact]
        public void GetStartupDirectory_ReturnsCurrentDirectory_WhenMultiThreadedButProjectFileIsNull()
        {
            string currentDirectory = NativeMethodsShared.GetCurrentDirectory();
            
            string result = TaskHostTask.GetStartupDirectory(null, true);
            
            result.ShouldBe(currentDirectory);
        }

        [Fact]
        public void GetStartupDirectory_ReturnsCurrentDirectory_WhenMultiThreadedButProjectFileIsEmpty()
        {
            string currentDirectory = NativeMethodsShared.GetCurrentDirectory();
            
            string result = TaskHostTask.GetStartupDirectory(string.Empty, true);
            
            result.ShouldBe(currentDirectory);
        }
    }
}
