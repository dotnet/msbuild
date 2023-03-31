// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using FluentAssertions;
using Microsoft.Extensions.DependencyModel;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Microsoft.NET.TestFramework.Utilities;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.NET.Sdk.Razor.Tests
{
    public abstract class MvcBuildIntegrationTestLegacy : AspNetSdkTest
    {
        public abstract string TestProjectName { get; }
        public abstract string TargetFramework { get; }

        // Remove Razor prefix from assembly name
        public virtual string OutputFileName => $"{TestProjectName}.dll";

        public MvcBuildIntegrationTestLegacy(ITestOutputHelper log) : base(log) { }

        [CoreMSBuildOnlyFact]
        public virtual void Building_Project()
        {
            var testAsset = $"Razor{TestProjectName}";
            var project = CreateAspNetSdkTestAsset(testAsset);

            // Build
            var build = new BuildCommand(project);
            build.Execute().Should().Pass();

            var outputPath = build.GetOutputDirectory(TargetFramework, "Debug").ToString();
            var intermediateOutputPath = build.GetIntermediateDirectory(TargetFramework, "Debug").ToString();

            new FileInfo(Path.Combine(outputPath, OutputFileName)).Should().Exist();
            new FileInfo(Path.Combine(outputPath, $"{TestProjectName}.pdb")).Should().Exist();
            new FileInfo(Path.Combine(outputPath, $"{TestProjectName}.Views.dll")).Should().Exist();
            new FileInfo(Path.Combine(outputPath, $"{TestProjectName}.Views.pdb")).Should().Exist();

            // Verify RazorTagHelper works
            new FileInfo(Path.Combine(intermediateOutputPath, $"{TestProjectName}.TagHelpers.input.cache")).Should().Exist();
            new FileInfo(Path.Combine(intermediateOutputPath, $"{TestProjectName}.TagHelpers.output.cache")).Should().Exist();
            new FileInfo(
                Path.Combine(intermediateOutputPath, $"{TestProjectName}.TagHelpers.output.cache")).Should().Contain(
                @"""Name"":""SimpleMvc.SimpleTagHelper""");
        }

        [CoreMSBuildOnlyFact]
        public virtual void BuildingProject_CopyToOutputDirectoryFiles()
        {
            var testAsset = $"Razor{TestProjectName}";
            var project = CreateAspNetSdkTestAsset(testAsset);

            // Build
            var build = new BuildCommand(project);
            build.Execute().Should().Pass();

            var outputPath = build.GetOutputDirectory(TargetFramework, "Debug").ToString();

            // No cshtml files should be in the build output directory
            new DirectoryInfo(Path.Combine(outputPath, "Views")).Should().NotExist();

            // For .NET Core projects, no ref assemblies should be present in the output directory.
            new DirectoryInfo(Path.Combine(outputPath, "refs")).Should().NotExist();

        }

        [CoreMSBuildOnlyFact]
        public virtual void Publish_Project()
        {
            var testAsset = $"Razor{TestProjectName}";
            var project = CreateAspNetSdkTestAsset(testAsset);

            var publish = new PublishCommand(Log, project.TestRoot);
            publish.Execute().Should().Pass();

            var outputPath = publish.GetOutputDirectory(TargetFramework, "Debug").ToString();

            new FileInfo(Path.Combine(outputPath, OutputFileName)).Should().Exist();
            new FileInfo(Path.Combine(outputPath, $"{TestProjectName}.pdb")).Should().Exist();
            new FileInfo(Path.Combine(outputPath, $"{TestProjectName}.Views.dll")).Should().Exist();
            new FileInfo(Path.Combine(outputPath, $"{TestProjectName}.Views.pdb")).Should().Exist();

            // By default refs and .cshtml files will not be copied on publish
            new DirectoryInfo(Path.Combine(outputPath, "refs")).Should().NotExist();
            new DirectoryInfo(Path.Combine(outputPath, "Views")).Should().NotExist();

        }

        [CoreMSBuildOnlyFact]
        public virtual void Publish_IncludesRefAssemblies_WhenCopyRefAssembliesToPublishDirectoryIsSet()
        {
            var testAsset = $"Razor{TestProjectName}";
            var project = CreateAspNetSdkTestAsset(testAsset);

            var publish = new PublishCommand(Log, project.TestRoot);
            publish.Execute("/p:CopyRefAssembliesToPublishDirectory=true").Should().Pass();

            var outputPath = publish.GetOutputDirectory(TargetFramework, "Debug").ToString();

            new FileInfo(Path.Combine(outputPath, "refs", "System.Threading.Tasks.Extensions.dll")).Should().Exist();
        }

        [CoreMSBuildOnlyFact]
        public void Build_ProducesDepsFileWithCompilationContext_ButNoReferences()
        {
            var testAsset = $"Razor{TestProjectName}";
            var projectDirectory = CreateAspNetSdkTestAsset(testAsset);

            var customDefine = "AspNetSdkTest";
            var build = new BuildCommand(projectDirectory);
            build.Execute($"/p:DefineConstants={customDefine}").Should().Pass();

            var outputPath = build.GetOutputDirectory(TargetFramework, "Debug").ToString();

            var depsFile = new FileInfo(Path.Combine(outputPath, $"{TestProjectName}.deps.json"));
            depsFile.Should().Exist();
            var dependencyContext = ReadDependencyContext(depsFile.FullName);

            // Ensure some compile references exist
            var packageReference = dependencyContext.CompileLibraries.First(l => l.Name == "System.Runtime.CompilerServices.Unsafe");
            packageReference.Assemblies.Should().NotBeEmpty();

            var projectReference = dependencyContext.CompileLibraries.First(l => l.Name == TestProjectName);
            projectReference.Assemblies.Should().NotBeEmpty();

            dependencyContext.CompilationOptions.Defines.Should().Contain(customDefine);

            // Verify no refs folder is produced
            new DirectoryInfo(Path.Combine(outputPath, "publish", "refs")).Should().NotExist();
        }

        private static DependencyContext ReadDependencyContext(string depsFilePath)
        {
            var reader = new DependencyContextJsonReader();
            using (var stream = File.OpenRead(depsFilePath))
            {
                return reader.Read(stream);
            }
        }
    }
}
