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
    public class ApplicationPartDiscoveryIntegrationTest : AspNetSdkTest
    {
        public ApplicationPartDiscoveryIntegrationTest(ITestOutputHelper log) : base(log) {}

        [CoreMSBuildOnlyFact]
        public void Build_ProjectWithDependencyThatReferencesMvc_AddsAttribute_WhenBuildingUsingDotnetMsbuild()
            => Build_ProjectWithDependencyThatReferencesMvc_AddsAttribute();

        private void Build_ProjectWithDependencyThatReferencesMvc_AddsAttribute()
        {
            var testAsset = "RazorAppWithP2PReference";
            var projectDirectory = CreateAspNetSdkTestAsset(testAsset);

            var build = new BuildCommand(projectDirectory, "AppWithP2PReference");
            build.Execute().Should().Pass();

            var intermediateOutputPath = build.GetIntermediateDirectory(DefaultTfm, "Debug").ToString();
            var outputPath = build.GetOutputDirectory(DefaultTfm, "Debug").ToString();

            new FileInfo(Path.Combine(intermediateOutputPath, "AppWithP2PReference.MvcApplicationPartsAssemblyInfo.cs")).Should().Exist();
            new FileInfo(Path.Combine(intermediateOutputPath, "AppWithP2PReference.MvcApplicationPartsAssemblyInfo.cs")).Should().Contain("[assembly: Microsoft.AspNetCore.Mvc.ApplicationParts.ApplicationPartAttribute(\"ClassLibrary\")]");
            new FileInfo(Path.Combine(outputPath, "AppWithP2PReference.dll")).AssemblyShould().HaveAttribute("Microsoft.AspNetCore.Mvc.ApplicationParts.ApplicationPartAttribute");
        }

        [Fact]
        public void Build_ProjectWithoutMvcReferencingDependencies_DoesNotGenerateAttribute()
        {
            var testAsset = "RazorSimpleMvc";
            var projectDirectory = CreateAspNetSdkTestAsset(testAsset);

            var build = new BuildCommand(projectDirectory);
            build.Execute().Should().Pass();

            string intermediateOutputPath = Path.Combine(build.GetBaseIntermediateDirectory().FullName, "Debug", DefaultTfm);

            File.Exists(Path.Combine(intermediateOutputPath, "SimpleMvc.MvcApplicationPartsAssemblyInfo.cs")).Should().BeFalse();;

            // We should produced a cache file for build incrementalism
            File.Exists(Path.Combine(intermediateOutputPath, "SimpleMvc.MvcApplicationPartsAssemblyInfo.cache")).Should().BeTrue();
        }

        // Regression test for https://github.com/dotnet/aspnetcore/issues/11315
        [Fact]
        public void BuildIncrementalism_CausingRecompilation_WhenApplicationPartAttributeIsGenerated()
        {
            var testAsset = "RazorAppWithP2PReference";
            var projectDirectory = CreateAspNetSdkTestAsset(testAsset);
            
            var build = new BuildCommand(projectDirectory, "AppWithP2PReference");
            build.Execute().Should().Pass();

            string intermediateOutputPath = build.GetIntermediateDirectory(DefaultTfm, "Debug").ToString();
            string outputPath = build.GetOutputDirectory(DefaultTfm, "Debug").ToString();

            var generatedAttributeFile = Path.Combine(intermediateOutputPath, "AppWithP2PReference.MvcApplicationPartsAssemblyInfo.cs");
            File.Exists(generatedAttributeFile).Should().BeTrue();
            new FileInfo(generatedAttributeFile).Should().Contain("[assembly: Microsoft.AspNetCore.Mvc.ApplicationParts.ApplicationPartAttribute(\"ClassLibrary\")]");

            var thumbPrint = FileThumbPrint.Create(generatedAttributeFile);

            // Touch a file in the main app which should call recompilation, but not the Mvc discovery tasks to re-run.
            File.AppendAllText(Path.Combine(build.ProjectRootPath, "Program.cs"), " ");
            
            build = new BuildCommand(projectDirectory, "AppWithP2PReference");
            build.Execute().Should().Pass();

            File.Exists(generatedAttributeFile).Should().BeTrue();
            Assert.Equal(thumbPrint, FileThumbPrint.Create(generatedAttributeFile));
            new FileInfo(Path.Combine(outputPath, "AppWithP2PReference.dll")).AssemblyShould().HaveAttribute("Microsoft.AspNetCore.Mvc.ApplicationParts.ApplicationPartAttribute");
        }
    }
}
