// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System.IO;
using System.Linq;
using System.Text.Json;
using System.Xml.Linq;
using FluentAssertions;
using Microsoft.AspNetCore.Razor.Tasks;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.NET.Sdk.BlazorWebAssembly.Tests
{
    public class WasmBuildIntegrationTest : BlazorWasmBaselineTests
    {
        public WasmBuildIntegrationTest(ITestOutputHelper log) : base(log, GenerateBaselines) { }

        [Fact]
        public void BuildMinimal_Works()
        {
            // Arrange
            // Minimal has no project references, service worker etc. This is pretty close to the project template.
            var testAsset = "BlazorWasmMinimal";
            var testInstance = CreateAspNetSdkTestAsset(testAsset);
            File.WriteAllText(Path.Combine(testInstance.TestRoot, "App.razor.css"), "h1 { font-size: 16px; }");

            var build = new BuildCommand(testInstance);
            build.Execute()
                .Should()
                .Pass();

            var buildOutputDirectory = build.GetOutputDirectory(DefaultTfm).ToString();

            new FileInfo(Path.Combine(buildOutputDirectory, "wwwroot", "_framework", "blazor.boot.json")).Should().Exist();
            new FileInfo(Path.Combine(buildOutputDirectory, "wwwroot", "_framework", "blazor.webassembly.js")).Should().Exist();
            new FileInfo(Path.Combine(buildOutputDirectory, "wwwroot", "_framework", "dotnet.wasm")).Should().Exist();
            new FileInfo(Path.Combine(buildOutputDirectory, "wwwroot", "_framework", "dotnet.timezones.blat")).Should().Exist();
            new FileInfo(Path.Combine(buildOutputDirectory, "wwwroot", "_framework", "dotnet.wasm.gz")).Should().Exist();
            new FileInfo(Path.Combine(buildOutputDirectory, "wwwroot", "_framework", "blazorwasm-minimal.dll")).Should().Exist();
        }

        [Theory]
        [InlineData("blazor")]
        [InlineData("blazor spaces")]
        public void Build_Works(string identifier)
        {
            // Arrange
            var testAppName = "BlazorWasmWithLibrary";
            var testInstance = CreateAspNetSdkTestAsset(testAppName, identifier: identifier);

            var buildCommand = new BuildCommand(testInstance, "blazorwasm");
            buildCommand.Execute()
                .Should().Pass();

            var buildOutputDirectory = buildCommand.GetOutputDirectory(DefaultTfm).ToString();

            new FileInfo(Path.Combine(buildOutputDirectory, "wwwroot", "_framework", "blazor.boot.json")).Should().Exist();
            new FileInfo(Path.Combine(buildOutputDirectory, "wwwroot", "_framework", "blazor.webassembly.js")).Should().Exist();
            new FileInfo(Path.Combine(buildOutputDirectory, "wwwroot", "_framework", "dotnet.wasm")).Should().Exist();
            new FileInfo(Path.Combine(buildOutputDirectory, "wwwroot", "_framework", "dotnet.wasm.gz")).Should().Exist();
            new FileInfo(Path.Combine(buildOutputDirectory, "wwwroot", "_framework", "blazorwasm.dll")).Should().Exist();
            new FileInfo(Path.Combine(buildOutputDirectory, "wwwroot", "_framework", "System.Text.Json.dll")).Should().Exist();
            new FileInfo(Path.Combine(buildOutputDirectory, "wwwroot", "_framework", "System.Text.Json.dll.gz")).Should().Exist();
            new FileInfo(Path.Combine(buildOutputDirectory, "wwwroot", "_framework", "System.dll")).Should().Exist();
            new FileInfo(Path.Combine(buildOutputDirectory, "wwwroot", "_framework", "System.dll.gz")).Should().Exist();

            new FileInfo(Path.Combine(buildOutputDirectory, "wwwroot", "_framework", "blazorwasm.pdb")).Should().Exist();
            new FileInfo(Path.Combine(buildOutputDirectory, "wwwroot", "_framework", "RazorClassLibrary.pdb")).Should().Exist();
            new FileInfo(Path.Combine(buildOutputDirectory, "wwwroot", "_framework", "RazorClassLibrary.dll")).Should().Exist();
        }

        [Fact]
        public void Build_Works_WithLibraryUsingHintPath()
        {
            // Arrange
            var testAppName = "BlazorWasmWithLibrary";
            var testInstance = CreateAspNetSdkTestAsset(testAppName);

            testInstance.WithProjectChanges((project, document) =>
            {
                if (Path.GetFileNameWithoutExtension(project) == "blazorwasm")
                {
                    var reference = document
                        .Descendants()
                        .Single(e => 
                            e.Name == "ProjectReference" && 
                            e.Attribute("Include").Value == @"..\razorclasslibrary\RazorClassLibrary.csproj");

                    reference.Name = "Reference";
                    reference.Add(new XElement(
                        "HintPath",
                        Path.Combine("..", "razorclasslibrary", "bin", "Debug", "net6.0", "RazorClassLibrary.dll")));
                }
            });

            var buildLibraryCommand = new BuildCommand(testInstance, "razorclasslibrary");
            buildLibraryCommand.WithWorkingDirectory(testInstance.TestRoot);
            buildLibraryCommand.Execute("/bl")
                .Should().Pass();

            var buildCommand = new BuildCommand(testInstance, "blazorwasm");
            buildCommand.WithWorkingDirectory(testInstance.TestRoot);
            buildCommand.Execute("/bl")
                .Should().Pass();

            var buildOutputDirectory = buildCommand.GetOutputDirectory(DefaultTfm).ToString();

            new FileInfo(Path.Combine(buildOutputDirectory, "wwwroot", "_framework", "blazor.boot.json")).Should().Exist();
            new FileInfo(Path.Combine(buildOutputDirectory, "wwwroot", "_framework", "blazor.webassembly.js")).Should().Exist();
            new FileInfo(Path.Combine(buildOutputDirectory, "wwwroot", "_framework", "dotnet.wasm")).Should().Exist();
            new FileInfo(Path.Combine(buildOutputDirectory, "wwwroot", "_framework", "dotnet.wasm.gz")).Should().Exist();
            new FileInfo(Path.Combine(buildOutputDirectory, "wwwroot", "_framework", "blazorwasm.dll")).Should().Exist();
            new FileInfo(Path.Combine(buildOutputDirectory, "wwwroot", "_framework", "System.Text.Json.dll")).Should().Exist();
            new FileInfo(Path.Combine(buildOutputDirectory, "wwwroot", "_framework", "System.Text.Json.dll.gz")).Should().Exist();
            new FileInfo(Path.Combine(buildOutputDirectory, "wwwroot", "_framework", "System.dll")).Should().Exist();
            new FileInfo(Path.Combine(buildOutputDirectory, "wwwroot", "_framework", "System.dll.gz")).Should().Exist();

            new FileInfo(Path.Combine(buildOutputDirectory, "wwwroot", "_framework", "blazorwasm.pdb")).Should().Exist();
            new FileInfo(Path.Combine(buildOutputDirectory, "wwwroot", "_framework", "RazorClassLibrary.pdb")).Should().Exist();
            new FileInfo(Path.Combine(buildOutputDirectory, "wwwroot", "_framework", "RazorClassLibrary.dll")).Should().Exist();
        }

        [Fact]
        public void Build_InRelease_Works()
        {
            // Arrange
            var testAppName = "BlazorWasmWithLibrary";
            var testInstance = CreateAspNetSdkTestAsset(testAppName);

            var buildCommand = new BuildCommand(testInstance, "blazorwasm");
            buildCommand.Execute("/p:Configuration=Release")
                .Should().Pass();

            var buildOutputDirectory = buildCommand.GetOutputDirectory(DefaultTfm, "Release").ToString();

            new FileInfo(Path.Combine(buildOutputDirectory, "wwwroot", "_framework", "blazor.boot.json")).Should().Exist();
            new FileInfo(Path.Combine(buildOutputDirectory, "wwwroot", "_framework", "blazor.webassembly.js")).Should().Exist();
            new FileInfo(Path.Combine(buildOutputDirectory, "wwwroot", "_framework", "dotnet.wasm")).Should().Exist();
            new FileInfo(Path.Combine(buildOutputDirectory, "wwwroot", "_framework", "dotnet.wasm.gz")).Should().Exist();
            new FileInfo(Path.Combine(buildOutputDirectory, "wwwroot", "_framework", "blazorwasm.dll")).Should().Exist();
            new FileInfo(Path.Combine(buildOutputDirectory, "wwwroot", "_framework", "System.Text.Json.dll")).Should().Exist();
            new FileInfo(Path.Combine(buildOutputDirectory, "wwwroot", "_framework", "System.Text.Json.dll.gz")).Should().Exist();
            new FileInfo(Path.Combine(buildOutputDirectory, "wwwroot", "_framework", "System.dll")).Should().Exist();
            new FileInfo(Path.Combine(buildOutputDirectory, "wwwroot", "_framework", "System.dll.gz")).Should().Exist();

            new FileInfo(Path.Combine(buildOutputDirectory, "wwwroot", "_framework", "blazorwasm.pdb")).Should().Exist();
            new FileInfo(Path.Combine(buildOutputDirectory, "wwwroot", "_framework", "RazorClassLibrary.pdb")).Should().Exist();
            new FileInfo(Path.Combine(buildOutputDirectory, "wwwroot", "_framework", "RazorClassLibrary.dll")).Should().Exist();
        }

        [Fact]
        public void Build_ProducesBootJsonDataWithExpectedContent()
        {
            // Arrange
            var testAppName = "BlazorWasmWithLibrary";
            var testInstance = CreateAspNetSdkTestAsset(testAppName);

            var wwwroot = Path.Combine(testInstance.TestRoot, "blazorwasm", "wwwroot");
            File.WriteAllText(Path.Combine(wwwroot, "appsettings.json"), "Default settings");
            File.WriteAllText(Path.Combine(wwwroot, "appsettings.development.json"), "Development settings");

            var buildCommand = new BuildCommand(testInstance, "blazorwasm");
            buildCommand.WithWorkingDirectory(testInstance.TestRoot);
            buildCommand.Execute("/bl")
                .Should().Pass();

            var buildOutputDirectory = buildCommand.GetOutputDirectory(DefaultTfm).ToString();

            var bootJsonPath = Path.Combine(buildOutputDirectory, "wwwroot", "_framework", "blazor.boot.json");
            var bootJsonData = ReadBootJsonData(bootJsonPath);

            var runtime = bootJsonData.resources.runtime;
            runtime.Should().ContainKey("dotnet.wasm");

            var assemblies = bootJsonData.resources.assembly;
            assemblies.Should().ContainKey("blazorwasm.dll");
            assemblies.Should().ContainKey("RazorClassLibrary.dll");
            assemblies.Should().ContainKey("System.Text.Json.dll");

            var pdb = bootJsonData.resources.pdb;
            pdb.Should().ContainKey("blazorwasm.pdb");
            pdb.Should().ContainKey("RazorClassLibrary.pdb");

            bootJsonData.resources.satelliteResources.Should().BeNull();

            bootJsonData.config.Should().Contain("appsettings.json");
            bootJsonData.config.Should().Contain("appsettings.development.json");
        }

        [Fact]
        public void Build_InRelease_ProducesBootJsonDataWithExpectedContent()
        {
            // Arrange
            var testAppName = "BlazorWasmWithLibrary";
            var testInstance = CreateAspNetSdkTestAsset(testAppName);

            var wwwroot = Path.Combine(testInstance.TestRoot, "blazorwasm", "wwwroot");
            File.WriteAllText(Path.Combine(wwwroot, "appsettings.json"), "Default settings");
            File.WriteAllText(Path.Combine(wwwroot, "appsettings.development.json"), "Development settings");

            var buildCommand = new BuildCommand(testInstance, "blazorwasm");
            buildCommand.WithWorkingDirectory(testInstance.TestRoot);
            buildCommand.Execute("/p:Configuration=Release", "/bl")
                .Should().Pass();

            var buildOutputDirectory = buildCommand.GetOutputDirectory(DefaultTfm, "Release").ToString();

            var bootJsonPath = Path.Combine(buildOutputDirectory, "wwwroot", "_framework", "blazor.boot.json");
            var bootJsonData = ReadBootJsonData(bootJsonPath);

            var runtime = bootJsonData.resources.runtime;
            runtime.Should().ContainKey("dotnet.wasm");

            var assemblies = bootJsonData.resources.assembly;
            assemblies.Should().ContainKey("blazorwasm.dll");
            assemblies.Should().ContainKey("RazorClassLibrary.dll");
            assemblies.Should().ContainKey("System.Text.Json.dll");

            var pdb = bootJsonData.resources.pdb;
            pdb.Should().ContainKey("blazorwasm.pdb");
            pdb.Should().ContainKey("RazorClassLibrary.pdb");

            bootJsonData.resources.satelliteResources.Should().BeNull();
        }

        [Fact]
        public void Build_WithBlazorEnableTimeZoneSupportDisabled_DoesNotCopyTimeZoneInfo()
        {
            // Arrange
            var testAppName = "BlazorWasmWithLibrary";
            var testInstance = CreateAspNetSdkTestAsset(testAppName);

            testInstance.WithProjectChanges((project) =>
            {
                var ns = project.Root.Name.Namespace;
                var itemGroup = new XElement(ns + "PropertyGroup");
                itemGroup.Add(new XElement("BlazorEnableTimeZoneSupport", false));
                project.Root.Add(itemGroup);
            });


            var buildCommand = new BuildCommand(testInstance, "blazorwasm");
            buildCommand.Execute()
                .Should().Pass();

            var buildOutputDirectory = buildCommand.GetOutputDirectory(DefaultTfm).ToString();

            var bootJsonPath = Path.Combine(buildOutputDirectory, "wwwroot", "_framework", "blazor.boot.json");
            var bootJsonData = ReadBootJsonData(bootJsonPath);

            var runtime = bootJsonData.resources.runtime;
            runtime.Should().ContainKey("dotnet.wasm");
            runtime.Should().NotContainKey("dotnet.timezones.blat");

            new FileInfo(Path.Combine(buildOutputDirectory, "wwwroot", "_framework", "dotnet.wasm")).Should().Exist();
            new FileInfo(Path.Combine(buildOutputDirectory, "wwwroot", "_framework", "dotnet.timezones.blat")).Should().NotExist();
        }

        [Fact]
        public void Build_WithInvariantGlobalizationEnabled_DoesNotCopyGlobalizationData()
        {
            // Arrange
            var testAppName = "BlazorWasmMinimal";
            var testInstance = CreateAspNetSdkTestAsset(testAppName);

            testInstance.WithProjectChanges((project) =>
            {
                var ns = project.Root.Name.Namespace;
                var itemGroup = new XElement(ns + "PropertyGroup");
                itemGroup.Add(new XElement("InvariantGlobalization", true));
                project.Root.Add(itemGroup);
            });

            var buildCommand = new BuildCommand(testInstance);
            buildCommand.Execute()
                .Should().Pass();

            var buildOutputDirectory = buildCommand.GetOutputDirectory(DefaultTfm).ToString();

            var bootJsonPath = Path.Combine(buildOutputDirectory, "wwwroot", "_framework", "blazor.boot.json");
            var bootJsonData = ReadBootJsonData(bootJsonPath);

            bootJsonData.icuDataMode.Should().Be(ICUDataMode.Invariant);
            var runtime = bootJsonData.resources.runtime;
            runtime.Should().ContainKey("dotnet.wasm");
            runtime.Should().ContainKey("dotnet.timezones.blat");

            runtime.Should().NotContainKey("icudt.dat");
            runtime.Should().NotContainKey("icudt_EFIGS.dat");

            new FileInfo(Path.Combine(buildOutputDirectory, "wwwroot", "_framework", "dotnet.wasm")).Should().Exist();
            new FileInfo(Path.Combine(buildOutputDirectory, "wwwroot", "_framework", "icudt.dat")).Should().NotExist();
            new FileInfo(Path.Combine(buildOutputDirectory, "wwwroot", "_framework", "icudt_CJK.dat")).Should().NotExist();
            new FileInfo(Path.Combine(buildOutputDirectory, "wwwroot", "_framework", "icudt_EFIGS.dat")).Should().NotExist();
            new FileInfo(Path.Combine(buildOutputDirectory, "wwwroot", "_framework", "icudt_no_CJK.dat")).Should().NotExist();
        }

        [Fact]
        public void Publish_WithInvariantGlobalizationEnabled_DoesNotCopyGlobalizationData()
        {
            // Arrange
            var testAppName = "BlazorHosted";
            var testInstance = CreateAspNetSdkTestAsset(testAppName);

            testInstance.WithProjectChanges((project) =>
            {
                var ns = project.Root.Name.Namespace;
                var itemGroup = new XElement(ns + "PropertyGroup");
                itemGroup.Add(new XElement("InvariantGlobalization", true));
                project.Root.Add(itemGroup);
            });

            var publishCommand = new PublishCommand(testInstance, "blazorhosted");
            publishCommand.WithWorkingDirectory(testInstance.TestRoot);
            publishCommand.Execute("/bl")
                .Should().Pass();

            var publishOutputDirectory = publishCommand.GetOutputDirectory(DefaultTfm).ToString();

            var bootJsonPath = Path.Combine(publishOutputDirectory, "wwwroot", "_framework", "blazor.boot.json");
            var bootJsonData = ReadBootJsonData(bootJsonPath);

            bootJsonData.icuDataMode.Should().Be(ICUDataMode.Invariant);
            var runtime = bootJsonData.resources.runtime;
            runtime.Should().ContainKey("dotnet.wasm");
            runtime.Should().ContainKey("dotnet.timezones.blat");

            runtime.Should().NotContainKey("icudt.dat");
            runtime.Should().NotContainKey("icudt_EFIGS.dat");

            new FileInfo(Path.Combine(publishOutputDirectory, "wwwroot", "_framework", "dotnet.wasm")).Should().Exist();
            new FileInfo(Path.Combine(publishOutputDirectory, "wwwroot", "_framework", "icudt.dat")).Should().NotExist();
            new FileInfo(Path.Combine(publishOutputDirectory, "wwwroot", "_framework", "icudt_CJK.dat")).Should().NotExist();
            new FileInfo(Path.Combine(publishOutputDirectory, "wwwroot", "_framework", "icudt_EFIGS.dat")).Should().NotExist();
            new FileInfo(Path.Combine(publishOutputDirectory, "wwwroot", "_framework", "icudt_no_CJK.dat")).Should().NotExist();
        }

        [Fact]
        public void Build_WithBlazorWebAssemblyLoadAllGlobalizationData_SetsICUDataMode()
        {
            // Arrange
            var testAppName = "BlazorWasmMinimal";
            var testInstance = CreateAspNetSdkTestAsset(testAppName);

            testInstance.WithProjectChanges((project) =>
            {
                var ns = project.Root.Name.Namespace;
                var itemGroup = new XElement(ns + "PropertyGroup");
                itemGroup.Add(new XElement("BlazorWebAssemblyLoadAllGlobalizationData", true));
                project.Root.Add(itemGroup);
            });

            var buildCommand = new BuildCommand(testInstance);
            buildCommand.Execute()
                .Should().Pass();

            var buildOutputDirectory = buildCommand.GetOutputDirectory(DefaultTfm).ToString();

            var bootJsonPath = Path.Combine(buildOutputDirectory, "wwwroot", "_framework", "blazor.boot.json");
            var bootJsonData = ReadBootJsonData(bootJsonPath);

            bootJsonData.icuDataMode.Should().Be(ICUDataMode.All);
            var runtime = bootJsonData.resources.runtime;

            runtime.Should().ContainKey("dotnet.wasm");
            runtime.Should().ContainKey("dotnet.timezones.blat");
            runtime.Should().ContainKey("icudt.dat");
            runtime.Should().ContainKey("icudt_EFIGS.dat");

            new FileInfo(Path.Combine(buildOutputDirectory, "wwwroot", "_framework", "dotnet.wasm")).Should().Exist();
            new FileInfo(Path.Combine(buildOutputDirectory, "wwwroot", "_framework", "icudt.dat")).Should().Exist();
            new FileInfo(Path.Combine(buildOutputDirectory, "wwwroot", "_framework", "icudt_CJK.dat")).Should().Exist();
            new FileInfo(Path.Combine(buildOutputDirectory, "wwwroot", "_framework", "icudt_EFIGS.dat")).Should().Exist();
            new FileInfo(Path.Combine(buildOutputDirectory, "wwwroot", "_framework", "icudt_no_CJK.dat")).Should().Exist();
        }

        [Fact]
        public void Publish_WithBlazorWebAssemblyLoadAllGlobalizationData_SetsICUDataMode()
        {
            // Arrange
            var testAppName = "BlazorHosted";
            var testInstance = CreateAspNetSdkTestAsset(testAppName);

            testInstance.WithProjectChanges((project) =>
            {
                var ns = project.Root.Name.Namespace;
                var itemGroup = new XElement(ns + "PropertyGroup");
                itemGroup.Add(new XElement("BlazorWebAssemblyLoadAllGlobalizationData", true));
                project.Root.Add(itemGroup);
            });

            var publishCommand = new PublishCommand(testInstance, "blazorhosted");
            publishCommand.WithWorkingDirectory(testInstance.TestRoot);
            publishCommand.Execute("/bl")
                .Should().Pass();

            var publishDirectory = publishCommand.GetOutputDirectory(DefaultTfm).ToString();

            var bootJsonPath = Path.Combine(publishDirectory, "wwwroot", "_framework", "blazor.boot.json");
            var bootJsonData = ReadBootJsonData(bootJsonPath);

            bootJsonData.icuDataMode.Should().Be(ICUDataMode.All);
            var runtime = bootJsonData.resources.runtime;

            runtime.Should().ContainKey("dotnet.wasm");
            runtime.Should().ContainKey("dotnet.timezones.blat");
            runtime.Should().ContainKey("icudt.dat");
            runtime.Should().ContainKey("icudt_EFIGS.dat");

            new FileInfo(Path.Combine(publishDirectory, "wwwroot", "_framework", "dotnet.wasm")).Should().Exist();
            new FileInfo(Path.Combine(publishDirectory, "wwwroot", "_framework", "icudt.dat")).Should().Exist();
            new FileInfo(Path.Combine(publishDirectory, "wwwroot", "_framework", "icudt_CJK.dat")).Should().Exist();
            new FileInfo(Path.Combine(publishDirectory, "wwwroot", "_framework", "icudt_EFIGS.dat")).Should().Exist();
            new FileInfo(Path.Combine(publishDirectory, "wwwroot", "_framework", "icudt_no_CJK.dat")).Should().Exist();
        }

        [Fact]
        public void Build_Hosted_Works()
        {
            // Arrange
            var testAppName = "BlazorHosted";
            var testInstance = CreateAspNetSdkTestAsset(testAppName);

            var buildCommand = new BuildCommand(testInstance, "blazorhosted");
            buildCommand.WithWorkingDirectory(testInstance.TestRoot);
            buildCommand.Execute("/bl").Should().Pass();

            var buildOutputDirectory = buildCommand.GetOutputDirectory(DefaultTfm).ToString();

            new FileInfo(Path.Combine(buildOutputDirectory, "wwwroot", "_framework", "_bin", "blazorwasm.dll")).Should().NotExist();
        }

        [Fact(Skip="https://github.com/dotnet/sdk/issues/28429")]
        public void Build_SatelliteAssembliesAreCopiedToBuildOutput()
        {
            // Arrange
            var testAppName = "BlazorWasmWithLibrary";
            ProjectDirectory = CreateAspNetSdkTestAsset(testAppName);

            ProjectDirectory.WithProjectChanges((path, project) =>
            {
                if (path.Contains("blazorwasm"))
                {
                    var ns = project.Root.Name.Namespace;
                    var propertyGroup = new XElement(ns + "PropertyGroup");
                    propertyGroup.Add(new XElement("DefineConstants", @"$(DefineConstants);REFERENCE_classlibrarywithsatelliteassemblies"));
                    var itemGroup = new XElement(ns + "ItemGroup");
                    itemGroup.Add(new XElement("ProjectReference", new XAttribute("Include", @"..\classlibrarywithsatelliteassemblies\classlibrarywithsatelliteassemblies.csproj")));
                    project.Root.Add(propertyGroup);
                    project.Root.Add(itemGroup);
                }
            });

            var resxfileInProject = Path.Combine(ProjectDirectory.TestRoot, "blazorwasm", "Resources.ja.resx.txt");
            File.Move(resxfileInProject, Path.Combine(ProjectDirectory.TestRoot, "blazorwasm", "Resource.ja.resx"));

            var buildCommand = new BuildCommand(ProjectDirectory, "blazorwasm");
            buildCommand.WithWorkingDirectory(ProjectDirectory.TestRoot);
            buildCommand.Execute("/bl").Should().Pass();

            var outputPath = buildCommand.GetOutputDirectory(DefaultTfm).ToString();
            var intermediateOutputPath = buildCommand.GetIntermediateDirectory(DefaultTfm).ToString();

            // GenerateStaticWebAssetsManifest should generate the manifest file.
            var path = Path.Combine(intermediateOutputPath, "staticwebassets.build.json");
            new FileInfo(path).Should().Exist();
            var manifest = StaticWebAssetsManifest.FromJsonBytes(File.ReadAllBytes(path));
            AssertManifest(manifest, LoadBuildManifest());

            // GenerateStaticWebAssetsManifest should copy the file to the output folder.
            var finalPath = Path.Combine(outputPath, "blazorwasm.staticwebassets.runtime.json");
            new FileInfo(finalPath).Should().Exist();

            AssertBuildAssets(
                manifest,
                outputPath,
                intermediateOutputPath);


            new FileInfo(Path.Combine(outputPath, "wwwroot", "_framework", "blazorwasm.dll")).Should().Exist();
            new FileInfo(Path.Combine(outputPath, "wwwroot", "_framework", "classlibrarywithsatelliteassemblies.dll")).Should().Exist();
            new FileInfo(Path.Combine(outputPath, "wwwroot", "_framework", "Microsoft.CodeAnalysis.CSharp.dll")).Should().Exist();
            new FileInfo(Path.Combine(outputPath, "wwwroot", "_framework", "fr", "Microsoft.CodeAnalysis.CSharp.resources.dll")).Should().Exist();

            var bootJsonPath = new FileInfo(Path.Combine(outputPath, "wwwroot", "_framework", "blazor.boot.json"));
            bootJsonPath.Should().Contain("\"Microsoft.CodeAnalysis.CSharp.dll\"");
            bootJsonPath.Should().Contain("\"fr\\/Microsoft.CodeAnalysis.CSharp.resources.dll\"");
        }

        [Fact(Skip = "https://github.com/dotnet/aspnetcore/issues/25959")]
        public void Build_WithCustomOutputPath_Works()
        {
            var testAppName = "BlazorWasmWithLibrary";
            var testInstance = CreateAspNetSdkTestAsset(testAppName);

            testInstance.WithProjectChanges((path, project) =>
            {
                if (path.Contains("blazorwasm"))
                {
                    var ns = project.Root.Name.Namespace;
                    var propertyGroup = new XElement(ns + "PropertyGroup");
                    propertyGroup.Add(new XElement("BaseOutputPath", @"$(MSBuildThisFileDirectory)build\bin\"));
                    propertyGroup.Add(new XElement("BaseIntermediateOutputPath", @"$(MSBuildThisFileDirectory)build\obj\"));
                    project.Root.Add(propertyGroup);
                }
            });

            var buildCommand = new BuildCommand(testInstance, "blazorwasm");
            buildCommand.Execute().Should().Pass();
        }

        [Fact]
        public void Build_WithTransitiveReference_Works()
        {
            // Regression test for https://github.com/dotnet/aspnetcore/issues/37574.
            var testInstance = CreateAspNetSdkTestAsset("BlazorWasmWithLibrary");

            var buildCommand = new BuildCommand(testInstance, "classlibrarywithsatelliteassemblies");
            buildCommand.Execute().Should().Pass();
            var referenceAssemblyPath = new FileInfo(Path.Combine(
                buildCommand.GetOutputDirectory(DefaultTfm).ToString(),
                "classlibrarywithsatelliteassemblies.dll"));

            referenceAssemblyPath.Should().Exist();

            testInstance.WithProjectChanges((path, project) =>
            {
                if (path.Contains("razorclasslibrary"))
                {
                    var ns = project.Root.Name.Namespace;
                    // <ItemGroup>
                    //  <Reference Include="classlibrarywithsatelliteassemblies" HintPath="$Path\classlibrarywithsatelliteassemblies.dll" />
                    // </ItemGroup>
                    var itemGroup = new XElement(ns + "ItemGroup",
                        new XElement(ns + "Reference",
                            new XAttribute("Include", "classlibrarywithsatelliteassemblies"),
                            new XAttribute("HintPath", referenceAssemblyPath)));

                    project.Root.Add(itemGroup);
                }
            });

            // Ensure a compile time reference exists between the project and the assembly added as a reference. This is required for 
            // the assembly to be resolved by the "app" as part of RAR
            File.WriteAllText(Path.Combine(testInstance.Path, "razorclasslibrary", "TestReference.cs"),
@"
public class TestReference
{
    public void Method() => System.GC.KeepAlive(typeof(classlibrarywithsatelliteassemblies.Class1));
}");

            buildCommand = new BuildCommand(testInstance, "blazorwasm");
            buildCommand.Execute().Should().Pass();

            // Assert
            var outputDirectory = buildCommand.GetOutputDirectory(DefaultTfm).ToString();
            var fileInWwwroot = new FileInfo(Path.Combine(outputDirectory, "wwwroot", "_framework", "classlibrarywithsatelliteassemblies.dll"));
            fileInWwwroot.Should().Exist();

            // Make sure it's a the correct copy.
            fileInWwwroot.Length.Should().Be(referenceAssemblyPath.Length);
            Assert.Equal(File.ReadAllBytes(referenceAssemblyPath.FullName), File.ReadAllBytes(fileInWwwroot.FullName));
        }

        [Fact]
        public void Build_WithReference_Works()
        {
            // Regression test for https://github.com/dotnet/aspnetcore/issues/37574.
            var testInstance = CreateAspNetSdkTestAsset("BlazorWasmWithLibrary");

            var buildCommand = new BuildCommand(testInstance, "classlibrarywithsatelliteassemblies");
            buildCommand.Execute().Should().Pass();
            var referenceAssemblyPath = new FileInfo(Path.Combine(
                buildCommand.GetOutputDirectory(DefaultTfm).ToString(),
                "classlibrarywithsatelliteassemblies.dll"));

            referenceAssemblyPath.Should().Exist();

            testInstance.WithProjectChanges((path, project) =>
            {
                if (path.Contains("blazorwasm"))
                {
                    var ns = project.Root.Name.Namespace;
                    // <ItemGroup>
                    //  <Reference Include="classlibrarywithsatelliteassemblies" HintPath="$Path\classlibrarywithsatelliteassemblies.dll" />
                    // </ItemGroup>
                    var itemGroup = new XElement(ns + "ItemGroup",
                        new XElement(ns + "Reference",
                            new XAttribute("Include", "classlibrarywithsatelliteassemblies"),
                            new XAttribute("HintPath", referenceAssemblyPath)));

                    project.Root.Add(itemGroup);
                }
            });

            // Ensure a compile time reference exists between the project and the assembly added as a reference. This is required for 
            // the assembly to be resolved by the "app" as part of RAR
            File.WriteAllText(Path.Combine(testInstance.Path, "blazorwasm", "TestReference.cs"),
@"
public class TestReference
{
    public void Method() => System.GC.KeepAlive(typeof(classlibrarywithsatelliteassemblies.Class1));
}");

            buildCommand = new BuildCommand(testInstance, "blazorwasm");
            buildCommand.Execute().Should().Pass();

            // Assert
            var outputDirectory = buildCommand.GetOutputDirectory(DefaultTfm).ToString();
            var fileInWwwroot = new FileInfo(Path.Combine(outputDirectory, "wwwroot", "_framework", "classlibrarywithsatelliteassemblies.dll"));
            fileInWwwroot.Should().Exist();

            // Make sure it's a the correct copy.
            fileInWwwroot.Length.Should().Be(referenceAssemblyPath.Length);
            Assert.Equal(File.ReadAllBytes(referenceAssemblyPath.FullName), File.ReadAllBytes(fileInWwwroot.FullName));
        }

        private static BootJsonData ReadBootJsonData(string path)
        {
            return JsonSerializer.Deserialize<BootJsonData>(
                File.ReadAllText(path),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
    }
}
