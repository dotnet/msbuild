// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;
using System.IO.Compression;
using System.Text.Json;
using System.Linq;
using System.Threading.Tasks;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Xml.Linq;
using Microsoft.NET.Sdk.BlazorWebAssembly;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Microsoft.NET.TestFramework.Utilities;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;
using static Microsoft.NET.Sdk.BlazorWebAssembly.Tests.ServiceWorkerAssert;
using ResourceHashesByNameDictionary = System.Collections.Generic.Dictionary<string, string>;

namespace Microsoft.NET.Sdk.BlazorWebAssembly.Tests
{
    public class WasmPublishIntegrationTest : AspNetSdkTest
    {
        public WasmPublishIntegrationTest(ITestOutputHelper log) : base(log) { }

        [Fact]
        public void Publish_MinimalApp_Works()
        {
            // Arrange
            var testAppName = "BlazorWasmMinimal";
            var testInstance = CreateAspNetSdkTestAsset(testAppName);

            var publishCommand = new PublishCommand(Log, testInstance.TestRoot);
            publishCommand.Execute().Should().Pass()
                .And.NotHaveStdOutContaining("warning IL");

            var publishDirectory = publishCommand.GetOutputDirectory(DefaultTfm);

            var expectedFiles = new[]
            {
                "wwwroot/_framework/blazor.boot.json",
                "wwwroot/_framework/blazor.webassembly.js",
                "wwwroot/_framework/dotnet.wasm",
                "wwwroot/_framework/blazorwasm-minimal.dll",
                "wwwroot/index.html",
                "web.config"
            };

            publishDirectory.Should().HaveFiles(expectedFiles);

            // Verify web.config
            var content = File.ReadAllText(Path.Combine(publishDirectory.ToString(), "web.config"));
            content.Should().Contain("<remove fileExtension=\".blat\" />");

            VerifyBootManifestHashes(testInstance, Path.Combine(publishDirectory.ToString(), "wwwroot"));
        }

        [Fact]
        public void Publish_WithDefaultSettings_Works()
        {
            // Arrange
            var testAppName = "BlazorWasmWithLibrary";
            var testInstance = CreateAspNetSdkTestAsset(testAppName);

            var publishCommand = new PublishCommand(Log, Path.Combine(testInstance.TestRoot, "blazorwasm"));
            publishCommand.Execute().Should().Pass();

            var publishDirectory = publishCommand.GetOutputDirectory(DefaultTfm);

            var expectedFiles = new[]
            {
                "wwwroot/_framework/blazor.boot.json",
                "wwwroot/_framework/blazor.webassembly.js",
                "wwwroot/_framework/dotnet.wasm",
                "wwwroot/_framework/blazorwasm.dll",
                "wwwroot/_framework/System.Text.Json.dll",
                "wwwroot/_content/RazorClassLibrary/wwwroot/exampleJsInterop.js",
                "wwwroot/_content/RazorClassLibrary/styles.css",
                "wwwroot/index.html",
                "wwwroot/js/LinkedScript.js",
                "wwwroot/css/app.css",
                "web.config"
            };

            publishDirectory.Should().HaveFiles(expectedFiles);

            var blazorPublishDirectory = Path.Combine(publishDirectory.ToString(), "wwwroot");
            var cssFile = new FileInfo(Path.Combine(blazorPublishDirectory, "css", "app.css"));
            cssFile.Should().Exist();
            cssFile.Should().Contain(".publish");

            new FileInfo(Path.Combine(publishDirectory.ToString(), "dist", "Fake-License.txt"));

            VerifyBootManifestHashes(testInstance, blazorPublishDirectory);
            VerifyServiceWorkerFiles(testInstance, blazorPublishDirectory,
                serviceWorkerPath: Path.Combine("serviceworkers", "my-service-worker.js"),
                serviceWorkerContent: "// This is the production service worker",
                assetsManifestPath: "custom-service-worker-assets.js");

            VerifyTypeGranularTrimming(blazorPublishDirectory);
        }

        [Fact]
        public void Publish_WithScopedCss_Works()
        {
            // Arrange
            var testAppName = "BlazorWasmWithLibrary";
            var testInstance = CreateAspNetSdkTestAsset(testAppName);
            File.WriteAllText(Path.Combine(testInstance.TestRoot, "blazorwasm", "App.razor.css"), "h1 { font-size: 16px; }");

            var publishCommand = new PublishCommand(Log, Path.Combine(testInstance.TestRoot, "blazorwasm"));
            publishCommand.Execute().Should().Pass();

            var publishDirectory = publishCommand.GetOutputDirectory(DefaultTfm);

            var blazorPublishDirectory = Path.Combine(publishDirectory.ToString(), "wwwroot");

            var expectedFiles = new[]
            {
                "wwwroot/_framework/blazor.boot.json",
                "wwwroot/_framework/blazor.webassembly.js",
                "wwwroot/_framework/dotnet.wasm",
                "wwwroot/_framework/blazorwasm.dll",
                "wwwroot/_framework/System.Text.Json.dll",
                "wwwroot/_content/RazorClassLibrary/wwwroot/exampleJsInterop.js",
                "wwwroot/_content/RazorClassLibrary/styles.css",
                "wwwroot/index.html",
                "wwwroot/js/LinkedScript.js",
                "wwwroot/blazorwasm.styles.css",
                "wwwroot/css/app.css",
                "web.config"
            };

            publishDirectory.Should().HaveFiles(expectedFiles);

            new FileInfo(Path.Combine(blazorPublishDirectory, "css", "app.css")).Should().Contain(".publish");

            VerifyBootManifestHashes(testInstance, blazorPublishDirectory);
            VerifyServiceWorkerFiles(testInstance, blazorPublishDirectory,
                serviceWorkerPath: Path.Combine("serviceworkers", "my-service-worker.js"),
                serviceWorkerContent: "// This is the production service worker",
                assetsManifestPath: "custom-service-worker-assets.js");
        }

        [Fact]
        public void Publish_InRelease_Works()
        {
            // Arrange
            var testAppName = "BlazorWasmWithLibrary";
            var testInstance = CreateAspNetSdkTestAsset(testAppName);
            File.WriteAllText(Path.Combine(testInstance.TestRoot, "blazorwasm", "App.razor.css"), "h1 { font-size: 16px; }");

            var publishCommand = new PublishCommand(Log, Path.Combine(testInstance.TestRoot, "blazorwasm"));
            publishCommand.Execute("/p:Configuration=Release").Should().Pass();

            var publishDirectory = publishCommand.GetOutputDirectory(DefaultTfm, "Release");

            var blazorPublishDirectory = Path.Combine(publishDirectory.ToString(), "wwwroot");

            var expectedFiles = new[]
            {
                "wwwroot/_framework/blazor.boot.json",
                "wwwroot/_framework/blazor.webassembly.js",
                "wwwroot/_framework/dotnet.wasm",
                "wwwroot/_framework/blazorwasm.dll",
                "wwwroot/_framework/System.Text.Json.dll",
                "wwwroot/_content/RazorClassLibrary/wwwroot/exampleJsInterop.js",
                "wwwroot/_content/RazorClassLibrary/styles.css",
                "wwwroot/index.html",
                "wwwroot/js/LinkedScript.js",
                "wwwroot/blazorwasm.styles.css",
                "wwwroot/css/app.css",
                "web.config"
            };

            publishDirectory.Should().HaveFiles(expectedFiles);
            
            new FileInfo(Path.Combine(blazorPublishDirectory, "css", "app.css")).Should().Contain(".publish");
        }

