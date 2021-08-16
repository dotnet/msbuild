// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;
using System.Runtime.InteropServices;
using FluentAssertions;
using Microsoft.AspNetCore.Razor.Tasks;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Xunit;
using Xunit.Abstractions;
using System.Linq;
using System.Diagnostics;
using System.Xml.Linq;
using NuGet.Packaging;
using System;

namespace Microsoft.NET.Sdk.Razor.Tests
{
    public class StaticWebAssetsIntegrationTest : AspNetSdkBaselineTest
    {
        public StaticWebAssetsIntegrationTest(ITestOutputHelper log) : base(log, GenerateBaselines) { }

        // Build Standalone project
        [Fact]
        public void Build_GeneratesJsonManifestAndCopiesItToOutputFolder()
        {
            var expectedManifest = LoadBuildManifest();
            var testAsset = "RazorComponentApp";
            ProjectDirectory = CreateAspNetSdkTestAsset(testAsset);

            var build = new BuildCommand(ProjectDirectory);
            build.WithWorkingDirectory(ProjectDirectory.TestRoot);
            build.Execute("/bl").Should().Pass();

            var intermediateOutputPath = build.GetIntermediateDirectory(DefaultTfm, "Debug").ToString();
            var outputPath = build.GetOutputDirectory(DefaultTfm, "Debug").ToString();

            // GenerateStaticWebAssetsManifest should generate the manifest file.
            var path = Path.Combine(intermediateOutputPath, "staticwebassets.build.json");
            new FileInfo(path).Should().Exist();
            var manifest = StaticWebAssetsManifest.FromJsonBytes(File.ReadAllBytes(path));
            AssertManifest(manifest, expectedManifest);

            // GenerateStaticWebAssetsManifest should copy the file to the output folder.
            var finalPath = Path.Combine(outputPath, "ComponentApp.staticwebassets.runtime.json");
            new FileInfo(finalPath).Should().Exist();
            
            AssertManifest(StaticWebAssetsManifest.FromJsonBytes(File.ReadAllBytes(Path.Combine(intermediateOutputPath, "staticwebassets.build.json"))), expectedManifest);
            AssertBuildAssets(StaticWebAssetsManifest.FromJsonBytes(File.ReadAllBytes(Path.Combine(intermediateOutputPath, "staticwebassets.build.json"))), outputPath, intermediateOutputPath);
        }

        [Fact]
        public void Build_DoesNotUpdateManifest_WhenHasNotChanged()
        {
            var testAsset = "RazorComponentApp";
            ProjectDirectory = CreateAspNetSdkTestAsset(testAsset);

            var build = new BuildCommand(ProjectDirectory);
            build.Execute().Should().Pass();

            var intermediateOutputPath = build.GetIntermediateDirectory(DefaultTfm, "Debug").ToString();
            var outputPath = build.GetOutputDirectory(DefaultTfm, "Debug").ToString();

            // GenerateStaticWebAssetsManifest should generate the manifest file.
            var path = Path.Combine(intermediateOutputPath, "staticwebassets.build.json");
            var originalObjFile = new FileInfo(path);
            originalObjFile.Should().Exist();
            var objManifestContents = File.ReadAllText(Path.Combine(intermediateOutputPath, "staticwebassets.build.json"));
            AssertManifest(
                StaticWebAssetsManifest.FromJsonString(objManifestContents),
                LoadBuildManifest());

            // GenerateStaticWebAssetsManifest should copy the file to the output folder.
            var finalPath = Path.Combine(outputPath, "ComponentApp.staticwebassets.runtime.json");
            var originalFile = new FileInfo(finalPath);
            originalFile.Should().Exist();
            var binManifestContents = File.ReadAllText(finalPath);

            var secondBuild = new BuildCommand(ProjectDirectory);
            secondBuild.Execute().Should().Pass();

            var secondPath = Path.Combine(intermediateOutputPath, "staticwebassets.build.json");
            var secondObjFile = new FileInfo(secondPath);
            secondObjFile.Should().Exist();
            var secondObjManifest = File.ReadAllText(secondPath);
            secondObjManifest.Should().Be(objManifestContents);

            // GenerateStaticWebAssetsManifest should copy the file to the output folder.
            var secondFinalPath = Path.Combine(outputPath, "ComponentApp.staticwebassets.runtime.json");
            var secondFinalFile = new FileInfo(secondFinalPath);
            secondFinalFile.Should().Exist();
            var secondBinManifest = File.ReadAllText(secondFinalPath);
            secondBinManifest.Should().Be(binManifestContents);

            secondFinalFile.LastWriteTimeUtc.Should().Be(originalFile.LastWriteTimeUtc);
        }

