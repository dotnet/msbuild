// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System.IO;
using System.Threading.Tasks;
using System.Xml.Linq;
using FluentAssertions;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Microsoft.NET.TestFramework.Utilities;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.NET.Sdk.Razor.Tests
{
    public class MvcBuildIntegrationTest22 : MvcBuildIntegrationTestLegacy
    {
        public MvcBuildIntegrationTest22(ITestOutputHelper log) : base(log) {}

        public override string TestProjectName => "SimpleMvc22";
        public override string TargetFramework => "netcoreapp2.2";

        [FullMSBuildOnlyFact]
        public void BuildProject_UsingDesktopMSBuild()
        {
            var testAsset = $"Razor{TestProjectName}";
            var project = _testAssetsManager
                .CopyTestAsset(testAsset)
                .WithSource();

            // Build
            // This is a regression test for https://github.com/dotnet/aspnetcore/issues/28333. We're trying to ensure
            // building in Desktop when DOTNET_HOST_PATH is not configured continues to work.
            // Explicitly unset it to verify a value is not being picked up as an ambient value.
            var build = new BuildCommand(project);
            build.Execute("/p:DOTNET_HOST_PATH=").Should().Pass();

            var outputPath = build.GetOutputDirectory(TargetFramework, "Debug").ToString();
            var intermediateOutputPath = build.GetIntermediateDirectory(TargetFramework, "Debug").ToString();

            new FileInfo(Path.Combine(outputPath, OutputFileName)).Should().Exist();
            new FileInfo(Path.Combine(outputPath, $"{TestProjectName}.pdb")).Should().Exist();
            new FileInfo(Path.Combine(outputPath, $"{TestProjectName}.Views.dll")).Should().Exist();
            new FileInfo(Path.Combine(outputPath, $"{TestProjectName}.Views.pdb")).Should().Exist();

            // Verify RazorTagHelper works
            new FileInfo(Path.Combine(intermediateOutputPath, $"{TestProjectName}.TagHelpers.input.cache")).Should().Exist();
            new FileInfo(Path.Combine(intermediateOutputPath, $"{TestProjectName}.TagHelpers.output.cache")).Should().Exist();
            new FileInfo(Path.Combine(intermediateOutputPath, $"{TestProjectName}.TagHelpers.output.cache")).Should().Contain(
                @"""Name"":""SimpleMvc.SimpleTagHelper"""
            );
        }
    }
}