        [Fact]
        public void Publish_WithExistingWebConfig_Works()
        {
            // Arrange
            var testAppName = "BlazorWasmWithLibrary";
            var testInstance = CreateAspNetSdkTestAsset(testAppName);

            var webConfigContents = "test webconfig contents";
            File.WriteAllText(Path.Combine(testInstance.TestRoot, "blazorwasm", "web.config"), webConfigContents);

            var publishCommand = new PublishCommand(Log, Path.Combine(testInstance.TestRoot, "blazorwasm"));
            publishCommand.Execute("/p:Configuration=Release").Should().Pass();

            var publishDirectory = publishCommand.GetOutputDirectory(DefaultTfm, "Release");

            // Verify web.config
            new FileInfo(Path.Combine(publishDirectory.ToString(), "..", "web.config")).Should().Exist();
            new FileInfo(Path.Combine(publishDirectory.ToString(), "..", "web.config")).Should().Contain(webConfigContents);
        }

        [Fact]
        public void Publish_WithNoBuild_Works()
        {
            // Arrange
            var testAppName = "BlazorWasmWithLibrary";
            var testInstance = CreateAspNetSdkTestAsset(testAppName);

            var buildCommand = new BuildCommand(testInstance, "blazorwasm");
            buildCommand.Execute()
                .Should().Pass();

            var publishCommand = new PublishCommand(Log, Path.Combine(testInstance.TestRoot, "blazorwasm"));
            publishCommand.WithWorkingDirectory(testInstance.TestRoot);
            publishCommand.Execute("/p:NoBuild=true", "/bl").Should().Pass();

            var publishDirectory = publishCommand.GetOutputDirectory(DefaultTfm);
            var blazorPublishDirectory = Path.Combine(publishDirectory.ToString(), "wwwroot");

            var expectedFiles = new[]
            {
                "wwwroot/_framework/blazor.boot.json",
                "wwwroot/_framework/blazor.webassembly.js",
                "wwwroot/_framework/dotnet.wasm",
                "wwwroot/_framework/blazorwasm.dll",
                "wwwroot/_framework/System.Text.Json.dll",
                "wwwroot/_content/RazorClassLibrary/wwwroot/exampleJsInterop.js",
                "wwwroot/_content/RazorClassLibrary/styles.css",
                "wwwroot/index.html",
                "wwwroot/js/LinkedScript.js",
                "wwwroot/css/app.css",
                "web.config"
            };

            publishDirectory.Should().HaveFiles(expectedFiles);

            VerifyBootManifestHashes(testInstance, blazorPublishDirectory);
            VerifyServiceWorkerFiles(testInstance, blazorPublishDirectory,
                serviceWorkerPath: Path.Combine("serviceworkers", "my-service-worker.js"),
                serviceWorkerContent: "// This is the production service worker",
                assetsManifestPath: "custom-service-worker-assets.js");

            VerifyCompression(testInstance, blazorPublishDirectory);
        }

        [Theory]
        [InlineData("different-path")]
        [InlineData("/different-path")]
        public void Publish_WithStaticWebBasePathWorks(string basePath)
        {
            // Arrange
            var testAppName = "BlazorWasmWithLibrary";
            var testInstance = CreateAspNetSdkTestAsset(testAppName, identifier: basePath);

            testInstance.WithProjectChanges((path, project) =>
            {
                if (path.Contains("blazorwasm"))
                {
                    var ns = project.Root.Name.Namespace;
                    var itemGroup = new XElement(ns + "PropertyGroup");
                    itemGroup.Add(new XElement("StaticWebAssetBasePath", basePath));
                    project.Root.Add(itemGroup);
                }

            });

            var publishCommand = new PublishCommand(Log, Path.Combine(testInstance.TestRoot, "blazorwasm"));
            publishCommand.Execute().Should().Pass();

            var publishDirectory = publishCommand.GetOutputDirectory(DefaultTfm);

            var expectedFiles = new[]
            {
                "wwwroot/different-path/_framework/blazor.boot.json",
                "wwwroot/different-path/_framework/blazor.webassembly.js",
                "wwwroot/different-path/_framework/dotnet.wasm",
                "wwwroot/different-path/_framework/blazorwasm.dll",
                "wwwroot/different-path/_framework/System.Text.Json.dll",
                "wwwroot/different-path/_content/RazorClassLibrary/wwwroot/exampleJsInterop.js",
                "wwwroot/different-path/_content/RazorClassLibrary/styles.css",
                "wwwroot/different-path/index.html",
                "wwwroot/different-path/js/LinkedScript.js",
                "wwwroot/different-path/css/app.css",
                "web.config"
            };

            publishDirectory.Should().HaveFiles(expectedFiles);

            // Verify nothing is published directly to the wwwroot directory
            new DirectoryInfo(Path.Combine(publishDirectory.ToString(), "wwwroot")).Should().HaveDirectory("different-path");

            var blazorPublishDirectory = Path.Combine(publishDirectory.ToString(), "wwwroot", "different-path");

            // Verify web.config
            var content = File.ReadAllText(Path.Combine(publishDirectory.ToString(), "web.config"));
            content.Should().Contain("<remove fileExtension=\".blat\" />");


            VerifyBootManifestHashes(testInstance, blazorPublishDirectory);
            VerifyServiceWorkerFiles(testInstance,
                Path.Combine(publishDirectory.ToString(), "wwwroot"),
                serviceWorkerPath: Path.Combine("serviceworkers", "my-service-worker.js"),
                serviceWorkerContent: "// This is the production service worker",
                assetsManifestPath: "custom-service-worker-assets.js",
                staticWebAssetsBasePath: "different-path");
        }