        [Fact]
        public void Build_UpdatesManifest_WhenFilesChange()
        {
            var testAsset = "RazorComponentApp";
            ProjectDirectory = CreateAspNetSdkTestAsset(testAsset);

            var build = new BuildCommand(ProjectDirectory);
            build.Execute().Should().Pass();

            var intermediateOutputPath = build.GetIntermediateDirectory(DefaultTfm, "Debug").ToString();
            var outputPath = build.GetOutputDirectory(DefaultTfm, "Debug").ToString();

            // GenerateStaticWebAssetsManifest should generate the manifest file.
            var path = Path.Combine(intermediateOutputPath, "staticwebassets.build.json");
            var originalObjFile = new FileInfo(path);
            originalObjFile.Should().Exist();
            var objManifestContents = File.ReadAllText(Path.Combine(intermediateOutputPath, "staticwebassets.build.json"));
            AssertManifest(StaticWebAssetsManifest.FromJsonString(objManifestContents), LoadBuildManifest());

            // GenerateStaticWebAssetsManifest should copy the file to the output folder.
            var finalPath = Path.Combine(outputPath, "ComponentApp.staticwebassets.runtime.json");
            var originalFile = new FileInfo(finalPath);
            originalFile.Should().Exist();
            var binManifestContents = File.ReadAllText(finalPath);

            AssertBuildAssets(
                StaticWebAssetsManifest.FromJsonString(objManifestContents),
                outputPath,
                intermediateOutputPath);

            // Second build
            Directory.CreateDirectory(Path.Combine(ProjectDirectory.Path, "wwwroot"));
            File.WriteAllText(Path.Combine(ProjectDirectory.Path, "wwwroot", "index.html"), "some html");

            var secondBuild = new BuildCommand(ProjectDirectory);
            secondBuild.Execute().Should().Pass();

            var secondPath = Path.Combine(intermediateOutputPath, "staticwebassets.build.json");
            var secondObjFile = new FileInfo(secondPath);
            secondObjFile.Should().Exist();
            var secondObjManifest = File.ReadAllText(secondPath);
            AssertManifest(
                StaticWebAssetsManifest.FromJsonString(secondObjManifest),
                LoadBuildManifest("Updated"),
                "Updated");

            secondObjManifest.Should().NotBe(objManifestContents);

            // GenerateStaticWebAssetsManifest should copy the file to the output folder.
            var secondFinalPath = Path.Combine(outputPath, "ComponentApp.staticwebassets.runtime.json");
            var secondFinalFile = new FileInfo(secondFinalPath);
            secondFinalFile.Should().Exist();
            var secondBinManifest = File.ReadAllText(secondFinalPath);
            secondBinManifest.Should().NotBe(binManifestContents);

            secondObjFile.LastWriteTimeUtc.Should().NotBe(originalObjFile.LastWriteTimeUtc);
            secondFinalFile.LastWriteTimeUtc.Should().NotBe(originalFile.LastWriteTimeUtc);

            AssertBuildAssets(
                StaticWebAssetsManifest.FromJsonString(secondObjManifest),
                outputPath,
                intermediateOutputPath,
                "Updated");
        }

        // Project with references

        [Fact]
        public void BuildProjectWithReferences_GeneratesJsonManifestAndCopiesItToOutputFolder()
        {
            var testAsset = "RazorAppWithPackageAndP2PReference";
            ProjectDirectory = CreateAspNetSdkTestAsset(testAsset);

            var build = new BuildCommand(ProjectDirectory, "AppWithPackageAndP2PReference");
            build.WithWorkingDirectory(ProjectDirectory.TestRoot);
            build.Execute("/bl").Should().Pass();

            var intermediateOutputPath = build.GetIntermediateDirectory(DefaultTfm, "Debug").ToString();
            var outputPath = build.GetOutputDirectory(DefaultTfm, "Debug").ToString();

            // GenerateStaticWebAssetsManifest should generate the manifest file.
            var path = Path.Combine(intermediateOutputPath, "staticwebassets.build.json");
            new FileInfo(path).Should().Exist();
            AssertManifest(
                StaticWebAssetsManifest.FromJsonBytes(File.ReadAllBytes(path)),
                LoadBuildManifest());

            // GenerateStaticWebAssetsManifest should copy the file to the output folder.
            var finalPath = Path.Combine(outputPath, "AppWithPackageAndP2PReference.staticwebassets.runtime.json");
            new FileInfo(finalPath).Should().Exist();
            AssertManifest(
                StaticWebAssetsManifest.FromJsonBytes(File.ReadAllBytes(Path.Combine(intermediateOutputPath, "staticwebassets.build.json"))),
                LoadBuildManifest());

            AssertBuildAssets(
                StaticWebAssetsManifest.FromJsonBytes(File.ReadAllBytes(Path.Combine(intermediateOutputPath, "staticwebassets.build.json"))),
                outputPath,
                intermediateOutputPath);
        }

