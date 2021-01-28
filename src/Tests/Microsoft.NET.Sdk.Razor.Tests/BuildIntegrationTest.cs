// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Xml.Linq;
using FluentAssertions;
using Microsoft.Extensions.DependencyModel;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Microsoft.NET.TestFramework.Utilities;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.NET.Sdk.Razor.Tests
{
    public class BuildIntegrationTest : RazorSdkTest
    {
        public BuildIntegrationTest(ITestOutputHelper log) : base(log) {}

        [CoreMSBuildOnlyFact]
        public void Build_SimpleMvc_UsingDotnetMSBuildAndWithoutBuildServer_CanBuildSuccessfully()
            => Build_SimpleMvc_WithoutBuildServer_CanBuildSuccessfully();

        [FullMSBuildOnlyFactAttribute]
        public void Build_SimpleMvc_UsingDesktopMSBuildAndWithoutBuildServer_CanBuildSuccessfully()
            => Build_SimpleMvc_WithoutBuildServer_CanBuildSuccessfully();

        private void Build_SimpleMvc_WithoutBuildServer_CanBuildSuccessfully()
        {
            var testAsset = "RazorSimpleMvc";
            var projectDirectory = CreateRazorSdkTestAsset(testAsset);

            var build = new BuildCommand(projectDirectory);
            var outputPath = build.GetOutputDirectory(DefaultTfm, "Debug").ToString();

            build.Execute("/p:UseRazorBuildServer=false")
                .Should()
                .Pass()
                .And.HaveStdOutContaining($"SimpleMvc -> {Path.Combine(projectDirectory.Path, outputPath, "SimpleMvc.Views.dll")}");

            new FileInfo(Path.Combine(outputPath, "SimpleMvc.dll")).Should().Exist();
            new FileInfo(Path.Combine(outputPath, "SimpleMvc.pdb")).Should().Exist();
            new FileInfo(Path.Combine(outputPath, "SimpleMvc.Views.dll")).Should().Exist();
            new FileInfo(Path.Combine(outputPath, "SimpleMvc.Views.pdb")).Should().Exist();
        }

        [Fact]
        public void Build_SimpleMvc_NoopsWithRazorCompileOnBuild_False()
        {
            var testAsset = "RazorSimpleMvc";
            var projectDirectory = CreateRazorSdkTestAsset(testAsset);

            var build = new BuildCommand(projectDirectory);
            build.Execute("/p:RazorCompileOnBuild=false").Should().Pass();

            var outputPath = build.GetOutputDirectory(DefaultTfm, "Debug").ToString();

            new FileInfo(Path.Combine(outputPath, "SimpleMvc.dll")).Should().Exist();
            new FileInfo(Path.Combine(outputPath, "SimpleMvc.pdb")).Should().Exist();
            new FileInfo(Path.Combine(outputPath, "SimpleMvc.Views.dll")).Should().NotExist();
            new FileInfo(Path.Combine(outputPath, "SimpleMvc.Views.pdb")).Should().NotExist();
        }

        [Fact]
        public void Build_ErrorInGeneratedCode_ReportsMSBuildError()
        {
            var testAsset = "RazorSimpleMvc";
            var projectDirectory = CreateRazorSdkTestAsset(testAsset);

            var filePath = Path.Combine(projectDirectory.Path, "Views", "Home", "Index.cshtml");
            File.WriteAllText(filePath, "@{ var foo = \"\".Substring(\"bleh\"); }");

            var location = filePath + "(1,27)";
            var build = new BuildCommand(projectDirectory);
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                // Absolute paths on OSX don't work well.
                build.Execute().Should().Fail().And.HaveStdOutContaining("CS1503");
            } else {
                build.Execute().Should().Fail().And.HaveStdOutContaining("CS1503").And.HaveStdOutContaining(location);
            }

            var intermediateOutputPath = Path.Combine(build.GetBaseIntermediateDirectory().ToString(), "Debug", DefaultTfm);

            // Compilation failed without creating the views assembly
            new FileInfo(Path.Combine(intermediateOutputPath, "SimpleMvc.dll")).Should().Exist();
            new FileInfo(Path.Combine(intermediateOutputPath, "SimpleMvc.Views.dll")).Should().NotExist();
        }

        [Fact]
        public void Build_WithP2P_CopiesRazorAssembly()
        {
            var testAsset = "RazorAppWithP2PReference";
            var projectDirectory = CreateRazorSdkTestAsset(testAsset);

            var build = new BuildCommand(projectDirectory, "AppWithP2PReference");
            build.Execute().Should().Pass();

            var outputPath = build.GetOutputDirectory(DefaultTfm, "Debug").ToString();

            new FileInfo(Path.Combine(outputPath, "AppWithP2PReference.dll")).Should().Exist();
            new FileInfo(Path.Combine(outputPath, "AppWithP2PReference.pdb")).Should().Exist();
            new FileInfo(Path.Combine(outputPath, "AppWithP2PReference.Views.dll")).Should().Exist();
            new FileInfo(Path.Combine(outputPath, "AppWithP2PReference.Views.pdb")).Should().Exist();
            new FileInfo(Path.Combine(outputPath, "ClassLibrary.dll")).Should().Exist();
            new FileInfo(Path.Combine(outputPath, "ClassLibrary.pdb")).Should().Exist();
            new FileInfo(Path.Combine(outputPath, "ClassLibrary.Views.dll")).Should().Exist();
            new FileInfo(Path.Combine(outputPath, "ClassLibrary.Views.pdb")).Should().Exist();
        }

        [Fact]
        public void Build_WithViews_ProducesDepsFileWithCompilationContext_ButNoReferences()
        {
            var testAsset = "RazorSimpleMvc";
            var projectDirectory = CreateRazorSdkTestAsset(testAsset);

            var customDefine = "RazorSdkTest";
            var build = new BuildCommand(projectDirectory);
            build.Execute($"/p:DefineConstants={customDefine}").Should().Pass();

            var outputPath = build.GetOutputDirectory(DefaultTfm, "Debug").ToString();

            new FileInfo(Path.Combine(outputPath, "SimpleMvc.deps.json")).Should().Exist();
            var depsFilePath = Path.Combine(outputPath, "SimpleMvc.deps.json");
            var dependencyContext = ReadDependencyContext(depsFilePath);

            // Pick a couple of libraries and ensure they have some compile references
            var packageReference = dependencyContext.CompileLibraries.First(l => l.Name == "System.Diagnostics.DiagnosticSource");
            packageReference.Assemblies.Should().NotBeEmpty();

            var projectReference = dependencyContext.CompileLibraries.First(l => l.Name == "SimpleMvc");
            projectReference.Assemblies.Should().NotBeEmpty();

            dependencyContext.CompilationOptions.Defines.Should().Contain(customDefine);

            // Verify no refs folder is produced
            new DirectoryInfo(Path.Combine(outputPath, "publish", "refs")).Should().NotExist();
        }

        [Fact]
        public void Build_WithPreserveCompilationReferencesEnabled_ProducesRefsDirectory()
        {
            var testAsset = "RazorSimpleMvc";
            var projectDirectory = CreateRazorSdkTestAsset(testAsset)
                .WithProjectChanges(project => {
                    var ns = project.Root.Name.Namespace;
                    var itemGroup = new XElement(ns + "PropertyGroup");
                    itemGroup.Add(new XElement("PreserveCompilationReferences", "true"));
                    project.Root.Add(itemGroup);
                });

            var build = new BuildCommand(projectDirectory);
            build.Execute().Should().Pass();

            var outputPath = build.GetOutputDirectory(DefaultTfm, "Debug").ToString();

            new FileInfo(Path.Combine(outputPath, "refs", "mscorlib.dll")).Should().Exist();
        }

        [Fact]
        public void Build_AddsApplicationPartAttributes()
        {
            var testAsset = "RazorSimpleMvc";
            var projectDirectory = CreateRazorSdkTestAsset(testAsset);

            var build = new BuildCommand(projectDirectory);
            build.Execute().Should().Pass();

            var intermediateOutputPath = build.GetIntermediateDirectory(DefaultTfm, "Debug").ToString();

            var razorAssemblyInfoPath = Path.Combine(intermediateOutputPath, "SimpleMvc.RazorAssemblyInfo.cs");
            var razorTargetAssemblyInfo = Path.Combine(intermediateOutputPath, "SimpleMvc.RazorTargetAssemblyInfo.cs");

            new FileInfo(razorAssemblyInfoPath).Should().Exist();
            new FileInfo(razorAssemblyInfoPath).Should().Contain("[assembly: Microsoft.AspNetCore.Mvc.ApplicationParts.RelatedAssemblyAttribute(\"SimpleMvc.Views\")]");

            new FileInfo(razorTargetAssemblyInfo).Should().Exist();
            new FileInfo(razorTargetAssemblyInfo).Should().Contain("[assembly: System.Reflection.AssemblyTitleAttribute(\"SimpleMvc.Views\")]");
            new FileInfo(razorTargetAssemblyInfo).Should().Contain("[assembly: Microsoft.AspNetCore.Mvc.ApplicationParts.ProvideApplicationPartFactoryAttribute(\"Microsoft.AspNetCore.Mvc.ApplicationParts.CompiledRazorAssemblyApplicationPartFac\"");
        }

        [Fact]
        public void Build_DoesNotAddRelatedAssemblyPart_IfViewCompilationIsDisabled()
        {
            var testAsset = "RazorSimpleMvc";
            var projectDirectory = CreateRazorSdkTestAsset(testAsset);
            
            var build = new BuildCommand(projectDirectory);
            build.Execute("/p:RazorCompileOnBuild=false", "/p:RazorCompileOnPublish=false").Should().Pass();

            var intermediateOutputPath = build.GetIntermediateDirectory(DefaultTfm, "Debug").ToString();
            var razorAssemblyInfo = Path.Combine(intermediateOutputPath, "SimpleMvc.RazorAssemblyInfo.cs");

            new FileInfo(razorAssemblyInfo).Should().NotExist();
            new FileInfo(Path.Combine(intermediateOutputPath, "SimpleMvc.RazorTargetAssemblyInfo.cs")).Should().NotExist();
        }

        [Fact]
        public void Build_AddsRelatedAssemblyPart_IfGenerateAssemblyInfoIsFalse()
        {
            var testAsset = "RazorSimpleMvc";
            var projectDirectory = CreateRazorSdkTestAsset(testAsset);
            
            var build = new BuildCommand(projectDirectory);
            build.Execute("/p:GenerateAssemblyInfo=false").Should().Pass();

            var intermediateOutputPath = build.GetIntermediateDirectory(DefaultTfm, "Debug").ToString();
            var razorAssemblyInfo = Path.Combine(intermediateOutputPath, "SimpleMvc.RazorAssemblyInfo.cs");

            new FileInfo(razorAssemblyInfo).Should().Exist();
            new FileInfo(razorAssemblyInfo).Should().Contain("[assembly: Microsoft.AspNetCore.Mvc.ApplicationParts.RelatedAssemblyAttribute(\"SimpleMvc.Views\")]");
        }

        [Fact]
        public void Build_WithP2P_WorksWhenBuildProjectReferencesIsDisabled()
        {
            var testAsset = "RazorAppWithP2PReference";
            var projectDirectory = CreateRazorSdkTestAsset(testAsset)
                .WithProjectChanges((path, project) =>
                {
                    if (path.Contains("AppWithP2PReference")) {
                        var ns = project.Root.Name.Namespace;
                        var itemGroup = new XElement(ns + "ItemGroup");
                        itemGroup.Add(new XElement("ProjectReference", new XAttribute("Include", "..\\AnotherClassLib\\AnotherClassLib.csproj")));
                        project.Root.Add(itemGroup);
                    }
                });;

            var build = new BuildCommand(projectDirectory, "AppWithP2PReference");
            build.Execute().Should().Pass();

            var outputPath = build.GetOutputDirectory(DefaultTfm, "Debug").ToString();

            new FileInfo(Path.Combine(outputPath, "AppWithP2PReference.dll")).Should().Exist();
            new FileInfo(Path.Combine(outputPath, "AppWithP2PReference.Views.dll")).Should().Exist();
            new FileInfo(Path.Combine(outputPath, "ClassLibrary.dll")).Should().Exist();
            new FileInfo(Path.Combine(outputPath, "ClassLibrary.Views.dll")).Should().Exist();
            new FileInfo(Path.Combine(outputPath, "AnotherClassLib.dll")).Should().Exist();
            new FileInfo(Path.Combine(outputPath, "AnotherClassLib.Views.dll")).Should().Exist();

            // Force a rebuild of ClassLibrary2 by changing a file
            var class2Path = Path.Combine(projectDirectory.Path, "AnotherClassLib", "Class2.cs");
            File.AppendAllText(class2Path, Environment.NewLine + "// Some changes");

            // dotnet msbuild /p:BuildProjectReferences=false
            build = new BuildCommand(projectDirectory, "AppWithP2PReference");
            build.Execute("/p:BuildProjectReferences=false").Should().Pass();
        }

        [Fact]
        public void Build_WithP2P_Referencing21Project_Works()
        {
            // Verifies building with different versions of Razor.Tasks works. Loosely modeled after the repro
            // scenario listed in https://github.com/Microsoft/msbuild/issues/3572
            var testAsset = "RazorAppWithP2PReference";
            var projectDirectory = CreateRazorSdkTestAsset(testAsset)
                .WithProjectChanges((path, project) =>
                {
                    if (path.Contains("AppWithP2PReference"))
                    {
                        var ns = project.Root.Name.Namespace;
                        var itemGroup = new XElement(ns + "ItemGroup");
                        itemGroup.Add(new XElement("ProjectReference", new XAttribute("Include", "..\\ClassLibraryMvc21\\ClassLibraryMvc21.csproj")));
                        project.Root.Add(itemGroup);
                    }
                });

            var build = new BuildCommand(projectDirectory, "AppWithP2PReference");
            build.Execute().Should().Pass();

            var outputPath = build.GetOutputDirectory(DefaultTfm, "Debug").ToString();

            new FileInfo(Path.Combine(outputPath, "AppWithP2PReference.dll")).Should().Exist();
            new FileInfo(Path.Combine(outputPath, "AppWithP2PReference.Views.dll")).Should().Exist();
            new FileInfo(Path.Combine(outputPath, "ClassLibrary.dll")).Should().Exist();
            new FileInfo(Path.Combine(outputPath, "ClassLibrary.Views.dll")).Should().Exist();
            new FileInfo(Path.Combine(outputPath, "ClassLibraryMvc21.dll")).Should().Exist();
            new FileInfo(Path.Combine(outputPath, "ClassLibraryMvc21.Views.dll")).Should().Exist();
        }

        [Fact]
        public void Build_WithStartupObjectSpecified_Works()
        {
            var testAsset = "RazorSimpleMvc";
            var projectDirectory = CreateRazorSdkTestAsset(testAsset);

            var build = new BuildCommand(projectDirectory);
            build.Execute("/p:StartupObject=SimpleMvc.Program").Should().Pass();

            var intermediateOutputPath = build.GetIntermediateDirectory(DefaultTfm, "Debug").ToString();

            new FileInfo(Path.Combine(intermediateOutputPath, "SimpleMvc.Views.dll")).Should().Exist();
            new FileInfo(Path.Combine(intermediateOutputPath, "SimpleMvc.Views.pdb")).Should().Exist();
        }

        [Fact]
        public void Build_WithDeterministicFlagSet_OutputsDeterministicViewsAssembly()
        {
            var testAsset = "RazorSimpleMvc";
            var projectDirectory = CreateRazorSdkTestAsset(testAsset);
            
            var build = new BuildCommand(projectDirectory);
            build.Execute($"/p:Deterministic=true").Should().Pass();

            var intermediateOutputPath = build.GetIntermediateDirectory(DefaultTfm, "Debug").ToString();

            new FileInfo(Path.Combine(intermediateOutputPath, "SimpleMvc.Views.dll")).Should().Exist();
            var filePath = Path.Combine(intermediateOutputPath, "SimpleMvc.Views.dll");
            var firstAssemblyBytes = File.ReadAllBytes(filePath);

            // Build 2
            build = new BuildCommand(projectDirectory);
            build.Execute($"/p:Deterministic=true").Should().Pass();

            new FileInfo(Path.Combine(intermediateOutputPath, "SimpleMvc.Views.dll")).Should().Exist();

            var secondAssemblyBytes = File.ReadAllBytes(filePath);
            Assert.Equal(firstAssemblyBytes, secondAssemblyBytes);
        }

        private static DependencyContext ReadDependencyContext(string depsFilePath)
        {
            var reader = new DependencyContextJsonReader();
            using (var stream = File.OpenRead(depsFilePath))
            {
                return reader.Read(stream);
            }
        }
    }
}
