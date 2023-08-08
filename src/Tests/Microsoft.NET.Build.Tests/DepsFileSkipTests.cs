using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Xml.Linq;
using FluentAssertions;
using Microsoft.Extensions.DependencyModel;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Microsoft.NET.TestFramework.ProjectConstruction;
using Newtonsoft.Json.Linq;
using NuGet.Frameworks;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.NET.Build.Tests
{
    public class DepsFileSkipTests : SdkTest
    {
        public DepsFileSkipTests(ITestOutputHelper log) : base(log)
        {

        }

        [Fact]
        public void RuntimeAssemblyFromPackageCanBeSkipped()
        {
            var testProject = new TestProject()
            {
                Name = "SkipRuntimeAssemblyFromPackage",
                TargetFrameworks = ToolsetInfo.CurrentTargetFramework,
                IsExe = true
            };

            testProject.PackageReferences.Add(new TestPackageReference("Newtonsoft.Json", ToolsetInfo.GetNewtonsoftJsonPackageVersion()));

            string filenameToSkip = "Newtonsoft.Json.dll";

            TestSkippingFile(testProject, filenameToSkip, "runtime");
        }

        [Fact]
        public void RuntimeAssemblyFromRuntimePackCanBeSkipped()
        {
            var testProject = new TestProject()
            {
                Name = "SkipRuntimeAssemblyFromRuntimePack",
                TargetFrameworks = ToolsetInfo.CurrentTargetFramework,
                IsExe = true
            };

            testProject.RuntimeIdentifier = EnvironmentInfo.GetCompatibleRid(testProject.TargetFrameworks);

            string filenameToSkip = "Microsoft.CSharp.dll";

            TestSkippingFile(testProject, filenameToSkip, "runtime");
        }

        [Fact]
        public void NativeAssetFromPackageCanBeSkipped()
        {
            var testProject = new TestProject()
            {
                Name = "SkipNativeAssetFromPackage",
                TargetFrameworks = ToolsetInfo.CurrentTargetFramework,
                IsExe = true
            };

            testProject.RuntimeIdentifier = EnvironmentInfo.GetCompatibleRid(testProject.TargetFrameworks);

            testProject.PackageReferences.Add(new TestPackageReference("sqlite", "3.13.0"));

            string filenameToSkip = FileConstants.DynamicLibPrefix + "sqlite3" + FileConstants.DynamicLibSuffix;

            TestSkippingFile(testProject, filenameToSkip, "native");
        }

        [Fact]
        public void RuntimeTargetFromPackageCanBeSkipped()
        {
            var testProject = new TestProject()
            {
                Name = "SkipNativeAssetFromPackage",
                TargetFrameworks = ToolsetInfo.CurrentTargetFramework,
                IsExe = true
            };

            testProject.PackageReferences.Add(new TestPackageReference("sqlite", "3.13.0"));

            string filenameToSkip = FileConstants.DynamicLibPrefix + "sqlite3" + FileConstants.DynamicLibSuffix;

            TestSkippingFile(testProject, filenameToSkip, "runtimeTargets");
        }

        [Fact]
        public void NativeAssetFromRuntimePackCanBeSkipped()
        {
            var testProject = new TestProject()
            {
                Name = "SkipNativeAssetFromRuntimePack",
                TargetFrameworks = ToolsetInfo.CurrentTargetFramework,
                IsExe = true
            };

            testProject.RuntimeIdentifier = EnvironmentInfo.GetCompatibleRid(testProject.TargetFrameworks);

            string filenameToSkip = FileConstants.DynamicLibPrefix + "coreclr" + FileConstants.DynamicLibSuffix;

            TestSkippingFile(testProject, filenameToSkip, "native");
        }

        [Fact]
        public void ResourceAssetFromPackageCanBeSkipped()
        {
            var testProject = new TestProject()
            {
                Name = "SkipResourceFromPackage",
                TargetFrameworks = ToolsetInfo.CurrentTargetFramework,
                IsExe = true
            };

            testProject.PackageReferences.Add(new TestPackageReference("Humanizer", "2.8.26"));

            string filenameToSkip = "de/Humanizer.resources.dll";
            string filenameNotToSkip = "es/Humanizer.resources.dll";
            string assetType = "resources";

            var testAsset = _testAssetsManager.CreateTestProject(testProject, testProject.Name)
               .WithProjectChanges(project => AddSkipTarget(project, filenameToSkip));

            var buildCommand = new BuildCommand(testAsset);

            buildCommand
               .Execute()
               .Should()
               .Pass();

            string outputFolder = buildCommand.GetOutputDirectory(testProject.TargetFrameworks,
                runtimeIdentifier: testProject.RuntimeIdentifier).FullName;

            string depsJsonPath = Path.Combine(outputFolder, $"{testProject.Name}.deps.json");

            var resourceAssets = GetDepsJsonAssets(depsJsonPath, testProject, assetType)
                .Select(GetDepsJsonLocalizedResourceRelativePath)
                .ToList();

            resourceAssets.Should().Contain(filenameToSkip);
            resourceAssets.Should().Contain(filenameNotToSkip);

            //  Force deps.json to be regenerated, otherwise it would be considered up-to-date
            File.Delete(depsJsonPath);

            buildCommand
               .Execute("/p:AddFileToSkip=true")
               .Should()
               .Pass();

            resourceAssets = GetDepsJsonAssets(depsJsonPath, testProject, assetType)
                .Select(GetDepsJsonLocalizedResourceRelativePath)
                .ToList();

            resourceAssets.Should().NotContain(filenameToSkip);
            resourceAssets.Should().Contain(filenameNotToSkip);
        }

        private void TestSkippingFile(TestProject testProject, string filenameToSkip, string assetType)
        {
            var testAsset = _testAssetsManager.CreateTestProject(testProject, testProject.Name, identifier: filenameToSkip + assetType)
                .WithProjectChanges(project => AddSkipTarget(project, filenameToSkip));

            var buildCommand = new BuildCommand(testAsset);

            buildCommand
               .Execute()
               .Should()
               .Pass();

            string outputFolder = buildCommand.GetOutputDirectory(testProject.TargetFrameworks,
                runtimeIdentifier: testProject.RuntimeIdentifier).FullName;

            string depsJsonPath = Path.Combine(outputFolder, $"{testProject.Name}.deps.json");

            var assets = GetDepsJsonAssets(depsJsonPath, testProject, assetType)
                .Select(GetDepsJsonFilename)
                .ToList();

            assets.Should().Contain(filenameToSkip);

            //  Force deps.json to be regenerated, otherwise it would be considered up-to-date
            File.Delete(depsJsonPath);

            buildCommand
               .Execute("/p:AddFileToSkip=true")
               .Should()
               .Pass();

            assets = GetDepsJsonAssets(depsJsonPath, testProject, assetType)
                .Select(GetDepsJsonFilename)
                .ToList();

            assets.Should().NotContain(filenameToSkip);
        }

        private void AddSkipTarget(XDocument project, string filenameToSkip)
        {
            var ns = project.Root.Name.Namespace;

            var target = new XElement(ns + "Target",
                new XAttribute("Name", "AddFilesToSkip"),
                new XAttribute("BeforeTargets", "GenerateBuildDependencyFile"),
                new XAttribute("Condition", "'$(AddFileToSkip)' == 'true'"));

            project.Root.Add(target);

            var itemGroup = new XElement(ns + "ItemGroup");
            target.Add(itemGroup);

            if (filenameToSkip.Contains('/'))
            {
                string filenameToSkipWithCorrectSlash = filenameToSkip.Replace('/', Path.DirectorySeparatorChar);

                //  This is a localized resource we need to skip
                var fileToSkipItem = new XElement(ns + "_FileToSkip",
                        new XAttribute("Include", "@(ResourceCopyLocalItems)"),
                        new XAttribute("Condition", $"'%(DestinationSubPath)' == '{filenameToSkipWithCorrectSlash}'"));

                itemGroup.Add(fileToSkipItem);
            }
            else
            {
                var fileToSkipItem = new XElement(ns + "_FileToSkip",
                                        new XAttribute("Include", "@(ReferencePath);@(ReferenceDependencyPaths);@(RuntimePackAsset);@(NativeCopyLocalItems);@(ResourceCopyLocalItems);@(RuntimeTargetsCopyLocalItems)"),
                                        new XAttribute("Condition", $"'%(Filename)%(Extension)' == '{filenameToSkip}'"));

                itemGroup.Add(fileToSkipItem);
            }            

            var conflictItem = new XElement(ns + "_ConflictPackageFiles",
                                new XAttribute("Include", "@(_FileToSkip)"),
                                new XAttribute("KeepMetadata", "-None-"));

            itemGroup.Add(conflictItem);
        }

        public static List<string> GetDepsJsonAssets(string depsJsonPath, TestProject testProject, string assetType)
        {
            string frameworkName = NuGetFramework.Parse(testProject.TargetFrameworks).DotNetFrameworkName;
            string targetName;
            if (testProject.RuntimeIdentifier == null)
            {
                targetName = frameworkName;
            }
            else
            {
                targetName = frameworkName + "/" + testProject.RuntimeIdentifier;
            }

            string depsJsonContents = File.ReadAllText(depsJsonPath);
            JObject depsJson = JObject.Parse(depsJsonContents);
            var target = (JObject)(JObject)(JObject)depsJson["targets"][targetName];
            var assets = target.Properties().SelectMany(lib => lib.Value[assetType] ?? Enumerable.Empty<JToken>()).ToList();
            var assetNames = assets.Select(library => ((JProperty)library).Name).ToList();
            return assetNames;
        }

        public static string GetDepsJsonFilename(string depsJsonFilePath)
        {
            return depsJsonFilePath.Split('/', StringSplitOptions.RemoveEmptyEntries).Last();
        }

        private string GetDepsJsonLocalizedResourceRelativePath(string depsJsonFilePath)
        {
            //  Covert a path such as: lib/netstandard1.0/de/Humanizer.resources.dll
            //  To a path such as: de/Humanizer.resources.dll
            return string.Join('/', depsJsonFilePath.Split('/', StringSplitOptions.RemoveEmptyEntries).TakeLast(2));
        }

    }
}