        [Fact]
        public void BuildProjectWithReferences_WorksWithStaticWebAssetsV1ClassLibraries()
        {
            var testAsset = "RazorAppWithPackageAndP2PReference";
            ProjectDirectory = CreateAspNetSdkTestAsset(testAsset)
                .WithProjectChanges((project, document) =>
                {
                    if (Path.GetFileName(project) == "AnotherClassLib.csproj")
                    {
                        document.Descendants("TargetFramework").Single().ReplaceNodes("netstandard2.1");
                        document.Descendants("FrameworkReference").Single().Remove();
                        document.Descendants("PropertyGroup").First().Add(new XElement("RazorLangVersion", "3.0"));
                    }
                    if (Path.GetFileName(project) == "ClassLibrary.csproj")
                    {
                        document.Descendants("TargetFramework").Single().ReplaceNodes("netstandard2.0");
                        document.Descendants("FrameworkReference").Single().Remove();
                        document.Descendants("PropertyGroup").First().Add(new XElement("RazorLangVersion", "3.0"));
                    }
                });

            // We are deleting Views and Components because we are only interested in the static web assets behavior for this test
            // and this makes it easier to validate the test.
            Directory.Delete(Path.Combine(ProjectDirectory.TestRoot, "AnotherClassLib", "Views"), recursive: true);
            Directory.Delete(Path.Combine(ProjectDirectory.TestRoot, "ClassLibrary", "Views"), recursive: true);
            Directory.Delete(Path.Combine(ProjectDirectory.TestRoot, "ClassLibrary", "Components"), recursive: true);

            var build = new BuildCommand(ProjectDirectory, "AppWithPackageAndP2PReference");
            build.WithWorkingDirectory(ProjectDirectory.TestRoot);
            build.Execute("/bl").Should().Pass();

            var intermediateOutputPath = build.GetIntermediateDirectory(DefaultTfm, "Debug").ToString();
            var outputPath = build.GetOutputDirectory(DefaultTfm, "Debug").ToString();

            // GenerateStaticWebAssetsManifest should generate the manifest file.
            var path = Path.Combine(intermediateOutputPath, "staticwebassets.build.json");
            new FileInfo(path).Should().Exist();
            AssertManifest(
                StaticWebAssetsManifest.FromJsonBytes(File.ReadAllBytes(path)),
                LoadBuildManifest());

            // GenerateStaticWebAssetsManifest should copy the file to the output folder.
            var finalPath = Path.Combine(outputPath, "AppWithPackageAndP2PReference.staticwebassets.runtime.json");
            new FileInfo(finalPath).Should().Exist();
            AssertManifest(
                StaticWebAssetsManifest.FromJsonBytes(File.ReadAllBytes(Path.Combine(intermediateOutputPath, "staticwebassets.build.json"))),
                LoadBuildManifest());

            AssertBuildAssets(
                StaticWebAssetsManifest.FromJsonBytes(File.ReadAllBytes(Path.Combine(intermediateOutputPath, "staticwebassets.build.json"))),
                outputPath,
                intermediateOutputPath);
        }

        [Fact]
        public void PublishProjectWithReferences_WorksWithStaticWebAssetsV1ClassLibraries()
        {
            var testAsset = "RazorAppWithPackageAndP2PReference";
            ProjectDirectory = CreateAspNetSdkTestAsset(testAsset)
                .WithProjectChanges((project, document) =>
                {
                    if (Path.GetFileName(project) == "AnotherClassLib.csproj")
                    {
                        document.Descendants("TargetFramework").Single().ReplaceNodes("netstandard2.1");
                        document.Descendants("FrameworkReference").Single().Remove();
                        document.Descendants("PropertyGroup").First().Add(new XElement("RazorLangVersion", "3.0"));
                    }
                    if (Path.GetFileName(project) == "ClassLibrary.csproj")
                    {
                        document.Descendants("TargetFramework").Single().ReplaceNodes("netstandard2.0");
                        document.Descendants("FrameworkReference").Single().Remove();
                        document.Descendants("PropertyGroup").First().Add(new XElement("RazorLangVersion", "3.0"));
                    }
                });

            // We are deleting Views and Components because we are only interested in the static web assets behavior for this test
            // and this makes it easier to validate the test.
            Directory.Delete(Path.Combine(ProjectDirectory.TestRoot, "AnotherClassLib", "Views"), recursive: true);
            Directory.Delete(Path.Combine(ProjectDirectory.TestRoot, "ClassLibrary", "Views"), recursive: true);
            Directory.Delete(Path.Combine(ProjectDirectory.TestRoot, "ClassLibrary", "Components"), recursive: true);

            var restore = new RestoreCommand(Log, Path.Combine(ProjectDirectory.TestRoot, "AppWithPackageAndP2PReference"));
            restore.Execute().Should().Pass();

            var publish = new PublishCommand(Log, Path.Combine(ProjectDirectory.TestRoot, "AppWithPackageAndP2PReference"));
            publish.WithWorkingDirectory(ProjectDirectory.Path);
            publish.Execute("/bl").Should().Pass();

            var intermediateOutputPath = publish.GetIntermediateDirectory(DefaultTfm, "Debug").ToString();
            var publishPath = publish.GetOutputDirectory(DefaultTfm, "Debug").ToString();

            // GenerateStaticWebAssetsManifest should generate the manifest file.
            var path = Path.Combine(intermediateOutputPath, "staticwebassets.build.json");
            new FileInfo(path).Should().Exist();
            AssertManifest(
                StaticWebAssetsManifest.FromJsonBytes(File.ReadAllBytes(path)),
                LoadBuildManifest());

            // GenerateStaticWebAssetsManifest should copy the file to the output folder.
            var finalPath = Path.Combine(publishPath, "AppWithPackageAndP2PReference.staticwebassets.runtime.json");
            new FileInfo(finalPath).Should().NotExist();

            // GenerateStaticWebAssetsPublishManifest should generate the publish manifest file.
            var intermediatePublishManifestPath = Path.Combine(intermediateOutputPath, "staticwebassets.publish.json");
            new FileInfo(path).Should().Exist();
            AssertManifest(
                StaticWebAssetsManifest.FromJsonBytes(File.ReadAllBytes(intermediatePublishManifestPath)),
                LoadPublishManifest());

            AssertPublishAssets(
                StaticWebAssetsManifest.FromJsonBytes(File.ReadAllBytes(intermediatePublishManifestPath)),
                publishPath,
                intermediateOutputPath);
        }

