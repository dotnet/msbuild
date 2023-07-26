// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.NET.Sdk.BlazorWebAssembly.Tests
{
    public class VanillaWasmTests : BlazorWasmBaselineTests
    {
        public VanillaWasmTests(ITestOutputHelper log) : base(log, GenerateBaselines)
        {
        }

        [CoreMSBuildOnlyFact]
        public void Build_Works()
        {
            var testAsset = "VanillaWasm";
            var targetFramework = "net8.0";
            var testInstance = CreateAspNetSdkTestAsset(testAsset);

            var build = new BuildCommand(testInstance);
            build.Execute()
                .Should()
                .Pass();

            var buildOutputDirectory = Path.Combine(testInstance.Path, "bin", "Debug", targetFramework);

            new FileInfo(Path.Combine(buildOutputDirectory, "wwwroot", "_framework", "dotnet.js")).Should().Exist();
            new FileInfo(Path.Combine(buildOutputDirectory, "wwwroot", "_framework", "blazor.boot.json")).Should().Exist();
            new FileInfo(Path.Combine(buildOutputDirectory, "wwwroot", "_framework", "blazor.webassembly.js")).Should().NotExist();
            new FileInfo(Path.Combine(buildOutputDirectory, "wwwroot", "_framework", "dotnet.native.wasm")).Should().Exist();
        }
    }
}
