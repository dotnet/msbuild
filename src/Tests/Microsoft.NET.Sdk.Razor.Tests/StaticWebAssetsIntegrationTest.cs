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
    public class StaticWebAssetsIntegrationTest : AspNetSdkTest
    {
        public StaticWebAssetsIntegrationTest(ITestOutputHelper log) : base(log) {}

        [Fact]
        public void Build_GeneratesStaticWebAssetsManifest_Success_CreatesManifest()
        {
            var testAsset = "RazorAppWithPackageAndP2PReference";
            var projectDirectory = CreateAspNetSdkTestAsset(testAsset);

            var build = new BuildCommand(projectDirectory, "AppWithPackageAndP2PReference");
            build.Execute().Should().Pass();

            var intermediateOutputPath = build.GetIntermediateDirectory(DefaultTfm, "Debug").ToString();
            var outputPath = build.GetOutputDirectory(DefaultTfm, "Debug").ToString();

            // GenerateStaticWebAssetsManifest should generate the manifest and the cache.
            new FileInfo(Path.Combine(intermediateOutputPath, "staticwebassets", "AppWithPackageAndP2PReference.StaticWebAssets.xml")).Should().Exist();
            new FileInfo(Path.Combine(intermediateOutputPath, "staticwebassets", "AppWithPackageAndP2PReference.StaticWebAssets.Manifest.cache")).Should().Exist();
            new FileInfo(Path.Combine(outputPath, "AppWithPackageAndP2PReference.StaticWebAssets.xml")).Should().Exist();

            var path = Path.Combine(outputPath, "AppWithPackageAndP2PReference.dll");
            new FileInfo(path).Should().Exist();
            var manifest = Path.Combine(outputPath, "AppWithPackageAndP2PReference.StaticWebAssets.xml");
            new FileInfo(manifest).Should().Exist();
            var data = File.ReadAllText(manifest);
            AssertExpectedManifest(projectDirectory, data);
        }

        [Fact]
        public void Publish_CopiesStaticWebAssetsToDestinationFolder()
        {
            var testAsset = "RazorAppWithPackageAndP2PReference";
            var projectDirectory = CreateAspNetSdkTestAsset(testAsset);

            var publish = new PublishCommand(Log, Path.Combine(projectDirectory.TestRoot, "AppWithPackageAndP2PReference"));
            publish.Execute().Should().Pass();

            var publishOutputPath = publish.GetOutputDirectory(DefaultTfm, "Debug").ToString();

            new FileInfo(Path.Combine(publishOutputPath, "wwwroot", "_content", "ClassLibrary", "ClassLibrary.bundle.scp.css")).Should().Exist();
            new FileInfo(Path.Combine(publishOutputPath, "wwwroot", "_content", "ClassLibrary", "js", "project-transitive-dep.js")).Should().Exist();
            new FileInfo(Path.Combine(publishOutputPath, "wwwroot", "_content", "ClassLibrary", "js", "project-transitive-dep.v4.js")).Should().Exist();
            new FileInfo(Path.Combine(publishOutputPath, "wwwroot", "_content", "AnotherClassLib", "css", "site.css")).Should().Exist();
            new FileInfo(Path.Combine(publishOutputPath, "wwwroot", "_content", "AnotherClassLib", "js", "project-direct-dep.js")).Should().Exist();
            new FileInfo(Path.Combine(publishOutputPath, "wwwroot", "_content", "PackageLibraryDirectDependency", "css", "site.css")).Should().Exist();
            new FileInfo(Path.Combine(publishOutputPath, "wwwroot", "_content", "PackageLibraryDirectDependency", "js", "pkg-direct-dep.js")).Should().Exist();
            new FileInfo(Path.Combine(publishOutputPath, "wwwroot", "_content", "PackageLibraryTransitiveDependency", "js", "pkg-transitive-dep.js")).Should().Exist();
            new FileInfo(Path.Combine(publishOutputPath, "wwwroot", "AppWithPackageAndP2PReference.styles.css")).Should().Exist();

            // Validate that static web assets don't get published as content too on their regular path
            new FileInfo(Path.Combine(publishOutputPath, "wwwroot", "js", "project-transitive-dep.js")).Should().NotExist();
            new FileInfo(Path.Combine(publishOutputPath, "wwwroot", "js", "project-transitive-dep.v4.js")).Should().NotExist();

            // Validate that the manifest never gets copied
            new FileInfo(Path.Combine(publishOutputPath, "AppWithPackageAndP2PReference.StaticWebAssets.xml")).Should().NotExist();
        }

        [WindowsOnlyFact]
        public void Publish_CopiesStaticWebAssetsToDestinationFolder_PublishSingleFile()
        {
            var tfm = "net5.0";
            var testAsset = "RazorAppWithPackageAndP2PReference";
            var projectDirectory = CreateAspNetSdkTestAsset(testAsset, overrideTfm: tfm)
                .WithProjectChanges((path, project) =>
                {
                    if (path.Contains("AppWithPackageAndP2PReference"))
                    {
                        var ns = project.Root.Name.Namespace;
                        var itemGroup = new XElement(ns + "PropertyGroup");
                        itemGroup.Add(new XElement("RuntimeIdentifier", "win-x64"));
                        project.Root.Add(itemGroup);
                    }
                });

            var publish = new PublishCommand(Log, Path.Combine(projectDirectory.TestRoot, "AppWithPackageAndP2PReference"));
            publish.Execute("/p:PublishSingleFile=true", "/p:ReferenceLocallyBuiltPackages=true").Should().Pass();

            var publishOutputPathWithRID = publish.GetOutputDirectory(tfm, "Debug", "win-x64").ToString();

            new FileInfo(Path.Combine(publishOutputPathWithRID, "wwwroot", "_content", "ClassLibrary", "ClassLibrary.bundle.scp.css")).Should().Exist();
            new FileInfo(Path.Combine(publishOutputPathWithRID, "wwwroot", "_content", "ClassLibrary", "js", "project-transitive-dep.js")).Should().Exist();
            new FileInfo(Path.Combine(publishOutputPathWithRID, "wwwroot", "_content", "ClassLibrary", "js", "project-transitive-dep.v4.js")).Should().Exist();
            new FileInfo(Path.Combine(publishOutputPathWithRID, "wwwroot", "_content", "AnotherClassLib", "css", "site.css")).Should().Exist();
            new FileInfo(Path.Combine(publishOutputPathWithRID, "wwwroot", "_content", "AnotherClassLib", "js", "project-direct-dep.js")).Should().Exist();
            new FileInfo(Path.Combine(publishOutputPathWithRID, "wwwroot", "_content", "PackageLibraryDirectDependency", "css", "site.css")).Should().Exist();
            new FileInfo(Path.Combine(publishOutputPathWithRID, "wwwroot", "_content", "PackageLibraryDirectDependency", "js", "pkg-direct-dep.js")).Should().Exist();
            new FileInfo(Path.Combine(publishOutputPathWithRID, "wwwroot", "_content", "PackageLibraryTransitiveDependency", "js", "pkg-transitive-dep.js")).Should().Exist();
            new FileInfo(Path.Combine(publishOutputPathWithRID, "wwwroot", "AppWithPackageAndP2PReference.styles.css")).Should().Exist();

            // Validate that static web assets don't get published as content too on their regular path
            new FileInfo(Path.Combine(publishOutputPathWithRID, "wwwroot", "js", "project-transitive-dep.js")).Should().NotExist();
            new FileInfo(Path.Combine(publishOutputPathWithRID, "wwwroot", "js", "project-transitive-dep.v4.js")).Should().NotExist();

            // Validate that the manifest never gets copied
            new FileInfo(Path.Combine(publishOutputPathWithRID, "AppWithPackageAndP2PReference.StaticWebAssets.xml")).Should().NotExist();
        }

        [Fact(Skip = "https://github.com/dotnet/aspnetcore/issues/30245")]
        public void Publish_WithBuildReferencesDisabled_CopiesStaticWebAssetsToDestinationFolder()
        {
            var testAsset = "RazorAppWithPackageAndP2PReference";
            var projectDirectory = CreateAspNetSdkTestAsset(testAsset);

            var build = new BuildCommand(projectDirectory, "AppWithPackageAndP2PReference");
            build.Execute().Should().Pass();

            var publish = new PublishCommand(Log, Path.Combine(projectDirectory.TestRoot, "AppWithPackageAndP2PReference"));
            publish.Execute("/p:BuildProjectReferences=false").Should().Pass();

            var publishOutputPath = publish.GetOutputDirectory(DefaultTfm, "Debug").ToString();

            new FileInfo(Path.Combine(publishOutputPath, "wwwroot", "_content", "ClassLibrary", "ClassLibrary.bundle.scp.css")).Should().Exist();
            new FileInfo(Path.Combine(publishOutputPath, "wwwroot", "_content", "ClassLibrary", "js", "project-transitive-dep.js")).Should().Exist();
            new FileInfo(Path.Combine(publishOutputPath, "wwwroot", "_content", "ClassLibrary", "js", "project-transitive-dep.v4.js")).Should().Exist();
            new FileInfo(Path.Combine(publishOutputPath, "wwwroot", "_content", "AnotherClassLib", "css", "site.css")).Should().Exist();
            new FileInfo(Path.Combine(publishOutputPath, "wwwroot", "_content", "AnotherClassLib", "js", "project-direct-dep.js")).Should().Exist();
            new FileInfo(Path.Combine(publishOutputPath, "wwwroot", "_content", "PackageLibraryDirectDependency", "css", "site.css")).Should().Exist();
            new FileInfo(Path.Combine(publishOutputPath, "wwwroot", "_content", "PackageLibraryDirectDependency", "js", "pkg-direct-dep.js")).Should().Exist();
            new FileInfo(Path.Combine(publishOutputPath, "wwwroot", "_content", "PackageLibraryTransitiveDependency", "js", "pkg-transitive-dep.js")).Should().Exist();
            new FileInfo(Path.Combine(publishOutputPath, "wwwroot", "AppWithPackageAndP2PReference.styles.css")).Should().Exist();
        }

        [Fact(Skip = "https://github.com/dotnet/aspnetcore/issues/29224")]
        public void Publish_NoBuild_CopiesStaticWebAssetsToDestinationFolder()
        {
            var testAsset = "RazorAppWithPackageAndP2PReference";
            var projectDirectory = CreateAspNetSdkTestAsset(testAsset);

            var build = new BuildCommand(projectDirectory, "AppWithPackageAndP2PReference");
            build.Execute().Should().Pass();

            var publish = new PublishCommand(Log, Path.Combine(projectDirectory.TestRoot, "AppWithPackageAndP2PReference"));
            publish.Execute("/p:NoBuild=true", "/p:CopyRazorGenerateFilesToPublishDirectory=false").Should().Pass();

            var publishOutputPath = publish.GetOutputDirectory(DefaultTfm, "Debug").ToString();

            new FileInfo(Path.Combine(publishOutputPath, "wwwroot", "_content", "ClassLibrary", "ClassLibrary.bundle.scp.css")).Should().Exist();
            new FileInfo(Path.Combine(publishOutputPath, "wwwroot", "_content", "ClassLibrary", "js", "project-transitive-dep.js")).Should().Exist();
            new FileInfo(Path.Combine(publishOutputPath, "wwwroot", "_content", "ClassLibrary", "js", "project-transitive-dep.v4.js")).Should().Exist();
            new FileInfo(Path.Combine(publishOutputPath, "wwwroot", "_content", "AnotherClassLib", "css", "site.css")).Should().Exist();
            new FileInfo(Path.Combine(publishOutputPath, "wwwroot", "_content", "AnotherClassLib", "js", "project-direct-dep.js")).Should().Exist();
            new FileInfo(Path.Combine(publishOutputPath, "wwwroot", "_content", "PackageLibraryDirectDependency", "css", "site.css")).Should().Exist();
            new FileInfo(Path.Combine(publishOutputPath, "wwwroot", "_content", "PackageLibraryDirectDependency", "js", "pkg-direct-dep.js")).Should().Exist();
            new FileInfo(Path.Combine(publishOutputPath, "wwwroot", "_content", "PackageLibraryTransitiveDependency", "js", "pkg-transitive-dep.js")).Should().Exist();
            new FileInfo(Path.Combine(publishOutputPath, "wwwroot", "AppWithPackageAndP2PReference.styles.css")).Should().Exist();
        }

        [Fact]
        public void Build_DoesNotGenerateManifestWhen_NoStaticResourcesAvailable()
        {
            var testAsset = "RazorSimpleMvc";
            var projectDirectory = CreateAspNetSdkTestAsset(testAsset);

            var build = new BuildCommand(projectDirectory);
            build.Execute("/p:ScopedCssEnabled=false").Should().Pass();

            var intermediateOutputPath = build.GetIntermediateDirectory(DefaultTfm, "Debug").ToString();
            var outputPath = build.GetOutputDirectory(DefaultTfm, "Debug").ToString();

            // GenerateStaticWebAssetsManifest should generate the manifest.
            new FileInfo(Path.Combine(intermediateOutputPath, "staticwebassets", "SimpleMvc.StaticWebAssets.Manifest.cache")).Should().Exist();
            new FileInfo(Path.Combine(outputPath, "SimpleMvc.StaticWebAssets.xml")).Should().NotExist();

            var path = Path.Combine(outputPath, "SimpleMvc.dll");
            new FileInfo(path).Should().Exist();
        }

        [Fact]
        public void Build_Fails_WhenConflictingAssetsFoundBetweenAStaticWebAssetAndAFileInTheWebRootFolder()
        {
            var testAsset = "RazorAppWithPackageAndP2PReference";
            var projectDirectory = CreateAspNetSdkTestAsset(testAsset);

            Directory.CreateDirectory(Path.Combine(projectDirectory.Path, "AppWithPackageAndP2PReference", "wwwroot", "_content", "ClassLibrary", "js"));
            File.WriteAllText(Path.Combine(projectDirectory.Path, "AppWithPackageAndP2PReference", "wwwroot", "_content", "ClassLibrary", "js", "project-transitive-dep.js"), "console.log('transitive-dep');");

            var build = new BuildCommand(projectDirectory, "AppWithPackageAndP2PReference");
            build.Execute().Should().Fail();
        }

        [Fact]
        public void Clean_Success_RemovesManifestAndCache()
        {
            var testAsset = "RazorAppWithPackageAndP2PReference";
            var projectDirectory = CreateAspNetSdkTestAsset(testAsset);

            var build = new BuildCommand(projectDirectory, "AppWithPackageAndP2PReference");
            build.Execute().Should().Pass();

            var intermediateOutputPath = build.GetIntermediateDirectory(DefaultTfm, "Debug").ToString().ToString();

            // GenerateStaticWebAssetsManifest should generate the manifest and the cache.
            new FileInfo(Path.Combine(intermediateOutputPath, "staticwebassets", "AppWithPackageAndP2PReference.StaticWebAssets.xml")).Should().Exist();
            new FileInfo(Path.Combine(intermediateOutputPath, "staticwebassets", "AppWithPackageAndP2PReference.StaticWebAssets.Manifest.cache")).Should().Exist();

            var cleanCommand = new MSBuildCommand(Log, "Clean", build.FullPathProjectFile);
            cleanCommand.Execute().Should().Pass();

            // Clean should delete the manifest and the cache.
            new FileInfo(Path.Combine(intermediateOutputPath, "staticwebassets", "AppWithPackageAndP2PReference.StaticWebAssets.Manifest.cache")).Should().NotExist();
            new FileInfo(Path.Combine(intermediateOutputPath, "staticwebassets", "AppWithPackageAndP2PReference.StaticWebAssets.xml")).Should().NotExist();
        }

        [Fact]
        public void Rebuild_Success_RecreatesManifestAndCache()
        {
            var testAsset = "RazorAppWithPackageAndP2PReference";
            var projectDirectory = CreateAspNetSdkTestAsset(testAsset);

            // Arrange
            var build = new BuildCommand(projectDirectory, "AppWithPackageAndP2PReference");
            build.Execute().Should().Pass();

            var intermediateOutputPath = build.GetIntermediateDirectory(DefaultTfm, "Debug").ToString();
            var outputPath = build.GetOutputDirectory(DefaultTfm, "Debug").ToString();

            // GenerateStaticWebAssetsManifest should generate the manifest and the cache.
            new FileInfo(Path.Combine(intermediateOutputPath, "staticwebassets", "AppWithPackageAndP2PReference.StaticWebAssets.xml")).Should().Exist();
            new FileInfo(Path.Combine(intermediateOutputPath, "staticwebassets", "AppWithPackageAndP2PReference.StaticWebAssets.Manifest.cache")).Should().Exist();

            var directoryPath = Path.Combine(intermediateOutputPath, "staticwebassets");
            var thumbPrints = new Dictionary<string, FileThumbPrint>();
            var thumbPrintFiles = new[]
            {
                Path.Combine(directoryPath, "AppWithPackageAndP2PReference.StaticWebAssets.xml"),
                Path.Combine(directoryPath, "AppWithPackageAndP2PReference.StaticWebAssets.Manifest.cache"),
            };

            foreach (var file in thumbPrintFiles)
            {
                var thumbprint = FileThumbPrint.Create(file);
                thumbPrints[file] = thumbprint;
            }

            // Act
            // var rebuild = new MSBuildCommand(Log, "Rebuild", Path.Combine(projectDirectory.Path, "AppWithPackageAndP2PReference"));
            // rebuild.Execute().Should().Pass();

            // foreach (var file in thumbPrintFiles)
            // {
            //     var thumbprint = FileThumbPrint.Create(file);
            //     Assert.NotEqual(thumbPrints[file], thumbprint);
            // }

            var path = Path.Combine(outputPath, "AppWithPackageAndP2PReference.dll");
            new FileInfo(path).Should().Exist();
            var manifest = Path.Combine(outputPath, "AppWithPackageAndP2PReference.StaticWebAssets.xml");
            new FileInfo(manifest).Should().Exist();
            var data = File.ReadAllText(manifest);
            AssertExpectedManifest(projectDirectory, data);
        }

        [Fact]
        public void GenerateStaticWebAssetsManifest_IncrementalBuild_ReusesManifest()
        {
            var testAsset = "RazorAppWithPackageAndP2PReference";
            var projectDirectory = CreateAspNetSdkTestAsset(testAsset);

            var command = new MSBuildCommand(Log, "GenerateStaticWebAssetsManifest", projectDirectory.Path, "AppWithPackageAndP2PReference");
            command.Execute().Should().Pass();

            var intermediateOutputPath = command.GetIntermediateDirectory(DefaultTfm, "Debug").ToString();
            var outputPath = command.GetOutputDirectory(DefaultTfm, "Debug").ToString();

            // GenerateStaticWebAssetsManifest should generate the manifest and the cache.
            new FileInfo(Path.Combine(intermediateOutputPath, "staticwebassets", "AppWithPackageAndP2PReference.StaticWebAssets.xml")).Should().Exist();
            new FileInfo(Path.Combine(intermediateOutputPath, "staticwebassets", "AppWithPackageAndP2PReference.StaticWebAssets.Manifest.cache")).Should().Exist();

            var directoryPath = Path.Combine(intermediateOutputPath, "staticwebassets");
            var thumbPrints = new Dictionary<string, FileThumbPrint>();
            var thumbPrintFiles = new[]
            {
                Path.Combine(directoryPath, "AppWithPackageAndP2PReference.StaticWebAssets.xml"),
                Path.Combine(directoryPath, "AppWithPackageAndP2PReference.StaticWebAssets.Manifest.cache"),
            };

            foreach (var file in thumbPrintFiles)
            {
                var thumbprint = FileThumbPrint.Create(file);
                thumbPrints[file] = thumbprint;
            }

            // Act
            var incremental = new MSBuildCommand(Log, "GenerateStaticWebAssetsManifest", projectDirectory.Path, "AppWithPackageAndP2PReference");

            // Assert
            incremental.Execute().Should().Pass();

            foreach (var file in thumbPrintFiles)
            {
                var thumbprint = FileThumbPrint.Create(file);
                Assert.Equal(thumbPrints[file], thumbprint);
            }
        }

        private void AssertExpectedManifest(TestAsset projectDirectory, string data)
        {
            var source = projectDirectory.Path;

            var restorePath = TestContext.Current.NuGetCachePath;

            var projects = new[]
            {
                Path.Combine(restorePath, "packagelibrarytransitivedependency", "1.0.0", "build", "..", "staticwebassets") + Path.DirectorySeparatorChar,
                Path.Combine(restorePath, "packagelibrarydirectdependency", "1.0.0", "build", "..", "staticwebassets") + Path.DirectorySeparatorChar,
                Path.GetFullPath(Path.Combine(source, "AnotherClassLib", "wwwroot")) + Path.DirectorySeparatorChar,
                Path.GetFullPath(Path.Combine(source, "ClassLibrary", "wwwroot")) + Path.DirectorySeparatorChar,
                Path.GetFullPath(Path.Combine(source, "ClassLibrary", "obj", "Debug", DefaultTfm, "scopedcss", "projectbundle")) + Path.DirectorySeparatorChar,
                Path.GetFullPath(Path.Combine(source, "AppWithPackageAndP2PReference", "obj", "Debug", DefaultTfm, "scopedcss", "bundle")) + Path.DirectorySeparatorChar,
            };

            var contentRoots = new[]
            {
                $@"<ContentRoot BasePath=""_content/AnotherClassLib"" Path=""{projects[2]}"" />",
                $@"<ContentRoot BasePath=""_content/ClassLibrary"" Path=""{projects[4]}"" />",
                $@"<ContentRoot BasePath=""_content/ClassLibrary"" Path=""{projects[3]}"" />",
                $@"<ContentRoot BasePath=""_content/PackageLibraryDirectDependency"" Path=""{projects[1]}"" />",
                $@"<ContentRoot BasePath=""_content/PackageLibraryTransitiveDependency"" Path=""{projects[0]}"" />",
                $@"<ContentRoot BasePath=""/"" Path=""{projects[5]}"" />"
            };

            foreach (var item in contentRoots)
            {
                data.Should().Contain(item);
            }
        }
    }
}