        // Build no dependencies
        [Fact]
        public void BuildProjectWithReferences_NoDependencies_GeneratesJsonManifestAndCopiesItToOutputFolder()
        {
            var testAsset = "RazorAppWithPackageAndP2PReference";
            ProjectDirectory = CreateAspNetSdkTestAsset(testAsset);

            var build = new BuildCommand(ProjectDirectory, "AppWithPackageAndP2PReference");
            build.Execute().Should().Pass();

            var intermediateOutputPath = build.GetIntermediateDirectory(DefaultTfm, "Debug").ToString();
            var outputPath = build.GetOutputDirectory(DefaultTfm, "Debug").ToString();

            // GenerateStaticWebAssetsManifest should generate the manifest file.
            var path = Path.Combine(intermediateOutputPath, "staticwebassets.build.json");
            new FileInfo(path).Should().Exist();
            AssertManifest(
                StaticWebAssetsManifest.FromJsonBytes(File.ReadAllBytes(path)),
                LoadBuildManifest());

            // GenerateStaticWebAssetsManifest should copy the file to the output folder.
            var finalPath = Path.Combine(outputPath, "AppWithPackageAndP2PReference.staticwebassets.runtime.json");
            new FileInfo(finalPath).Should().Exist();
            var manifest = File.ReadAllText(finalPath);
            AssertManifest(
                StaticWebAssetsManifest.FromJsonString(File.ReadAllText(path)),
                LoadBuildManifest());

            // Second build
            var secondBuild = new BuildCommand(ProjectDirectory, "AppWithPackageAndP2PReference");
            secondBuild.Execute("/p:BuildProjectReferences=false").Should().Pass();

            // GenerateStaticWebAssetsManifest should generate the manifest file.
            new FileInfo(path).Should().Exist();
            AssertManifest(
                StaticWebAssetsManifest.FromJsonBytes(File.ReadAllBytes(path)),
                LoadBuildManifest("NoDependencies"),
                "NoDependencies");

            // GenerateStaticWebAssetsManifest should copy the file to the output folder.
            new FileInfo(finalPath).Should().Exist();
            AssertManifest(
                StaticWebAssetsManifest.FromJsonBytes(File.ReadAllBytes(Path.Combine(intermediateOutputPath, "staticwebassets.build.json"))),
                LoadBuildManifest("NoDependencies"),
                "NoDependencies");

            AssertBuildAssets(
                StaticWebAssetsManifest.FromJsonBytes(File.ReadAllBytes(Path.Combine(intermediateOutputPath, "staticwebassets.build.json"))),
                outputPath,
                intermediateOutputPath,
                "NoDependencies");

            // Check that the two manifests are the same
            manifest.Should().Be(File.ReadAllText(finalPath));
        }

        // Rebuild
        [Fact]
        public void Rebuild_RegeneratesJsonManifestAndCopiesItToOutputFolder()
        {
            var testAsset = "RazorComponentApp";
            ProjectDirectory = CreateAspNetSdkTestAsset(testAsset);

            var build = new BuildCommand(ProjectDirectory);
            build.Execute().Should().Pass();

            var intermediateOutputPath = build.GetIntermediateDirectory(DefaultTfm, "Debug").ToString();
            var outputPath = build.GetOutputDirectory(DefaultTfm, "Debug").ToString();

            // GenerateStaticWebAssetsManifest should generate the manifest file.
            var path = Path.Combine(intermediateOutputPath, "staticwebassets.build.json");
            var originalObjFile = new FileInfo(path);
            originalObjFile.Should().Exist();
            var objManifestContents = File.ReadAllText(Path.Combine(intermediateOutputPath, "staticwebassets.build.json"));
            AssertManifest(StaticWebAssetsManifest.FromJsonString(objManifestContents), LoadBuildManifest());

            // GenerateStaticWebAssetsManifest should copy the file to the output folder.
            var finalPath = Path.Combine(outputPath, "ComponentApp.staticwebassets.runtime.json");
            var originalFile = new FileInfo(finalPath);
            originalFile.Should().Exist();
            var binManifestContents = File.ReadAllText(finalPath);

            // rebuild build
            var rebuild = new RebuildCommand(Log, ProjectDirectory.Path);
            rebuild.Execute().Should().Pass();

            var secondPath = Path.Combine(intermediateOutputPath, "staticwebassets.build.json");
            var secondObjFile = new FileInfo(secondPath);
            secondObjFile.Should().Exist();
            var secondObjManifest = File.ReadAllText(secondPath);
            AssertManifest(
                StaticWebAssetsManifest.FromJsonString(secondObjManifest),
                LoadBuildManifest("Rebuild"),
                "Rebuild");

            secondObjManifest.Should().Be(objManifestContents);

            // GenerateStaticWebAssetsManifest should copy the file to the output folder.
            var secondFinalPath = Path.Combine(outputPath, "ComponentApp.staticwebassets.runtime.json");
            var secondFinalFile = new FileInfo(secondFinalPath);
            secondFinalFile.Should().Exist();
            var secondBinManifest = File.ReadAllText(secondFinalPath);
            secondBinManifest.Should().Be(binManifestContents);

            secondObjFile.LastWriteTimeUtc.Should().NotBe(originalObjFile.LastWriteTimeUtc);
            secondFinalFile.LastWriteTimeUtc.Should().NotBe(originalFile.LastWriteTimeUtc);

            AssertBuildAssets(
                StaticWebAssetsManifest.FromJsonBytes(File.ReadAllBytes(Path.Combine(intermediateOutputPath, "staticwebassets.build.json"))),
                outputPath,
                intermediateOutputPath,
                "Rebuild");
        }

