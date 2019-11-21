// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using FluentAssertions;
using System.IO;
using System.Linq;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Microsoft.NET.TestFramework.ProjectConstruction;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.NET.Publish.Tests
{
    public class GivenThatWeWantToPublishIncrementally : SdkTest
    {
        public GivenThatWeWantToPublishIncrementally(ITestOutputHelper log) : base(log)
        {
        }

        [Fact]
        public void It_cleans_before_single_exe_publish()
        {
            var testProject = new TestProject()
            {
                Name = "RegularPublishToSingleExe",
                IsSdkProject = true,
                TargetFrameworks = "netcoreapp3.0",
                IsExe = true,
                RuntimeIdentifier = "win-x86"
            };
            var testAsset = _testAssetsManager.CreateTestProject(testProject, testProject.Name);

            var publishDir = Path.Combine(testAsset.TestRoot, testProject.Name, "bin", "Debug", testProject.TargetFrameworks, testProject.RuntimeIdentifier, "publish");
            var expectedNonSingleExeFileExtensions = new string[] { ".dll", ".deps.json", ".runtimeconfig.json" };
            var expectedSingleExeFileExtensions = new string[] { ".exe", ".pdb" };

            new PublishCommand(Log, Path.Combine(testAsset.TestRoot, testProject.Name))
                .Execute("/bl:C:/code/binlogs/regularPublish.binlog")
                .Should()
                .Pass();

            foreach (var expectedFileEnding in expectedNonSingleExeFileExtensions.Concat(expectedSingleExeFileExtensions))
            {
                File.Exists(Path.Combine(publishDir, testProject.Name + expectedFileEnding)).Should().BeTrue();
            }

            File.WriteAllText(Path.Combine(publishDir, "UserData.txt"), string.Empty);

            new PublishCommand(Log, Path.Combine(testAsset.TestRoot, testProject.Name))
                .Execute(@"/p:PublishSingleFile=true", "/bl:C:/code/binlogs/singleExe.binlog")
                .Should()
                .Pass();

            foreach (var expectedFile in expectedSingleExeFileExtensions)
            {
                File.Exists(Path.Combine(publishDir, testProject.Name + expectedFile)).Should().BeTrue();
            }
            foreach (var expectedFile in expectedNonSingleExeFileExtensions)
            {
                File.Exists(Path.Combine(publishDir, testProject.Name + expectedFile)).Should().BeFalse();
            }
            // File manually dropped in publish folder should still be there
            File.Exists(Path.Combine(publishDir, "UserData.txt")).Should().BeTrue();
        }

    }
}
