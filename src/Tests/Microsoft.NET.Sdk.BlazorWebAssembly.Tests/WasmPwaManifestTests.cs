// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Microsoft.NET.TestFramework.Utilities;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;
using static Microsoft.NET.Sdk.BlazorWebAssembly.Tests.ServiceWorkerAssert;


namespace Microsoft.NET.Sdk.BlazorWebAssembly.Tests
{
    public class WasmPwaManifestTests : AspNetSdkTest
    {
        public WasmPwaManifestTests(ITestOutputHelper log) : base(log) {}

        [Fact]
        public void Build_ServiceWorkerAssetsManifest_Works()
        {
            // Arrange
            var expectedExtensions = new[] { ".dll", ".pdb", ".js", ".wasm" };
            var testAppName = "BlazorWasmWithLibrary";
            var testInstance = CreateAspNetSdkTestAsset(testAppName);

            var buildCommand = new BuildCommand(testInstance, "blazorwasm");
            buildCommand.WithWorkingDirectory(testInstance.TestRoot);
            buildCommand.Execute("/p:ServiceWorkerAssetsManifest=service-worker-assets.js", "/bl")
                .Should().Pass();

            var buildOutputDirectory = buildCommand.GetOutputDirectory(DefaultTfm).ToString();

            new FileInfo(Path.Combine(buildOutputDirectory, "wwwroot", "_framework", "blazor.boot.json")).Should().Exist();
            new FileInfo(Path.Combine(buildOutputDirectory, "wwwroot", "_framework", "dotnet.wasm")).Should().Exist();
            new FileInfo(Path.Combine(buildOutputDirectory, "wwwroot", "_framework", "blazorwasm.dll")).Should().Exist();

            var serviceWorkerAssetsManifest = Path.Combine(buildOutputDirectory, "wwwroot", "service-worker-assets.js");
            // Trim prefix 'self.assetsManifest = ' and suffix ';'
            var manifestContents = File.ReadAllText(serviceWorkerAssetsManifest).TrimEnd()[22..^1];

            var manifestContentsJson = JsonDocument.Parse(manifestContents);
            manifestContentsJson.RootElement.TryGetProperty("assets", out var assets).Should().BeTrue();
            assets.ValueKind.Should().Be(JsonValueKind.Array);

            var entries = assets.EnumerateArray().Select(e => e.GetProperty("url").GetString()).OrderBy(e => e).ToArray();
            entries.Should().Contain(e => expectedExtensions.Contains(Path.GetExtension(e)));

            VerifyServiceWorkerFiles(testInstance,
               Path.Combine(buildOutputDirectory, "wwwroot"),
               serviceWorkerPath: Path.Combine("serviceworkers", "my-service-worker.js"),
               serviceWorkerContent: "// This is the development service worker",
               assetsManifestPath: "service-worker-assets.js");
        }

        [Fact]
        public void Build_HostedAppWithServiceWorker_Works()
        {
            // Arrange
            var expectedExtensions = new[] { ".dll", ".pdb", ".js", ".wasm" };
            var testAppName = "BlazorHosted";
            var testInstance = CreateAspNetSdkTestAsset(testAppName);

            var buildCommand = new BuildCommand(testInstance, "blazorhosted");
            buildCommand.Execute()
                .Should().Pass();

            var buildOutputDirectory = Path.Combine(testInstance.TestRoot, "blazorwasm", "bin", "Debug", DefaultTfm);

            var serviceWorkerAssetsManifest = Path.Combine(buildOutputDirectory, "wwwroot", "custom-service-worker-assets.js");
            // Trim prefix 'self.assetsManifest = ' and suffix ';'
            var manifestContents = File.ReadAllText(serviceWorkerAssetsManifest).TrimEnd()[22..^1];

            var manifestContentsJson = JsonDocument.Parse(manifestContents);
            manifestContentsJson.RootElement.TryGetProperty("assets", out var assets).Should().BeTrue();
            assets.ValueKind.Should().Be(JsonValueKind.Array);

            var entries = assets.EnumerateArray().Select(e => e.GetProperty("url").GetString()).OrderBy(e => e).ToArray();
            entries.Should().Contain(e => expectedExtensions.Contains(Path.GetExtension(e)));
        }

        [Fact]
        public void PublishWithPWA_ProducesAssets()
        {
            // Arrange
            var expectedExtensions = new[] { ".dll", ".pdb", ".js", ".wasm" };
            var testAppName = "BlazorWasmWithLibrary";
            var testInstance = CreateAspNetSdkTestAsset(testAppName);

            var publishCommand = new PublishCommand(Log, Path.Combine(testInstance.TestRoot, "blazorwasm"));
            publishCommand.Execute().Should().Pass();

            var publishOutputDirectory = publishCommand.GetOutputDirectory(DefaultTfm).ToString();

            var serviceWorkerAssetsManifest = Path.Combine(publishOutputDirectory, "wwwroot", "custom-service-worker-assets.js");
            // Trim prefix 'self.assetsManifest = ' and suffix ';'
            var manifestContents = File.ReadAllText(serviceWorkerAssetsManifest).TrimEnd()[22..^1];

            var manifestContentsJson = JsonDocument.Parse(manifestContents);
            manifestContentsJson.RootElement.TryGetProperty("assets", out var assets).Should().BeTrue();
            Assert.Equal(JsonValueKind.Array, assets.ValueKind);

            var entries = assets.EnumerateArray().Select(e => e.GetProperty("url").GetString()).OrderBy(e => e).ToArray();
            entries.Should().Contain(e => expectedExtensions.Contains(Path.GetExtension(e)));

            var serviceWorkerFile = Path.Combine(publishOutputDirectory, "wwwroot", "serviceworkers", "my-service-worker.js");
            // Assert.FileContainsLine(result, serviceWorkerFile, "// This is the production service worker");
        }