        // Publish
        [Fact]
        public void Publish_GeneratesPublishJsonManifestAndCopiesPublishAssets()
        {
            var testAsset = "RazorComponentApp";
            ProjectDirectory = CreateAspNetSdkTestAsset(testAsset);

            var publish = new PublishCommand(ProjectDirectory);
            publish.Execute().Should().Pass();

            var intermediateOutputPath = publish.GetIntermediateDirectory(DefaultTfm, "Debug").ToString();
            var publishPath = publish.GetOutputDirectory(DefaultTfm, "Debug").ToString();

            // GenerateStaticWebAssetsManifest should generate the build manifest file.
            var path = Path.Combine(intermediateOutputPath, "staticwebassets.build.json");
            new FileInfo(path).Should().Exist();
            var manifest = StaticWebAssetsManifest.FromJsonBytes(File.ReadAllBytes(path));
            AssertManifest(manifest, LoadBuildManifest());

            // GenerateStaticWebAssetsManifest should copy the file to the output folder.
            var finalPath = Path.Combine(publishPath, "ComponentApp.staticwebassets.runtime.json");
            new FileInfo(finalPath).Should().NotExist();

            // GenerateStaticWebAssetsManifest should generate the publish manifest file.
            var intermediatePublishManifestPath = Path.Combine(intermediateOutputPath, "staticwebassets.publish.json");
            new FileInfo(path).Should().Exist();
            var publishManifest = StaticWebAssetsManifest.FromJsonBytes(File.ReadAllBytes(intermediatePublishManifestPath));
            AssertManifest(publishManifest, LoadPublishManifest());

            AssertPublishAssets(
                StaticWebAssetsManifest.FromJsonBytes(File.ReadAllBytes(intermediatePublishManifestPath)),
                publishPath,
                intermediateOutputPath);
        }

        [Fact]
        public void Publish_PublishSingleFile_GeneratesPublishJsonManifestAndCopiesPublishAssets()
        {
            var expectedManifest = LoadBuildManifest();
            var testAsset = "RazorComponentApp";
            ProjectDirectory = CreateAspNetSdkTestAsset(testAsset);

            var publish = new PublishCommand(ProjectDirectory);
            publish.Execute($"/p:PublishSingleFile=true /p:RuntimeIdentifier={RuntimeInformation.RuntimeIdentifier}").Should().Pass();

            var intermediateOutputPath = publish.GetIntermediateDirectory(DefaultTfm, "Debug").ToString();
            var publishPath = publish.GetOutputDirectory(DefaultTfm, "Debug").ToString();

            // GenerateStaticWebAssetsManifest should generate the build manifest file.
            var path = Path.Combine(intermediateOutputPath, "staticwebassets.build.json");
            new FileInfo(path).Should().Exist();
            var manifest = StaticWebAssetsManifest.FromJsonBytes(File.ReadAllBytes(path));
            AssertManifest(manifest, expectedManifest);

            // GenerateStaticWebAssetsManifest should not copy the file to the output folder.
            var finalPath = Path.Combine(publishPath, "ComponentApp.staticwebassets.runtime.json");
            new FileInfo(finalPath).Should().NotExist();

            // GenerateStaticWebAssetsManifest should generate the publish manifest file.
            var intermediatePublishManifestPath = Path.Combine(intermediateOutputPath, "staticwebassets.publish.json");
            new FileInfo(path).Should().Exist();
            AssertManifest(
                StaticWebAssetsManifest.FromJsonBytes(File.ReadAllBytes(intermediatePublishManifestPath)),
                LoadPublishManifest());

            AssertPublishAssets(
                StaticWebAssetsManifest.FromJsonBytes(File.ReadAllBytes(intermediatePublishManifestPath)),
                publishPath,
                intermediateOutputPath);
        }

