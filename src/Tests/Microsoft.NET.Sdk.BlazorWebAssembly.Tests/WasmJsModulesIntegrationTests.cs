// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json;
using Microsoft.AspNetCore.StaticWebAssets.Tasks;

namespace Microsoft.NET.Sdk.BlazorWebAssembly.Tests
{
    public class WasmJsModulesIntegrationTests : BlazorWasmBaselineTests
    {
        public WasmJsModulesIntegrationTests(ITestOutputHelper log) : base(log, GenerateBaselines)
        {
        }

        [Fact]
        public void Build_DoesNotGenerateManifestJson_IncludesJSModulesOnBlazorBootJsonManifest()
        {
            // Arrange
            var testAsset = "BlazorWasmMinimal";
            ProjectDirectory = CreateAspNetSdkTestAsset(testAsset);
            File.WriteAllText(Path.Combine(ProjectDirectory.TestRoot, "wwwroot", "blazorwasm-minimal.lib.module.js"), "console.log('Hello initializer')");

            var build = new BuildCommand(ProjectDirectory);
            build.WithWorkingDirectory(ProjectDirectory.TestRoot);
            var buildResult = build.Execute();
            buildResult.Should().Pass();

            var outputPath = build.GetOutputDirectory(DefaultTfm).ToString();
            var intermediateOutputPath = build.GetIntermediateDirectory(DefaultTfm, "Debug").ToString();

            var path = Path.Combine(intermediateOutputPath, "staticwebassets.build.json");
            new FileInfo(path).Should().Exist();
            var manifest = StaticWebAssetsManifest.FromJsonBytes(File.ReadAllBytes(path));
            AssertManifest(manifest, LoadBuildManifest());

            var blazorBootJson = new FileInfo(Path.Combine(intermediateOutputPath, "blazor.boot.json"));
            blazorBootJson.Should().Exist();
            var contents = JsonSerializer.Deserialize<JsonDocument>(blazorBootJson.OpenRead());
            contents.RootElement.TryGetProperty("resources", out var resources).Should().BeTrue();
            resources.TryGetProperty("libraryInitializers", out var initializers).Should().BeTrue();
            initializers.TryGetProperty("blazorwasm-minimal.lib.module.js", out _).Should().BeTrue();

            new FileInfo(Path.Combine(outputPath, "wwwroot", "blazorwasm-minimal.modules.json")).Should().NotExist();
        }

        [Fact]
        public void JSModules_ManifestIncludesModuleTargetPaths()
        {
            // Arrange
            var testAsset = "BlazorHosted";
            ProjectDirectory = CreateAspNetSdkTestAsset(testAsset);
            File.WriteAllText(Path.Combine(ProjectDirectory.TestRoot, "blazorwasm", "wwwroot", "blazorwasm.lib.module.js"), "console.log('Hello initializer')");
            File.WriteAllText(Path.Combine(ProjectDirectory.TestRoot, "razorclasslibrary", "wwwroot", "razorclasslibrary.lib.module.js"), "console.log('Hello RCL initializer')");

            var build = new BuildCommand(ProjectDirectory, "blazorhosted");
            build.WithWorkingDirectory(ProjectDirectory.TestRoot);
            var buildResult = build.Execute();
            buildResult.Should().Pass();

            var outputPath = build.GetOutputDirectory(DefaultTfm).ToString();
            var intermediateOutputPath = build.GetIntermediateDirectory(DefaultTfm, "Debug").ToString();

            var path = Path.Combine(intermediateOutputPath, "staticwebassets.build.json");
            new FileInfo(path).Should().Exist();
            var manifest = StaticWebAssetsManifest.FromJsonBytes(File.ReadAllBytes(path));
            AssertManifest(manifest, LoadBuildManifest());

            var blazorBootJson = new FileInfo(Path.Combine(intermediateOutputPath.Replace("blazorhosted", "blazorwasm"), "blazor.boot.json"));
            blazorBootJson.Should().Exist();
            var contents = JsonSerializer.Deserialize<JsonDocument>(blazorBootJson.OpenRead());
            contents.RootElement.TryGetProperty("resources", out var resources).Should().BeTrue();
            resources.TryGetProperty("libraryInitializers", out var initializers).Should().BeTrue();
            initializers.TryGetProperty("blazorwasm.lib.module.js", out _).Should().BeTrue();
            initializers.TryGetProperty("_content/RazorClassLibrary/razorclasslibrary.lib.module.js", out var hash).Should().BeTrue();

            // Do some validation to ensure the hash is included
            Convert.TryFromBase64String(hash.GetString().Substring("SHA256-".Length), new byte[256], out _).Should().BeTrue();

            new FileInfo(Path.Combine(outputPath, "wwwroot", "blazorhosted.modules.json")).Should().NotExist();
        }

