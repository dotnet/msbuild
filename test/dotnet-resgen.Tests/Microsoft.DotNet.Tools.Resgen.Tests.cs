// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Microsoft.DotNet.Tools.Test.Utilities;
using Microsoft.Extensions.PlatformAbstractions;
using Xunit;
using System;

namespace Microsoft.DotNet.Tools.Resgen.Tests
{
    public class ResgenTests : TestBase
    {
        private readonly string _testProjectsRoot;
        private readonly TempDirectory _root;

        public ResgenTests()
        {
            _testProjectsRoot = Path.Combine(AppContext.BaseDirectory, "TestAssets", "TestProjects");
            _root = Temp.CreateDirectory();
        }

        [Fact]
        public void Test_Build_Project_with_Resources_with_Space_in_Path_Should_Succeed()
        {
            var spaceBufferDirectory = _root.CreateDirectory("space directory");
            var testAppDir = spaceBufferDirectory.CreateDirectory("TestProjectWithResource");

            CopyProjectToTempDir(Path.Combine(_testProjectsRoot, "TestProjectWithResource"), testAppDir);

            var testProject = GetProjectPath(testAppDir);
            var buildCommand = new BuildCommand(testProject);

            buildCommand.Execute().Should().Pass();
        }

        private void CopyProjectToTempDir(string projectDir, TempDirectory tempDir)
        {
            foreach (var file in Directory.EnumerateFiles(projectDir))
            {
                tempDir.CopyFile(file);
            }
        }

        private string GetProjectPath(TempDirectory projectDir)
        {
            return Path.Combine(projectDir.Path, "project.json");
        }
    }
}