        [Fact]
        public void Publish_NoBuild_GeneratesPublishJsonManifestAndCopiesPublishAssets()
        {
            var expectedManifest = LoadBuildManifest();
            var testAsset = "RazorComponentApp";
            ProjectDirectory = CreateAspNetSdkTestAsset(testAsset);

            var build = new BuildCommand(ProjectDirectory);
            build.Execute().Should().Pass();

            var intermediateOutputPath = build.GetIntermediateDirectory(DefaultTfm, "Debug").ToString();
            var publishPath = build.GetOutputDirectory(DefaultTfm, "Debug").ToString();

            // GenerateStaticWebAssetsManifest should generate the manifest file.
            var path = Path.Combine(intermediateOutputPath, "staticwebassets.build.json");
            var objManifestFile = new FileInfo(path);
            objManifestFile.Should().Exist();
            var objManifestFileTimeStamp = objManifestFile.LastWriteTimeUtc;

            var manifest = StaticWebAssetsManifest.FromJsonBytes(File.ReadAllBytes(path));
            AssertManifest(manifest, expectedManifest);

            // GenerateStaticWebAssetsManifest should copy the file to the output folder.
            var finalPath = Path.Combine(publishPath, "ComponentApp.staticwebassets.runtime.json");
            var binManifestFile = new FileInfo(finalPath);
            binManifestFile.Should().Exist();
            var binManifestTimeStamp = binManifestFile.LastWriteTimeUtc;

            var finalManifest = StaticWebAssetsManifest.FromJsonBytes(File.ReadAllBytes(Path.Combine(intermediateOutputPath, "staticwebassets.build.json")));
            AssertManifest(finalManifest, expectedManifest);
            
            // Publish no build

            var publish = new PublishCommand(ProjectDirectory);
            publish.Execute("/p:NoBuild=true").Should().Pass();

            var secondObjTimeStamp = new FileInfo(path).LastWriteTimeUtc;

            secondObjTimeStamp.Should().Be(objManifestFileTimeStamp);

            var seconbObjManifest = StaticWebAssetsManifest.FromJsonBytes(File.ReadAllBytes(path));
            AssertManifest(seconbObjManifest, expectedManifest);

            // GenerateStaticWebAssetsManifest should copy the file to the output folder.
            var seconBinManifestPath = Path.Combine(publishPath, "ComponentApp.staticwebassets.runtime.json");
            var secondBinManifestFile = new FileInfo(seconBinManifestPath);
            secondBinManifestFile.Should().Exist();

            secondBinManifestFile.LastWriteTimeUtc.Should().Be(binManifestTimeStamp);

            var secondBinManifest = StaticWebAssetsManifest.FromJsonBytes(File.ReadAllBytes(Path.Combine(intermediateOutputPath, "staticwebassets.build.json")));
            AssertManifest(secondBinManifest, expectedManifest);

            // GenerateStaticWebAssetsManifest should generate the publish manifest file.
            var intermediatePublishManifestPath = Path.Combine(intermediateOutputPath, "staticwebassets.publish.json");
            new FileInfo(path).Should().Exist();
            AssertManifest(
                StaticWebAssetsManifest.FromJsonBytes(File.ReadAllBytes(intermediatePublishManifestPath)),
                LoadPublishManifest());

            AssertPublishAssets(
                StaticWebAssetsManifest.FromJsonBytes(File.ReadAllBytes(intermediatePublishManifestPath)),
                publishPath,
                intermediateOutputPath);
        }

        [Fact(Skip = "https://github.com/dotnet/sdk/issues/17979")]
        public void Build_DeployOnPublish_GeneratesPublishJsonManifestAndCopiesPublishAssets()
        {
            var expectedManifest = LoadBuildManifest();
            var testAsset = "RazorComponentApp";
            ProjectDirectory = CreateAspNetSdkTestAsset(testAsset);

            var build = new BuildCommand(ProjectDirectory);
            build.Execute("/p:DeployOnBuild=true").Should().Pass();

            var intermediateOutputPath = build.GetIntermediateDirectory(DefaultTfm, "Debug").ToString();
            var outputPath = build.GetOutputDirectory(DefaultTfm, "Debug").ToString();

            // GenerateStaticWebAssetsManifest should generate the build manifest file.
            var path = Path.Combine(intermediateOutputPath, "staticwebassets.build.json");
            new FileInfo(path).Should().Exist();
            var manifest = StaticWebAssetsManifest.FromJsonBytes(File.ReadAllBytes(path));
            AssertManifest(manifest, expectedManifest);

            // GenerateStaticWebAssetsManifest should copy the file to the output folder.
            var finalPath = Path.Combine(outputPath, "ComponentApp.staticwebassets.runtime.json");
            new FileInfo(finalPath).Should().Exist();
            var finalManifest = StaticWebAssetsManifest.FromJsonBytes(File.ReadAllBytes(Path.Combine(intermediateOutputPath, "staticwebassets.build.json")));
            AssertManifest(finalManifest, expectedManifest);

            // GenerateStaticWebAssetsManifest should generate the publish manifest file.
            var intermediatePublishManifestPath = Path.Combine(intermediateOutputPath, "staticwebassets.publish.json");
            new FileInfo(path).Should().Exist();
            var publishManifest = StaticWebAssetsManifest.FromJsonBytes(File.ReadAllBytes(intermediatePublishManifestPath));
            AssertManifest(publishManifest, LoadPublishManifest());
        }

        [Fact]
        public void PublishProjectWithReferences_GeneratesPublishJsonManifestAndCopiesPublishAssets()
        {
            var testAsset = "RazorAppWithPackageAndP2PReference";
            ProjectDirectory = CreateAspNetSdkTestAsset(testAsset);

            var restore = new RestoreCommand(Log, Path.Combine(ProjectDirectory.TestRoot, "AppWithPackageAndP2PReference"));
            restore.Execute().Should().Pass();

            var publish = new PublishCommand(Log, Path.Combine(ProjectDirectory.TestRoot, "AppWithPackageAndP2PReference"));
            publish.WithWorkingDirectory(ProjectDirectory.Path);
            publish.Execute("/bl").Should().Pass();

            var intermediateOutputPath = publish.GetIntermediateDirectory(DefaultTfm, "Debug").ToString();
            var publishPath = publish.GetOutputDirectory(DefaultTfm, "Debug").ToString();

            // GenerateStaticWebAssetsManifest should generate the manifest file.
            var path = Path.Combine(intermediateOutputPath, "staticwebassets.build.json");
            new FileInfo(path).Should().Exist();
            AssertManifest(
                StaticWebAssetsManifest.FromJsonBytes(File.ReadAllBytes(path)),
                LoadBuildManifest());

            // GenerateStaticWebAssetsManifest should copy the file to the output folder.
            var finalPath = Path.Combine(publishPath, "AppWithPackageAndP2PReference.staticwebassets.runtime.json");
            new FileInfo(finalPath).Should().NotExist();

            // GenerateStaticWebAssetsPublishManifest should generate the publish manifest file.
            var intermediatePublishManifestPath = Path.Combine(intermediateOutputPath, "staticwebassets.publish.json");
            new FileInfo(path).Should().Exist();
            AssertManifest(
                StaticWebAssetsManifest.FromJsonBytes(File.ReadAllBytes(intermediatePublishManifestPath)),
                LoadPublishManifest());

            AssertPublishAssets(
                StaticWebAssetsManifest.FromJsonBytes(File.ReadAllBytes(intermediatePublishManifestPath)),
                publishPath,
                intermediateOutputPath);
        }

