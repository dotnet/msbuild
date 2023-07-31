// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.RegularExpressions;

namespace Microsoft.NET.Build.Tests
{
    public class GivenThatWeWantToResolveConflicts : SdkTest
    {
        public GivenThatWeWantToResolveConflicts(ITestOutputHelper log) : base(log)
        {
        }

        [Theory]
        [InlineData("netcoreapp2.0")]
        [InlineData("netstandard2.0")]
        public void The_same_references_are_used_with_or_without_DisableDefaultPackageConflictOverrides(string targetFramework)
        {
            var defaultProject = new TestProject()
            {
                Name = "DefaultProject",
                TargetFrameworks = targetFramework,
            };
            AddConflictReferences(defaultProject);
            GetReferences(
                defaultProject,
                expectConflicts: false,
                references: out List<string> defaultReferences,
                referenceCopyLocalPaths: out List<string> defaultReferenceCopyLocalPaths,
                targetFramework);

            var disableProject = new TestProject()
            {
                Name = "DisableProject",
                TargetFrameworks = targetFramework,
            };
            disableProject.AdditionalProperties.Add("DisableDefaultPackageConflictOverrides", "true");
            AddConflictReferences(disableProject);
            GetReferences(
                disableProject,
                expectConflicts: true,
                references: out List<string> disableReferences,
                referenceCopyLocalPaths: out List<string> disableReferenceCopyLocalPaths,
                targetFramework);

            Assert.Equal(defaultReferences, disableReferences);
            Assert.Equal(defaultReferenceCopyLocalPaths, disableReferenceCopyLocalPaths);
        }

        private void AddConflictReferences(TestProject testProject)
        {
            foreach (var dependency in ConflictResolutionAssets.ConflictResolutionDependencies)
            {
                testProject.PackageReferences.Add(new TestPackageReference(dependency.Item1, dependency.Item2));
            }
        }

