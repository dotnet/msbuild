// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Microsoft.NET.TestFramework.Utilities;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.NET.Sdk.Razor.Tests
{
    public class BuildWithComponents31IntegrationTest : AspNetSdkTest
    {
        public BuildWithComponents31IntegrationTest(ITestOutputHelper log) : base(log) {}

        [CoreMSBuildOnlyFact]
        public void Build_Components_WithDotNetCoreMSBuild_Works()
        {
            var testAsset = "Razorblazor31";
            var projectDirectory = CreateAspNetSdkTestAsset(testAsset);

            var build = new BuildCommand(projectDirectory);
            build.Execute().Should().Pass();

            string outputPath = build.GetOutputDirectory("netcoreapp3.1").ToString();

            new FileInfo(Path.Combine(outputPath, "blazor31.dll")).Should().Exist();
            new FileInfo(Path.Combine(outputPath, "blazor31.pdb")).Should().Exist();
            new FileInfo(Path.Combine(outputPath, "blazor31.Views.dll")).Should().Exist();
            new FileInfo(Path.Combine(outputPath, "blazor31.Views.pdb")).Should().Exist();
        
            new FileInfo(Path.Combine(outputPath, "blazor31.dll")).AssemblyShould().ContainType("blazor31.Pages.Index");
            new FileInfo(Path.Combine(outputPath, "blazor31.dll")).AssemblyShould().ContainType("blazor31.Shared.NavMenu");

            // Verify a regular View appears in the views dll, but not in the main assembly.
            new FileInfo(Path.Combine(outputPath, "blazor31.dll")).AssemblyShould().NotContainType("blazor31.Pages.Pages__Host");
            new FileInfo(Path.Combine(outputPath, "blazor31.Views.dll")).AssemblyShould().ContainType("blazor31.Pages.Pages__Host");
        }
    }
}
