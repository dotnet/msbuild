// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.AspNetCore.StaticWebAssets.Tasks;

namespace Microsoft.NET.Sdk.BlazorWebAssembly.Tests
{
    public class BlazorWasmStaticWebAssetsIntegrationTest : BlazorWasmBaselineTests
    {
        public BlazorWasmStaticWebAssetsIntegrationTest(ITestOutputHelper log) : base(log, GenerateBaselines)
        {
        }

        [Fact]
        public void StaticWebAssets_BuildMinimal_Works()
        {
            // Arrange
            // Minimal has no project references, service worker etc. This is pretty close to the project template.
            var testAsset = "BlazorWasmMinimal";
            ProjectDirectory = CreateAspNetSdkTestAsset(testAsset);
            File.WriteAllText(Path.Combine(ProjectDirectory.TestRoot, "App.razor.css"), "h1 { font-size: 16px; }");
            File.WriteAllText(Path.Combine(ProjectDirectory.TestRoot, "wwwroot", "appsettings.development.json"), "{}");

            var build = new BuildCommand(ProjectDirectory);
            build.WithWorkingDirectory(ProjectDirectory.TestRoot);
            var buildResult = build.Execute();
            buildResult.Should().Pass();

            var outputPath = build.GetOutputDirectory(DefaultTfm).ToString();
            var intermediateOutputPath = build.GetIntermediateDirectory(DefaultTfm, "Debug").ToString();

            // GenerateStaticWebAssetsManifest should generate the manifest file.
            var path = Path.Combine(intermediateOutputPath, "staticwebassets.build.json");
            new FileInfo(path).Should().Exist();
            var manifest = StaticWebAssetsManifest.FromJsonBytes(File.ReadAllBytes(path));
            AssertManifest(manifest, LoadBuildManifest());

            // GenerateStaticWebAssetsManifest should copy the file to the output folder.
            var finalPath = Path.Combine(outputPath, "blazorwasm-minimal.staticwebassets.runtime.json");
            new FileInfo(finalPath).Should().Exist();

            AssertBuildAssets(
                manifest,
                outputPath,
                intermediateOutputPath);
        }

        [Fact]
        public void StaticWebAssets_PublishMinimal_Works()
        {
            // Arrange
            // Minimal has no project references, service worker etc. This is pretty close to the project template.
            var testAsset = "BlazorWasmMinimal";
            ProjectDirectory = CreateAspNetSdkTestAsset(testAsset);
            File.WriteAllText(Path.Combine(ProjectDirectory.TestRoot, "App.razor.css"), "h1 { font-size: 16px; }");
            File.WriteAllText(Path.Combine(ProjectDirectory.TestRoot, "wwwroot", "appsettings.development.json"), "{}");

            var publish = new PublishCommand(ProjectDirectory);
            publish.WithWorkingDirectory(ProjectDirectory.TestRoot);
            var publishResult = publish.Execute();
            publishResult.Should().Pass();

            var publishPath = publish.GetOutputDirectory(DefaultTfm).ToString();
            var intermediateOutputPath = publish.GetIntermediateDirectory(DefaultTfm, "Debug").ToString();

            // GenerateStaticWebAssetsManifest should generate the manifest file.
            var path = Path.Combine(intermediateOutputPath, "staticwebassets.publish.json");
            new FileInfo(path).Should().Exist();
            var manifest = StaticWebAssetsManifest.FromJsonBytes(File.ReadAllBytes(path));
            AssertManifest(manifest, LoadPublishManifest());

            AssertPublishAssets(
                manifest,
                publishPath,
                intermediateOutputPath);
        }

