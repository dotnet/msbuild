// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json;
using Microsoft.NET.Sdk.WebAssembly;

namespace Microsoft.NET.Sdk.BlazorWebAssembly.Tests
{
    public class WasmBuildLazyLoadTest : AspNetSdkTest
    {
        public WasmBuildLazyLoadTest(ITestOutputHelper log) : base(log) { }

        [Fact]
        public void Build_LazyLoadExplicitAssembly_Debug_Works()
        {
            // Arrange
            var testAppName = "BlazorWasmWithLibrary";
            var testInstance = CreateAspNetSdkTestAsset(testAppName);

            testInstance.WithProjectChanges((project) =>
            {
                var ns = project.Root.Name.Namespace;
                var itemGroup = new XElement(ns + "ItemGroup");
                itemGroup.Add(new XElement("BlazorWebAssemblyLazyLoad",
                    new XAttribute("Include", "RazorClassLibrary.wasm")));
                project.Root.Add(itemGroup);
            });

            // Act
            var buildCommand = new BuildCommand(testInstance, "blazorwasm");
            buildCommand.WithWorkingDirectory(testInstance.TestRoot);
            buildCommand.Execute()
                .Should().Pass();

            var outputDirectory = buildCommand.GetOutputDirectory(DefaultTfm);

            // Assert
            var expectedFiles = new[]
            {
                "wwwroot/_framework/blazor.boot.json",
                "wwwroot/_framework/RazorClassLibrary.wasm"
            };

            outputDirectory.Should().HaveFiles(expectedFiles);

            var bootJson = ReadBootJsonData(Path.Combine(outputDirectory.ToString(), "wwwroot", "_framework", "blazor.boot.json"));

            // And that it has been labelled as a dynamic assembly in the boot.json
            var lazyAssemblies = bootJson.resources.lazyAssembly;
            var assemblies = bootJson.resources.assembly;

            lazyAssemblies.Should().NotBeNull();

            lazyAssemblies.Keys.Should().Contain("RazorClassLibrary.wasm");
            assemblies.Keys.Should().NotContain("RazorClassLibrary.wasm");

            // App assembly should not be lazy loaded
            lazyAssemblies.Keys.Should().NotContain("blazorwasm.wasm");
            assemblies.Keys.Should().Contain("blazorwasm.wasm");
        }

        [Fact]
        public void Build_LazyLoadExplicitAssembly_Release_Works()
        {
            // Arrange
            var testAppName = "BlazorWasmWithLibrary";
            var testInstance = CreateAspNetSdkTestAsset(testAppName);

            testInstance.WithProjectChanges((project) =>
            {
                var ns = project.Root.Name.Namespace;
                var itemGroup = new XElement(ns + "ItemGroup");
                itemGroup.Add(new XElement("BlazorWebAssemblyLazyLoad",
                    new XAttribute("Include", "RazorClassLibrary.wasm")));
                project.Root.Add(itemGroup);
            });

            // Act
            var buildCommand = new BuildCommand(testInstance, "blazorwasm");
            buildCommand.Execute("/p:Configuration=Release")
                .Should().Pass();

            var outputDirectory = buildCommand.GetOutputDirectory(DefaultTfm, "Release");

            // Assert
            var expectedFiles = new[]
            {
                "wwwroot/_framework/blazor.boot.json",
                "wwwroot/_framework/RazorClassLibrary.wasm"
            };

            outputDirectory.Should().HaveFiles(expectedFiles);

            var bootJson = ReadBootJsonData(Path.Combine(outputDirectory.ToString(), "wwwroot", "_framework", "blazor.boot.json"));

            // And that it has been labelled as a dynamic assembly in the boot.json
            var lazyAssemblies = bootJson.resources.lazyAssembly;
            var assemblies = bootJson.resources.assembly;

            lazyAssemblies.Should().NotBeNull();

            lazyAssemblies.Keys.Should().Contain("RazorClassLibrary.wasm");
            assemblies.Keys.Should().NotContain("RazorClassLibrary.wasm");

            // App assembly should not be lazy loaded
            lazyAssemblies.Keys.Should().NotContain("blazorwasm.wasm");
            assemblies.Keys.Should().Contain("blazorwasm.wasm");
        }

