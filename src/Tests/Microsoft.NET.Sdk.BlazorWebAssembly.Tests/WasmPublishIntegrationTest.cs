// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO.Compression;
using System.Text.Json;
using static Microsoft.NET.Sdk.BlazorWebAssembly.Tests.ServiceWorkerAssert;
using Microsoft.NET.Sdk.WebAssembly;

namespace Microsoft.NET.Sdk.BlazorWebAssembly.Tests
{
    public class WasmPublishIntegrationTest : WasmPublishIntegrationTestBase
    {
        public WasmPublishIntegrationTest(ITestOutputHelper log) : base(log) { }

        [Fact]
        public void Publish_MinimalApp_Works()
        {
            // Arrange
            var testAppName = "BlazorWasmMinimal";
            var testInstance = CreateAspNetSdkTestAsset(testAppName);

            var publishCommand = new PublishCommand(testInstance);
            publishCommand.Execute().Should().Pass()
                .And.NotHaveStdOutContaining("warning IL");

            var publishDirectory = publishCommand.GetOutputDirectory(DefaultTfm);

            var expectedFiles = new[]
            {
                "wwwroot/_framework/blazor.boot.json",
                "wwwroot/_framework/blazor.webassembly.js",
                "wwwroot/_framework/dotnet.native.wasm",
                "wwwroot/_framework/blazorwasm-minimal.wasm",
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

            var publishCommand = new PublishCommand(testInstance, "blazorwasm");
            publishCommand.Execute().Should().Pass();

            var publishDirectory = publishCommand.GetOutputDirectory(DefaultTfm);

            var expectedFiles = new[]
            {
                "wwwroot/_framework/blazor.boot.json",
                "wwwroot/_framework/blazor.webassembly.js",
                "wwwroot/_framework/dotnet.native.wasm",
                "wwwroot/_framework/blazorwasm.wasm",
                "wwwroot/_framework/System.Text.Json.wasm",
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
        public void Publish_Works_WithLibraryUsingHintPath()
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
                        Path.Combine("..", "razorclasslibrary", "bin", "Debug", ToolsetInfo.CurrentTargetFramework, "RazorClassLibrary.dll")));
                }
            });

            var buildLibraryCommand = new BuildCommand(testInstance, "razorclasslibrary");
            buildLibraryCommand.WithWorkingDirectory(testInstance.TestRoot);
            buildLibraryCommand.Execute()
                .Should().Pass();

            var publishCommand = new PublishCommand(testInstance, "blazorwasm");
            publishCommand.WithWorkingDirectory(testInstance.TestRoot);
            publishCommand.Execute().Should().Pass();

            var publishOutputDirectory = publishCommand.GetOutputDirectory(DefaultTfm).ToString();

            new FileInfo(Path.Combine(publishOutputDirectory, "wwwroot", "_framework", "blazor.boot.json")).Should().Exist();
            new FileInfo(Path.Combine(publishOutputDirectory, "wwwroot", "_framework", "blazor.webassembly.js")).Should().Exist();
            new FileInfo(Path.Combine(publishOutputDirectory, "wwwroot", "_framework", "dotnet.native.wasm")).Should().Exist();
            new FileInfo(Path.Combine(publishOutputDirectory, "wwwroot", "_framework", "dotnet.native.wasm.gz")).Should().Exist();
            new FileInfo(Path.Combine(publishOutputDirectory, "wwwroot", "_framework", "blazorwasm.wasm")).Should().Exist();
            new FileInfo(Path.Combine(publishOutputDirectory, "wwwroot", "_framework", "System.Text.Json.wasm")).Should().Exist();
            new FileInfo(Path.Combine(publishOutputDirectory, "wwwroot", "_framework", "System.Text.Json.wasm.gz")).Should().Exist();
            new FileInfo(Path.Combine(publishOutputDirectory, "wwwroot", "_framework", "System.wasm")).Should().Exist();
            new FileInfo(Path.Combine(publishOutputDirectory, "wwwroot", "_framework", "System.wasm.gz")).Should().Exist();

            new FileInfo(Path.Combine(publishOutputDirectory, "wwwroot", "_framework", "RazorClassLibrary.wasm")).Should().Exist();
        }

        [Fact]
        public void Publish_WithScopedCss_Works()
        {
            // Arrange
            var testAppName = "BlazorWasmWithLibrary";
            var testInstance = CreateAspNetSdkTestAsset(testAppName);
            File.WriteAllText(Path.Combine(testInstance.TestRoot, "blazorwasm", "App.razor.css"), "h1 { font-size: 16px; }");

            var publishCommand = new PublishCommand(testInstance, "blazorwasm");
            publishCommand.Execute().Should().Pass();

            var publishDirectory = publishCommand.GetOutputDirectory(DefaultTfm);

            var blazorPublishDirectory = Path.Combine(publishDirectory.ToString(), "wwwroot");

            var expectedFiles = new[]
            {
                "wwwroot/_framework/blazor.boot.json",
                "wwwroot/_framework/blazor.webassembly.js",
                "wwwroot/_framework/dotnet.native.wasm",
                "wwwroot/_framework/blazorwasm.wasm",
                "wwwroot/_framework/System.Text.Json.wasm",
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

            var publishCommand = new PublishCommand(testInstance, "blazorwasm");
            publishCommand.Execute("/p:Configuration=Release").Should().Pass();

            var publishDirectory = publishCommand.GetOutputDirectory(DefaultTfm, "Release");

            var blazorPublishDirectory = Path.Combine(publishDirectory.ToString(), "wwwroot");

            var expectedFiles = new[]
            {
                "wwwroot/_framework/blazor.boot.json",
                "wwwroot/_framework/blazor.webassembly.js",
                "wwwroot/_framework/dotnet.native.wasm",
                "wwwroot/_framework/blazorwasm.wasm",
                "wwwroot/_framework/System.Text.Json.wasm",
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

            var publishCommand = new PublishCommand(testInstance, "blazorwasm");
            publishCommand.Execute("/p:Configuration=Release").Should().Pass();

            // Verify web.config
            var outputDirectory = new BuildCommand(testInstance, "blazorwasm").GetOutputDirectory(configuration: "Release");
            var webConfig = outputDirectory.File("web.config");
            webConfig.Should().Exist();
            webConfig.Should().Contain(webConfigContents);
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

            var publishCommand = new PublishCommand(testInstance, "blazorwasm");
            publishCommand.WithWorkingDirectory(testInstance.TestRoot);
            publishCommand.Execute("/p:NoBuild=true").Should().Pass();

            var publishDirectory = publishCommand.GetOutputDirectory(DefaultTfm);
            var blazorPublishDirectory = Path.Combine(publishDirectory.ToString(), "wwwroot");

            var expectedFiles = new[]
            {
                "wwwroot/_framework/blazor.boot.json",
                "wwwroot/_framework/blazor.webassembly.js",
                "wwwroot/_framework/dotnet.native.wasm",
                "wwwroot/_framework/blazorwasm.wasm",
                "wwwroot/_framework/System.Text.Json.wasm",
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

            var publishCommand = new PublishCommand(testInstance, "blazorwasm");
            publishCommand.Execute().Should().Pass();

            var publishDirectory = publishCommand.GetOutputDirectory(DefaultTfm);

            var expectedFiles = new[]
            {
                "wwwroot/different-path/_framework/blazor.boot.json",
                "wwwroot/different-path/_framework/blazor.webassembly.js",
                "wwwroot/different-path/_framework/dotnet.native.wasm",
                "wwwroot/different-path/_framework/blazorwasm.wasm",
                "wwwroot/different-path/_framework/System.Text.Json.wasm",
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

            var publishCommand = new PublishCommand(testInstance, "blazorhosted");
            publishCommand.Execute().Should().Pass();

            var publishDirectory = publishCommand.GetOutputDirectory(DefaultTfm);

            var expectedFiles = new[]
            {
                "wwwroot/different-path/_framework/blazor.boot.json",
                "wwwroot/different-path/_framework/blazor.webassembly.js",
                "wwwroot/different-path/_framework/dotnet.native.wasm",
                "wwwroot/different-path/_framework/dotnet.native.wasm.br",
                "wwwroot/different-path/_framework/dotnet.native.wasm.gz",
                "wwwroot/different-path/_framework/blazorwasm.wasm",
                "wwwroot/different-path/_framework/blazorwasm.wasm.gz",
                "wwwroot/different-path/_framework/System.Text.Json.wasm",
                "wwwroot/different-path/_framework/System.Text.Json.wasm.gz",
                "wwwroot/different-path/_framework/System.Text.Json.wasm.br",
                "wwwroot/different-path/_framework/RazorClassLibrary.wasm.gz",
                "wwwroot/different-path/_framework/RazorClassLibrary.wasm.br",
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

            var publishCommand = new PublishCommand(testInstance, "blazorwasm");
            publishCommand.Execute().Should().Pass();

            var publishDirectory = publishCommand.GetOutputDirectory(DefaultTfm);
            var blazorPublishDirectory = Path.Combine(publishDirectory.ToString(), "wwwroot");

            publishDirectory.Should().HaveFiles(new[]
            {
                "wwwroot/_framework/blazor.boot.json",
                "wwwroot/_framework/blazor.webassembly.js",
                "wwwroot/_framework/dotnet.native.wasm",
                "wwwroot/_framework/blazorwasm.wasm",
                "wwwroot/_framework/System.Text.Json.wasm"
            });

            // Verify compression works
            publishDirectory.Should().HaveFiles(new[]
            {
                "wwwroot/_framework/dotnet.native.wasm.br",
                "wwwroot/_framework/System.Text.Json.wasm.br"
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
            var loggingAssemblyPath = Path.Combine(blazorPublishDirectory, "_framework", "Microsoft.Extensions.Logging.Abstractions.wasm");
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

            var publishCommand = new PublishCommand(testInstance, "blazorwasm");
            publishCommand.Execute().Should().Pass();

            var publishDirectory = publishCommand.GetOutputDirectory(DefaultTfm);
            var blazorPublishDirectory = Path.Combine(publishDirectory.ToString(), "wwwroot");

            publishDirectory.Should().HaveFiles(new[]
            {
                "wwwroot/_framework/Microsoft.CodeAnalysis.CSharp.wasm",
                "wwwroot/_framework/fr/Microsoft.CodeAnalysis.CSharp.resources.wasm"
            });

            var bootJsonData = new FileInfo(Path.Combine(blazorPublishDirectory, "_framework", "blazor.boot.json"));
            bootJsonData.Should().Contain("\"Microsoft.CodeAnalysis.CSharp.wasm\"");
            bootJsonData.Should().Contain("\"fr\"");
            bootJsonData.Should().Contain("\"Microsoft.CodeAnalysis.CSharp.resources.wasm\"");

            VerifyBootManifestHashes(testInstance, blazorPublishDirectory);
        }

        [Fact]
        public void Publish_HostedApp_DefaultSettings_Works()
        {
            // Arrange
            var testAppName = "BlazorHosted";
            var testInstance = CreateAspNetSdkTestAsset(testAppName);

            var publishCommand = new PublishCommand(testInstance, "blazorhosted");
            publishCommand.WithWorkingDirectory(testInstance.TestRoot);
            publishCommand.Execute().Should().Pass();

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
                "wwwroot/_framework/dotnet.native.wasm",
                "wwwroot/_framework/blazorwasm.wasm",
                "wwwroot/_framework/System.Text.Json.wasm"
            });

            // Verify project references appear as static web assets
            publishOutputDirectory.Should().HaveFiles(new[]
            {
                "wwwroot/_framework/RazorClassLibrary.wasm",
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
                "wwwroot/_framework/dotnet.native.wasm.br",
                "wwwroot/_framework/blazorwasm.wasm.br",
                "wwwroot/_framework/RazorClassLibrary.wasm.br",
                "wwwroot/_framework/System.Text.Json.wasm.br"
            });

            publishOutputDirectory.Should().HaveFiles(new[]
            {
                "wwwroot/_framework/dotnet.native.wasm.gz",
                "wwwroot/_framework/blazorwasm.wasm.gz",
                "wwwroot/_framework/RazorClassLibrary.wasm.gz",
                "wwwroot/_framework/System.Text.Json.wasm.gz"
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

            var publishCommand = new PublishCommand(testInstance, "blazorhosted");
            publishCommand.Execute().Should().Pass();

            var buildOutputDirectory = publishCommand.GetOutputDirectory(DefaultTfm).ToString();

            var bootJsonPath = Path.Combine(buildOutputDirectory, "wwwroot", "_framework", "blazor.boot.json");
            var bootJsonData = ReadBootJsonData(bootJsonPath);

            bootJsonData.resources.wasmNative.Should().ContainKey("dotnet.native.wasm");

            var assemblies = bootJsonData.resources.assembly;
            assemblies.Should().ContainKey("blazorwasm.wasm");
            assemblies.Should().ContainKey("RazorClassLibrary.wasm");
            assemblies.Should().ContainKey("System.Text.Json.wasm");

            bootJsonData.resources.satelliteResources.Should().BeNull();

            bootJsonData.config.Should().Contain("../appsettings.json");
            bootJsonData.config.Should().Contain("../appsettings.development.json");
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

            var publishCommand = new PublishCommand(testInstance, "blazorhosted");
            publishCommand.WithWorkingDirectory(testInstance.TestRoot);
            publishCommand.Execute().Should().Pass();

            var publishOutputDirectory = publishCommand.GetOutputDirectory(DefaultTfm);

            var bootJsonData = new FileInfo(Path.Combine(publishOutputDirectory.ToString(), "wwwroot", "_framework", "blazor.boot.json"));

            publishOutputDirectory.Should().HaveFiles(new[]
            {
                "wwwroot/_framework/blazorwasm.wasm",
                "wwwroot/_framework/classlibrarywithsatelliteassemblies.wasm",
                "wwwroot/_framework/Microsoft.CodeAnalysis.CSharp.wasm",
                "wwwroot/_framework/fr/Microsoft.CodeAnalysis.CSharp.resources.wasm",
            });

            bootJsonData.Should().Contain("\"Microsoft.CodeAnalysis.CSharp.wasm\"");
            bootJsonData.Should().Contain("\"fr\"");
            bootJsonData.Should().Contain("\"Microsoft.CodeAnalysis.CSharp.resources.wasm\"");
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
            var publishCommand = new PublishCommand(testInstance, "blazorhosted");
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
                "wwwroot/_framework/dotnet.native.wasm",
                "wwwroot/_framework/blazorwasm.wasm",
                "wwwroot/_framework/System.Text.Json.wasm"
            });

            // Verify project references appear as static web assets
            // Also verify project references to the server project appear in the publish output
            publishDirectory.Should().HaveFiles(new[]
            {
                "wwwroot/_framework/RazorClassLibrary.wasm",
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
                "wwwroot/_framework/dotnet.native.wasm.br",
                "wwwroot/_framework/blazorwasm.wasm.br",
                "wwwroot/_framework/RazorClassLibrary.wasm.br",
                "wwwroot/_framework/System.Text.Json.wasm.br"
            });

            publishDirectory.Should().HaveFiles(new[]
            {
                "wwwroot/_framework/dotnet.native.wasm.gz",
                "wwwroot/_framework/blazorwasm.wasm.gz",
                "wwwroot/_framework/RazorClassLibrary.wasm.gz",
                "wwwroot/_framework/System.Text.Json.wasm.gz"
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

            var publishCommand = new PublishCommand(testInstance, "blazorhosted");
            publishCommand.WithWorkingDirectory(testInstance.TestRoot);
            publishCommand.Execute("/p:NoBuild=true").Should().Pass();

            var publishDirectory = publishCommand.GetOutputDirectory(DefaultTfm);
            // Make sure the main project exists
            new FileInfo(Path.Combine(publishDirectory.ToString(), "blazorhosted.dll")).Should().Exist();

            publishDirectory.Should().HaveFiles(new[]
            {
                "wwwroot/_framework/blazor.boot.json",
                "wwwroot/_framework/blazor.webassembly.js",
                "wwwroot/_framework/dotnet.native.wasm",
                "wwwroot/_framework/blazorwasm.wasm",
                "wwwroot/_framework/System.Text.Json.wasm"
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
            var publishCommand = new PublishCommand(testInstance, "blazorhosted");
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
                "wwwroot/_framework/dotnet.native.wasm",
                "wwwroot/_framework/blazorwasm.wasm",
                "wwwroot/_framework/System.Text.Json.wasm"
            });

            // Verify project references appear as static web assets
            // Also verify project references to the server project appear in the publish output
            publishDirectory.Should().HaveFiles(new[]
            {
                "wwwroot/_framework/RazorClassLibrary.wasm",
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
                "wwwroot/_framework/dotnet.native.wasm.br",
                "wwwroot/_framework/blazorwasm.wasm.br",
                "wwwroot/_framework/RazorClassLibrary.wasm.br",
                "wwwroot/_framework/System.Text.Json.wasm.br"
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
            var publishCommand = new PublishCommand(testInstance, "blazorhosted");
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
                "wwwroot/_framework/dotnet.native.wasm",
                "wwwroot/_framework/blazorwasm.wasm",
                "wwwroot/_framework/System.Text.Json.wasm"
            });

            // Verify project references appear as static web assets
            // Also verify project references to the server project appear in the publish output
            publishDirectory.Should().HaveFiles(new[]
            {
                "wwwroot/_framework/RazorClassLibrary.wasm",
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
                "wwwroot/_framework/dotnet.native.wasm.br",
                "wwwroot/_framework/blazorwasm.wasm.br",
                "wwwroot/_framework/RazorClassLibrary.wasm.br",
                "wwwroot/_framework/System.Text.Json.wasm.br"
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

            var publishCommand = new PublishCommand(testInstance, "blazorwasm");
            publishCommand.WithWorkingDirectory(testInstance.TestRoot);
            publishCommand.Execute("/p:BuildProjectReferences=false", "/bl:publish-msbuild.binlog").Should().Pass();

            var publishDirectory = publishCommand.GetOutputDirectory(DefaultTfm);
            var blazorPublishDirectory = Path.Combine(publishDirectory.ToString(), "wwwroot");

            publishDirectory.Should().HaveFiles(new[]
            {
                "wwwroot/_framework/blazor.boot.json",
                "wwwroot/_framework/ja/blazorwasm.resources.wasm",
                "wwwroot/_framework/fr/Microsoft.CodeAnalysis.CSharp.resources.wasm"
            });

            var bootJsonData = new FileInfo(Path.Combine(blazorPublishDirectory, "_framework", "blazor.boot.json"));
            bootJsonData.Should().Contain("\"es-ES\"");
            bootJsonData.Should().Contain("\"ja\"");
            bootJsonData.Should().Contain("\"fr\"");
            bootJsonData.Should().Contain("\"classlibrarywithsatelliteassemblies.resources.wasm\"");
            bootJsonData.Should().Contain("\"blazorwasm.resources.wasm\"");
            bootJsonData.Should().Contain("\"Microsoft.CodeAnalysis.CSharp.resources.wasm\"");

            VerifyBootManifestHashes(testInstance, blazorPublishDirectory);
        }

        [Fact]
        public void Publish_HostedApp_WithRidSpecifiedInCLI_Works()
        {
            // Arrange
            var testAppName = "BlazorHostedRID";
            var testInstance = CreateAspNetSdkTestAsset(testAppName);

            var publishCommand = new PublishCommand(testInstance, "blazorhosted");
            publishCommand.WithWorkingDirectory(testInstance.TestRoot);
            publishCommand.Execute("/p:RuntimeIdentifier=linux-x64").Should().Pass();

            AssertRIDPublishOuput(publishCommand, testInstance, hosted: true);
        }

        [ConditionalFact()]
        public void Publish_HostedApp_WithRidSpecifiedAsArgument_NoSelfContained_Works()
        {
            // Arrange
            var testAppName = "BlazorHostedRID";
            var testInstance = CreateAspNetSdkTestAsset(testAppName);
            testInstance.WithProjectChanges((project, doc) =>
            {
                if (Path.GetFileName(project) == "blazorhosted-rid.csproj")
                {
                    var projectReference = doc.Descendants("ProjectReference").Single();
                    var itemGroup = projectReference.Parent;
                    projectReference.Remove();
                    itemGroup.Add(XElement.Parse("""
    <ProjectReference Include="..\blazorwasm\blazorwasm.csproj">
      <GlobalPropertiesToRemove>SelfContained</GlobalPropertiesToRemove>
    </ProjectReference>
    """));
                }
            });
            var publishCommand = new DotnetPublishCommand(Log, Path.Combine(testInstance.TestRoot, "blazorhosted"));
            publishCommand.WithRuntime("linux-x64");
            publishCommand.WithWorkingDirectory(Path.Combine(testInstance.TestRoot, "blazorhosted"));
            var result = publishCommand.Execute("--no-self-contained");
            result.Should().Pass();
            AssertRIDPublishOuput(publishCommand, testInstance, hosted: true, selfContained: false);
        }

        [Fact]
        public void Publish_HostedApp_WithRidSpecifiedAsArgument_Works()
        {
            // Arrange
            var testAppName = "BlazorHostedRID";
            var testInstance = CreateAspNetSdkTestAsset(testAppName);

            var publishCommand = new DotnetPublishCommand(Log, Path.Combine(testInstance.TestRoot, "blazorhosted"));
            publishCommand.WithWorkingDirectory(Path.Combine(testInstance.TestRoot, "blazorhosted"));
            publishCommand.WithRuntime("linux-x64");
            var result = publishCommand.Execute("--self-contained");

            result.Should().Pass();
            AssertRIDPublishOuput(publishCommand, testInstance, hosted: true);
        }

        [Fact]
        public void Publish_HostedApp_WithRid_Works()
        {
            // Arrange
            var testAppName = "BlazorHostedRID";
            var testInstance = CreateAspNetSdkTestAsset(testAppName);

            var publishCommand = new PublishCommand(testInstance, "blazorhosted");
            publishCommand.WithWorkingDirectory(testInstance.TestRoot);
            publishCommand.Execute().Should().Pass();

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
                "wwwroot/_framework/dotnet.native.wasm",
                "wwwroot/_framework/blazorwasm.wasm",
                "wwwroot/_framework/System.Text.Json.wasm"
            });


            publishDirectory.Should().HaveFiles(new[]
            {
                // Verify project references appear as static web assets
                "wwwroot/_framework/RazorClassLibrary.wasm",
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
                "wwwroot/_framework/dotnet.native.wasm.br",
                "wwwroot/_framework/blazorwasm.wasm.br",
                "wwwroot/_framework/RazorClassLibrary.wasm.br",
                "wwwroot/_framework/System.Text.Json.wasm.br"
            });
            publishDirectory.Should().HaveFiles(new[]
            {
                "wwwroot/_framework/dotnet.native.wasm.gz",
                "wwwroot/_framework/blazorwasm.wasm.gz",
                "wwwroot/_framework/RazorClassLibrary.wasm.gz",
                "wwwroot/_framework/System.Text.Json.wasm.gz"
            });

            VerifyServiceWorkerFiles(testInstance, Path.Combine(publishDirectory.ToString(), "wwwroot"),
                serviceWorkerPath: Path.Combine("serviceworkers", "my-service-worker.js"),
                serviceWorkerContent: "// This is the production service worker",
                assetsManifestPath: "custom-service-worker-assets.js");
        }

        private void AssertRIDPublishOuput(DotnetPublishCommand command, TestAsset testInstance, bool hosted = false, bool selfContained = true)
        {
            var publishDirectory = command.GetOutputDirectory(DefaultTfm, "Release", "linux-x64");

            if (selfContained)
            {
                // Make sure the main project exists
                publishDirectory.Should().HaveFiles(new[]
                {
                    "libhostfxr.so" // Verify that we're doing a self-contained deployment
                });
            }

            publishDirectory.Should().HaveFiles(new[]
            {
                "RazorClassLibrary.dll",
                "blazorwasm.dll",
            });

            publishDirectory.Should().HaveFiles(new[]
            {
                "wwwroot/_framework/blazor.boot.json",
                "wwwroot/_framework/blazor.webassembly.js",
                "wwwroot/_framework/dotnet.native.wasm",
                "wwwroot/_framework/blazorwasm.wasm",
                "wwwroot/_framework/System.Text.Json.wasm"
            });


            publishDirectory.Should().HaveFiles(new[]
            {
                // Verify project references appear as static web assets
                "wwwroot/_framework/RazorClassLibrary.wasm",
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
                "wwwroot/_framework/dotnet.native.wasm.br",
                "wwwroot/_framework/blazorwasm.wasm.br",
                "wwwroot/_framework/RazorClassLibrary.wasm.br",
                "wwwroot/_framework/System.Text.Json.wasm.br"
            });
            publishDirectory.Should().HaveFiles(new[]
            {
                "wwwroot/_framework/dotnet.native.wasm.gz",
                "wwwroot/_framework/blazorwasm.wasm.gz",
                "wwwroot/_framework/RazorClassLibrary.wasm.gz",
                "wwwroot/_framework/System.Text.Json.wasm.gz"
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

            var publishCommand = new PublishCommand(testInstance);
            publishCommand.Execute().Should().Pass();

            var publishOutputDirectory = publishCommand.GetOutputDirectory(DefaultTfm).ToString();

            var bootJsonPath = Path.Combine(publishOutputDirectory.ToString(), "wwwroot", "_framework", "blazor.boot.json");
            var bootJsonData = ReadBootJsonData(bootJsonPath);

            bootJsonData.globalizationMode.Should().Be("invariant");

            bootJsonData.resources.wasmNative.Should().ContainKey("dotnet.native.wasm");
            bootJsonData.resources.icu.Should().BeNull();

            new FileInfo(Path.Combine(publishOutputDirectory, "wwwroot", "_framework", "dotnet.native.wasm")).Should().Exist();
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

            var publishCommand = new PublishCommand(testInstance, "BlazorMultipleApps.Server");
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
            firstBootJson.resources.assembly.Keys.Should().Contain("System.Text.Json.wasm");

            VerifyBootManifestHashes(testInstance, firstAppPublishDirectory);

            // Verify compression works
            new FileInfo(Path.Combine(firstAppPublishDirectory, "_framework", "dotnet.native.wasm.br")).Should().Exist();
            new FileInfo(Path.Combine(firstAppPublishDirectory, "_framework", "BlazorMultipleApps.FirstClient.wasm.br")).Should().Exist();
            new FileInfo(Path.Combine(firstAppPublishDirectory, "_framework", "Newtonsoft.Json.wasm.br")).Should().Exist();

            var secondAppPublishDirectory = Path.Combine(publishOutputDirectory, "wwwroot", "SecondApp");

            var secondCss = Path.Combine(secondAppPublishDirectory, "css", "app.css");
            new FileInfo(secondCss).Should().Exist();
            new FileInfo(secondCss).Should().Exist("/* Second app.css */");

            var secondBootJsonPath = Path.Combine(secondAppPublishDirectory, "_framework", "blazor.boot.json");
            var secondBootJson = ReadBootJsonData(secondBootJsonPath);

            // Do a sanity check that the boot json has files.
            secondBootJson.resources.assembly.Keys.Should().Contain("System.Private.CoreLib.wasm");

            VerifyBootManifestHashes(testInstance, secondAppPublishDirectory);

            // Verify compression works
            new FileInfo(Path.Combine(secondAppPublishDirectory, "_framework", "dotnet.native.wasm.br")).Should().Exist();
            new FileInfo(Path.Combine(secondAppPublishDirectory, "_framework", "BlazorMultipleApps.SecondClient.wasm.br")).Should().Exist();
            new FileInfo(Path.Combine(secondAppPublishDirectory, "_framework", "System.Private.CoreLib.wasm.br")).Should().Exist();
            new FileInfo(Path.Combine(secondAppPublishDirectory, "_framework", "Newtonsoft.Json.wasm.br")).Should().NotExist();
        }

        [Fact]
        public void Publish_WithTransitiveReference_Works()
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
                    //  <Reference Include="classlibrarywithsatelliteassemblies" HintPath="$Path\classlibrarywithsatelliteassemblies.wasm" />
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

            var publishCommand = new PublishCommand(testInstance, "blazorwasm");
            publishCommand.Execute().Should().Pass();

            // Assert
            var outputDirectory = publishCommand.GetOutputDirectory(DefaultTfm).ToString();
            var fileInWwwroot = new FileInfo(Path.Combine(outputDirectory, "wwwroot", "_framework", "classlibrarywithsatelliteassemblies.wasm"));
            fileInWwwroot.Should().Exist();
        }

        private void VerifyTypeGranularTrimming(string blazorPublishDirectory)
        {
            VerifyAssemblyHasTypes(Path.Combine(blazorPublishDirectory, "_framework", "Microsoft.AspNetCore.Components.wasm"), new[] {
                    "Microsoft.AspNetCore.Components.RouteView",
                    "Microsoft.AspNetCore.Components.RouteData",
                    "Microsoft.AspNetCore.Components.CascadingParameterAttribute"
                });
        }

        private void VerifyAssemblyHasTypes(string assemblyPath, string[] expectedTypes)
        {
            new FileInfo(assemblyPath).Should().Exist();

            // TODO MF: Test moved to runtime
            // using (var file = File.OpenRead(assemblyPath))
            // {
            //     using var peReader = new PEReader(file);
            //     var metadataReader = peReader.GetMetadataReader();
            //     var types = metadataReader.TypeDefinitions.Where(t => !t.IsNil).Select(t =>
            //     {
            //         var type = metadataReader.GetTypeDefinition(t);
            //         return metadataReader.GetString(type.Namespace) + "." + metadataReader.GetString(type.Name);
            //     }).ToArray();
            //     types.Should().Contain(expectedTypes);
            // }
        }

        private static BootJsonData ReadBootJsonData(string path)
        {
            return JsonSerializer.Deserialize<BootJsonData>(
                File.ReadAllText(path),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
    }

    internal static class DotNetPublishCommandExtensions
    {
        public static DirectoryInfo GetOutputDirectory(this DotnetPublishCommand command, string targetFramework = "netcoreapp1.1", string configuration = "Debug", string runtimeIdentifier = "")
        {
            targetFramework = targetFramework ?? string.Empty;
            configuration = configuration ?? string.Empty;
            runtimeIdentifier = runtimeIdentifier ?? string.Empty;

            string output = Path.Combine(command.WorkingDirectory, "bin", configuration, targetFramework, runtimeIdentifier);
            var baseDirectory = new DirectoryInfo(output);

            return new DirectoryInfo(Path.Combine(baseDirectory.FullName, "publish"));
        }
    }
}
