// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using System.Xml.Linq;
using Microsoft.NET.Sdk.BlazorWebAssembly;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Microsoft.NET.TestFramework.Utilities;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.NET.Sdk.BlazorWebAssembly.Tests
{
    public class WasmBuildLazyLoadTest : AspNetSdkTest
    {
        public WasmBuildLazyLoadTest(ITestOutputHelper log) : base(log) {}

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
                    new XAttribute("Include", "RazorClassLibrary.dll")));
                project.Root.Add(itemGroup);
            });

            // Act
            var buildCommand = new BuildCommand(testInstance, "blazorwasm");
            buildCommand.WithWorkingDirectory(testInstance.TestRoot);
            buildCommand.Execute("/bl")
                .Should().Pass();

            var outputDirectory = buildCommand.GetOutputDirectory(DefaultTfm);

            // Assert
            var expectedFiles = new[]
            {
                "wwwroot/_framework/blazor.boot.json",
                "wwwroot/_framework/RazorClassLibrary.dll"
            };

            outputDirectory.Should().HaveFiles(expectedFiles);

            var bootJson = ReadBootJsonData(Path.Combine(outputDirectory.ToString(), "wwwroot", "_framework", "blazor.boot.json"));

            // And that it has been labelled as a dynamic assembly in the boot.json
            var lazyAssemblies = bootJson.resources.lazyAssembly;
            var assemblies = bootJson.resources.assembly;

            lazyAssemblies.Should().NotBeNull();

            lazyAssemblies.Keys.Should().Contain("RazorClassLibrary.dll");
            assemblies.Keys.Should().NotContain("RazorClassLibrary.dll");

            // App assembly should not be lazy loaded
            lazyAssemblies.Keys.Should().NotContain("blazorwasm.dll");
            assemblies.Keys.Should().Contain("blazorwasm.dll");
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
                    new XAttribute("Include", "RazorClassLibrary.dll")));
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
                "wwwroot/_framework/RazorClassLibrary.dll"
            };

            outputDirectory.Should().HaveFiles(expectedFiles);

            var bootJson = ReadBootJsonData(Path.Combine(outputDirectory.ToString(), "wwwroot", "_framework", "blazor.boot.json"));

            // And that it has been labelled as a dynamic assembly in the boot.json
            var lazyAssemblies = bootJson.resources.lazyAssembly;
            var assemblies = bootJson.resources.assembly;

            lazyAssemblies.Should().NotBeNull();

            lazyAssemblies.Keys.Should().Contain("RazorClassLibrary.dll");
            assemblies.Keys.Should().NotContain("RazorClassLibrary.dll");

            // App assembly should not be lazy loaded
            lazyAssemblies.Keys.Should().NotContain("blazorwasm.dll");
            assemblies.Keys.Should().Contain("blazorwasm.dll");
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
                    new XAttribute("Include", "RazorClassLibrary.dll")));
                project.Root.Add(itemGroup);
            });

            // Act
            var publishCommand = new PublishCommand(Log, Path.Combine(testInstance.TestRoot, "blazorwasm"));
            publishCommand.WithWorkingDirectory(testInstance.TestRoot);
            publishCommand.Execute("/bl")
                .Should().Pass();

            var outputDirectory = publishCommand.GetOutputDirectory(DefaultTfm);

            // Assert
            var expectedFiles = new[]
            {
                "wwwroot/_framework/blazor.boot.json",
                "wwwroot/_framework/RazorClassLibrary.dll"
            };

            outputDirectory.Should().HaveFiles(expectedFiles);

            var bootJson = ReadBootJsonData(Path.Combine(outputDirectory.ToString(), "wwwroot", "_framework", "blazor.boot.json"));

            // And that it has been labelled as a dynamic assembly in the boot.json
            var lazyAssemblies = bootJson.resources.lazyAssembly;
            var assemblies = bootJson.resources.assembly;

            lazyAssemblies.Should().NotBeNull();

            lazyAssemblies.Keys.Should().Contain("RazorClassLibrary.dll");
            assemblies.Keys.Should().NotContain("RazorClassLibrary.dll");

            // App assembly should not be lazy loaded
            lazyAssemblies.Keys.Should().NotContain("blazorwasm.dll");
            assemblies.Keys.Should().Contain("blazorwasm.dll");
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
                    new XAttribute("Include", "RazorClassLibrary.dll")));
                project.Root.Add(itemGroup);
            });

            // Act
            var publishCommand = new PublishCommand(Log, Path.Combine(testInstance.TestRoot, "blazorwasm"));
            publishCommand.Execute("/p:Configuration=Release")
                .Should().Pass();

            var outputDirectory = publishCommand.GetOutputDirectory(DefaultTfm, "Release");

            // Assert
            var expectedFiles = new[]
            {
                "wwwroot/_framework/blazor.boot.json",
                "wwwroot/_framework/RazorClassLibrary.dll"
            };

            outputDirectory.Should().HaveFiles(expectedFiles);

            var bootJson = ReadBootJsonData(Path.Combine(outputDirectory.ToString(), "wwwroot", "_framework", "blazor.boot.json"));

            // And that it has been labelled as a dynamic assembly in the boot.json
            var lazyAssemblies = bootJson.resources.lazyAssembly;
            var assemblies = bootJson.resources.assembly;

            lazyAssemblies.Should().NotBeNull();

            lazyAssemblies.Keys.Should().Contain("RazorClassLibrary.dll");
            assemblies.Keys.Should().NotContain("RazorClassLibrary.dll");

            // App assembly should not be lazy loaded
            lazyAssemblies.Keys.Should().NotContain("blazorwasm.dll");
            assemblies.Keys.Should().Contain("blazorwasm.dll");
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
                    new XAttribute("Include", "RazorClassLibraryInvalid.dll")));
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
                    new XAttribute("Include", "RazorClassLibraryInvalid.dll")));
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
