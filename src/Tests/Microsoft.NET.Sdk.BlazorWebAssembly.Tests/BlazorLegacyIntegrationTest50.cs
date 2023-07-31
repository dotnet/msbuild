// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.NET.Sdk.Razor.Tests;

namespace Microsoft.NET.Sdk.BlazorWebAssembly.Tests
{
    public class BlazorLegacyIntegrationTest50 : AspNetSdkBaselineTest
    {
        public BlazorLegacyIntegrationTest50(ITestOutputHelper log) : base(log)
        {
        }

        [CoreMSBuildOnlyFact]
        public void Build50Hosted_Works()
        {
            // Arrange
            var testAsset = "BlazorWasmHosted50";
            var targetFramework = "net5.0";
            var testInstance = CreateAspNetSdkTestAsset(testAsset);

            var build = new BuildCommand(testInstance, "Server");
            build.Execute()
                .Should()
                .Pass();

            var clientBuildOutputDirectory = Path.Combine(testInstance.Path, "Client", "bin", "Debug", targetFramework);

            new FileInfo(Path.Combine(clientBuildOutputDirectory, "wwwroot", "_framework", "blazor.boot.json")).Should().Exist();
            new FileInfo(Path.Combine(clientBuildOutputDirectory, "wwwroot", "_framework", "blazor.webassembly.js")).Should().Exist();
            new FileInfo(Path.Combine(clientBuildOutputDirectory, "wwwroot", "_framework", "dotnet.wasm")).Should().Exist();
            new FileInfo(Path.Combine(clientBuildOutputDirectory, "wwwroot", "_framework", "dotnet.timezones.blat")).Should().Exist();
            new FileInfo(Path.Combine(clientBuildOutputDirectory, "wwwroot", "_framework", "dotnet.wasm.gz")).Should().Exist();
            new FileInfo(Path.Combine(clientBuildOutputDirectory, "wwwroot", "_framework", $"{testAsset}.Client.dll")).Should().Exist();

            var serverBuildOutputDirectory = Path.Combine(testInstance.Path, "Server", "bin", "Debug", targetFramework);
            new FileInfo(Path.Combine(serverBuildOutputDirectory, $"{testAsset}.Server.dll")).Should().Exist();
            new FileInfo(Path.Combine(serverBuildOutputDirectory, $"{testAsset}.Client.dll")).Should().Exist();
            new FileInfo(Path.Combine(serverBuildOutputDirectory, $"{testAsset}.Shared.dll")).Should().Exist();

            // Verify static assets
            var serverIntermediateDirectory = Path.Combine(testInstance.Path, "Server", "obj", "Debug", targetFramework, "staticwebassets");
            var fileInfo = new FileInfo(Path.Combine(serverIntermediateDirectory, $"{testAsset}.Server.StaticWebAssets.xml"));
            fileInfo.Should().Exist();
            var content = File.ReadAllText(fileInfo.FullName);
            content.Should().Contain(Path.Combine("Client", "bin", "Debug", targetFramework, "wwwroot"));
            content.Should().Contain(Path.Combine("Client", "obj", "Debug", targetFramework, "scopedcss"));
            content.Should().Contain(Path.Combine("Client", "wwwroot"));
        }

        [CoreMSBuildOnlyFact]
        public void Publish50Hosted_Works()
        {
            // Arrange
            var testAsset = "BlazorWasmHosted50";
            var targetFramework = "net5.0";
            var testInstance = CreateAspNetSdkTestAsset(testAsset);

            var publish = new PublishCommand(testInstance, "Server");
            publish.Execute()
                .Should()
                .Pass()
                .And.NotHaveStdOutContaining("warning IL");

            var publishOutputDirectory = publish.GetOutputDirectory(targetFramework);

            publishOutputDirectory.Should().HaveFiles(new[]
            {
                $"{testAsset}.Client.dll",
                $"{testAsset}.Shared.dll",
                "wwwroot/index.html",
                "wwwroot/_framework/blazor.boot.json",
                "wwwroot/_framework/blazor.webassembly.js",
                "wwwroot/_framework/dotnet.wasm",
                "wwwroot/_framework/System.Text.Json.dll",
                $"wwwroot/_framework/{testAsset}.Client.dll",
                $"wwwroot/_framework/{testAsset}.Shared.dll",
                "wwwroot/css/app.css",
                // Verify compression works
                "wwwroot/_framework/dotnet.wasm.br",
                $"wwwroot/_framework/{testAsset}.Client.dll.br",
                "wwwroot/_framework/System.Text.Json.dll.br"
            });
        }
    }
}
