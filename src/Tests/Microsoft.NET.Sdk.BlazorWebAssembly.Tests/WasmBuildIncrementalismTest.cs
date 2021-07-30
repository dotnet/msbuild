// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using System.Linq;
using Microsoft.NET.Sdk.BlazorWebAssembly;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Microsoft.NET.TestFramework.Utilities;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.NET.Sdk.BlazorWebAssembly.Tests
{
    public class WasmBuildIncrementalismTest : AspNetSdkTest
    {
        public WasmBuildIncrementalismTest(ITestOutputHelper log) : base(log) { }

        [Fact]
        public void Build_IsIncremental()
        {
            // Arrange
            var testAsset = "BlazorWasmWithLibrary";
            var projectDirectory = CreateAspNetSdkTestAsset(testAsset);

            var build = new BuildCommand(projectDirectory, "blazorwasm");
            build.Execute()
                .Should()
                .Pass();

            var buildOutputDirectory = build.GetOutputDirectory(DefaultTfm).ToString();

            // Act
            var thumbPrint = FileThumbPrint.CreateFolderThumbprint(projectDirectory, buildOutputDirectory);

            // Assert
            for (var i = 0; i < 3; i++)
            {
                build = new BuildCommand(projectDirectory, "blazorwasm");
                build.Execute().Should().Pass();

                var newThumbPrint = FileThumbPrint.CreateFolderThumbprint(projectDirectory, buildOutputDirectory);
                newThumbPrint.Count.Should().Be(thumbPrint.Count);
                for (var j = 0; j < thumbPrint.Count; j++)
                {
                    thumbPrint[j].Equals(newThumbPrint[j]).Should().BeTrue();
                }
            }
        }

        [Fact]
        public void Build_GzipCompression_IsIncremental()
        {
            // Arrange
            var testAsset = "BlazorWasmWithLibrary";
            var projectDirectory = CreateAspNetSdkTestAsset(testAsset);

            var build = new BuildCommand(projectDirectory, "blazorwasm");
            build.Execute()
                .Should()
                .Pass();

            var gzipCompressionDirectory = Path.Combine(projectDirectory.TestRoot, "blazorwasm", "obj", "Debug", DefaultTfm, "build-gz");
            new DirectoryInfo(gzipCompressionDirectory).Should().Exist();

            // Act
            var thumbPrint = FileThumbPrint.CreateFolderThumbprint(projectDirectory, gzipCompressionDirectory);

            // Assert
            for (var i = 0; i < 3; i++)
            {
                build = new BuildCommand(projectDirectory, "blazorwasm");
                build.Execute()
                    .Should()
                    .Pass();

                var newThumbPrint = FileThumbPrint.CreateFolderThumbprint(projectDirectory, gzipCompressionDirectory);
                Assert.Equal(thumbPrint.Count, newThumbPrint.Count);
                for (var j = 0; j < thumbPrint.Count; j++)
                {
                    thumbPrint[j].Equals(newThumbPrint[j]).Should().BeTrue();
                }
            }
        }

        [Fact]
        public void Build_SatelliteAssembliesFileIsPreserved()
        {
            // Arrange
            var testAsset = "BlazorWasmWithLibrary";
            var projectDirectory = CreateAspNetSdkTestAsset(testAsset);
            File.Move(Path.Combine(projectDirectory.TestRoot, "blazorwasm", "Resources.ja.resx.txt"), Path.Combine(projectDirectory.TestRoot, "blazorwasm", "Resource.ja.resx"));

            var build = new BuildCommand(projectDirectory, "blazorwasm");
            build.Execute()
                .Should()
                .Pass();

            var satelliteAssemblyFile = Path.Combine(build.GetOutputDirectory(DefaultTfm).ToString(), "wwwroot", "_framework", "ja", "blazorwasm.resources.dll");
            var bootJson = Path.Combine(build.GetOutputDirectory(DefaultTfm).ToString(), "wwwroot", "_framework", "blazor.boot.json");

            // Assert
            for (var i = 0; i < 3; i++)
            {
                build = new BuildCommand(projectDirectory, "blazorwasm");
                build.Execute()
                    .Should()
                    .Pass();

                Verify();
            }

            // Assert - incremental builds with BuildingProject=false
            for (var i = 0; i < 3; i++)
            {
                build = new BuildCommand(projectDirectory, "blazorwasm");
                build.Execute("/p:BuildingProject=false")
                    .Should()
                    .Pass();

                Verify();
            }

            void Verify()
            {
                new FileInfo(satelliteAssemblyFile).Should().Exist();

                var bootJsonFile = JsonSerializer.Deserialize<BootJsonData>(File.ReadAllText(bootJson), new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                var satelliteResources = bootJsonFile.resources.satelliteResources;


                satelliteResources.Should().HaveCount(1);

                var kvp = satelliteResources.SingleOrDefault(p => p.Key == "ja");
                kvp.Should().NotBeNull();

                kvp.Value.Should().ContainKey("ja/blazorwasm.resources.dll");
            }
        }

        [Fact]
        public void Build_SatelliteAssembliesFileIsCreated_IfNewFileIsAdded()
        {
            // Arrange
            var testAsset = "BlazorWasmWithLibrary";
            var projectDirectory = CreateAspNetSdkTestAsset(testAsset);

            var build = new BuildCommand(projectDirectory, "blazorwasm");
            build.WithWorkingDirectory(projectDirectory.TestRoot);
            build.Execute("/bl:build1-msbuild.binlog")
                .Should()
                .Pass();

            var satelliteAssemblyFile = Path.Combine(build.GetOutputDirectory(DefaultTfm).ToString(), "wwwroot", "_framework", "ja", "blazorwasm.resources.dll");
            var bootJson = Path.Combine(build.GetOutputDirectory(DefaultTfm).ToString(), "wwwroot", "_framework", "blazor.boot.json");

            build = new BuildCommand(projectDirectory, "blazorwasm");
            build.WithWorkingDirectory(projectDirectory.TestRoot);
            build.Execute("/bl:build2-msbuild.binlog")
                .Should()
                .Pass();

            new FileInfo(satelliteAssemblyFile).Should().NotExist();

            var bootJsonFile = JsonSerializer.Deserialize<BootJsonData>(File.ReadAllText(bootJson), new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            var satelliteResources = bootJsonFile.resources.satelliteResources;
            satelliteResources.Should().BeNull();

            File.Move(Path.Combine(projectDirectory.TestRoot, "blazorwasm", "Resources.ja.resx.txt"), Path.Combine(projectDirectory.TestRoot, "blazorwasm", "Resource.ja.resx"));
            build = new BuildCommand(projectDirectory, "blazorwasm");
            build.WithWorkingDirectory(projectDirectory.TestRoot);
            build.Execute("/bl:build3-msbuild.binlog")
                .Should()
                .Pass();

            new FileInfo(satelliteAssemblyFile).Should().Exist();
            bootJsonFile = JsonSerializer.Deserialize<BootJsonData>(File.ReadAllText(bootJson), new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            satelliteResources = bootJsonFile.resources.satelliteResources;
            satelliteResources.Should().HaveCount(1);

            var kvp = satelliteResources.SingleOrDefault(p => p.Key == "ja");
            kvp.Should().NotBeNull();

            kvp.Value.Should().ContainKey("ja/blazorwasm.resources.dll");
        }

        [Fact]
        public void Build_SatelliteAssembliesFileIsDeleted_IfAllSatelliteFilesAreRemoved()
        {
            // Arrange
            var testAsset = "BlazorWasmWithLibrary";
            var projectDirectory = CreateAspNetSdkTestAsset(testAsset);
            File.Move(Path.Combine(projectDirectory.TestRoot, "blazorwasm", "Resources.ja.resx.txt"), Path.Combine(projectDirectory.TestRoot, "blazorwasm", "Resource.ja.resx"));

            var build = new BuildCommand(projectDirectory, "blazorwasm");
            build.Execute()
                .Should()
                .Pass();

            var satelliteAssemblyFile = Path.Combine(build.GetOutputDirectory(DefaultTfm).ToString(), "wwwroot", "_framework", "ja", "blazorwasm.resources.dll");
            var bootJson = Path.Combine(build.GetOutputDirectory(DefaultTfm).ToString(), "wwwroot", "_framework", "blazor.boot.json");

            build = new BuildCommand(projectDirectory, "blazorwasm");
            build.Execute()
                .Should()
                .Pass();

            new FileInfo(satelliteAssemblyFile).Should().Exist();

            var bootJsonFile = JsonSerializer.Deserialize<BootJsonData>(File.ReadAllText(bootJson), new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            var satelliteResources = bootJsonFile.resources.satelliteResources;
            satelliteResources.Should().HaveCount(1);

            var kvp = satelliteResources.SingleOrDefault(p => p.Key == "ja");
            kvp.Should().NotBeNull();

            kvp.Value.Should().ContainKey("ja/blazorwasm.resources.dll");


            File.Delete(Path.Combine(projectDirectory.TestRoot, "blazorwasm", "Resource.ja.resx"));
            build = new BuildCommand(projectDirectory, "blazorwasm");
            build.Execute()
                .Should()
                .Pass();

            bootJsonFile = JsonSerializer.Deserialize<BootJsonData>(File.ReadAllText(bootJson), new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            satelliteResources = bootJsonFile.resources.satelliteResources;
            satelliteResources.Should().BeNull();
        }
    }
}
