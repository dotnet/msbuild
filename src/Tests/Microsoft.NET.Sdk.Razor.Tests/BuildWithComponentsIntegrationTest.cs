// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;
using System.Runtime.CompilerServices;
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
    public class BuildWithComponentsIntegrationTest : AspNetSdkTest
    {
        public BuildWithComponentsIntegrationTest(ITestOutputHelper log) : base(log) {}

        [CoreMSBuildOnlyFact]
        public void Build_Components_WithDotNetCoreMSBuild_Works() => Build_ComponentsWorks();

        [RequiresMSBuildVersionFact("17.7.0.25102")]
        public void Build_Components_WithDesktopMSBuild_Works() => Build_ComponentsWorks();

        [Fact]
        public void Building_NetstandardComponentLibrary()
        {
            var testAsset = "RazorComponentLibrary";
            var projectDirectory = CreateAspNetSdkTestAsset(testAsset);

            // Build
            var build = new BuildCommand(projectDirectory);
            build.Execute().Should().Pass();

            string outputPath = build.GetOutputDirectory("netstandard2.0").ToString();

            new FileInfo(Path.Combine(outputPath, "ComponentLibrary.dll")).Should().Exist();
            new FileInfo(Path.Combine(outputPath, "ComponentLibrary.pdb")).Should().Exist();

            new FileInfo(Path.Combine(outputPath, "ComponentLibrary.Views.dll")).Should().NotExist();
            new FileInfo(Path.Combine(outputPath, "ComponentLibrary.Views.pdb")).Should().NotExist();
        }

        private void Build_ComponentsWorks([CallerMemberName] string callerName = "")
        {
            var testAsset = "RazorMvcWithComponents";
            var projectDirectory = CreateAspNetSdkTestAsset(testAsset, callerName);

            var build = new BuildCommand(projectDirectory);
            build.Execute().Should().Pass();

            string outputPath = build.GetOutputDirectory(DefaultTfm).ToString();

            new FileInfo(Path.Combine(outputPath, "MvcWithComponents.dll")).Should().Exist();
            new FileInfo(Path.Combine(outputPath, "MvcWithComponents.pdb")).Should().Exist();
            new FileInfo(Path.Combine(outputPath, "MvcWithComponents.Views.dll")).Should().NotExist();
            new FileInfo(Path.Combine(outputPath, "MvcWithComponents.Views.pdb")).Should().NotExist();

            new FileInfo(Path.Combine(outputPath, "MvcWithComponents.dll")).AssemblyShould().ContainType("MvcWithComponents.TestComponent");
            new FileInfo(Path.Combine(outputPath, "MvcWithComponents.dll")).AssemblyShould().ContainType("MvcWithComponents.Views.Shared.NavMenu");

            // Components should appear in the app assembly.
            new FileInfo(Path.Combine(outputPath, "MvcWithComponents.dll")).AssemblyShould().ContainType("MvcWithComponents.Components.Counter");
            // Views should also appear in the app assembly.
            new FileInfo(Path.Combine(outputPath, "MvcWithComponents.dll")).AssemblyShould().ContainType("AspNetCoreGeneratedDocument.Views_Home_Index");
        }
    }
}