        private void GetReferences(TestProject testProject, bool expectConflicts, out List<string> references, out List<string> referenceCopyLocalPaths, string identifier)
        {
            string targetFramework = testProject.TargetFrameworks;
            TestAsset tempTestAsset = _testAssetsManager.CreateTestProject(testProject, identifier: identifier);

            string projectFolder = Path.Combine(tempTestAsset.TestRoot, testProject.Name);

            var getReferenceCommand = new GetValuesCommand(
                Log,
                projectFolder,
                targetFramework,
                "Reference",
                GetValuesCommand.ValueType.Item);
            getReferenceCommand.DependsOnTargets = "Build";
            var result = getReferenceCommand.Execute("/v:detailed").Should().Pass();
            if (expectConflicts)
            {
                result.And.HaveStdOutMatching("Encountered conflict", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
            }
            else
            {
                result.And.NotHaveStdOutMatching("Encountered conflict", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
            }

            references = getReferenceCommand.GetValues();

            var getReferenceCopyLocalPathsCommand = new GetValuesCommand(
                Log,
                projectFolder,
                targetFramework,
                "ReferenceCopyLocalPaths",
                GetValuesCommand.ValueType.Item);
            getReferenceCopyLocalPathsCommand.DependsOnTargets = "Build";
            getReferenceCopyLocalPathsCommand.Execute().Should().Pass();

            referenceCopyLocalPaths = getReferenceCopyLocalPathsCommand.GetValues();
        }

        [Fact]
        public void CompileConflictsAreNotRemovedFromRuntimeDepsAssets()
        {
            TestProject testProject = new TestProject()
            {
                Name = "NetStandard2Library",
                TargetFrameworks = "netstandard2.0",
                //  In deps file, assets are under the ".NETStandard,Version=v2.0/" target (ie with empty RID) for some reason
                RuntimeIdentifier = string.Empty
            };

            testProject.PackageReferences.Add(new TestPackageReference("Microsoft.AspNetCore.Mvc.Razor", "2.1.0"));

            var testAsset = _testAssetsManager.CreateTestProject(testProject);

            var buildCommand = new BuildCommand(testAsset);

            buildCommand
                .Execute()
                .Should()
                .Pass();

            string outputFolder = buildCommand.GetOutputDirectory(testProject.TargetFrameworks,
                runtimeIdentifier: testProject.RuntimeIdentifier).FullName;

            string depsJsonPath = Path.Combine(outputFolder, $"{testProject.Name}.deps.json");

            var assets = DepsFileSkipTests.GetDepsJsonAssets(depsJsonPath, testProject, "runtime")
                .Select(DepsFileSkipTests.GetDepsJsonFilename)
                .ToList();

            assets.Should().Contain("System.ValueTuple.dll");

        }

        [Fact]
        public void AProjectCanReferenceADllInAPackageDirectly()
        {
            TestProject testProject = new TestProject()
            {
                Name = "ReferencePackageDllDirectly",
                TargetFrameworks = ToolsetInfo.CurrentTargetFramework,
                IsExe = true
            };

            testProject.PackageReferences.Add(new TestPackageReference("Microsoft.VisualStudio.Composition", "15.8.112"));

            var testAsset = _testAssetsManager.CreateTestProject(testProject)
                .WithProjectChanges(p =>
                {
                    var ns = p.Root.Name.Namespace;

                    var itemGroup = p.Root.Element(ns + "ItemGroup");
                    itemGroup.Add(new XElement(ns + "Reference",
                        new XAttribute("Include", @"$(NuGetPackageRoot)/microsoft.visualstudio.composition/15.8.112/lib/net45/Microsoft.VisualStudio.Composition.dll"),
                        new XAttribute("Private", "true")));
                });

            var buildCommand = new BuildCommand(testAsset);

            buildCommand
                .Execute()
                .Should()
                .Pass();
        }

        [Fact]
        public void DuplicateFrameworkAssembly()
        {
            TestProject testProject = new TestProject()
            {
                Name = "DuplicateFrameworkAssembly",
                TargetFrameworks = "net472",
                IsExe = true
            };
            testProject.References.Add("System.Runtime");
            testProject.References.Add("System.Runtime");
            testProject.PackageReferences.Add(new TestPackageReference("System.Runtime", "4.3.0"));

            var testAsset = _testAssetsManager.CreateTestProject(testProject);

            var buildCommand = new BuildCommand(testAsset);

            buildCommand
                .Execute()
                .Should()
                .Pass();
        }

        [Fact]
        public void FilesFromAspNetCoreSharedFrameworkAreNotIncluded()
        {
            var testProject = new TestProject()
            {
                Name = "AspNetCoreProject",
                TargetFrameworks = ToolsetInfo.CurrentTargetFramework,
                IsExe = true
            };

            testProject.PackageReferences.Add(new TestPackageReference("Microsoft.Extensions.DependencyInjection.Abstractions", "2.2.0"));

            var testAsset = _testAssetsManager.CreateTestProject(testProject)
                .WithProjectChanges(project =>
                {
                    var ns = project.Root.Name.Namespace;
                    var itemGroup = new XElement(ns + "ItemGroup");
                    project.Root.Add(itemGroup);
                    itemGroup.Add(new XElement(ns + "FrameworkReference",
                                    new XAttribute("Include", "Microsoft.AspNetCore.App")));
                });

            var buildCommand = new BuildCommand(testAsset);

            buildCommand
                .Execute()
                .Should()
                .Pass();

            var outputDirectory = buildCommand.GetOutputDirectory(testProject.TargetFrameworks);

            outputDirectory.Should().NotHaveFile("Microsoft.Extensions.DependencyInjection.Abstractions.dll");
        }

        [CoreMSBuildOnlyFact]
        public void AnalyzersAreConflictResolved()
        {
            var testProject = new TestProject()
            {
                Name = nameof(AnalyzersAreConflictResolved),
                TargetFrameworks = ToolsetInfo.CurrentTargetFramework
            };

            // add the package referenced analyzers
            testProject.PackageReferences.Add(new TestPackageReference("Microsoft.CodeAnalysis.NetAnalyzers", "5.0.3"));

            // enable inbox analyzers too
            var testAsset = _testAssetsManager.CreateTestProject(testProject)
                .WithProjectChanges(project =>
                {
                    var ns = project.Root.Name.Namespace;
                    var itemGroup = new XElement(ns + "PropertyGroup");
                    project.Root.Add(itemGroup);
                    itemGroup.Add(new XElement(ns + "EnableNETAnalyzers", "true"));
                    itemGroup.Add(new XElement(ns + "TreatWarningsAsErrors", "true"));
                    
                    // Don't error when generators/analyzers can't be loaded.
                    // This can occur when running tests against FullFramework MSBuild
                    // if the build machine has an MSBuild install with an older version of Roslyn
                    // than the generators in the SDK reference. We aren't testing the generators here
                    // and this failure will occur more clearly in other places when it's
                    // actually an important failure, so don't error out here.
                    itemGroup.Add(new XElement(ns + "WarningsNotAsErrors", "CS9057"));
                });

            var buildCommand = new BuildCommand(testAsset);

            buildCommand
                .Execute()
                .Should()
                .Pass();
        }
    }
}