        [Fact]
        public void Publish_DoesNotGenerateManifestJson_IncludesJSModulesOnBlazorBootJsonManifest()
        {
            // Arrange
            var testAsset = "BlazorWasmMinimal";
            ProjectDirectory = CreateAspNetSdkTestAsset(testAsset);
            File.WriteAllText(Path.Combine(ProjectDirectory.TestRoot, "wwwroot", "blazorwasm-minimal.lib.module.js"), "console.log('Hello initializer')");

            var publish = new PublishCommand(ProjectDirectory);
            publish.WithWorkingDirectory(ProjectDirectory.TestRoot);
            var publishResult = publish.Execute();
            publishResult.Should().Pass();

            var outputPath = publish.GetOutputDirectory(DefaultTfm).ToString();
            var intermediateOutputPath = publish.GetIntermediateDirectory(DefaultTfm, "Debug").ToString();

            var path = Path.Combine(intermediateOutputPath, "staticwebassets.publish.json");
            new FileInfo(path).Should().Exist();
            var manifest = StaticWebAssetsManifest.FromJsonBytes(File.ReadAllBytes(path));
            AssertManifest(manifest, LoadPublishManifest());

            var blazorBootJson = new FileInfo(Path.Combine(intermediateOutputPath, "blazor.publish.boot.json"));
            blazorBootJson.Should().Exist();
            var contents = JsonSerializer.Deserialize<JsonDocument>(blazorBootJson.OpenRead());
            contents.RootElement.TryGetProperty("resources", out var resources).Should().BeTrue();
            resources.TryGetProperty("libraryInitializers", out var initializers).Should().BeTrue();
            initializers.TryGetProperty("blazorwasm-minimal.lib.module.js", out var hash).Should().BeTrue();
            Convert.TryFromBase64String(hash.GetString().Substring("SHA256-".Length), new byte[256], out _).Should().BeTrue();

            new FileInfo(Path.Combine(outputPath, "wwwroot", "blazorwasm-minimal.modules.json")).Should().NotExist();

            var lib = new FileInfo(Path.Combine(outputPath, "wwwroot", "blazorwasm-minimal.lib.module.js"));
            lib.Should().Exist();

            AssertPublishAssets(
                manifest,
                outputPath,
                intermediateOutputPath);
        }

        [Fact]
        public void JsModules_CanHaveDifferentBuildAndPublishModules()
        {
            // Arrange
            var testAsset = "BlazorWasmMinimal";
            ProjectDirectory = CreateAspNetSdkTestAsset(testAsset);

            File.WriteAllText(Path.Combine(ProjectDirectory.TestRoot, "wwwroot", "blazorwasm-minimal.lib.module.js"), "console.log('Publish initializer')");
            File.WriteAllText(Path.Combine(ProjectDirectory.TestRoot, "wwwroot", "blazorwasm-minimal.lib.module.build.js"), "console.log('Build initializer')");

            ProjectDirectory.WithProjectChanges(document =>
            {
                document.Root.Add(new XElement("ItemGroup",
                    new XElement("Content",
                        new XAttribute("Update", "wwwroot\\blazorwasm-minimal.lib.module.build.js"),
                        new XAttribute("CopyToPublishDirectory", "Never"),
                        new XAttribute("TargetPath", "wwwroot\\blazorwasm-minimal.lib.module.js"))));
            });


            var publish = new PublishCommand(ProjectDirectory);
            publish.WithWorkingDirectory(ProjectDirectory.TestRoot);
            var publishResult = publish.Execute();
            publishResult.Should().Pass();

            var outputPath = publish.GetOutputDirectory(DefaultTfm).ToString();
            var intermediateOutputPath = publish.GetIntermediateDirectory(DefaultTfm, "Debug").ToString();

            var path = Path.Combine(intermediateOutputPath, "staticwebassets.publish.json");
            new FileInfo(path).Should().Exist();
            var manifest = StaticWebAssetsManifest.FromJsonBytes(File.ReadAllBytes(path));
            AssertManifest(manifest, LoadPublishManifest());

            var buildLibrary = GetLibraryInitializer(Path.Combine(intermediateOutputPath, "blazor.boot.json"));
            var publishLibrary = GetLibraryInitializer(Path.Combine(intermediateOutputPath, "blazor.publish.boot.json"));

            publishLibrary.GetString().Should().NotBe(buildLibrary.GetString());

            new FileInfo(Path.Combine(outputPath, "wwwroot", "blazorwasm-minimal.modules.json")).Should().NotExist();
            var lib = new FileInfo(Path.Combine(outputPath, "wwwroot", "blazorwasm-minimal.lib.module.js"));
            lib.Should().Exist();

            var wwwrootPublishLibrary = GetLibraryInitializer(Path.Combine(outputPath, "wwwroot", "_framework", "blazor.boot.json"));
            publishLibrary.GetString().Should().Be(wwwrootPublishLibrary.GetString());

            AssertPublishAssets(
                manifest,
                outputPath,
                intermediateOutputPath);

            static JsonElement GetLibraryInitializer(string path)
            {
                var blazorBootJson = new FileInfo(path);
                blazorBootJson.Should().Exist();
                var contents = JsonSerializer.Deserialize<JsonDocument>(blazorBootJson.OpenRead());
                contents.RootElement.TryGetProperty("resources", out var resources).Should().BeTrue();
                resources.TryGetProperty("libraryInitializers", out var initializers).Should().BeTrue();
                initializers.TryGetProperty("blazorwasm-minimal.lib.module.js", out var buildLibrary).Should().BeTrue();
                return buildLibrary;
            }
        }