        [Fact]
        public void StaticWebAssets_Build_Hosted_Works()
        {
            // Arrange
            var testAppName = "BlazorHosted";
            ProjectDirectory = CreateAspNetSdkTestAsset(testAppName);

            var build = new BuildCommand(ProjectDirectory, "blazorhosted");
            build.WithWorkingDirectory(ProjectDirectory.TestRoot);
            var buildResult = build.Execute();
            buildResult.Should().Pass();

            var outputPath = build.GetOutputDirectory(DefaultTfm).ToString();
            var intermediateOutputPath = build.GetIntermediateDirectory(DefaultTfm, "Debug").ToString();

            // GenerateStaticWebAssetsManifest should generate the manifest file.
            var path = Path.Combine(intermediateOutputPath, "staticwebassets.build.json");
            new FileInfo(path).Should().Exist();
            var manifest = StaticWebAssetsManifest.FromJsonBytes(File.ReadAllBytes(path));
            AssertManifest(manifest, LoadBuildManifest());

            // GenerateStaticWebAssetsManifest should copy the file to the output folder.
            var finalPath = Path.Combine(outputPath, "blazorhosted.staticwebassets.runtime.json");
            new FileInfo(finalPath).Should().Exist();

            AssertBuildAssets(
                manifest,
                outputPath,
                intermediateOutputPath);
        }

        [Fact]
        public void StaticWebAssets_Publish_Hosted_Works()
        {
            // Arrange
            var testAppName = "BlazorHosted";
            ProjectDirectory = CreateAspNetSdkTestAsset(testAppName);

            // Check that static web assets is correctly configured by setting up a css file to triger css isolation.
            // The list of publish files should not include bundle.scp.css and should include blazorwasm.styles.css
            File.WriteAllText(Path.Combine(ProjectDirectory.TestRoot, "blazorwasm", "App.razor.css"), "h1 { font-size: 16px; }");

            var publish = new PublishCommand(ProjectDirectory, "blazorhosted");
            publish.WithWorkingDirectory(ProjectDirectory.TestRoot);
            var publishResult = publish.Execute();
            publishResult.Should().Pass();

            var publishPath = publish.GetOutputDirectory(DefaultTfm).ToString();
            var intermediateOutputPath = publish.GetIntermediateDirectory(DefaultTfm, "Debug").ToString();

            // GenerateStaticWebAssetsManifest should generate the manifest file.
            var path = Path.Combine(intermediateOutputPath, "staticwebassets.publish.json");
            new FileInfo(path).Should().Exist();
            var manifest = StaticWebAssetsManifest.FromJsonBytes(File.ReadAllBytes(path));
            AssertManifest(manifest, LoadPublishManifest());

            AssertPublishAssets(
                manifest,
                publishPath,
                intermediateOutputPath);
        }

        [Fact]
        public void StaticWebAssets_Publish_DoesNotIncludeXmlDocumentationFiles_AsAssets()
        {
            // Arrange
            var testAppName = "BlazorHosted";
            ProjectDirectory = CreateAspNetSdkTestAsset(testAppName);

            // Check that static web assets is correctly configured by setting up a css file to triger css isolation.
            // The list of publish files should not include bundle.scp.css and should include blazorwasm.styles.css
            File.WriteAllText(Path.Combine(ProjectDirectory.TestRoot, "blazorwasm", "App.razor.css"), "h1 { font-size: 16px; }");

            var publish = new PublishCommand(ProjectDirectory, "blazorhosted");
            publish.WithWorkingDirectory(ProjectDirectory.TestRoot);
            var publishResult = publish.Execute("/p:GenerateDocumentationFile=true");
            publishResult.Should().Pass();

            var publishPath = publish.GetOutputDirectory(DefaultTfm).ToString();
            var intermediateOutputPath = publish.GetIntermediateDirectory(DefaultTfm, "Debug").ToString();

            // GenerateStaticWebAssetsManifest should generate the manifest file.
            var path = Path.Combine(intermediateOutputPath, "staticwebassets.publish.json");
            new FileInfo(path).Should().Exist();
            var manifest = StaticWebAssetsManifest.FromJsonBytes(File.ReadAllBytes(path));
            AssertManifest(manifest, LoadPublishManifest());

            AssertPublishAssets(
                manifest,
                publishPath,
                intermediateOutputPath);
        }