        [Fact]
        public void Publish_LazyLoadExplicitAssembly_Debug_Works()
        {
            // Arrange
            var testAppName = "BlazorWasmWithLibrary";
            var testInstance = CreateAspNetSdkTestAsset(testAppName);

            testInstance.WithProjectChanges((project) =>
            {
                var ns = project.Root.Name.Namespace;
                var itemGroup = new XElement(ns + "ItemGroup");
                itemGroup.Add(new XElement("BlazorWebAssemblyLazyLoad",
                    new XAttribute("Include", "RazorClassLibrary.wasm")));
                project.Root.Add(itemGroup);
            });

            // Act
            var publishCommand = new PublishCommand(testInstance, "blazorwasm");
            publishCommand.WithWorkingDirectory(testInstance.TestRoot);
            publishCommand.Execute()
                .Should().Pass();

            var outputDirectory = publishCommand.GetOutputDirectory(DefaultTfm);

            // Assert
            var expectedFiles = new[]
            {
                "wwwroot/_framework/blazor.boot.json",
                "wwwroot/_framework/RazorClassLibrary.wasm"
            };

            outputDirectory.Should().HaveFiles(expectedFiles);

            var bootJson = ReadBootJsonData(Path.Combine(outputDirectory.ToString(), "wwwroot", "_framework", "blazor.boot.json"));

            // And that it has been labelled as a dynamic assembly in the boot.json
            var lazyAssemblies = bootJson.resources.lazyAssembly;
            var assemblies = bootJson.resources.assembly;

            lazyAssemblies.Should().NotBeNull();

            lazyAssemblies.Keys.Should().Contain("RazorClassLibrary.wasm");
            assemblies.Keys.Should().NotContain("RazorClassLibrary.wasm");

            // App assembly should not be lazy loaded
            lazyAssemblies.Keys.Should().NotContain("blazorwasm.wasm");
            assemblies.Keys.Should().Contain("blazorwasm.wasm");
        }

        [Fact]
        public void Publish_LazyLoadExplicitAssembly_Release_Works()
        {
            // Arrange
            var testAppName = "BlazorWasmWithLibrary";
            var testInstance = CreateAspNetSdkTestAsset(testAppName);

            testInstance.WithProjectChanges((project) =>
            {
                var ns = project.Root.Name.Namespace;
                var itemGroup = new XElement(ns + "ItemGroup");
                itemGroup.Add(new XElement("BlazorWebAssemblyLazyLoad",
                    new XAttribute("Include", "RazorClassLibrary.wasm")));
                project.Root.Add(itemGroup);
            });

            // Act
            var publishCommand = new PublishCommand(testInstance, "blazorwasm");
            publishCommand.Execute("/p:Configuration=Release")
                .Should().Pass();

            var outputDirectory = publishCommand.GetOutputDirectory(DefaultTfm, "Release");

            // Assert
            var expectedFiles = new[]
            {
                "wwwroot/_framework/blazor.boot.json",
                "wwwroot/_framework/RazorClassLibrary.wasm"
            };

            outputDirectory.Should().HaveFiles(expectedFiles);

            var bootJson = ReadBootJsonData(Path.Combine(outputDirectory.ToString(), "wwwroot", "_framework", "blazor.boot.json"));

            // And that it has been labelled as a dynamic assembly in the boot.json
            var lazyAssemblies = bootJson.resources.lazyAssembly;
            var assemblies = bootJson.resources.assembly;

            lazyAssemblies.Should().NotBeNull();

            lazyAssemblies.Keys.Should().Contain("RazorClassLibrary.wasm");
            assemblies.Keys.Should().NotContain("RazorClassLibrary.wasm");

            // App assembly should not be lazy loaded
            lazyAssemblies.Keys.Should().NotContain("blazorwasm.wasm");
            assemblies.Keys.Should().Contain("blazorwasm.wasm");
        }

        [Fact]
        public void Build_LazyLoadExplicitAssembly_InvalidAssembly()
        {
            // Arrange
            var testAppName = "BlazorWasmWithLibrary";
            var testInstance = CreateAspNetSdkTestAsset(testAppName);

            testInstance.WithProjectChanges((project) =>
            {
                var ns = project.Root.Name.Namespace;
                var itemGroup = new XElement(ns + "ItemGroup");
                itemGroup.Add(new XElement("BlazorWebAssemblyLazyLoad",
                    new XAttribute("Include", "RazorClassLibraryInvalid.wasm")));
                project.Root.Add(itemGroup);
            });

            // Assert
            var buildCommand = new BuildCommand(testInstance, "blazorwasm");
            buildCommand.Execute().Should().Fail().And.HaveStdOutContaining("BLAZORSDK1001");
        }

        [Fact]
        public void Publish_LazyLoadExplicitAssembly_InvalidAssembly()
        {
            // Arrange
            var testAppName = "BlazorWasmWithLibrary";
            var testInstance = CreateAspNetSdkTestAsset(testAppName);

            testInstance.WithProjectChanges((project) =>
            {
                var ns = project.Root.Name.Namespace;
                var itemGroup = new XElement(ns + "ItemGroup");
                itemGroup.Add(new XElement("BlazorWebAssemblyLazyLoad",
                    new XAttribute("Include", "RazorClassLibraryInvalid.wasm")));
                project.Root.Add(itemGroup);
            });

            // Assert
            var publishCommand = new PublishCommand(Log, Path.Combine(testInstance.TestRoot, "blazorwasm"));
            publishCommand.Execute("/p:Configuration=Release").Should().Fail().And.HaveStdOutContaining("BLAZORSDK1001");
        }

        private static BootJsonData ReadBootJsonData(string path)
        {
            return JsonSerializer.Deserialize<BootJsonData>(
                File.ReadAllText(path),
                new JsonSerializerOptions(JsonSerializerDefaults.Web));
        }
    }
}
