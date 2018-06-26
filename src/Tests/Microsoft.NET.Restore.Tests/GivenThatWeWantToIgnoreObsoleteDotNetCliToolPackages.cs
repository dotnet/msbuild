// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO;
using System.Xml.Linq;
using FluentAssertions;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Microsoft.NET.TestFramework.ProjectConstruction;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.NET.Restore.Tests
{
    public class GivenThatWeWantToIgnoreObsoleteDotNetCliToolPackages : SdkTest
    {
        public GivenThatWeWantToIgnoreObsoleteDotNetCliToolPackages(ITestOutputHelper log) : base(log)
        {
        }

        [Fact]
        public void It_issues_warning_and_skips_restore_for_obsolete_DotNetCliToolReference()
        {
            const string obsoletePackageId = "Banana.CommandLineTool";

            TestProject toolProject = new TestProject()
            {
                Name = "ObsoleteCliToolRefRestoreProject",
                IsSdkProject = true,
                TargetFrameworks = "netstandard2.0",
            };

            toolProject.DotNetCliToolReferences.Add(new TestPackageReference(obsoletePackageId, "99.99.99", null));

            TestAsset toolProjectInstance = _testAssetsManager.CreateTestProject(toolProject, identifier: toolProject.Name)
                .WithProjectChanges(project =>
                {
                    XNamespace ns = project.Root.Name.Namespace;

                    var itemGroup = new XElement(ns + "ItemGroup");
                    project.Root.Add(itemGroup);

                    itemGroup.Add(new XElement(ns + "BundledDotNetCliToolReference",
                        new XAttribute("Include", obsoletePackageId)));
                });

            NuGetConfigWriter.Write(toolProjectInstance.TestRoot, NuGetConfigWriter.DotnetCoreBlobFeed);

            toolProjectInstance.Restore(Log, toolProject.Name, "/v:n");

            RestoreCommand restoreCommand = toolProjectInstance.GetRestoreCommand(Log, toolProject.Name);
            restoreCommand.Execute("/v:n").Should()
                .Pass()
                .And
                .HaveStdOutContaining($"warning NETSDK1059: The tool '{obsoletePackageId}' is now included in the .NET Core SDK. Information on resolving this warning is available at (https://aka.ms/dotnetclitools-in-box).");

            string toolAssetsFilePath = Path.Combine(TestContext.Current.NuGetCachePath, ".tools", toolProject.Name.ToLowerInvariant(), "99.99.99", toolProject.TargetFrameworks, "project.assets.json");
            Assert.False(File.Exists(toolAssetsFilePath), "Tool assets path should not have been generated");
        }
    }
}
