// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using FluentAssertions.Collections;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Microsoft.NET.TestFramework.ProjectConstruction;
using Xunit;
using Xunit.Abstractions;

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
                testProject.AdditionalProperties["SelfContained"] = "true";
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

            //  Set packs folder to nonexistant folder so the project won't use installed targeting or runtime packs
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

            //  Set packs folder to nonexistant folder so the project won't use installed targeting or runtime packs
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

            var testProject = new TestProject()
            {
                TargetFrameworks = ToolsetInfo.CurrentTargetFramework,
                IsExe = true,
            };

            testProject.RuntimeIdentifier = EnvironmentInfo.GetCompatibleRid();
            testProject.AdditionalProperties["SelfContained"] = "true";

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
