// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO;
using FluentAssertions;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Commands;
using Xunit;
using Xunit.Abstractions;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.ProjectConstruction;
using System.Xml.Linq;

namespace Microsoft.NET.Build.Tests
{
    public class GivenThatWeHaveAPpContentFile : SdkTest
    {
        public GivenThatWeHaveAPpContentFile(ITestOutputHelper log) : base(log)
        {}

        [Fact]
        public void It_copies_to_output_successfully()
        {
            var packageReference = GetPackageReference();

            TestProject testProject = new TestProject()
            {
                Name = "CopyPPToOutputTest",
                IsExe = true, 
                TargetFrameworks = ToolsetInfo.CurrentTargetFramework
            };
            testProject.PackageReferences.Add(packageReference);
            testProject.AdditionalProperties.Add("RestoreAdditionalProjectSources", Path.GetDirectoryName(packageReference.NupkgPath));
            //  Use a test-specific packages folder
            testProject.AdditionalProperties["RestorePackagesPath"] = @"$(MSBuildProjectDirectory)\..\pkg";

            var testAsset = _testAssetsManager.CreateTestProject(testProject);

            var buildCommand = new BuildCommand(testAsset);
            buildCommand.Execute()
                .Should()
                .Pass();

            var outputPath = Path.Combine(testAsset.TestRoot, testProject.Name, "bin", "Debug", testProject.TargetFrameworks);
            File.Exists(Path.Combine(outputPath, packageReference.ID + ".dll")).Should().BeTrue();
            File.Exists(Path.Combine(outputPath, "Nontransformed.ps1")).Should().BeTrue();
            File.Exists(Path.Combine(outputPath, "Test.ps1")).Should().BeTrue();
        }

        private TestPackageReference GetPackageReference()
        {
            var referencedPackage = new TestProject()
            {
                Name = "CopyPPFilesToOutput",
                TargetFrameworks = ToolsetInfo.CurrentTargetFramework,
            };

            var packageAsset = _testAssetsManager.CreateTestProject(referencedPackage);
            WriteFile(Path.Combine(packageAsset.TestRoot, referencedPackage.Name, "Nontransformed.ps1"), "Content file");
            WriteFile(Path.Combine(packageAsset.TestRoot, referencedPackage.Name, "Test.ps1.pp"), "Content file");
            packageAsset = packageAsset
                .WithProjectChanges(project => AddContent(project));

            var packCommand = new PackCommand(Log, packageAsset.TestRoot, referencedPackage.Name);
            packCommand.Execute()
                .Should()
                .Pass();
            return new TestPackageReference(referencedPackage.Name, "1.0.0", packCommand.GetNuGetPackage(referencedPackage.Name));
        }

        private void AddContent(XDocument package)
        {
            var ns = package.Root.Name.Namespace;
            XElement itemGroup = new XElement(ns + "ItemGroup");
            itemGroup.Add(new XElement(ns + "Content", new XAttribute("Include", "Nontransformed.ps1"),
                new XAttribute("PackageCopyToOutput", "true")));
            itemGroup.Add(new XElement(ns + "Content", new XAttribute("Include", "Test.ps1.pp"),
                new XAttribute("PackageCopyToOutput", "true")));
            package.Root.Add(itemGroup);
        }

        private void WriteFile(string path, string contents)
        {
            string folder = Path.GetDirectoryName(path);
            Directory.CreateDirectory(folder);
            File.WriteAllText(path, contents);
        }
    }
}