        [Theory]
        [InlineData("different-path/")]
        [InlineData("/different-path/")]
        public void Publish_Hosted_WithStaticWebBasePathWorks(string basePath)
        {
            var testAppName = "BlazorHosted";
            var testInstance = CreateAspNetSdkTestAsset(testAppName, identifier: basePath);

            testInstance.WithProjectChanges((path, project) =>
            {
                if (path.Contains("blazorwasm"))
                {
                    var ns = project.Root.Name.Namespace;
                    var itemGroup = new XElement(ns + "PropertyGroup");
                    itemGroup.Add(new XElement("StaticWebAssetBasePath", basePath));
                    project.Root.Add(itemGroup);
                }

            });

            var publishCommand = new PublishCommand(Log, Path.Combine(testInstance.TestRoot, "blazorhosted"));
            publishCommand.Execute().Should().Pass();

            var publishDirectory = publishCommand.GetOutputDirectory(DefaultTfm);

            var expectedFiles = new[]
            {
                "wwwroot/different-path/_framework/blazor.boot.json",
                "wwwroot/different-path/_framework/blazor.webassembly.js",
                "wwwroot/different-path/_framework/dotnet.wasm",
                "wwwroot/different-path/_framework/dotnet.wasm.br",
                "wwwroot/different-path/_framework/dotnet.wasm.gz",
                "wwwroot/different-path/_framework/blazorwasm.dll",
                "wwwroot/different-path/_framework/blazorwasm.dll.gz",
                "wwwroot/different-path/_framework/System.Text.Json.dll",
                "wwwroot/different-path/_framework/System.Text.Json.dll.gz",
                "wwwroot/different-path/_framework/System.Text.Json.dll.br",
                "wwwroot/different-path/_framework/RazorClassLibrary.dll.gz",
                "wwwroot/different-path/_framework/RazorClassLibrary.dll.br",
                "wwwroot/_content/RazorClassLibrary/wwwroot/exampleJsInterop.js",
                "wwwroot/_content/RazorClassLibrary/styles.css",
                "wwwroot/different-path/index.html",
                "web.config"
            };

            publishDirectory.Should().HaveFiles(expectedFiles);

            // Verify nothing is published directly to the wwwroot directory
            new DirectoryInfo(Path.Combine(publishDirectory.ToString(), "wwwroot")).Should().HaveDirectory("different-path");

            var blazorPublishDirectory = Path.Combine(publishDirectory.ToString(), "wwwroot", "different-path");

            VerifyBootManifestHashes(testInstance, blazorPublishDirectory);
        }

        private static void VerifyCompression(TestAsset testAsset, string blazorPublishDirectory)
        {
            var original = Path.Combine(blazorPublishDirectory, "_framework", "blazor.boot.json");
            var compressed = Path.Combine(blazorPublishDirectory, "_framework", "blazor.boot.json.br");
            using var brotliStream = new BrotliStream(File.OpenRead(compressed), CompressionMode.Decompress);
            using var textReader = new StreamReader(brotliStream);
            var uncompressedText = textReader.ReadToEnd();
            var originalText = File.ReadAllText(original);

            uncompressedText.Should().Be(originalText);
        }

        [Fact]
        public void Publish_WithTrimmingdDisabled_Works()
        {
            // Arrange
            var testAppName = "BlazorWasmWithLibrary";
            var testInstance = CreateAspNetSdkTestAsset(testAppName);

            testInstance.WithProjectChanges((path, project) =>
            {
                if (path.Contains("blazorwasm"))
                {
                    var ns = project.Root.Name.Namespace;
                    var itemGroup = new XElement(ns + "PropertyGroup");
                    itemGroup.Add(new XElement("PublishTrimmed", false));
                    project.Root.Add(itemGroup);
                }

            });

            var publishCommand = new PublishCommand(Log, Path.Combine(testInstance.TestRoot, "blazorwasm"));
            publishCommand.Execute().Should().Pass();

            var publishDirectory = publishCommand.GetOutputDirectory(DefaultTfm);
            var blazorPublishDirectory = Path.Combine(publishDirectory.ToString(), "wwwroot");

            publishDirectory.Should().HaveFiles(new[]
            {
                "wwwroot/_framework/blazor.boot.json",
                "wwwroot/_framework/blazor.webassembly.js",
                "wwwroot/_framework/dotnet.wasm",
                "wwwroot/_framework/blazorwasm.dll",
                "wwwroot/_framework/System.Text.Json.dll"
            });

            // Verify compression works
            publishDirectory.Should().HaveFiles(new[]
            {
                "wwwroot/_framework/dotnet.wasm.br",
                "wwwroot/_framework/System.Text.Json.dll.br"
            });

            // Verify static assets are in the publish directory
            publishDirectory.Should().HaveFiles(new[]
            {
                "wwwroot/index.html"
            });

            // Verify referenced static web assets
            publishDirectory.Should().HaveFiles(new[]
            {
                "wwwroot/_content/RazorClassLibrary/wwwroot/exampleJsInterop.js",
                "wwwroot/_content/RazorClassLibrary/styles.css",
            });

            // Verify web.config
            publishDirectory.Should().HaveFiles(new[]
            {
                "web.config"
            });

            VerifyBootManifestHashes(testInstance, blazorPublishDirectory);
            VerifyServiceWorkerFiles(testInstance, blazorPublishDirectory,
                serviceWorkerPath: Path.Combine("serviceworkers", "my-service-worker.js"),
                serviceWorkerContent: "// This is the production service worker",
                assetsManifestPath: "custom-service-worker-assets.js");

            // Verify assemblies are not trimmed
            var loggingAssemblyPath = Path.Combine(blazorPublishDirectory, "_framework", "Microsoft.Extensions.Logging.Abstractions.dll");
            VerifyAssemblyHasTypes(loggingAssemblyPath, new[] { "Microsoft.Extensions.Logging.Abstractions.NullLogger" });
        }