        [Fact]
        public void PublishProjectWithReferences_PublishSingleFile_GeneratesPublishJsonManifestAndCopiesPublishAssets()
        {
            var testAsset = "RazorAppWithPackageAndP2PReference";
            ProjectDirectory = CreateAspNetSdkTestAsset(testAsset);

            var publish = new PublishCommand(Log, Path.Combine(ProjectDirectory.TestRoot, "AppWithPackageAndP2PReference"));
            publish.Execute($"/p:PublishSingleFile=true /p:RuntimeIdentifier={RuntimeInformation.RuntimeIdentifier}").Should().Pass();

            var intermediateOutputPath = publish.GetIntermediateDirectory(DefaultTfm, "Debug").ToString();
            var publishPath = publish.GetOutputDirectory(DefaultTfm, "Debug").ToString();

            // GenerateStaticWebAssetsManifest should generate the manifest file.
            var path = Path.Combine(intermediateOutputPath, "staticwebassets.build.json");
            new FileInfo(path).Should().Exist();
            AssertManifest(
                StaticWebAssetsManifest.FromJsonBytes(File.ReadAllBytes(path)),
                LoadBuildManifest());

            // GenerateStaticWebAssetsManifest should not copy the file to the output folder.
            var finalPath = Path.Combine(publishPath, "AppWithPackageAndP2PReference.staticwebassets.runtime.json");
            new FileInfo(finalPath).Should().NotExist();

            // GenerateStaticWebAssetsPublishManifest should generate the publish manifest file.
            var intermediatePublishManifestPath = Path.Combine(intermediateOutputPath, "staticwebassets.publish.json");
            new FileInfo(path).Should().Exist();
            var publishManifest = StaticWebAssetsManifest.FromJsonBytes(File.ReadAllBytes(intermediatePublishManifestPath));
            AssertManifest(publishManifest, LoadPublishManifest());

            AssertPublishAssets(
                StaticWebAssetsManifest.FromJsonBytes(File.ReadAllBytes(intermediatePublishManifestPath)),
                publishPath,
                intermediateOutputPath);
        }

        [Fact]
        public void PublishProjectWithReferences_NoBuild_GeneratesPublishJsonManifestAndCopiesPublishAssets()
        {
            var testAsset = "RazorAppWithPackageAndP2PReference";
            ProjectDirectory = CreateAspNetSdkTestAsset(testAsset);

            var build = new BuildCommand(ProjectDirectory, "AppWithPackageAndP2PReference");
            build.WithWorkingDirectory(ProjectDirectory.TestRoot);
            build.Execute("/bl").Should().Pass();

            var intermediateOutputPath = build.GetIntermediateDirectory(DefaultTfm, "Debug").ToString();
            var outputPath = build.GetOutputDirectory(DefaultTfm, "Debug").ToString();

            // GenerateStaticWebAssetsManifest should generate the manifest file.
            var path = Path.Combine(intermediateOutputPath, "staticwebassets.build.json");
            var objManifestFile = new FileInfo(path);
            objManifestFile.Should().Exist();
            var objManifestFileTimeStamp = objManifestFile.LastWriteTimeUtc;

            AssertManifest(
                StaticWebAssetsManifest.FromJsonBytes(File.ReadAllBytes(path)),
                LoadBuildManifest());

            // GenerateStaticWebAssetsManifest should copy the file to the output folder.
            var finalPath = Path.Combine(outputPath, "AppWithPackageAndP2PReference.staticwebassets.runtime.json");
            var binManifestFile = new FileInfo(finalPath);
            binManifestFile.Should().Exist();
            var binManifestTimeStamp = binManifestFile.LastWriteTimeUtc;

            AssertManifest(
                StaticWebAssetsManifest.FromJsonString(File.ReadAllText(path)),
                LoadBuildManifest());

            // Publish no build

            var publish = new PublishCommand(Log, Path.Combine(ProjectDirectory.TestRoot, "AppWithPackageAndP2PReference"));
            var publishResult = publish.Execute("/p:NoBuild=true", "/p:ErrorOnDuplicatePublishOutputFiles=false");
            var publishPath = build.GetOutputDirectory(DefaultTfm, "Debug").ToString();


            publishResult.Should().Pass();

            new FileInfo(path).LastWriteTimeUtc.Should().Be(objManifestFileTimeStamp);

            var seconbObjManifest = StaticWebAssetsManifest.FromJsonBytes(File.ReadAllBytes(path));
            AssertManifest(seconbObjManifest, LoadBuildManifest());

            // GenerateStaticWebAssetsManifest should copy the file to the output folder.
            var seconBinManifestPath = Path.Combine(outputPath, "AppWithPackageAndP2PReference.staticwebassets.runtime.json");
            var secondBinManifestFile = new FileInfo(seconBinManifestPath);
            secondBinManifestFile.Should().Exist();

            secondBinManifestFile.LastWriteTimeUtc.Should().Be(binManifestTimeStamp);

            // GenerateStaticWebAssetsManifest should generate the publish manifest file.
            var intermediatePublishManifestPath = Path.Combine(intermediateOutputPath, "staticwebassets.publish.json");
            new FileInfo(path).Should().Exist();
            var publishManifest = StaticWebAssetsManifest.FromJsonBytes(File.ReadAllBytes(intermediatePublishManifestPath));
            AssertManifest(publishManifest, LoadPublishManifest());

            AssertPublishAssets(
                StaticWebAssetsManifest.FromJsonBytes(File.ReadAllBytes(intermediatePublishManifestPath)),
                publishPath,
            intermediateOutputPath);
        }

