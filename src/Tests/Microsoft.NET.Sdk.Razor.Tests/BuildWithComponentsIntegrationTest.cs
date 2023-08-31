// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.CompilerServices;

namespace Microsoft.NET.Sdk.Razor.Tests
{
    public class BuildWithComponentsIntegrationTest : AspNetSdkTest
    {
        public BuildWithComponentsIntegrationTest(ITestOutputHelper log) : base(log) { }

        [CoreMSBuildOnlyFact]
        public void Build_Components_WithDotNetCoreMSBuild_Works() => Build_ComponentsWorks();

        [RequiresMSBuildVersionFact("17.8.0.37606")]
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
