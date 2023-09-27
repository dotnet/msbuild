// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.CompilerServices;

namespace Microsoft.NET.Build.Tests
{
    public class GivenTransitiveFrameworkReferencesAreDisabled : SdkTest
    {
        public GivenTransitiveFrameworkReferencesAreDisabled(ITestOutputHelper log) : base(log)
        {
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void TargetingPacksAreNotDownloadedIfNotDirectlyReferenced(bool referenceAspNet)
        {
            TestPackagesNotDownloaded(referenceAspNet, selfContained: false);
        }

        [Fact]
        public void RuntimePacksAreNotDownloadedIfNotDirectlyReferenced()
        {
            TestPackagesNotDownloaded(referenceAspNet: false, selfContained: true);
        }

        void TestPackagesNotDownloaded(bool referenceAspNet, bool selfContained, [CallerMemberName] string testName = null)
        {
            string nugetPackagesFolder = _testAssetsManager.CreateTestDirectory(testName, identifier: "packages_" + referenceAspNet).Path;

            var testProject = new TestProject(testName)
            {
                TargetFrameworks = ToolsetInfo.CurrentTargetFramework,
                IsExe = true,
            };

            if (selfContained)
            {
                testProject.RuntimeIdentifier = EnvironmentInfo.GetCompatibleRid();
                testProject.SelfContained = "true";
            }
            else
            {
                //  Don't use AppHost in order to avoid additional download to packages folder
                testProject.AdditionalProperties["UseAppHost"] = "False";
            }

            if (referenceAspNet)
            {
                testProject.FrameworkReferences.Add("Microsoft.AspNetCore.App");
            }

            testProject.AdditionalProperties["DisableTransitiveFrameworkReferenceDownloads"] = "True";
            testProject.AdditionalProperties["RestorePackagesPath"] = nugetPackagesFolder;

            //  Set packs folder to nonexistent folder so the project won't use installed targeting or runtime packs
            testProject.AdditionalProperties["NetCoreTargetingPackRoot"] = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

            var testAsset = _testAssetsManager.CreateTestProject(testProject, testName, identifier: referenceAspNet.ToString());

            var buildCommand = new BuildCommand(testAsset);

            buildCommand.Execute()
                .Should()
                .Pass();

            var expectedPackages = new List<string>()
            {
                "microsoft.netcore.app.ref"
            };

            if (selfContained)
            {
                expectedPackages.Add("microsoft.netcore.app.runtime.**RID**");
                expectedPackages.Add("microsoft.netcore.app.host.**RID**");
            }

            if (referenceAspNet)
            {
                expectedPackages.Add("microsoft.aspnetcore.app.ref");
            }

            Directory.EnumerateDirectories(nugetPackagesFolder)
                .Select(Path.GetFileName)
                .Select(package =>
                {
                    if (package.Contains(".runtime.") || (package.Contains(".host.")))
                    {
                        //  Replace RuntimeIdentifier, which should be the last dotted segment in the package name, with "**RID**"
                        package = package.Substring(0, package.LastIndexOf('.') + 1) + "**RID**";
                    }

                    return package;
                })
                .Should().BeEquivalentTo(expectedPackages);
        }

        [Fact]
        public void TransitiveFrameworkReferenceGeneratesError()
        {
            string nugetPackagesFolder = _testAssetsManager.CreateTestDirectory(identifier: "packages").Path;

            var referencedProject = new TestProject()
            {
                Name = "ReferencedProject",
                TargetFrameworks = ToolsetInfo.CurrentTargetFramework,
            };

            referencedProject.FrameworkReferences.Add("Microsoft.AspNetCore.App");

            var testProject = new TestProject()
            {
                TargetFrameworks = ToolsetInfo.CurrentTargetFramework,
                IsExe = true,
            };

            //  Don't use AppHost in order to avoid additional download to packages folder
            testProject.AdditionalProperties["UseAppHost"] = "False";

            testProject.AdditionalProperties["DisableTransitiveFrameworkReferenceDownloads"] = "True";
            testProject.AdditionalProperties["RestorePackagesPath"] = nugetPackagesFolder;

            //  Set packs folder to nonexistent folder so the project won't use installed targeting or runtime packs
            testProject.AdditionalProperties["NetCoreTargetingPackRoot"] = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

            testProject.ReferencedProjects.Add(referencedProject);

            var testAsset = _testAssetsManager.CreateTestProject(testProject);

            var buildCommand = new BuildCommand(testAsset);

            buildCommand
                .Execute()
                .Should()
                .Fail()
                .And.HaveStdOutContaining("NETSDK1184:");
        }

        [Fact]
        public void TransitiveFrameworkReferenceGeneratesRuntimePackError()
        {
            string nugetPackagesFolder = _testAssetsManager.CreateTestDirectory(identifier: "packages").Path;

            var referencedProject = new TestProject()
            {
                Name = "ReferencedProject",
                TargetFrameworks = ToolsetInfo.CurrentTargetFramework,
            };

            referencedProject.FrameworkReferences.Add("Microsoft.AspNetCore.App");

            var testProject = new TestProject
            {
                TargetFrameworks = ToolsetInfo.CurrentTargetFramework,
                IsExe = true,
                SelfContained = "true",
                RuntimeIdentifier = EnvironmentInfo.GetCompatibleRid()
            };
            testProject.AdditionalProperties["DisableTransitiveFrameworkReferenceDownloads"] = "True";
            testProject.AdditionalProperties["RestorePackagesPath"] = nugetPackagesFolder;

            testProject.ReferencedProjects.Add(referencedProject);

            var testAsset = _testAssetsManager.CreateTestProject(testProject);

            var buildCommand = new BuildCommand(testAsset);

            buildCommand
                .Execute()
                .Should()
                .Fail()
                .And.HaveStdOutContaining("NETSDK1185:");
        }

    }
}