        [Fact]
        public void PublishHostedWithPWA_ProducesAssets()
        {
            // Arrange
            var expectedExtensions = new[] { ".dll", ".pdb", ".js", ".wasm" };
            var testAppName = "BlazorHosted";
            var testInstance = CreateAspNetSdkTestAsset(testAppName);

            var publishCommand = new PublishCommand(Log, Path.Combine(testInstance.TestRoot, "blazorhosted"));
            publishCommand.Execute().Should().Pass();

            var publishOutputDirectory = publishCommand.GetOutputDirectory(DefaultTfm).ToString();

            var serviceWorkerAssetsManifest = Path.Combine(publishOutputDirectory, "wwwroot", "custom-service-worker-assets.js");
            // Trim prefix 'self.assetsManifest = ' and suffix ';'
            var manifestContents = File.ReadAllText(serviceWorkerAssetsManifest).TrimEnd()[22..^1];

            var manifestContentsJson = JsonDocument.Parse(manifestContents);
            manifestContentsJson.RootElement.TryGetProperty("assets", out var assets).Should().BeTrue();
            assets.ValueKind.Should().Be(JsonValueKind.Array);

            var entries = assets.EnumerateArray().Select(e => e.GetProperty("url").GetString()).OrderBy(e => e).ToArray();
            entries.Should().Contain(e => expectedExtensions.Contains(Path.GetExtension(e)));

            var serviceWorkerFile = Path.Combine(publishOutputDirectory, "wwwroot", "serviceworkers", "my-service-worker.js");
            // Assert.FileContainsLine(result, serviceWorkerFile, "// This is the production service worker");
        }

        [Fact]
        public void Publish_UpdatesServiceWorkerVersionHash_WhenSourcesChange()
        {
            // Arrange
            var testAppName = "BlazorWasmWithLibrary";
            var testInstance = CreateAspNetSdkTestAsset(testAppName);

            var publishCommand = new PublishCommand(Log, Path.Combine(testInstance.TestRoot, "blazorwasm"));
            publishCommand.Execute("/p:ServiceWorkerAssetsManifest=service-worker-assets.js").Should().Pass();

            var publishOutputDirectory = publishCommand.GetOutputDirectory(DefaultTfm).ToString();

            var serviceWorkerFile = Path.Combine(publishOutputDirectory, "wwwroot", "serviceworkers", "my-service-worker.js");
            var version = File.ReadAllLines(serviceWorkerFile).Last();
            var match = Regex.Match(version, "\\/\\* Manifest version: (.{8}) \\*\\/");
            match.Success.Should().BeTrue();
            match.Groups.Count.Should().Be(2);
            match.Groups[1].Value.Should().NotBeNull();

            var capture = match.Groups[1].Value;

            // Act
            var cssFile = Path.Combine(testInstance.TestRoot, "blazorwasm", "LinkToWebRoot", "css", "app.css");
            File.WriteAllText(cssFile, ".updated { }");

            // Assert
            publishCommand = new PublishCommand(Log, Path.Combine(testInstance.TestRoot, "blazorwasm"));
            publishCommand.Execute("/p:ServiceWorkerAssetsManifest=service-worker-assets.js").Should().Pass();

            var updatedVersion = File.ReadAllLines(serviceWorkerFile).Last();
            var updatedMatch = Regex.Match(updatedVersion, "\\/\\* Manifest version: (.{8}) \\*\\/");

            updatedMatch.Success.Should().BeTrue();
            updatedMatch.Groups.Count.Should().Be(2);
            updatedMatch.Groups[1].Value.Should().NotBeNull();

            var updatedCapture = updatedMatch.Groups[1].Value;
            updatedCapture.Should().NotBe(capture);
        }

        [Fact]
        public void Publish_DeterministicAcrossBuilds_WhenNoSourcesChange()
        {
            // Arrange
            var testAppName = "BlazorWasmWithLibrary";
            var testInstance = CreateAspNetSdkTestAsset(testAppName);

            var publishCommand = new PublishCommand(Log, Path.Combine(testInstance.TestRoot, "blazorwasm"));
            publishCommand.Execute("/p:ServiceWorkerAssetsManifest=service-worker-assets.js").Should().Pass();

            var publishOutputDirectory = publishCommand.GetOutputDirectory(DefaultTfm).ToString();

            var serviceWorkerFile = Path.Combine(publishOutputDirectory, "wwwroot", "serviceworkers", "my-service-worker.js");
            var version = File.ReadAllLines(serviceWorkerFile).Last();
            var match = Regex.Match(version, "\\/\\* Manifest version: (.{8}) \\*\\/");
            match.Success.Should().BeTrue();
            match.Groups.Count.Should().Be(2);
            match.Groups[1].Value.Should().NotBeNull();

            var capture = match.Groups[1].Value;

            // Act && Assert
            publishCommand = new PublishCommand(Log, Path.Combine(testInstance.TestRoot, "blazorwasm"));
            publishCommand.Execute("/p:ServiceWorkerAssetsManifest=service-worker-assets.js").Should().Pass();

            var updatedVersion = File.ReadAllLines(serviceWorkerFile).Last();
            var updatedMatch = Regex.Match(updatedVersion, "\\/\\* Manifest version: (.{8}) \\*\\/");

            updatedMatch.Success.Should().BeTrue();
            updatedMatch.Groups.Count.Should().Be(2);
            updatedMatch.Groups[1].Value.Should().NotBeNull();

            var updatedCapture = updatedMatch.Groups[1].Value;
            updatedCapture.Should().Be(capture);
        }
    }
}
