// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using FluentAssertions;
using Xunit;
using Microsoft.DotNet.Cli.Sln.Internal;
using Microsoft.DotNet.TestFramework;
using Microsoft.DotNet.Tools.Test.Utilities;

namespace Microsoft.DotNet.Cli.Sln.Internal.Tests
{
    public class GivenAnSlnFile : TestBase
    {
        [Fact]
        public void It_reads_an_sln_file()
        {
            var solutionDirectory =
                TestAssetsManager.CreateTestInstance("TestAppWithSln", callingMethod: "p").Path;

            var solutionFullPath = Path.Combine(solutionDirectory, "TestAppWithSln.sln");

            var slnFile = new SlnFile();
            slnFile.Read(solutionFullPath);

            slnFile.FormatVersion.Should().Be("12.00");
            slnFile.ProductDescription.Should().Be("Visual Studio 14");
            slnFile.VisualStudioVersion.Should().Be("14.0.25420.1");
            slnFile.MinimumVisualStudioVersion.Should().Be("10.0.40219.1");
            slnFile.BaseDirectory.Should().Be(solutionDirectory);
            slnFile.FileName.FileName.Should().Be("TestAppWithSln.sln");

            SlnFile.GetFileVersion(solutionFullPath).Should().Be("12.00");

            slnFile.Projects.Count.Should().Be(1);
            var project = slnFile.Projects[0];
            project.Id.Should().Be("{0138CB8F-4AA9-4029-A21E-C07C30F425BA}");
            project.TypeGuid.Should().Be("{8BB2217D-0F2D-49D1-97BC-3654ED321F3B}");
            project.Name.Should().Be("TestAppWithSln");
            project.FilePath.Should().Be("TestAppWithSln.xproj");
        }

        [Fact]
        public void It_writes_an_sln_file()
        {
            var solutionDirectory =
                TestAssetsManager.CreateTestInstance("TestAppWithSln", callingMethod: "p").Path;

            var solutionFullPath = Path.Combine(solutionDirectory, "TestAppWithSln.sln");

            var slnFile = new SlnFile();
            slnFile.Read(solutionFullPath);

            slnFile.Projects.Count.Should().Be(1);
            var project = slnFile.Projects[0];
            project.Name.Should().Be("TestAppWithSln");
            project.Name = "New Project Name";
            project.FilePath.Should().Be("TestAppWithSln.xproj");
            project.FilePath = "New File Path";

            var newSolutionFullPath = Path.Combine(solutionDirectory, "TestAppWithSln_modified.sln");
            slnFile.Write(newSolutionFullPath);

            slnFile = new SlnFile();
            slnFile.Read(newSolutionFullPath);
            slnFile.FormatVersion.Should().Be("12.00");
            slnFile.ProductDescription.Should().Be("Visual Studio 14");
            slnFile.VisualStudioVersion.Should().Be("14.0.25420.1");
            slnFile.MinimumVisualStudioVersion.Should().Be("10.0.40219.1");
            slnFile.BaseDirectory.Should().Be(solutionDirectory);
            slnFile.FileName.FileName.Should().Be("TestAppWithSln_modified.sln");
            SlnFile.GetFileVersion(solutionFullPath).Should().Be("12.00");
            slnFile.Projects.Count.Should().Be(1);
            project = slnFile.Projects[0];
            project.Id.Should().Be("{0138CB8F-4AA9-4029-A21E-C07C30F425BA}");
            project.TypeGuid.Should().Be("{8BB2217D-0F2D-49D1-97BC-3654ED321F3B}");
            project.Name.Should().Be("New Project Name");
            project.FilePath.Should().Be("New File Path");
            slnFile.Projects.Count.Should().Be(1);
            project = slnFile.Projects[0];
        }
    }
}