        [Fact]
        public void JsModules_CanCustomizeBlazorInitialization()
        {
            // Arrange
            var testAsset = "BlazorWasmMinimal";
            ProjectDirectory = CreateAspNetSdkTestAsset(testAsset);
            File.WriteAllText(Path.Combine(ProjectDirectory.TestRoot, "wwwroot", "blazorwasm-minimal.lib.module.js"), "console.log('Hello initializer')");

            ProjectDirectory.WithProjectChanges(document =>
            {
                document.Root.Add(
                    XElement.Parse(@"
<PropertyGroup>
  <ComputeBlazorExtensionsDependsOn>$(ComputeBlazorExtensionsDependsOn);_CustomizeBlazorBootProcess</ComputeBlazorExtensionsDependsOn>
</PropertyGroup>"),
                    XElement.Parse(@"
<Target Name=""_CustomizeBlazorBootProcess"">
  <ItemGroup>
  <BlazorPublishExtension Include=""$(IntermediateOutputPath)publish.extension.txt"">
    <ExtensionName>my-custom-extension</ExtensionName>
    <RelativePath>_bin/publish.extension.txt</RelativePath>
  </BlazorPublishExtension>
  <FileWrites Include=""$(IntermediateOutputPath)publish.extension.txt"" />
  </ItemGroup>

  <WriteLinesToFile
    Lines=""@(_BlazorBootFilesToUpdate->'%(FullPath)')""
    File=""$(IntermediateOutputPath)publish.extension.txt""
    WriteOnlyWhenDifferent=""true"" />
</Target>"));
            });

            var publish = new PublishCommand(ProjectDirectory);
            publish.WithWorkingDirectory(ProjectDirectory.TestRoot);
            var publishResult = publish.Execute();
            publishResult.Should().Pass();

            var outputPath = publish.GetOutputDirectory(DefaultTfm).ToString();
            var intermediateOutputPath = publish.GetIntermediateDirectory(DefaultTfm, "Debug").ToString();

            var path = Path.Combine(intermediateOutputPath, "staticwebassets.publish.json");
            new FileInfo(path).Should().Exist();
            var manifest = StaticWebAssetsManifest.FromJsonBytes(File.ReadAllBytes(path));
            AssertManifest(manifest, LoadPublishManifest());

            var publishExtension = GetPublishExtension(Path.Combine(intermediateOutputPath, "blazor.publish.boot.json"));
            GetPublishExtensionEntriesCount(Path.Combine(intermediateOutputPath, "blazor.publish.boot.json")).Should().Be(1);

            new FileInfo(Path.Combine(outputPath, "wwwroot", "blazorwasm-minimal.modules.json")).Should().NotExist();
            var lib = new FileInfo(Path.Combine(outputPath, "wwwroot", "blazorwasm-minimal.lib.module.js"));
            lib.Should().Exist();

            var wwwrootPublishExtension = GetPublishExtension(Path.Combine(outputPath, "wwwroot", "_framework", "blazor.boot.json"));
            publishExtension.GetString().Should().Be(wwwrootPublishExtension.GetString());

            var extension = new FileInfo(Path.Combine(outputPath, "wwwroot", "_bin", "publish.extension.txt"));
            extension.Should().Exist();

            AssertPublishAssets(
                manifest,
                outputPath,
                intermediateOutputPath);
        }

        [Fact]
        public void JsModules_Hosted_CanCustomizeBlazorInitialization()
        {
            // Arrange
            var testAsset = "BlazorHosted";
            ProjectDirectory = CreateAspNetSdkTestAsset(testAsset);
            File.WriteAllText(Path.Combine(ProjectDirectory.TestRoot, "blazorwasm", "wwwroot", "blazorwasm.lib.module.js"), "console.log('Hello initializer')");

            ProjectDirectory.WithProjectChanges((path, document) =>
            {
                if (Path.GetFileNameWithoutExtension(path) == "blazorwasm")
                {
                    document.Root.Add(
                        XElement.Parse(@"
<PropertyGroup>
  <ComputeBlazorExtensionsDependsOn>$(ComputeBlazorExtensionsDependsOn);_CustomizeBlazorBootProcess</ComputeBlazorExtensionsDependsOn>
</PropertyGroup>"),
                    XElement.Parse(@"
<Target Name=""_CustomizeBlazorBootProcess"">
  <ItemGroup>
  <BlazorPublishExtension Include=""$(IntermediateOutputPath)publish.extension.txt"">
    <ExtensionName>my-custom-extension</ExtensionName>
    <RelativePath>_bin/publish.extension.txt</RelativePath>
  </BlazorPublishExtension>
  <FileWrites Include=""$(IntermediateOutputPath)publish.extension.txt"" />
  </ItemGroup>

  <WriteLinesToFile
    Lines=""@(_BlazorBootFilesToUpdate->'%(FullPath)')""
    File=""$(IntermediateOutputPath)publish.extension.txt""
    WriteOnlyWhenDifferent=""true"" />
</Target>"));
                }
            });

            var publish = new PublishCommand(ProjectDirectory, "blazorhosted");
            publish.WithWorkingDirectory(ProjectDirectory.TestRoot);
            var publishResult = publish.Execute();
            publishResult.Should().Pass();

            var outputPath = publish.GetOutputDirectory(DefaultTfm).ToString();
            var intermediateOutputPath = publish.GetIntermediateDirectory(DefaultTfm, "Debug").ToString();

            var path = Path.Combine(intermediateOutputPath, "staticwebassets.publish.json");
            new FileInfo(path).Should().Exist();
            var manifest = StaticWebAssetsManifest.FromJsonBytes(File.ReadAllBytes(path));
            AssertManifest(manifest, LoadPublishManifest());

            var publishExtension = GetPublishExtension(Path.Combine(intermediateOutputPath.Replace("blazorhosted", "blazorwasm"), "blazor.publish.boot.json"));

            new FileInfo(Path.Combine(outputPath, "wwwroot", "blazorhosted.modules.json")).Should().Exist();
            var lib = new FileInfo(Path.Combine(outputPath, "wwwroot", "blazorwasm.lib.module.js"));
            lib.Should().Exist();

            var wwwrootPublishExtension = GetPublishExtension(Path.Combine(outputPath, "wwwroot", "_framework", "blazor.boot.json"));
            publishExtension.GetString().Should().Be(wwwrootPublishExtension.GetString());

            var extension = new FileInfo(Path.Combine(outputPath, "wwwroot", "_bin", "publish.extension.txt"));
            extension.Should().Exist();

            AssertPublishAssets(
                manifest,
                outputPath,
                intermediateOutputPath);
        }

        private static JsonElement GetPublishExtension(string path)
        {
            var blazorBootJson = new FileInfo(path);
            blazorBootJson.Should().Exist();
            var contents = JsonSerializer.Deserialize<JsonDocument>(blazorBootJson.OpenRead());
            contents.RootElement.TryGetProperty("resources", out var resources).Should().BeTrue();
            resources.TryGetProperty("extensions", out var extensions).Should().BeTrue();
            extensions.TryGetProperty("my-custom-extension", out var extension).Should().BeTrue();
            extension.TryGetProperty("_bin/publish.extension.txt", out var file).Should().BeTrue();
            return file;
        }

        private static int GetPublishExtensionEntriesCount(string path)
        {
            var blazorBootJson = new FileInfo(path);
            blazorBootJson.Should().Exist();
            var contents = JsonSerializer.Deserialize<JsonDocument>(blazorBootJson.OpenRead());
            contents.RootElement.TryGetProperty("resources", out var resources).Should().BeTrue();
            resources.TryGetProperty("extensions", out var extensions).Should().BeTrue();
            extensions.TryGetProperty("my-custom-extension", out var extension).Should().BeTrue();
            return extension.EnumerateObject().Count();
        }
    }
}
