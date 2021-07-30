// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml.Linq;
using FluentAssertions;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Microsoft.NET.TestFramework.Utilities;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.NET.Sdk.Razor.Tests
{
    public class PackIntegrationTest : AspNetSdkTest
    {

        public PackIntegrationTest(ITestOutputHelper log) : base(log) {}

        [Fact]
        public void Pack_NoBuild_Works_IncludesAssembly()
        {
            var testAsset = "RazorClassLibrary";
            var projectDirectory = CreateAspNetSdkTestAsset(testAsset);

            var build = new BuildCommand(projectDirectory);
            build.Execute().Should().Pass();

            var pack = new MSBuildCommand(Log, "Pack", projectDirectory.Path);
            var result = pack.Execute("/p:NoBuild=true");

            result.Should().Pass();

            var outputPath = build.GetOutputDirectory(DefaultTfm, "Debug").ToString();

            new FileInfo(Path.Combine(outputPath, "ClassLibrary.dll")).Should().Exist();
            new FileInfo(Path.Combine(outputPath, "ClassLibrary.Views.dll")).Should().NotExist();

            result.Should().NuSpecContain(
                Path.Combine(projectDirectory.Path, "obj", "Debug", "ClassLibrary.1.0.0.nuspec"),
                $"<file src=\"{Path.Combine(projectDirectory.Path, "bin", "Debug", DefaultTfm, "ClassLibrary.dll")}\" " +
                $"target=\"{Path.Combine("lib", DefaultTfm, "ClassLibrary.dll")}\" />");

            result.Should().NuSpecDoesNotContain(
                Path.Combine(projectDirectory.Path, "obj", "Debug", "ClassLibrary.1.0.0.nuspec"),
                $"<file src=\"{Path.Combine(projectDirectory.Path, "bin", "Debug", DefaultTfm, "ClassLibrary.Views.dll")}\" " +
                $"target=\"{Path.Combine("lib", DefaultTfm, "ClassLibrary.Views.dll")}\" />");

            result.Should().NuSpecDoesNotContain(
                Path.Combine(projectDirectory.Path, "obj", "Debug", "ClassLibrary.1.0.0.nuspec"),
                $"<file src=\"{Path.Combine(projectDirectory.Path, "bin", "Debug", DefaultTfm, "ClassLibrary.Views.pdb")}\" " +
                $"target=\"{Path.Combine("lib", DefaultTfm, "ClassLibrary.Views.pdb")}\" />");

            result.Should().NuSpecDoesNotContain(
                Path.Combine(projectDirectory.Path, "obj", "Debug", "ClassLibrary.1.0.0.nuspec"),
                $@"<files include=""any/{DefaultTfm}/Views/Shared/_Layout.cshtml"" buildAction=""Content"" />");

            result.Should().NuPkgContain(
                Path.Combine(projectDirectory.Path, "bin", "Debug", "ClassLibrary.1.0.0.nupkg"),
                Path.Combine("lib", DefaultTfm, "ClassLibrary.dll"));
        }

        [Fact]
        public void Pack_FailsWhenStaticWebAssetsHaveConflictingPaths()
        {
            var testAsset = "PackageLibraryDirectDependency";
            var projectDirectory = CreateAspNetSdkTestAsset(testAsset, subdirectory: "TestPackages")
                .WithProjectChanges(project =>
                {
                    var ns = project.Root.Name.Namespace;
                    var itemGroup = new XElement(ns + "ItemGroup");
                    var element = new XElement("StaticWebAsset", new XAttribute("Include", @"bundle\js\pkg-direct-dep.js"));
                    element.Add(new XElement("SourceType"));
                    element.Add(new XElement("SourceId", "PackageLibraryDirectDependency"));
                    element.Add(new XElement("ContentRoot", "$([MSBuild]::NormalizeDirectory('$(MSBuildProjectDirectory)\\bundle\\'))"));
                    element.Add(new XElement("BasePath", "_content/PackageLibraryDirectDependency"));
                    element.Add(new XElement("RelativePath", "js/pkg-direct-dep.js"));
                    itemGroup.Add(element);
                    project.Root.Add(itemGroup);
                });

            Directory.CreateDirectory(Path.Combine(projectDirectory.Path, "bundle", "js"));
            File.WriteAllText(Path.Combine(projectDirectory.Path, "bundle", "js", "pkg-direct-dep.js"), "console.log('bundle');");

            var pack = new MSBuildCommand(Log, "Pack", projectDirectory.Path, "PackageLibraryDirectDependency");
            pack.Execute().Should().Fail();
        }

        // If you modify this test, make sure you also modify the test below this one to assert that things are not included as content.
        [Fact]
        public void Pack_IncludesStaticWebAssets()
        {
            var testAsset = "PackageLibraryDirectDependency";
            var projectDirectory = CreateAspNetSdkTestAsset(testAsset, subdirectory: "TestPackages");

            var pack = new MSBuildCommand(Log, "Pack", projectDirectory.Path, "PackageLibraryDirectDependency");
            pack.WithWorkingDirectory(projectDirectory.Path);
            var result = pack.Execute("/bl");
            
            result.Should().Pass();

            var outputPath = pack.GetOutputDirectory(DefaultTfm, "Debug").ToString();

            new FileInfo(Path.Combine(outputPath, "PackageLibraryDirectDependency.dll")).Should().Exist();

            result.Should().NuPkgContain(
                Path.Combine(projectDirectory.Path, "PackageLibraryDirectDependency", "bin", "Debug", "PackageLibraryDirectDependency.1.0.0.nupkg"),
                filePaths: new[]
                {
                    Path.Combine("staticwebassets", "js", "pkg-direct-dep.js"),
                    Path.Combine("staticwebassets", "css", "site.css"),
                    Path.Combine("staticwebassets", "PackageLibraryDirectDependency.bundle.scp.css"),
                    Path.Combine("build", "Microsoft.AspNetCore.StaticWebAssets.props"),
                    Path.Combine("build", "PackageLibraryDirectDependency.props"),
                    Path.Combine("buildMultiTargeting", "PackageLibraryDirectDependency.props"),
                    Path.Combine("buildTransitive", "PackageLibraryDirectDependency.props")
                });
        }

        [Fact]
        public void Pack_DoesNotInclude_TransitiveBundleOrScopedCssAsStaticWebAsset()
        {
            var testAsset = "PackageLibraryDirectDependency";
            var projectDirectory = CreateAspNetSdkTestAsset(testAsset, subdirectory: "TestPackages");

            var pack = new MSBuildCommand(Log, "Pack", projectDirectory.Path, "PackageLibraryDirectDependency");
            pack.WithWorkingDirectory(projectDirectory.TestRoot);
            var result = pack.Execute("/bl");

            result.Should().Pass();

            var outputPath = pack.GetOutputDirectory(DefaultTfm, "Debug").ToString();

            new FileInfo(Path.Combine(outputPath, "PackageLibraryDirectDependency.dll")).Should().Exist();

            result.Should().NuPkgDoesNotContain(
                Path.Combine(projectDirectory.Path, "PackageLibraryDirectDependency", "bin", "Debug", "PackageLibraryDirectDependency.1.0.0.nupkg"),
                filePaths: new[]
                {
                    // This is to make sure we don't include the scoped css files on the package when bundling is enabled.
                    Path.Combine("staticwebassets", "Components", "App.razor.rz.scp.css"),
                    Path.Combine("staticwebassets", "PackageLibraryDirectDependency.styles.css"),
                });
        }

        [Fact]
        public void Pack_DoesNotIncludeStaticWebAssetsAsContent()
        {
            var testAsset = "PackageLibraryDirectDependency";
            var projectDirectory = CreateAspNetSdkTestAsset(testAsset, subdirectory: "TestPackages");

            var pack = new MSBuildCommand(Log, "Pack", projectDirectory.Path, "PackageLibraryDirectDependency");
            var result = pack.Execute();

            result.Should().Pass();

            var outputPath = pack.GetOutputDirectory(DefaultTfm, "Debug").ToString();

            new FileInfo(Path.Combine(outputPath, "PackageLibraryDirectDependency.dll")).Should().Exist();

            result.Should().NuPkgDoesNotContain(
                Path.Combine(projectDirectory.Path, "PackageLibraryDirectDependency", "bin", "Debug", "PackageLibraryDirectDependency.1.0.0.nupkg"),
                filePaths: new[]
                {
                    Path.Combine("content", "js", "pkg-direct-dep.js"),
                    Path.Combine("content", "css", "site.css"),
                    Path.Combine("content", "Components", "App.razor.css"),
                    // This is to make sure we don't include the unscoped css file on the package.
                    Path.Combine("content", "Components", "App.razor.css"),
                    Path.Combine("content", "Components", "App.razor.rz.scp.css"),
                    Path.Combine("contentFiles", "js", "pkg-direct-dep.js"),
                    Path.Combine("contentFiles", "css", "site.css"),
                    Path.Combine("contentFiles", "Components", "App.razor.css"),
                    Path.Combine("contentFiles", "Components", "App.razor.rz.scp.css"),
                });
        }

        [Fact]
        public void Pack_NoBuild_IncludesStaticWebAssets()
        {
            var testAsset = "PackageLibraryDirectDependency";
            var projectDirectory = CreateAspNetSdkTestAsset(testAsset, subdirectory: "TestPackages");

            var build = new BuildCommand(projectDirectory, "PackageLibraryDirectDependency");
            build.Execute().Should().Pass();

            var pack = new MSBuildCommand(Log, "Pack", projectDirectory.Path, "PackageLibraryDirectDependency");
            pack.WithWorkingDirectory(projectDirectory.TestRoot);
            var result = pack.Execute("/p:NoBuild=true", "/bl");

            var outputPath = pack.GetOutputDirectory(DefaultTfm, "Debug").ToString();

            new FileInfo(Path.Combine(outputPath, "PackageLibraryDirectDependency.dll")).Should().Exist();

            result.Should().NuPkgContain(
                Path.Combine(projectDirectory.Path, "PackageLibraryDirectDependency", "bin", "Debug", "PackageLibraryDirectDependency.1.0.0.nupkg"),
                filePaths: new[]
                {
                    Path.Combine("staticwebassets", "js", "pkg-direct-dep.js"),
                    Path.Combine("staticwebassets", "PackageLibraryDirectDependency.bundle.scp.css"),
                    Path.Combine("staticwebassets", "css", "site.css"),
                    Path.Combine("build", "Microsoft.AspNetCore.StaticWebAssets.props"),
                    Path.Combine("build", "PackageLibraryDirectDependency.props"),
                    Path.Combine("buildMultiTargeting", "PackageLibraryDirectDependency.props"),
                    Path.Combine("buildTransitive", "PackageLibraryDirectDependency.props")
                });
        }

        [Fact]
        public void Pack_DoesNotIncludeAnyCustomPropsFiles_WhenNoStaticAssetsAreAvailable()
        {
            var testAsset = "RazorComponentLibrary";
            var projectDirectory = CreateAspNetSdkTestAsset(testAsset);

            var pack = new MSBuildCommand(Log, "Pack", projectDirectory.Path);
            var result = pack.Execute();

            var outputPath = pack.GetOutputDirectory("netstandard2.0", "Debug").ToString();

            new FileInfo(Path.Combine(outputPath, "ComponentLibrary.dll")).Should().Exist();

            result.Should().NuPkgDoesNotContain(
                Path.Combine(projectDirectory.Path, "bin", "Debug", "ComponentLibrary.1.0.0.nupkg"),
                filePaths: new[]
                {
                    Path.Combine("build", "Microsoft.AspNetCore.StaticWebAssets.props"),
                    Path.Combine("build", "ComponentLibrary.props"),
                    Path.Combine("buildMultiTargeting", "ComponentLibrary.props"),
                    Path.Combine("buildTransitive", "ComponentLibrary.props")
                });
        }

        [Fact]
        public void Pack_Incremental_DoesNotRegenerateCacheAndPropsFiles()
        {
            var testAsset = "PackageLibraryTransitiveDependency";
            var projectDirectory = _testAssetsManager
                .CopyTestAsset(testAsset, testAssetSubdirectory: "TestPackages")
                .WithSource();

            var pack = new MSBuildCommand(Log, "Pack", projectDirectory.Path);
            pack.WithWorkingDirectory(projectDirectory.TestRoot);
            var result = pack.Execute("/bl");

            var intermediateOutputPath = pack.GetIntermediateDirectory("net6.0", "Debug").ToString();
            var outputPath = pack.GetOutputDirectory("net6.0", "Debug").ToString();

            new FileInfo(Path.Combine(outputPath, "PackageLibraryTransitiveDependency.dll")).Should().Exist();

            new FileInfo(Path.Combine(intermediateOutputPath, "staticwebassets", "msbuild.PackageLibraryTransitiveDependency.Microsoft.AspNetCore.StaticWebAssets.props")).Should().Exist();
            new FileInfo(Path.Combine(intermediateOutputPath, "staticwebassets", "msbuild.build.PackageLibraryTransitiveDependency.props")).Should().Exist();
            new FileInfo(Path.Combine(intermediateOutputPath, "staticwebassets", "msbuild.buildMultiTargeting.PackageLibraryTransitiveDependency.props")).Should().Exist();
            new FileInfo(Path.Combine(intermediateOutputPath, "staticwebassets", "msbuild.buildTransitive.PackageLibraryTransitiveDependency.props")).Should().Exist();

            var directoryPath = Path.Combine(intermediateOutputPath, "staticwebassets");
            var thumbPrints = new Dictionary<string, FileThumbPrint>();
            var thumbPrintFiles = new[]
            {
                Path.Combine(directoryPath, "msbuild.PackageLibraryTransitiveDependency.Microsoft.AspNetCore.StaticWebAssets.props"),
                Path.Combine(directoryPath, "msbuild.build.PackageLibraryTransitiveDependency.props"),
                Path.Combine(directoryPath, "msbuild.buildMultiTargeting.PackageLibraryTransitiveDependency.props"),
                Path.Combine(directoryPath, "msbuild.buildTransitive.PackageLibraryTransitiveDependency.props"),
            };

            foreach (var file in thumbPrintFiles)
            {
                var thumbprint = FileThumbPrint.Create(file);
                thumbPrints[file] = thumbprint;
            }

            // Act
            var incremental = new MSBuildCommand(Log, "Pack", projectDirectory.Path);
            incremental.Execute().Should().Pass();
            foreach (var file in thumbPrintFiles)
            {
                var thumbprint = FileThumbPrint.Create(file);
                Assert.Equal(thumbPrints[file], thumbprint);
            }
        }
    }
}