        [Fact]
        public void Publish_SatelliteAssemblies_AreCopiedToBuildOutput()
        {
            // Arrange
            var testAppName = "BlazorHosted";
            var testInstance = CreateAspNetSdkTestAsset(testAppName);

            testInstance.WithProjectChanges((path, project) =>
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

            var publishCommand = new PublishCommand(Log, Path.Combine(testInstance.TestRoot, "blazorwasm"));
            publishCommand.Execute().Should().Pass();

            var publishDirectory = publishCommand.GetOutputDirectory(DefaultTfm);
            var blazorPublishDirectory = Path.Combine(publishDirectory.ToString(), "wwwroot");

            publishDirectory.Should().HaveFiles(new[]
            {
                "wwwroot/_framework/Microsoft.CodeAnalysis.CSharp.dll",
                "wwwroot/_framework/fr/Microsoft.CodeAnalysis.CSharp.resources.dll"
            });

            var bootJsonData = new FileInfo(Path.Combine(blazorPublishDirectory, "_framework", "blazor.boot.json"));
            bootJsonData.Should().Contain("\"Microsoft.CodeAnalysis.CSharp.dll\"");
            bootJsonData.Should().Contain("\"fr\\/Microsoft.CodeAnalysis.CSharp.resources.dll\"");

            VerifyBootManifestHashes(testInstance, blazorPublishDirectory);
        }

        [Fact]
        public void Publish_HostedApp_DefaultSettings_Works()
        {
            // Arrange
            var testAppName = "BlazorHosted";
            var testInstance = CreateAspNetSdkTestAsset(testAppName);

            var publishCommand = new PublishCommand(Log, Path.Combine(testInstance.TestRoot, "blazorhosted"));
            publishCommand.WithWorkingDirectory(testInstance.TestRoot);
            publishCommand.Execute("/bl").Should().Pass();

            var publishOutputDirectory = publishCommand.GetOutputDirectory(DefaultTfm);

            // Verification for https://github.com/dotnet/aspnetcore/issues/19926. Verify binaries for projects
            // referenced by the Hosted project appear in the publish directory
            publishOutputDirectory.Should().HaveFiles(new[]
            {
                "RazorClassLibrary.dll",
                "blazorwasm.dll"
            });

            var blazorPublishDirectory = Path.Combine(publishOutputDirectory.ToString(), "wwwroot");
            publishOutputDirectory.Should().HaveFiles(new[]
            {
                "wwwroot/_framework/blazor.boot.json",
                "wwwroot/_framework/blazor.webassembly.js",
                "wwwroot/_framework/dotnet.wasm",
                "wwwroot/_framework/blazorwasm.dll",
                "wwwroot/_framework/System.Text.Json.dll"
            });

            // Verify project references appear as static web assets
            publishOutputDirectory.Should().HaveFiles(new[]
            {
                "wwwroot/_framework/RazorClassLibrary.dll",
                "RazorClassLibrary.dll"
            });

            // Verify static assets are in the publish directory
            publishOutputDirectory.Should().HaveFiles(new[]
            {
                "wwwroot/index.html"
            });

            // Verify static web assets from referenced projects are copied.
            publishOutputDirectory.Should().HaveFiles(new[]
            {
                "wwwroot/_content/RazorClassLibrary/wwwroot/exampleJsInterop.js",
                "wwwroot/_content/RazorClassLibrary/styles.css",
            });

            // Verify web.config
            publishOutputDirectory.Should().HaveFiles(new[]
            {
                "web.config"
            });

            VerifyBootManifestHashes(testInstance, blazorPublishDirectory);

            // Verify compression works
            publishOutputDirectory.Should().HaveFiles(new[]
            {
                "wwwroot/_framework/dotnet.wasm.br",
                "wwwroot/_framework/blazorwasm.dll.br",
                "wwwroot/_framework/RazorClassLibrary.dll.br",
                "wwwroot/_framework/System.Text.Json.dll.br"
            });

            publishOutputDirectory.Should().HaveFiles(new[]
            {
                "wwwroot/_framework/dotnet.wasm.gz",
                "wwwroot/_framework/blazorwasm.dll.gz",
                "wwwroot/_framework/RazorClassLibrary.dll.gz",
                "wwwroot/_framework/System.Text.Json.dll.gz"
            });

            VerifyServiceWorkerFiles(testInstance, blazorPublishDirectory,
                serviceWorkerPath: Path.Combine("serviceworkers", "my-service-worker.js"),
                serviceWorkerContent: "// This is the production service worker",
                assetsManifestPath: "custom-service-worker-assets.js");

            VerifyTypeGranularTrimming(blazorPublishDirectory);
        }

        [Fact]
        public void Publish_HostedApp_ProducesBootJsonDataWithExpectedContent()
        {
            // Arrange
            var testAppName = "BlazorHosted";
            var testInstance = CreateAspNetSdkTestAsset(testAppName);

            var wwwroot = Path.Combine(testInstance.TestRoot, "blazorwasm", "wwwroot");
            File.WriteAllText(Path.Combine(wwwroot, "appsettings.json"), "Default settings");
            File.WriteAllText(Path.Combine(wwwroot, "appsettings.development.json"), "Development settings");

            var publishCommand = new PublishCommand(Log, Path.Combine(testInstance.TestRoot, "blazorhosted"));
            publishCommand.Execute().Should().Pass();

            var buildOutputDirectory = publishCommand.GetOutputDirectory(DefaultTfm).ToString();

            var bootJsonPath = Path.Combine(buildOutputDirectory, "wwwroot", "_framework", "blazor.boot.json");
            var bootJsonData = ReadBootJsonData(bootJsonPath);

            var runtime = bootJsonData.resources.runtime;
            runtime.Should().ContainKey("dotnet.wasm");

            var assemblies = bootJsonData.resources.assembly;
            assemblies.Should().ContainKey("blazorwasm.dll");
            assemblies.Should().ContainKey("RazorClassLibrary.dll");
            assemblies.Should().ContainKey("System.Text.Json.dll");

            bootJsonData.resources.satelliteResources.Should().BeNull();

            bootJsonData.config.Should().Contain("appsettings.json");
            bootJsonData.config.Should().Contain("appsettings.development.json");
        }

        [Fact]
        public void Publish_HostedApp_WithSatelliteAssemblies()
        {
            // Arrange
            var testAppName = "BlazorHosted";
            var testInstance = CreateAspNetSdkTestAsset(testAppName);

            testInstance.WithProjectChanges((path, project) =>
            {
                if (path.Contains("blazorwasm"))
                {
                    var ns = project.Root.Name.Namespace;
                    // Workaround for https://github.com/mono/linker/issues/1390
                    var propertyGroup = new XElement(ns + "PropertyGroup");

                    propertyGroup.Add(new XElement("PublishTrimmed", false));
                    propertyGroup.Add(new XElement("DefineConstants", @"$(DefineConstants);REFERENCE_classlibrarywithsatelliteassemblies"));
                    var itemGroup = new XElement(ns + "ItemGroup");
                    itemGroup.Add(new XElement("ProjectReference", new XAttribute("Include", @"..\classlibrarywithsatelliteassemblies\classlibrarywithsatelliteassemblies.csproj")));
                    project.Root.Add(propertyGroup);
                    project.Root.Add(itemGroup);
                }

            });

            var resxfileInProject = Path.Combine(testInstance.TestRoot, "blazorwasm", "Resources.ja.resx.txt");
            File.Move(resxfileInProject, Path.Combine(testInstance.TestRoot, "blazorwasm", "Resource.ja.resx"));

            var publishCommand = new PublishCommand(Log, Path.Combine(testInstance.TestRoot, "blazorhosted"));
            publishCommand.WithWorkingDirectory(testInstance.TestRoot);
            publishCommand.Execute("/bl").Should().Pass();

            var publishOutputDirectory = publishCommand.GetOutputDirectory(DefaultTfm);

            var bootJsonData = new FileInfo(Path.Combine(publishOutputDirectory.ToString(), "wwwroot", "_framework", "blazor.boot.json"));

            publishOutputDirectory.Should().HaveFiles(new[]
            {
                "wwwroot/_framework/blazorwasm.dll",
                "wwwroot/_framework/classlibrarywithsatelliteassemblies.dll",
                "wwwroot/_framework/Microsoft.CodeAnalysis.CSharp.dll",
                "wwwroot/_framework/fr/Microsoft.CodeAnalysis.CSharp.resources.dll",
            });

            bootJsonData.Should().Contain("\"Microsoft.CodeAnalysis.CSharp.dll\"");
            bootJsonData.Should().Contain("\"fr\\/Microsoft.CodeAnalysis.CSharp.resources.dll\"");
        }

        [Fact]
        // Regression test for https://github.com/dotnet/aspnetcore/issues/18752
        public void Publish_HostedApp_WithoutTrimming_Works()
        {
            // Arrange
            var testAppName = "BlazorHosted";
            var testInstance = CreateAspNetSdkTestAsset(testAppName);

            testInstance.WithProjectChanges((path, project) =>
            {
                if (path.Contains("blazorwasm"))
                {
                    var ns = project.Root.Name.Namespace;
                    var propertyGroup = new XElement(ns + "PropertyGroup");

                    propertyGroup.Add(new XElement("PublishTrimmed", false));
                    project.Root.Add(propertyGroup);
                }
            });

            // VS builds projects individually and then a publish with BuildDependencies=false, but building the main project is a close enough approximation for this test.
            var buildCommand = new BuildCommand(testInstance, "blazorwasm");
            buildCommand.Execute().Should().Pass();

            // Publish
            var publishCommand = new PublishCommand(Log, Path.Combine(testInstance.TestRoot, "blazorhosted"));
            publishCommand.Execute("/p:BuildDependencies=false /bl").Should().Pass();

            var publishDirectory = publishCommand.GetOutputDirectory(DefaultTfm);
            // Make sure the main project exists
            new FileInfo(Path.Combine(publishDirectory.ToString(), "blazorhosted.dll")).Should().Exist();

            // Verification for https://github.com/dotnet/aspnetcore/issues/19926. Verify binaries for projects
            // referenced by the Hosted project appear in the publish directory
            publishDirectory.Should().HaveFiles(new[]
            {
                "RazorClassLibrary.dll",
                "blazorwasm.dll"
            });

            publishDirectory.Should().HaveFiles(new[]
            {
                "wwwroot/_framework/blazor.boot.json",
                "wwwroot/_framework/blazor.webassembly.js",
                "wwwroot/_framework/dotnet.wasm",
                "wwwroot/_framework/blazorwasm.dll",
                "wwwroot/_framework/System.Text.Json.dll"
            });

            // Verify project references appear as static web assets
            // Also verify project references to the server project appear in the publish output
            publishDirectory.Should().HaveFiles(new[]
            {
                "wwwroot/_framework/RazorClassLibrary.dll",
                "RazorClassLibrary.dll"
            });

            // Verify static assets are in the publish directory
            publishDirectory.Should().HaveFiles(new[]
            {
                "wwwroot/index.html"
            });

            // Verify static web assets from referenced projects are copied.
            publishDirectory.Should().HaveFiles(new[]
            {
                "wwwroot/_content/RazorClassLibrary/wwwroot/exampleJsInterop.js",
                "wwwroot/_content/RazorClassLibrary/styles.css",
            });

            // Verify web.config
            publishDirectory.Should().HaveFiles(new[]
            {
                "web.config"
            });

            VerifyBootManifestHashes(testInstance, Path.Combine(publishDirectory.ToString(), "wwwroot"));

            // Verify compression works
            publishDirectory.Should().HaveFiles(new[]
            {
                "wwwroot/_framework/dotnet.wasm.br",
                "wwwroot/_framework/blazorwasm.dll.br",
                "wwwroot/_framework/RazorClassLibrary.dll.br",
                "wwwroot/_framework/System.Text.Json.dll.br"
            });

            publishDirectory.Should().HaveFiles(new[]
            {
                "wwwroot/_framework/dotnet.wasm.gz",
                "wwwroot/_framework/blazorwasm.dll.gz",
                "wwwroot/_framework/RazorClassLibrary.dll.gz",
                "wwwroot/_framework/System.Text.Json.dll.gz"
            });

            VerifyServiceWorkerFiles(testInstance, Path.Combine(publishDirectory.ToString(), "wwwroot"),
                serviceWorkerPath: Path.Combine("serviceworkers", "my-service-worker.js"),
                serviceWorkerContent: "// This is the production service worker",
                assetsManifestPath: "custom-service-worker-assets.js");
        }

        [Fact]
        public void Publish_HostedApp_WithNoBuild_Works()
        {
            // Arrange
            var testAppName = "BlazorHosted";
            var testInstance = CreateAspNetSdkTestAsset(testAppName);

            var buildCommand = new BuildCommand(testInstance, "blazorhosted");
            buildCommand.Execute().Should().Pass();

            var publishCommand = new PublishCommand(Log, Path.Combine(testInstance.TestRoot, "blazorhosted"));
            publishCommand.WithWorkingDirectory(testInstance.TestRoot);
            publishCommand.Execute("/p:NoBuild=true", "/bl").Should().Pass();

            var publishDirectory = publishCommand.GetOutputDirectory(DefaultTfm);
            // Make sure the main project exists
            new FileInfo(Path.Combine(publishDirectory.ToString(), "blazorhosted.dll")).Should().Exist();

            publishDirectory.Should().HaveFiles(new[]
            {
                "wwwroot/_framework/blazor.boot.json",
                "wwwroot/_framework/blazor.webassembly.js",
                "wwwroot/_framework/dotnet.wasm",
                "wwwroot/_framework/blazorwasm.dll",
                "wwwroot/_framework/System.Text.Json.dll"
            });

            // Verify static assets are in the publish directory
            publishDirectory.Should().HaveFiles(new[]
            {
                "wwwroot/index.html"
            });

            // Verify static web assets from referenced projects are copied.
            publishDirectory.Should().HaveFiles(new[]
            {
                "wwwroot/_content/RazorClassLibrary/wwwroot/exampleJsInterop.js",
                "wwwroot/_content/RazorClassLibrary/styles.css",
            });

            // Verify web.config
            publishDirectory.Should().HaveFiles(new[]
            {
                "web.config"
            });

            VerifyBootManifestHashes(testInstance, Path.Combine(publishDirectory.ToString(), "wwwroot"));
            VerifyServiceWorkerFiles(testInstance, Path.Combine(publishDirectory.ToString(), "wwwroot"),
                serviceWorkerPath: Path.Combine("serviceworkers", "my-service-worker.js"),
                serviceWorkerContent: "// This is the production service worker",
                assetsManifestPath: "custom-service-worker-assets.js");
        }

        [Fact]
        public void Publish_HostedApp_VisualStudio()
        {
            // Simulates publishing the same way VS does by setting BuildProjectReferences=false.
            // Arrange
            var testAppName = "BlazorHosted";
            var testInstance = CreateAspNetSdkTestAsset(testAppName);

            // VS builds projects individually and then a publish with BuildDependencies=false, but building the main project is a close enough approximation for this test.
            var buildCommand = new BuildCommand(testInstance, "blazorwasm");
            buildCommand.Execute("/p:BuildInsideVisualStudio=true").Should().Pass();

            // Publish
            var publishCommand = new PublishCommand(Log, Path.Combine(testInstance.TestRoot, "blazorhosted"));
            publishCommand.Execute("/p:BuildProjectReferences=false /p:BuildInsideVisualStudio=true").Should().Pass();

            var publishDirectory = publishCommand.GetOutputDirectory(DefaultTfm);
            // Make sure the main project exists
            new FileInfo(Path.Combine(publishDirectory.ToString(), "blazorhosted.dll")).Should().Exist();

            // Verification for https://github.com/dotnet/aspnetcore/issues/19926. Verify binaries for projects
            // referenced by the Hosted project appear in the publish directory
            publishDirectory.Should().HaveFiles(new[]
            {
                "RazorClassLibrary.dll",
                "blazorwasm.dll"
            });

            publishDirectory.Should().HaveFiles(new[]
            {
                "wwwroot/_framework/blazor.boot.json",
                "wwwroot/_framework/blazor.webassembly.js",
                "wwwroot/_framework/dotnet.wasm",
                "wwwroot/_framework/blazorwasm.dll",
                "wwwroot/_framework/System.Text.Json.dll"
            });

            // Verify project references appear as static web assets
            // Also verify project references to the server project appear in the publish output
            publishDirectory.Should().HaveFiles(new[]
            {
                "wwwroot/_framework/RazorClassLibrary.dll",
                "RazorClassLibrary.dll"
            });

            // Verify static assets are in the publish directory
            publishDirectory.Should().HaveFiles(new[]
            {
                "wwwroot/index.html"
            });

            // Verify static web assets from referenced projects are copied.
            publishDirectory.Should().HaveFiles(new[]
            {
                "wwwroot/_content/RazorClassLibrary/wwwroot/exampleJsInterop.js",
                "wwwroot/_content/RazorClassLibrary/styles.css",
            });

            // Verify web.config
            publishDirectory.Should().HaveFiles(new[]
            {
                "web.config"
            });

            VerifyBootManifestHashes(testInstance, Path.Combine(publishDirectory.ToString(), "wwwroot"));

            // Verify compression works
            publishDirectory.Should().HaveFiles(new[]
            {
                "wwwroot/_framework/dotnet.wasm.br",
                "wwwroot/_framework/blazorwasm.dll.br",
                "wwwroot/_framework/RazorClassLibrary.dll.br",
                "wwwroot/_framework/System.Text.Json.dll.br"
            });

            var blazorPublishDirectory = Path.Combine(publishDirectory.ToString(), "wwwroot");

            VerifyBootManifestHashes(testInstance, blazorPublishDirectory);
            VerifyServiceWorkerFiles(testInstance, blazorPublishDirectory,
                serviceWorkerPath: Path.Combine("serviceworkers", "my-service-worker.js"),
                serviceWorkerContent: "// This is the production service worker",
                assetsManifestPath: "custom-service-worker-assets.js");
        }

        [Fact]
        public void Publish_HostedAppWithScopedCss_VisualStudio()
        {
            // Simulates publishing the same way VS does by setting BuildProjectReferences=false.
            var testAppName = "BlazorHosted";
            var testInstance = CreateAspNetSdkTestAsset(testAppName);
            File.WriteAllText(Path.Combine(testInstance.TestRoot, "blazorwasm", "App.razor.css"), "h1 { font-size: 16px; }");

            // VS builds projects individually and then a publish with BuildDependencies=false, but building the main project is a close enough approximation for this test.
            var buildCommand = new BuildCommand(testInstance, "blazorwasm");
            buildCommand.Execute("/p:BuildInsideVisualStudio=true /p:Configuration=Release").Should().Pass();

            // Publish
            var publishCommand = new PublishCommand(Log, Path.Combine(testInstance.TestRoot, "blazorhosted"));
            publishCommand.Execute("/p:BuildProjectReferences=false /p:BuildInsideVisualStudio=true").Should().Pass();

            var publishDirectory = publishCommand.GetOutputDirectory(DefaultTfm);
            var blazorPublishDirectory = Path.Combine(publishDirectory.ToString(), "wwwroot");

            // Make sure the main project exists
            new FileInfo(Path.Combine(publishDirectory.ToString(), "blazorhosted.dll")).Should().Exist();

            // Verification for https://github.com/dotnet/aspnetcore/issues/19926. Verify binaries for projects
            // referenced by the Hosted project appear in the publish directory
            publishDirectory.Should().HaveFiles(new[]
            {
                "RazorClassLibrary.dll",
                "blazorwasm.dll"
            });

            publishDirectory.Should().HaveFiles(new[]
            {
                "wwwroot/_framework/blazor.boot.json",
                "wwwroot/_framework/blazor.webassembly.js",
                "wwwroot/_framework/dotnet.wasm",
                "wwwroot/_framework/blazorwasm.dll",
                "wwwroot/_framework/System.Text.Json.dll"
            });

            // Verify project references appear as static web assets
            // Also verify project references to the server project appear in the publish output
            publishDirectory.Should().HaveFiles(new[]
            {
                "wwwroot/_framework/RazorClassLibrary.dll",
                "RazorClassLibrary.dll"
            });

            // Verify static assets are in the publish directory
            publishDirectory.Should().HaveFiles(new[]
            {
                "wwwroot/index.html"
            });

            // Verify scoped css
            publishDirectory.Should().HaveFiles(new[]
            {
                "wwwroot/blazorwasm.styles.css"
            });

            // Verify static web assets from referenced projects are copied.
            publishDirectory.Should().HaveFiles(new[]
            {
                "wwwroot/_content/RazorClassLibrary/wwwroot/exampleJsInterop.js",
                "wwwroot/_content/RazorClassLibrary/styles.css",
            });

            // Verify web.config
            publishDirectory.Should().HaveFiles(new[]
            {
                "web.config"
            });

            VerifyBootManifestHashes(testInstance, Path.Combine(publishDirectory.ToString(), "wwwroot"));

            // Verify compression works
            publishDirectory.Should().HaveFiles(new[]
            {
                "wwwroot/_framework/dotnet.wasm.br",
                "wwwroot/_framework/blazorwasm.dll.br",
                "wwwroot/_framework/RazorClassLibrary.dll.br",
                "wwwroot/_framework/System.Text.Json.dll.br"
            });

            VerifyBootManifestHashes(testInstance, blazorPublishDirectory);
            VerifyServiceWorkerFiles(testInstance, blazorPublishDirectory,
                serviceWorkerPath: Path.Combine("serviceworkers", "my-service-worker.js"),
                serviceWorkerContent: "// This is the production service worker",
                assetsManifestPath: "custom-service-worker-assets.js");
        }

        // Regression test to verify satellite assemblies from the blazor app are copied to the published app's wwwroot output directory as
        // part of publishing in VS
        [Fact]
        public void Publish_HostedApp_VisualStudio_WithSatelliteAssemblies()
        {
            var testAppName = "BlazorWasmWithLibrary";
            var testInstance = CreateAspNetSdkTestAsset(testAppName);

            testInstance.WithProjectChanges((path, project) =>
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

            var resxfileInProject = Path.Combine(testInstance.TestRoot, "blazorwasm", "Resources.ja.resx.txt");
            File.Move(resxfileInProject, Path.Combine(testInstance.TestRoot, "blazorwasm", "Resource.ja.resx"));

            var buildCommand = new BuildCommand(testInstance, "blazorwasm");
            buildCommand.WithWorkingDirectory(testInstance.TestRoot);
            buildCommand.Execute("/bl:build-msbuild.binlog").Should().Pass();

            var publishCommand = new PublishCommand(Log, Path.Combine(testInstance.TestRoot, "blazorwasm"));
            publishCommand.WithWorkingDirectory(testInstance.TestRoot);
            publishCommand.Execute("/p:BuildProjectReferences=false", "/bl:publish-msbuild.binlog").Should().Pass();

            var publishDirectory = publishCommand.GetOutputDirectory(DefaultTfm);
            var blazorPublishDirectory = Path.Combine(publishDirectory.ToString(), "wwwroot");

            publishDirectory.Should().HaveFiles(new[]
            {
                "wwwroot/_framework/blazor.boot.json",
                "wwwroot/_framework/ja/blazorwasm.resources.dll",
                "wwwroot/_framework/fr/Microsoft.CodeAnalysis.CSharp.resources.dll"
            });

            var bootJsonData = new FileInfo(Path.Combine(blazorPublishDirectory, "_framework", "blazor.boot.json"));
            bootJsonData.Should().Contain("\"es-ES\\/classlibrarywithsatelliteassemblies.resources.dll\"");
            bootJsonData.Should().Contain("\"ja\\/blazorwasm.resources.dll\"");
            bootJsonData.Should().Contain("\"fr\\/Microsoft.CodeAnalysis.CSharp.resources.dll\"");

            VerifyBootManifestHashes(testInstance, blazorPublishDirectory);
        }

        [Fact]
        public void Publish_HostedApp_WithRidSpecifiedInCLI_Works()
        {
            // Arrange
            var testAppName = "BlazorHostedRID";
            var testInstance = CreateAspNetSdkTestAsset(testAppName);

            var publishCommand = new PublishCommand(Log, Path.Combine(testInstance.TestRoot, "blazorhosted"));
            publishCommand.WithWorkingDirectory(testInstance.TestRoot);
            publishCommand.Execute("/p:RuntimeIdentifier=linux-x64", "/bl").Should().Pass();

            AssertRIDPublishOuput(publishCommand, testInstance, hosted: true);
        }

        [Fact]
        public void Publish_HostedApp_WithRid_Works()
        {
            // Arrange
            var testAppName = "BlazorHostedRID";
            var testInstance = CreateAspNetSdkTestAsset(testAppName);

            var publishCommand = new PublishCommand(Log, Path.Combine(testInstance.TestRoot, "blazorhosted"));
            publishCommand.WithWorkingDirectory(testInstance.TestRoot);
            publishCommand.Execute("/bl").Should().Pass();

            AssertRIDPublishOuput(publishCommand, testInstance, hosted: true);
        }

        private void AssertRIDPublishOuput(PublishCommand command, TestAsset testInstance, bool hosted = false)
        {
            var publishDirectory = command.GetOutputDirectory(DefaultTfm, "Debug", "linux-x64");

            // Make sure the main project exists
            publishDirectory.Should().HaveFiles(new[]
            {
                "libhostfxr.so" // Verify that we're doing a self-contained deployment
            });

            publishDirectory.Should().HaveFiles(new[]
            {
                "RazorClassLibrary.dll",
                "blazorwasm.dll",
            });

            publishDirectory.Should().HaveFiles(new[]
            {
                "wwwroot/_framework/blazor.boot.json",
                "wwwroot/_framework/blazor.webassembly.js",
                "wwwroot/_framework/dotnet.wasm",
                "wwwroot/_framework/blazorwasm.dll",
                "wwwroot/_framework/System.Text.Json.dll"
            });


            publishDirectory.Should().HaveFiles(new[]
            {
                // Verify project references appear as static web assets
                "wwwroot/_framework/RazorClassLibrary.dll",
                // Also verify project references to the server project appear in the publish output
                "RazorClassLibrary.dll",
            });

            // Verify static assets are in the publish directory
            publishDirectory.Should().HaveFiles(new[]
            {
                "wwwroot/index.html"
            });

            // Verify static web assets from referenced projects are copied.
            publishDirectory.Should().HaveFiles(new[]
            {
                "wwwroot/_content/RazorClassLibrary/wwwroot/exampleJsInterop.js",
                "wwwroot/_content/RazorClassLibrary/styles.css",
            });

            if (!hosted)
            {
                // Verify web.config
                publishDirectory.Should().HaveFiles(new[]
                {
                    "web.config"
                });
            }

            VerifyBootManifestHashes(testInstance, Path.Combine(publishDirectory.ToString(), "wwwroot"));

            // Verify compression works
            publishDirectory.Should().HaveFiles(new[]
            {
                "wwwroot/_framework/dotnet.wasm.br",
                "wwwroot/_framework/blazorwasm.dll.br",
                "wwwroot/_framework/RazorClassLibrary.dll.br",
                "wwwroot/_framework/System.Text.Json.dll.br"
            });
            publishDirectory.Should().HaveFiles(new[]
            {
                "wwwroot/_framework/dotnet.wasm.gz",
                "wwwroot/_framework/blazorwasm.dll.gz",
                "wwwroot/_framework/RazorClassLibrary.dll.gz",
                "wwwroot/_framework/System.Text.Json.dll.gz"
            });

            VerifyServiceWorkerFiles(testInstance, Path.Combine(publishDirectory.ToString(), "wwwroot"),
                serviceWorkerPath: Path.Combine("serviceworkers", "my-service-worker.js"),
                serviceWorkerContent: "// This is the production service worker",
                assetsManifestPath: "custom-service-worker-assets.js");
        }

        [Fact]
        public void Publish_WithInvariantGlobalizationEnabled_DoesNotCopyGlobalizationData()
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

            var publishCommand = new PublishCommand(Log, testInstance.TestRoot);
            publishCommand.Execute().Should().Pass();

            var publishOutputDirectory = publishCommand.GetOutputDirectory(DefaultTfm).ToString();

            var bootJsonPath = Path.Combine(publishOutputDirectory.ToString(), "wwwroot", "_framework", "blazor.boot.json");
            var bootJsonData = ReadBootJsonData(bootJsonPath);

            bootJsonData.icuDataMode.Should().Be(ICUDataMode.Invariant);
            var runtime = bootJsonData.resources.runtime;

            runtime.Should().ContainKey("dotnet.wasm");
            runtime.Should().NotContainKey("icudt.dat");
            runtime.Should().NotContainKey("icudt_EFIGS.dat");


            new FileInfo(Path.Combine(publishOutputDirectory, "wwwroot", "_framework", "dotnet.wasm")).Should().Exist();
            new FileInfo(Path.Combine(publishOutputDirectory, "wwwroot", "_framework", "icudt.dat")).Should().NotExist();
            new FileInfo(Path.Combine(publishOutputDirectory, "wwwroot", "_framework", "icudt_CJK.dat")).Should().NotExist();
            new FileInfo(Path.Combine(publishOutputDirectory, "wwwroot", "_framework", "icudt_EFIGS.dat")).Should().NotExist();
            new FileInfo(Path.Combine(publishOutputDirectory, "wwwroot", "_framework", "icudt_no_CJK.dat")).Should().NotExist();
        }

        [Fact]
        public void Publish_HostingMultipleBlazorWebApps_Works()
        {
            // Regression test for https://github.com/dotnet/aspnetcore/issues/29264
            // Arrange
            var testAppName = "BlazorMultiApp";
            var testInstance = CreateAspNetSdkTestAsset(testAppName);

            var publishCommand = new PublishCommand(Log, Path.Combine(testInstance.TestRoot, "BlazorMultipleApps.Server"));
            publishCommand.Execute().Should().Pass();

            var publishOutputDirectory = publishCommand.GetOutputDirectory(DefaultTfm).ToString();

            new FileInfo(Path.Combine(publishOutputDirectory, "BlazorMultipleApps.Server.dll")).Should().Exist();
            new FileInfo(Path.Combine(publishOutputDirectory, "BlazorMultipleApps.FirstClient.dll")).Should().Exist();
            new FileInfo(Path.Combine(publishOutputDirectory, "BlazorMultipleApps.SecondClient.dll")).Should().Exist();

            var firstAppPublishDirectory = Path.Combine(publishOutputDirectory, "wwwroot", "FirstApp");

            var firstCss = Path.Combine(firstAppPublishDirectory, "css", "app.css");
            new FileInfo(firstCss).Should().Exist();
            new FileInfo(firstCss).Should().Exist("/* First app.css */");

            var firstBootJsonPath = Path.Combine(firstAppPublishDirectory, "_framework", "blazor.boot.json");
            var firstBootJson = ReadBootJsonData(firstBootJsonPath);

            // Do a sanity check that the boot json has files.
            firstBootJson.resources.assembly.Keys.Should().Contain("System.Text.Json.dll");

            VerifyBootManifestHashes(testInstance, firstAppPublishDirectory);

            // Verify compression works
            new FileInfo(Path.Combine(firstAppPublishDirectory, "_framework", "dotnet.wasm.br")).Should().Exist();
            new FileInfo(Path.Combine(firstAppPublishDirectory, "_framework", "BlazorMultipleApps.FirstClient.dll.br")).Should().Exist();
            new FileInfo(Path.Combine(firstAppPublishDirectory, "_framework", "Newtonsoft.Json.dll.br")).Should().Exist();

            var secondAppPublishDirectory = Path.Combine(publishOutputDirectory, "wwwroot", "SecondApp");

            var secondCss = Path.Combine(secondAppPublishDirectory, "css", "app.css");
            new FileInfo(secondCss).Should().Exist();
            new FileInfo(secondCss).Should().Exist("/* Second app.css */");

            var secondBootJsonPath = Path.Combine(secondAppPublishDirectory, "_framework", "blazor.boot.json");
            var secondBootJson = ReadBootJsonData(secondBootJsonPath);

            // Do a sanity check that the boot json has files.
            secondBootJson.resources.assembly.Keys.Should().Contain("System.Private.CoreLib.dll");

            VerifyBootManifestHashes(testInstance, secondAppPublishDirectory);

            // Verify compression works
            new FileInfo(Path.Combine(secondAppPublishDirectory, "_framework", "dotnet.wasm.br")).Should().Exist();
            new FileInfo(Path.Combine(secondAppPublishDirectory, "_framework", "BlazorMultipleApps.SecondClient.dll.br")).Should().Exist();
            new FileInfo(Path.Combine(secondAppPublishDirectory, "_framework", "System.Private.CoreLib.dll.br")).Should().Exist();
            new FileInfo(Path.Combine(secondAppPublishDirectory, "_framework", "Newtonsoft.Json.dll.br")).Should().NotExist();
        }

        private static void VerifyBootManifestHashes(TestAsset testAsset, string blazorPublishDirectory)
        {
            var bootManifestResolvedPath = Path.Combine(blazorPublishDirectory, "_framework", "blazor.boot.json");
            var bootManifestJson = File.ReadAllText(bootManifestResolvedPath);
            var bootManifest = JsonSerializer.Deserialize<BootJsonData>(bootManifestJson);

            VerifyBootManifestHashes(testAsset, blazorPublishDirectory, bootManifest.resources.assembly);
            VerifyBootManifestHashes(testAsset, blazorPublishDirectory, bootManifest.resources.runtime);

            if (bootManifest.resources.pdb != null)
            {
                VerifyBootManifestHashes(testAsset, blazorPublishDirectory, bootManifest.resources.pdb);
            }

            if (bootManifest.resources.satelliteResources != null)
            {
                foreach (var resourcesForCulture in bootManifest.resources.satelliteResources.Values)
                {
                    VerifyBootManifestHashes(testAsset, blazorPublishDirectory, resourcesForCulture);
                }
            }

            static void VerifyBootManifestHashes(TestAsset testAsset, string blazorPublishDirectory, ResourceHashesByNameDictionary resources)
            {
                foreach (var (name, hash) in resources)
                {
                    var relativePath = Path.Combine(blazorPublishDirectory, "_framework", name);
                    new FileInfo(Path.Combine(testAsset.TestRoot, relativePath)).Should().HashEquals(ParseWebFormattedHash(hash));
                }
            }

            static string ParseWebFormattedHash(string webFormattedHash)
            {
                Assert.StartsWith("sha256-", webFormattedHash);
                return webFormattedHash.Substring(7);
            }
        }

        private void VerifyTypeGranularTrimming(string blazorPublishDirectory)
        {
            VerifyAssemblyHasTypes(Path.Combine(blazorPublishDirectory, "_framework", "Microsoft.AspNetCore.Components.dll"), new[] {
                    "Microsoft.AspNetCore.Components.RouteView",
                    "Microsoft.AspNetCore.Components.RouteData",
                    "Microsoft.AspNetCore.Components.CascadingParameterAttribute"
                });
        }

        private void VerifyAssemblyHasTypes(string assemblyPath, string[] expectedTypes)
        {
            new FileInfo(assemblyPath).Should().Exist();

            using (var file = File.OpenRead(assemblyPath))
            {
                using var peReader = new PEReader(file);
                var metadataReader = peReader.GetMetadataReader();
                var types = metadataReader.TypeDefinitions.Where(t => !t.IsNil).Select(t =>
                {
                    var type = metadataReader.GetTypeDefinition(t);
                    return metadataReader.GetString(type.Namespace) + "." + metadataReader.GetString(type.Name);
                }).ToArray();
                types.Should().Contain(expectedTypes);
            }
        }

        private static BootJsonData ReadBootJsonData(string path)
        {
            return JsonSerializer.Deserialize<BootJsonData>(
                File.ReadAllText(path),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
    }
}