        [Fact(Skip = "https://github.com/dotnet/sdk/issues/29111 https://github.com/dotnet/sdk/issues/28429")]
        public void StaticWebAssets_HostedApp_ReferencingNetStandardLibrary_Works()
        {
            // Arrange
            var testAppName = "BlazorHosted";
            ProjectDirectory = CreateAspNetSdkTestAsset(testAppName);

            ProjectDirectory.WithProjectChanges((project, document) =>
            {
                if (Path.GetFileNameWithoutExtension(project) == "blazorwasm")
                {
                    document.Descendants("TargetFramework").Single().ReplaceNodes("net5");
                }
                if (Path.GetFileNameWithoutExtension(project) == "RazorClassLibrary")
                {
                    document.Descendants("TargetFramework").Single().ReplaceNodes("netstandard2.1");
                    document.Descendants("PropertyGroup").First().Add(new XElement("RazorLangVersion", "3.0"));
                }
                if (Path.GetFileNameWithoutExtension(project) == "classlibrarywithsatelliteassemblies")
                {
                    document.Descendants("TargetFramework").Single().ReplaceNodes("netstandard2.1");
                    document.Descendants("PropertyGroup").First().Add(new XElement("RazorLangVersion", "3.0"));
                }
            });

            var build = new BuildCommand(ProjectDirectory, "blazorhosted");
            build.WithWorkingDirectory(ProjectDirectory.TestRoot);
            var buildResult = build.Execute();
            buildResult.Should().Pass();

            var outputPath = build.GetOutputDirectory(DefaultTfm).ToString();
            var intermediateOutputPath = build.GetIntermediateDirectory(DefaultTfm, "Debug").ToString();

            // GenerateStaticWebAssetsManifest should generate the manifest file.
            var path = Path.Combine(intermediateOutputPath, "staticwebassets.build.json");
            new FileInfo(path).Should().Exist();
            var manifest = StaticWebAssetsManifest.FromJsonBytes(File.ReadAllBytes(path));

            AssertManifest(manifest, LoadBuildManifest());

            // GenerateStaticWebAssetsManifest should copy the file to the output folder.
            var finalPath = Path.Combine(outputPath, "blazorhosted.staticwebassets.runtime.json");
            new FileInfo(finalPath).Should().Exist();

            AssertBuildAssets(
                manifest,
                outputPath,
                intermediateOutputPath);
        }

        [Fact]
        public void StaticWebAssets_BackCompatibilityPublish_Hosted_Works()
        {
            // Arrange
            var testAppName = "BlazorHosted";
            ProjectDirectory = CreateAspNetSdkTestAsset(testAppName);

            ProjectDirectory.WithProjectChanges((project, document) =>
            {
                if (Path.GetFileNameWithoutExtension(project) == "blazorwasm")
                {
                    document.Descendants("TargetFramework").Single().ReplaceNodes("net5");
                }
                if (Path.GetFileNameWithoutExtension(project) == "RazorClassLibrary")
                {
                    document.Descendants("TargetFramework").Single().ReplaceNodes("netstandard2.1");
                    document.Descendants("PropertyGroup").First().Add(new XElement("RazorLangVersion", "3.0"));
                }
                if (Path.GetFileNameWithoutExtension(project) == "classlibrarywithsatelliteassemblies")
                {
                    document.Descendants("TargetFramework").Single().ReplaceNodes("netstandard2.1");
                    document.Descendants("PropertyGroup").First().Add(new XElement("RazorLangVersion", "3.0"));
                }
            });

            // Check that static web assets is correctly configured by setting up a css file to triger css isolation.
            // The list of publish files should not include bundle.scp.css and should include blazorwasm.styles.css
            File.WriteAllText(Path.Combine(ProjectDirectory.TestRoot, "blazorwasm", "App.razor.css"), "h1 { font-size: 16px; }");

            var publish = new PublishCommand(ProjectDirectory, "blazorhosted");
            publish.WithWorkingDirectory(ProjectDirectory.TestRoot);
            var publishResult = publish.Execute();
            publishResult.Should().Pass();

            var publishPath = publish.GetOutputDirectory(DefaultTfm).ToString();
            var intermediateOutputPath = publish.GetIntermediateDirectory(DefaultTfm, "Debug").ToString();

            // GenerateStaticWebAssetsManifest should generate the manifest file.
            var path = Path.Combine(intermediateOutputPath, "staticwebassets.publish.json");
            new FileInfo(path).Should().Exist();
            var manifest = StaticWebAssetsManifest.FromJsonBytes(File.ReadAllBytes(path));

            AssertManifest(manifest, LoadPublishManifest());

            AssertPublishAssets(
                manifest,
                publishPath,
                intermediateOutputPath);
        }
    }
}
