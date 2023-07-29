// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using NuGet.Common;
using NuGet.Frameworks;
using NuGet.ProjectModel;

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
                TargetFrameworks = "netcoreapp1.0",
                IsExe = true
            };
            toolProject.AdditionalProperties.Add("PackageType", "DotnetCliTool");

            var toolProjectInstance = _testAssetsManager.CreateTestProject(toolProject, identifier: toolProject.Name);

            var packCommand = new PackCommand(Log, Path.Combine(toolProjectInstance.TestRoot, toolProject.Name));
            packCommand.Execute().Should().Pass();

            string nupkgPath = Path.Combine(packCommand.ProjectRootPath, "bin", "Debug");

            TestProject toolReferenceProject = new TestProject()
            {
                Name = "DotNetCliToolReferenceProject",
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
