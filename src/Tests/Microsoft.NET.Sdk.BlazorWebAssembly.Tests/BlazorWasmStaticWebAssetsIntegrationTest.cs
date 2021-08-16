// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System.Xml.Linq;
using Microsoft.AspNetCore.Razor.Tasks;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Xunit;
using Xunit.Abstractions;

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
            var buildResult = build.Execute("/bl");
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
                StaticWebAssetsManifest.FromJsonBytes(File.ReadAllBytes(path)),
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
            var publishResult = publish.Execute("/bl");
            publishResult.Should().Pass();

            var publishPath = publish.GetOutputDirectory(DefaultTfm).ToString();
            var intermediateOutputPath = publish.GetIntermediateDirectory(DefaultTfm, "Debug").ToString();

            // GenerateStaticWebAssetsManifest should generate the manifest file.
            var path = Path.Combine(intermediateOutputPath, "staticwebassets.publish.json");
            new FileInfo(path).Should().Exist();
            var manifest = StaticWebAssetsManifest.FromJsonBytes(File.ReadAllBytes(path));
            AssertManifest(manifest, LoadPublishManifest());

            AssertPublishAssets(
                StaticWebAssetsManifest.FromJsonBytes(File.ReadAllBytes(path)),
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
            var buildResult = build.Execute("/bl");
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
                StaticWebAssetsManifest.FromJsonBytes(File.ReadAllBytes(path)),
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
            var publishResult = publish.Execute("/bl");
            publishResult.Should().Pass();

            var publishPath = publish.GetOutputDirectory(DefaultTfm).ToString();
            var intermediateOutputPath = publish.GetIntermediateDirectory(DefaultTfm, "Debug").ToString();

            // GenerateStaticWebAssetsManifest should generate the manifest file.
            var path = Path.Combine(intermediateOutputPath, "staticwebassets.publish.json");
            new FileInfo(path).Should().Exist();
            var manifest = StaticWebAssetsManifest.FromJsonBytes(File.ReadAllBytes(path));
            AssertManifest(manifest, LoadPublishManifest());

            AssertPublishAssets(
                StaticWebAssetsManifest.FromJsonBytes(File.ReadAllBytes(path)),
                publishPath,
                intermediateOutputPath);
        }

        [Fact]
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
            var buildResult = build.Execute("/bl");
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
                StaticWebAssetsManifest.FromJsonBytes(File.ReadAllBytes(path)),
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
            var publishResult = publish.Execute("/bl");
            publishResult.Should().Pass();

            var publishPath = publish.GetOutputDirectory(DefaultTfm).ToString();
            var intermediateOutputPath = publish.GetIntermediateDirectory(DefaultTfm, "Debug").ToString();

            // GenerateStaticWebAssetsManifest should generate the manifest file.
            var path = Path.Combine(intermediateOutputPath, "staticwebassets.publish.json");
            new FileInfo(path).Should().Exist();
            var manifest = StaticWebAssetsManifest.FromJsonBytes(File.ReadAllBytes(path));
            AssertManifest(manifest, LoadPublishManifest());

            AssertPublishAssets(
                StaticWebAssetsManifest.FromJsonBytes(File.ReadAllBytes(path)),
                publishPath,
                intermediateOutputPath);
        }
    }
}
