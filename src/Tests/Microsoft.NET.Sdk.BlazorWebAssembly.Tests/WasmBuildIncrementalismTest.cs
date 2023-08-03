// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json;
using Microsoft.NET.Sdk.WebAssembly;

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
            var projectDirectory = CreateAspNetSdkTestAsset(testAsset).WithProjectChanges((path, document) =>
            {
                if (Path.GetFileNameWithoutExtension(path) == "blazorwasm")
                {
                    // Since blazor.boot.json gets modified on each build, we explicitly exclude it from compression so
                    // its compressed asset doesn't fail the thumb print check.
                    document.Root.Add(XElement.Parse("""
                        <PropertyGroup>
                          <CompressionExcludePatterns>$(CompressionExcludePatterns);_framework\blazor.boot.json</CompressionExcludePatterns>
                        </PropertyGroup>
                        """));
                }
            });

            var build = new BuildCommand(projectDirectory, "blazorwasm");
            build.WithWorkingDirectory(projectDirectory.TestRoot);
            build.Execute()
                .Should()
                .Pass();

            var gzipCompressionDirectory = Path.Combine(projectDirectory.TestRoot, "blazorwasm", "obj", "Debug", DefaultTfm, "compressed");
            new DirectoryInfo(gzipCompressionDirectory).Should().Exist();

            // Act
            var thumbPrint = FileThumbPrint.CreateFolderThumbprint(projectDirectory, gzipCompressionDirectory);

            // Assert
            for (var i = 0; i < 3; i++)
            {
                build = new BuildCommand(projectDirectory, "blazorwasm");
                build.WithWorkingDirectory(projectDirectory.TestRoot);
                build.Execute($"/bl:msbuild{i}.binlog")
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

            var satelliteAssemblyFile = Path.Combine(build.GetOutputDirectory(DefaultTfm).ToString(), "wwwroot", "_framework", "ja", "blazorwasm.resources.wasm");
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

                kvp.Value.Should().ContainKey("blazorwasm.resources.wasm");
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

            var satelliteAssemblyFile = Path.Combine(build.GetOutputDirectory(DefaultTfm).ToString(), "wwwroot", "_framework", "ja", "blazorwasm.resources.wasm");
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

            kvp.Value.Should().ContainKey("blazorwasm.resources.wasm");
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

            var satelliteAssemblyFile = Path.Combine(build.GetOutputDirectory(DefaultTfm).ToString(), "wwwroot", "_framework", "ja", "blazorwasm.resources.wasm");
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

            kvp.Value.Should().ContainKey("blazorwasm.resources.wasm");


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
