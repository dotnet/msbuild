// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO;
using System.Linq;
using System.Xml.Linq;
using FluentAssertions;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Microsoft.NET.TestFramework.ProjectConstruction;
using NuGet.Common;
using NuGet.Frameworks;
using NuGet.ProjectModel;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.NET.Restore.Tests
{
    public class GivenThatWeWantToRestoreDotNetCliToolReference : SdkTest
    {
        private const string ProjectToolVersion = "1.0.0";
        private const string ExpectedProjectToolRestoreTargetFrameworkMoniker = "netcoreapp2.2";

        public GivenThatWeWantToRestoreDotNetCliToolReference(ITestOutputHelper log) : base(log)
        {
        }

        [Fact]
        public void It_can_restore_with_netcoreapp2_2()
        {
            TestProject toolProject = new TestProject()
            {
                Name = "TestTool" + nameof(It_can_restore_with_netcoreapp2_2),
                IsSdkProject = true,
                TargetFrameworks = "netcoreapp1.0",
                IsExe = true
            };
            toolProject.AdditionalProperties.Add("PackageType", "DotnetCliTool");

            var toolProjectInstance = _testAssetsManager.CreateTestProject(toolProject, identifier: toolProject.Name);
            toolProjectInstance.Restore(Log, toolProject.Name, "/v:n");

            var packCommand = new PackCommand(Log, Path.Combine(toolProjectInstance.TestRoot, toolProject.Name));
            packCommand.Execute().Should().Pass();

            string nupkgPath = Path.Combine(packCommand.ProjectRootPath, "bin", "Debug");

            TestProject toolReferenceProject = new TestProject()
            {
                Name = "DotNetCliToolReferenceProject",
                IsSdkProject = true,
                IsExe = true,
                TargetFrameworks = "netcoreapp1.0",
            };

            toolReferenceProject.DotNetCliToolReferences.Add(
                new TestPackageReference(id: toolProject.Name,
                             version: ProjectToolVersion,
                             nupkgPath: null));

            TestAsset toolReferenceProjectInstance = _testAssetsManager.CreateTestProject(toolReferenceProject, identifier: toolReferenceProject.Name);

            DeleteFolder(Path.Combine(TestContext.Current.NuGetCachePath, toolProject.Name.ToLowerInvariant()));
            DeleteFolder(Path.Combine(TestContext.Current.NuGetCachePath, ".tools", toolProject.Name.ToLowerInvariant()));
            NuGetConfigWriter.Write(toolReferenceProjectInstance.TestRoot, NuGetConfigWriter.DotnetCoreBlobFeed, nupkgPath);

            RestoreCommand restoreCommand =
                toolReferenceProjectInstance.GetRestoreCommand(log: Log, relativePath: toolReferenceProject.Name);

            var restoreResult = restoreCommand
                .Execute("/v:n");

            if (restoreResult.ExitCode != 0)
            {
                // retry once since it downloads from the web
                toolReferenceProjectInstance.Restore(Log, toolReferenceProject.Name, "/v:n");
            }

            var assetsJsonPath = Path.Combine(TestContext.Current.NuGetCachePath,
                                             ".tools",
                                             toolProject.Name.ToLowerInvariant(),
                                             ProjectToolVersion,
                                             ExpectedProjectToolRestoreTargetFrameworkMoniker,
                                             "project.assets.json");
            LockFile lockFile = LockFileUtilities.GetLockFile(assetsJsonPath, NullLogger.Instance);
            lockFile.Targets.Single().TargetFramework
                .Should().Be(NuGetFramework.Parse(ExpectedProjectToolRestoreTargetFrameworkMoniker),
                "Restore target framework should be capped at netcoreapp2.2 due to moving away from project tools." +
                "Even when SDK's TFM is higher and the project's TFM is netcoreapp1.0");
        }

        private static void DeleteFolder(string path)
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, true);
            }
        }
    }
}