        [Fact(Skip = "https://github.com/dotnet/sdk/issues/17979")]
        public void BuildProjectWithReferences_DeployOnPublish_GeneratesPublishJsonManifestAndCopiesPublishAssets()
        {
            var testAsset = "RazorAppWithPackageAndP2PReference";
            ProjectDirectory = CreateAspNetSdkTestAsset(testAsset);

            var build = new BuildCommand(ProjectDirectory, "AppWithPackageAndP2PReference");
            build.Execute("/p:DeployOnBuild=true").Should().Pass();

            var intermediateOutputPath = build.GetIntermediateDirectory(DefaultTfm, "Debug").ToString();
            var outputPath = build.GetOutputDirectory(DefaultTfm, "Debug").ToString();

            // GenerateStaticWebAssetsManifest should generate the manifest file.
            var path = Path.Combine(intermediateOutputPath, "staticwebassets.build.json");
            new FileInfo(path).Should().Exist();

            AssertManifest(
                StaticWebAssetsManifest.FromJsonBytes(File.ReadAllBytes(path)),
                LoadBuildManifest());

            // GenerateStaticWebAssetsManifest should copy the file to the output folder.
            var finalPath = Path.Combine(outputPath, "AppWithPackageAndP2PReference.staticwebassets.runtime.json");
            new FileInfo(finalPath).Should().Exist();

            var manifest = File.ReadAllText(finalPath);
            AssertManifest(
                StaticWebAssetsManifest.FromJsonString(manifest),
                LoadBuildManifest());

            // GenerateStaticWebAssetsManifest should generate the publish manifest file.
            var intermediatePublishManifestPath = Path.Combine(intermediateOutputPath, "staticwebassets.publish.json");
            new FileInfo(path).Should().Exist();
            var publishManifest = StaticWebAssetsManifest.FromJsonBytes(File.ReadAllBytes(intermediatePublishManifestPath));
            AssertManifest(publishManifest, LoadPublishManifest());
        }

        // Pack

        // Clean
        [Fact]
        public void Clean_RemovesManifestFrom_BuildAndIntermediateOutput()
        {
            var expectedManifest = LoadBuildManifest();
            var testAsset = "RazorComponentApp";
            ProjectDirectory = CreateAspNetSdkTestAsset(testAsset);

            var build = new BuildCommand(ProjectDirectory);
            build.Execute().Should().Pass();

            var intermediateOutputPath = build.GetIntermediateDirectory(DefaultTfm, "Debug").ToString();
            var outputPath = build.GetOutputDirectory(DefaultTfm, "Debug").ToString();

            // GenerateStaticWebAssetsManifest should generate the manifest file.
            var path = Path.Combine(intermediateOutputPath, "staticwebassets.build.json");
            new FileInfo(path).Should().Exist();
            var manifest = StaticWebAssetsManifest.FromJsonBytes(File.ReadAllBytes(path));
            AssertManifest(manifest, expectedManifest);

            // GenerateStaticWebAssetsManifest should copy the file to the output folder.
            var finalPath = Path.Combine(outputPath, "ComponentApp.staticwebassets.runtime.json");
            new FileInfo(finalPath).Should().Exist();
            var finalManifest = StaticWebAssetsManifest.FromJsonBytes(File.ReadAllBytes(Path.Combine(intermediateOutputPath, "staticwebassets.build.json")));
            AssertManifest(finalManifest, expectedManifest);

            var clean = new CleanCommand(Log, ProjectDirectory.Path);
            clean.Execute().Should().Pass();

            // Obj folder manifest does not exist
            new FileInfo(path).Should().NotExist();

            // Bin folder manifest does not exist
            new FileInfo(finalPath).Should().NotExist();
        }

        [Fact(Skip = "Pending new implementation")]
        public void Build_Fails_WhenConflictingAssetsFoundBetweenAStaticWebAssetAndAFileInTheWebRootFolder()
        {
            var testAsset = "RazorAppWithPackageAndP2PReference";
            var projectDirectory = CreateAspNetSdkTestAsset(testAsset);

            Directory.CreateDirectory(Path.Combine(projectDirectory.Path, "AppWithPackageAndP2PReference", "wwwroot", "_content", "ClassLibrary", "js"));
            File.WriteAllText(Path.Combine(projectDirectory.Path, "AppWithPackageAndP2PReference", "wwwroot", "_content", "ClassLibrary", "js", "project-transitive-dep.js"), "console.log('transitive-dep');");

            var build = new BuildCommand(projectDirectory, "AppWithPackageAndP2PReference");
            build.Execute().Should().Fail();
        }
    }
}
