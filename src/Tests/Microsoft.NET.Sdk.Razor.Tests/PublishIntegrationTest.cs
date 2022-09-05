// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Reflection;
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
    public class PublishIntegrationTest : AspNetSdkTest
    {
        public PublishIntegrationTest(ITestOutputHelper log) : base(log) {}

        [Fact]
        public void Publish_RazorCompileOnPublish_IsDefault()
        {
            var testAsset = "RazorSimpleMvc";
            var projectDirectory = CreateAspNetSdkTestAsset(testAsset);

            var publish = new PublishCommand(Log, projectDirectory.TestRoot);
            publish.Execute().Should().Pass();

            var outputPath = Path.Combine(projectDirectory.Path, "bin", "Debug", DefaultTfm);
            var publishOutputPath = publish.GetOutputDirectory(DefaultTfm, "Debug").ToString();

            new FileInfo(Path.Combine(publishOutputPath, "SimpleMvc.dll")).Should().Exist();
            new FileInfo(Path.Combine(publishOutputPath, "SimpleMvc.pdb")).Should().Exist();
            new FileInfo(Path.Combine(publishOutputPath, "appsettings.json")).Should().Exist();
            new FileInfo(Path.Combine(publishOutputPath, "appsettings.Development.json")).Should().Exist();

            new FileInfo(Path.Combine(outputPath, "SimpleMvc.dll")).Should().Exist();
            new FileInfo(Path.Combine(outputPath, "SimpleMvc.pdb")).Should().Exist();
            new FileInfo(Path.Combine(outputPath, "appsettings.json")).Should().Exist();
            new FileInfo(Path.Combine(outputPath, "appsettings.Development.json")).Should().Exist();

            // Verify assets get published
            new FileInfo(Path.Combine(publishOutputPath, "wwwroot", "js", "SimpleMvc.js")).Should().Exist();
            new FileInfo(Path.Combine(publishOutputPath, "wwwroot", "css", "site.css")).Should().Exist();
            new FileInfo(Path.Combine(publishOutputPath, "wwwroot", ".well-known", "security.txt")).Should().Exist();

            // By default refs and .cshtml files will not be copied on publish
            new DirectoryInfo(Path.Combine(publishOutputPath, "refs")).Should().NotExist();
            new DirectoryInfo(Path.Combine(publishOutputPath, "Views")).Should().NotExist();
            new FileInfo(Path.Combine(publishOutputPath, "wwwroot", ".not-copied", "test.txt")).Should().NotExist();
        }

        [Fact(Skip = "https://github.com/dotnet/aspnetcore/issues/28781")]
        public void Publish_WithRazorCompileOnBuildFalse_PublishesAssembly()
        {
            var testAsset = "RazorSimpleMvc";
            var projectDirectory = CreateAspNetSdkTestAsset(testAsset);

            var publish = new PublishCommand(Log, projectDirectory.TestRoot);
            publish.Execute("/p:RazorCompileOnBuild=false").Should().Pass();

            var outputPath = Path.Combine(projectDirectory.Path, "bin", "Debug", DefaultTfm);
            var publishOutputPath = publish.GetOutputDirectory(DefaultTfm, "Debug").ToString();

            new FileInfo(Path.Combine(publishOutputPath, "SimpleMvc.dll")).Should().Exist();
            new FileInfo(Path.Combine(publishOutputPath, "SimpleMvc.pdb")).Should().Exist();

            // By default refs and .cshtml files will not be copied on publish
            new DirectoryInfo(Path.Combine(publishOutputPath, "refs")).Should().NotExist();
            new DirectoryInfo(Path.Combine(publishOutputPath, "Views")).Should().NotExist();
        }

        [Fact]
        public void Publish_NoopsWith_RazorCompileOnPublishFalse()
        {
            var testAsset = "RazorSimpleMvc";
            var projectDirectory = CreateAspNetSdkTestAsset(testAsset);

            Directory.Delete(Path.Combine(projectDirectory.Path, "Views"), recursive: true);

            var publish = new PublishCommand(Log, projectDirectory.TestRoot);
            publish.Execute("/p:RazorCompileOnPublish=false").Should().Pass();

            var publishOutputPath = publish.GetOutputDirectory(DefaultTfm, "Debug").ToString();

            // Everything we do should noop - including building the app.
            new FileInfo(Path.Combine(publishOutputPath, "SimpleMvc.dll")).Should().Exist();
            new FileInfo(Path.Combine(publishOutputPath, "SimpleMvc.pdb")).Should().Exist();
        }

        [Fact]
        public void Publish_IncludeCshtmlAndRefAssemblies_CopiesFiles()
        {
            var testAsset = "RazorSimpleMvc";
            var projectDirectory = CreateAspNetSdkTestAsset(testAsset);

            var publish = new PublishCommand(Log, projectDirectory.TestRoot);
            publish.Execute("/p:CopyRazorGenerateFilesToPublishDirectory=true", "/p:CopyRefAssembliesToPublishDirectory=true").Should().Pass();

            var publishOutputPath = publish.GetOutputDirectory(DefaultTfm, "Debug").ToString();
            var intermediateOutputPath = Path.Combine(publish.GetBaseIntermediateDirectory().ToString(), "Debug", DefaultTfm);

            new FileInfo(Path.Combine(publishOutputPath, "SimpleMvc.dll")).Should().Exist();
            new FileInfo(Path.Combine(publishOutputPath, "SimpleMvc.pdb")).Should().Exist();

            // By default refs and .cshtml files will not be copied on publish
            new FileInfo(Path.Combine(publishOutputPath, "refs", "mscorlib.dll")).Should().Exist();
            new DirectoryInfo(Path.Combine(publishOutputPath, "Views")).Should().NotBeEmpty();
        }

        [Fact]
        public void Publish_WithPreserveCompilationReferencesSetInProjectFile_CopiesRefs()
        {
            var testAsset = "RazorSimpleMvc";
            var projectDirectory = CreateAspNetSdkTestAsset(testAsset)
                .WithProjectChanges(project =>
                {
                    var ns = project.Root.Name.Namespace;
                    var itemGroup = new XElement(ns + "PropertyGroup");
                    itemGroup.Add(new XElement("PreserveCompilationReferences", true));
                    project.Root.Add(itemGroup);
                });


            var publish = new PublishCommand(Log, projectDirectory.TestRoot);
            publish.Execute().Should().Pass();

            var publishOutputPath = publish.GetOutputDirectory(DefaultTfm, "Debug").ToString();

            new FileInfo(Path.Combine(publishOutputPath, "SimpleMvc.dll")).Should().Exist();
            new FileInfo(Path.Combine(publishOutputPath, "SimpleMvc.pdb")).Should().Exist();

            // By default refs and .cshtml files will not be copied on publish
            new FileInfo(Path.Combine(publishOutputPath, "refs", "mscorlib.dll")).Should().Exist();
        }

        [Fact]
        public void Publish_WithP2P_AndRazorCompileOnBuild_CopiesRazorAssembly()
        {
            var testAsset = "RazorAppWithP2PReference";
            var projectDirectory = CreateAspNetSdkTestAsset(testAsset);

            var publish = new PublishCommand(Log, Path.Combine(projectDirectory.TestRoot, "AppWithP2PReference"));
            publish.Execute().Should().Pass();

            var publishOutputPath = publish.GetOutputDirectory(DefaultTfm, "Debug").ToString();

            new FileInfo(Path.Combine(publishOutputPath, "AppWithP2PReference.dll")).Should().Exist();
            new FileInfo(Path.Combine(publishOutputPath, "AppWithP2PReference.pdb")).Should().Exist();
            new FileInfo(Path.Combine(publishOutputPath, "ClassLibrary.dll")).Should().Exist();
            new FileInfo(Path.Combine(publishOutputPath, "ClassLibrary.pdb")).Should().Exist();

            // Verify fix for https://github.com/aspnet/Razor/issues/2295. No cshtml files should be published from the app
            // or the ClassLibrary.
            new DirectoryInfo(Path.Combine(publishOutputPath, "refs")).Should().NotExist();
            new DirectoryInfo(Path.Combine(publishOutputPath, "Views")).Should().NotExist();
        }

        [Fact]
        public void Publish_WithP2P_WorksWhenBuildProjectReferencesIsDisabled()
        {
            // Simulates publishing the same way VS does by setting BuildProjectReferences=false.
            // With this flag, P2P references aren't resolved during GetCopyToPublishDirectoryItems which would cause
            // any target that uses References as inputs to not be incremental. This test verifies no Razor Sdk work
            // is performed at this time.
            var testAsset = "RazorAppWithP2PReference";
            var projectDirectory = CreateAspNetSdkTestAsset(testAsset)
                .WithProjectChanges((path, project) =>
                {
                    if (path.Contains("AppWithP2PReference"))
                    {
                        var ns = project.Root.Name.Namespace;
                        var itemGroup = new XElement(ns + "ItemGroup");
                        itemGroup.Add(new XElement("ProjectReference", new XAttribute("Include", "..\\AnotherClassLib\\AnotherClassLib.csproj")));
                        project.Root.Add(itemGroup);
                    }
                    
                });

            var build = new BuildCommand(projectDirectory, "AppWithP2PReference");
            build.Execute().Should().Pass();

            var outputPath = build.GetOutputDirectory(DefaultTfm, "Debug").ToString();

            new FileInfo(Path.Combine(outputPath, "AppWithP2PReference.dll")).Should().Exist();
            new FileInfo(Path.Combine(outputPath, "ClassLibrary.dll")).Should().Exist();
            new FileInfo(Path.Combine(outputPath, "AnotherClassLib.dll")).Should().Exist();

            // dotnet msbuild /t:Publish /p:BuildProjectReferences=false
            var publish = new PublishCommand(Log, Path.Combine(projectDirectory.TestRoot, "AppWithP2PReference"));
            publish.Execute("/p:BuildProjectReferences=false", "/p:ErrorOnDuplicatePublishOutputFiles=false").Should().Pass();

            var publishOutputPath = publish.GetOutputDirectory(DefaultTfm, "Debug").ToString();

            new FileInfo(Path.Combine(publishOutputPath, "AppWithP2PReference.dll")).Should().Exist();
            new FileInfo(Path.Combine(publishOutputPath, "AppWithP2PReference.pdb")).Should().Exist();

            new FileInfo(Path.Combine(publishOutputPath, "ClassLibrary.dll")).Should().Exist();
            new FileInfo(Path.Combine(publishOutputPath, "ClassLibrary.pdb")).Should().Exist();

            new FileInfo(Path.Combine(publishOutputPath, "AnotherClassLib.dll")).Should().Exist();
            new FileInfo(Path.Combine(publishOutputPath, "AnotherClassLib.pdb")).Should().Exist();
        }

        [Fact]
        public void Publish_WithNoBuild_CopiesAlreadyCompiledViews()
        {
            var testAsset = "RazorSimpleMvc";
            var projectDirectory = CreateAspNetSdkTestAsset(testAsset);

            // Build
            var build = new BuildCommand(projectDirectory);
            build.Execute("/p:AssemblyVersion=1.1.1.1").Should().Pass();

            var outputPath = build.GetOutputDirectory(DefaultTfm, "Debug").ToString();

            var assemblyPath = Path.Combine(outputPath, "SimpleMvc.dll");
            new FileInfo(assemblyPath).Should().Exist();
            var assemblyVersion = AssemblyName.GetAssemblyName(assemblyPath).Version;

            // Publish should copy dlls from OutputPath
            var publish = new PublishCommand(Log, projectDirectory.TestRoot);
            publish.Execute("/p:NoBuild=true").Should().Pass();

            var publishOutputPath = publish.GetOutputDirectory(DefaultTfm, "Debug").ToString();

            var publishAssemblyPath = Path.Combine(publishOutputPath, "SimpleMvc.dll");
            new FileInfo(publishAssemblyPath).Should().Exist();

            var publishAssemblyVersion = AssemblyName.GetAssemblyName(publishAssemblyPath).Version;

            Assert.Equal(assemblyVersion, publishAssemblyVersion);
        }
    }
}
