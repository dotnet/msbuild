// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Utilities;
using NuGet.Common;
using NuGet.Frameworks;
using NuGet.ProjectModel;

namespace Microsoft.NET.Restore.Tests
{
    public class GivenThatWeWantAutomaticTargetingPackReferences : SdkTest
    {
        public GivenThatWeWantAutomaticTargetingPackReferences(ITestOutputHelper log) : base(log)
        {
        }

        [Theory]
        [InlineData("4.7.1")]
        [InlineData("4.7.2")]
        [InlineData("4.5.2")]
        [InlineData("4.8")]
        public void It_restores_net_framework_project_successfully(string version)
        {
            var targetFrameworkVersion = (TargetDotNetFrameworkVersion)System.Enum.Parse(typeof(TargetDotNetFrameworkVersion), "Version" + string.Join("", version.Split('.')));
            var targetFramework = "net" + string.Join("", version.Split('.'));
            var testProject = new TestProject()
            {
                Name = "ProjectWithoutTargetingPackRef",
                TargetFrameworks = targetFramework,
            };

            var testAsset = _testAssetsManager.CreateTestProject(testProject, identifier: version);

            string projectAssetsJsonPath = Path.Combine(
                testAsset.Path,
                testProject.Name,
                "obj",
                "project.assets.json");

            var restoreCommand =
                testAsset.GetRestoreCommand(Log, relativePath: testProject.Name);
            restoreCommand.Execute().Should().Pass();

            LockFile lockFile = LockFileUtilities.GetLockFile(projectAssetsJsonPath, NullLogger.Instance);
            var netFrameworkLibrary = lockFile.GetTarget(NuGetFramework.Parse(".NETFramework,Version=v" + version), null).Libraries.FirstOrDefault((file) => file.Name.Contains(targetFramework));

            if (TestProject.ReferenceAssembliesAreInstalled(targetFrameworkVersion))
            {
                netFrameworkLibrary.Should().BeNull();
            }
            else
            {
                netFrameworkLibrary.Name.Should().Be("Microsoft.NETFramework.ReferenceAssemblies." + targetFramework);
                netFrameworkLibrary.Type.Should().Be("package");
            }
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void It_restores_multitargeted_net_framework_project_successfully(bool includeExplicitReference)
        {
            var testProject = new TestProject()
            {
                Name = "ProjectWithoutTargetingPackRef",
                TargetFrameworks = $"net471;net472;{ToolsetInfo.CurrentTargetFramework}",
            };

            TestAsset testAsset = null;
            if (includeExplicitReference)
            {
                // Add explicit reference to assembly packs
                testAsset = _testAssetsManager.CreateTestProject(testProject, identifier: includeExplicitReference.ToString()).WithProjectChanges(project =>
                {
                    var ns = project.Root.Name.Namespace;
                    var itemGroup = project.Root.Elements(ns + "ItemGroup").FirstOrDefault();
                    itemGroup.Add(new XElement(ns + "PackageReference",
                        new XAttribute("Include", $"Microsoft.NETFramework.ReferenceAssemblies"),
                        new XAttribute("Version", $"1.0.0-preview.2")));
                });
            }
            else
            {
                testAsset = _testAssetsManager.CreateTestProject(testProject);
            }

            string projectAssetsJsonPath = Path.Combine(
                testAsset.Path,
                testProject.Name,
                "obj",
                "project.assets.json");

            var restoreCommand = testAsset.GetRestoreCommand(Log, relativePath: testProject.Name);
            restoreCommand.Execute()
                .Should()
                .Pass()
                .And
                .NotHaveStdOutContaining("NETSDK1023");

            LockFile lockFile = LockFileUtilities.GetLockFile(
                projectAssetsJsonPath,
                NullLogger.Instance);

            var net471FrameworkLibrary = lockFile.GetTarget(NuGetFramework.Parse(".NETFramework,Version=v4.7.1"), null).Libraries.FirstOrDefault((file) => file.Name.Contains("net471"));
            if (TestProject.ReferenceAssembliesAreInstalled(TargetDotNetFrameworkVersion.Version471) && !includeExplicitReference)
            {
                net471FrameworkLibrary.Should().BeNull();
            }
            else
            {
                net471FrameworkLibrary.Name.Should().Be("Microsoft.NETFramework.ReferenceAssemblies.net471");
                net471FrameworkLibrary.Type.Should().Be("package");
            }

            var net472FrameworkLibrary = lockFile.GetTarget(NuGetFramework.Parse(".NETFramework,Version=v4.7.2"), null).Libraries.FirstOrDefault((file) => file.Name.Contains("net472"));

            if (TestProject.ReferenceAssembliesAreInstalled(TargetDotNetFrameworkVersion.Version472) && !includeExplicitReference)
            {
                net472FrameworkLibrary.Should().BeNull();
            }
            else
            {
                net472FrameworkLibrary.Name.Should().Be("Microsoft.NETFramework.ReferenceAssemblies.net472");
                net472FrameworkLibrary.Type.Should().Be("package");
            }
        }

        [Fact]
        public void It_restores_net_framework_project_with_existing_references()
        {
            var targetFramework = "net471";
            var testProject = new TestProject()
            {
                Name = "ProjectWithoutTargetingPackRef",
                TargetFrameworks = targetFramework,
            };

            // Add explicit reference to assembly packs
            var testAsset = _testAssetsManager.CreateTestProject(testProject).WithProjectChanges(project =>
            {
                var ns = project.Root.Name.Namespace;
                var itemGroup = project.Root.Elements(ns + "ItemGroup").FirstOrDefault();
                itemGroup.Add(new XElement(ns + "PackageReference",
                    new XAttribute("Include", $"Newtonsoft.Json"),
                    new XAttribute("Version", ToolsetInfo.GetNewtonsoftJsonPackageVersion())));
                itemGroup.Add(new XElement(ns + "PackageReference",
                    new XAttribute("Include", $"sqlite"),
                    new XAttribute("Version", $"3.13.0")));
                itemGroup.Add(new XElement(ns + "PackageReference",
                    new XAttribute("Include", $"Microsoft.NETFramework.ReferenceAssemblies"),
                    new XAttribute("Version", $"1.0.0")));
            });

            string projectAssetsJsonPath = Path.Combine(
                testAsset.Path,
                testProject.Name,
                "obj",
                "project.assets.json");

            var restoreCommand =
                testAsset.GetRestoreCommand(Log, relativePath: testProject.Name);
            restoreCommand.Execute()
                .Should()
                .Pass()
                .And
                .NotHaveStdOutContaining("NETSDK1023");

            LockFile lockFile = LockFileUtilities.GetLockFile(projectAssetsJsonPath, NullLogger.Instance);
            var netFrameworkLibrary = lockFile.GetTarget(NuGetFramework.Parse(".NETFramework,Version=v4.7.1"), null).Libraries.FirstOrDefault((file) => file.Name.Contains(targetFramework));

            netFrameworkLibrary.Name.Should().Be("Microsoft.NETFramework.ReferenceAssemblies." + targetFramework);
            netFrameworkLibrary.Type.Should().Be("package");
            netFrameworkLibrary.Version.ToFullString().Should().Be("1.0.0");
        }

        [Fact]
        public void It_fails_without_assembly_pack_reference()
        {
            var targetFramework = "net472";
            var testProject = new TestProject()
            {
                Name = "ProjectWithoutTargetingPackRef",
                TargetFrameworks = targetFramework,
            };
            testProject.AdditionalProperties["AutomaticallyUseReferenceAssemblyPackages"] = "false";

            var testAsset = _testAssetsManager.CreateTestProject(testProject);

            var buildCommand = new BuildCommand(testAsset);
            if (TestProject.ReferenceAssembliesAreInstalled(TargetDotNetFrameworkVersion.Version472))
            {
                buildCommand.Execute()
                    .Should()
                    .Pass();

                string projectAssetsJsonPath = Path.Combine(
                    testAsset.Path,
                    testProject.Name,
                    "obj",
                    "project.assets.json");
                LockFile lockFile = LockFileUtilities.GetLockFile(
                    projectAssetsJsonPath,
                    NullLogger.Instance);
                lockFile.GetTarget(NuGetFramework.Parse(".NETFramework,Version=v4.7.2"), null).Libraries.FirstOrDefault((file) => file.Name.Contains("net472"))
                        .Should()
                        .BeNull();
            }
            else
            {
                buildCommand.Execute()
                    .Should()
                    .Fail();
            }
        }
    }
}
