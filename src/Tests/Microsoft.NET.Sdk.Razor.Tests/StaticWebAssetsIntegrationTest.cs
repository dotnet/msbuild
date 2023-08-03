// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.AspNetCore.StaticWebAssets.Tasks;

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

            var manifest1 = StaticWebAssetsManifest.FromJsonBytes(File.ReadAllBytes(Path.Combine(intermediateOutputPath, "staticwebassets.build.json")));
            AssertManifest(manifest1, expectedManifest);
            AssertBuildAssets(manifest1, outputPath, intermediateOutputPath);
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
            var firstManifest = StaticWebAssetsManifest.FromJsonString(objManifestContents);
            AssertManifest(firstManifest, LoadBuildManifest());

            // GenerateStaticWebAssetsManifest should copy the file to the output folder.
            var finalPath = Path.Combine(outputPath, "ComponentApp.staticwebassets.runtime.json");
            var originalFile = new FileInfo(finalPath);
            originalFile.Should().Exist();
            var binManifestContents = File.ReadAllText(finalPath);

            AssertBuildAssets(
                firstManifest,
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
            var secondManifest = StaticWebAssetsManifest.FromJsonString(secondObjManifest);
            AssertManifest(
                secondManifest,
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
                secondManifest,
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
            build.Execute().Should().Pass();

            var intermediateOutputPath = build.GetIntermediateDirectory(DefaultTfm, "Debug").ToString();
            var outputPath = build.GetOutputDirectory(DefaultTfm, "Debug").ToString();

            // GenerateStaticWebAssetsManifest should generate the manifest file.
            var path = Path.Combine(intermediateOutputPath, "staticwebassets.build.json");
            new FileInfo(path).Should().Exist();
            var manifest = StaticWebAssetsManifest.FromJsonBytes(File.ReadAllBytes(path));
            AssertManifest(
                manifest,
                LoadBuildManifest());

            // GenerateStaticWebAssetsManifest should copy the file to the output folder.
            var finalPath = Path.Combine(outputPath, "AppWithPackageAndP2PReference.staticwebassets.runtime.json");
            new FileInfo(finalPath).Should().Exist();

            AssertBuildAssets(
                manifest,
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
            build.Execute().Should().Pass();

            var intermediateOutputPath = build.GetIntermediateDirectory(DefaultTfm, "Debug").ToString();
            var outputPath = build.GetOutputDirectory(DefaultTfm, "Debug").ToString();

            // GenerateStaticWebAssetsManifest should generate the manifest file.
            var path = Path.Combine(intermediateOutputPath, "staticwebassets.build.json");
            new FileInfo(path).Should().Exist();
            var manifest = StaticWebAssetsManifest.FromJsonBytes(File.ReadAllBytes(Path.Combine(intermediateOutputPath, "staticwebassets.build.json")));
            AssertManifest(
                manifest,
                LoadBuildManifest());

            // GenerateStaticWebAssetsManifest should copy the file to the output folder.
            var finalPath = Path.Combine(outputPath, "AppWithPackageAndP2PReference.staticwebassets.runtime.json");
            new FileInfo(finalPath).Should().Exist();
            AssertManifest(
                manifest,
                LoadBuildManifest());

            AssertBuildAssets(
                manifest,
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

            var publish = new PublishCommand(ProjectDirectory, "AppWithPackageAndP2PReference");
            publish.WithWorkingDirectory(ProjectDirectory.Path);
            publish.Execute().Should().Pass();

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
            var publishManifest = StaticWebAssetsManifest.FromJsonBytes(File.ReadAllBytes(intermediatePublishManifestPath));
            AssertManifest(
                publishManifest,
                LoadPublishManifest());

            AssertPublishAssets(
                publishManifest,
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
            var manifestContents = File.ReadAllText(finalPath);
            var initialManifest = StaticWebAssetsManifest.FromJsonString(File.ReadAllText(path));
            AssertManifest(
                initialManifest,
                LoadBuildManifest());

            // Second build
            var secondBuild = new BuildCommand(ProjectDirectory, "AppWithPackageAndP2PReference");
            secondBuild.Execute("/p:BuildProjectReferences=false").Should().Pass();

            // GenerateStaticWebAssetsManifest should generate the manifest file.
            new FileInfo(path).Should().Exist();
            var manifestNoDeps = StaticWebAssetsManifest.FromJsonBytes(File.ReadAllBytes(path));
            AssertManifest(
                manifestNoDeps,
                LoadBuildManifest("NoDependencies"),
                "NoDependencies");

            // GenerateStaticWebAssetsManifest should copy the file to the output folder.
            new FileInfo(finalPath).Should().Exist();
            var manifest = StaticWebAssetsManifest.FromJsonBytes(File.ReadAllBytes(Path.Combine(intermediateOutputPath, "staticwebassets.build.json")));
            AssertManifest(
                manifest,
                LoadBuildManifest("NoDependencies"),
                "NoDependencies");

            AssertBuildAssets(
                manifest,
                outputPath,
                intermediateOutputPath,
                "NoDependencies");

            // Check that the two manifests are the same
            manifestContents.Should().Be(File.ReadAllText(finalPath));
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
            var secondObjManifestContents = File.ReadAllText(secondPath);
            var secondManifest = StaticWebAssetsManifest.FromJsonString(secondObjManifestContents);
            AssertManifest(
                secondManifest,
                LoadBuildManifest("Rebuild"),
                "Rebuild");

            secondObjManifestContents.Should().Be(objManifestContents);

            // GenerateStaticWebAssetsManifest should copy the file to the output folder.
            var secondFinalPath = Path.Combine(outputPath, "ComponentApp.staticwebassets.runtime.json");
            var secondFinalFile = new FileInfo(secondFinalPath);
            secondFinalFile.Should().Exist();
            var secondBinManifest = File.ReadAllText(secondFinalPath);
            secondBinManifest.Should().Be(binManifestContents);

            secondObjFile.LastWriteTimeUtc.Should().NotBe(originalObjFile.LastWriteTimeUtc);
            secondFinalFile.LastWriteTimeUtc.Should().NotBe(originalFile.LastWriteTimeUtc);

            AssertBuildAssets(
                secondManifest,
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
                publishManifest,
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
            publish.Execute("/p:PublishSingleFile=true", $"/p:RuntimeIdentifier={RuntimeInformation.RuntimeIdentifier}").Should().Pass();

            var intermediateOutputPath = publish.GetIntermediateDirectory(DefaultTfm, "Debug", RuntimeInformation.RuntimeIdentifier).ToString();
            var publishPath = publish.GetOutputDirectory(DefaultTfm, "Debug").ToString();

            // GenerateStaticWebAssetsManifest should generate the build manifest file.
            var path = Path.Combine(intermediateOutputPath, "staticwebassets.build.json");
            new FileInfo(path).Should().Exist();
            var manifest = StaticWebAssetsManifest.FromJsonBytes(File.ReadAllBytes(path));
            AssertManifest(manifest, expectedManifest, runtimeIdentifier: RuntimeInformation.RuntimeIdentifier);

            // GenerateStaticWebAssetsManifest should not copy the file to the output folder.
            var finalPath = Path.Combine(publishPath, "ComponentApp.staticwebassets.runtime.json");
            new FileInfo(finalPath).Should().NotExist();

            // GenerateStaticWebAssetsManifest should generate the publish manifest file.
            var intermediatePublishManifestPath = Path.Combine(intermediateOutputPath, "staticwebassets.publish.json");
            new FileInfo(path).Should().Exist();
            var publishManifest = StaticWebAssetsManifest.FromJsonBytes(File.ReadAllBytes(intermediatePublishManifestPath));
            AssertManifest(
                publishManifest,
                LoadPublishManifest(),
                runtimeIdentifier: RuntimeInformation.RuntimeIdentifier);

            AssertPublishAssets(
                publishManifest,
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
            var publishManifest = StaticWebAssetsManifest.FromJsonBytes(File.ReadAllBytes(intermediatePublishManifestPath));
            AssertManifest(
                publishManifest,
                LoadPublishManifest());

            AssertPublishAssets(
                publishManifest,
                publishPath,
                intermediateOutputPath);
        }

        [Fact]
        public void Build_DeployOnBuild_GeneratesPublishJsonManifestAndCopiesPublishAssets()
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
            AssertManifest(manifest, LoadBuildManifest());

            // GenerateStaticWebAssetsManifest should copy the file to the output folder.
            var finalPath = Path.Combine(outputPath, "ComponentApp.staticwebassets.runtime.json");
            new FileInfo(finalPath).Should().Exist();

            // GenerateStaticWebAssetsManifest should generate the publish manifest file.
            var intermediatePublishManifestPath = Path.Combine(intermediateOutputPath, "staticwebassets.publish.json");
            new FileInfo(path).Should().Exist();
            var publishManifest = StaticWebAssetsManifest.FromJsonBytes(File.ReadAllBytes(intermediatePublishManifestPath));
            AssertManifest(publishManifest, LoadPublishManifest());

            AssertPublishAssets(
                publishManifest,
                Path.Combine(outputPath, "publish"),
                intermediateOutputPath);
        }

        [Fact]
        public void PublishProjectWithReferences_GeneratesPublishJsonManifestAndCopiesPublishAssets()
        {
            var testAsset = "RazorAppWithPackageAndP2PReference";
            ProjectDirectory = CreateAspNetSdkTestAsset(testAsset);

            var restore = new RestoreCommand(Log, Path.Combine(ProjectDirectory.TestRoot, "AppWithPackageAndP2PReference"));
            restore.Execute().Should().Pass();

            var publish = new PublishCommand(ProjectDirectory, "AppWithPackageAndP2PReference");
            publish.WithWorkingDirectory(ProjectDirectory.Path);
            publish.Execute().Should().Pass();

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
            var publishManifest = StaticWebAssetsManifest.FromJsonBytes(File.ReadAllBytes(intermediatePublishManifestPath));
            AssertManifest(
                publishManifest,
                LoadPublishManifest());

            AssertPublishAssets(
                publishManifest,
                publishPath,
                intermediateOutputPath);
        }

        [Fact]
        public void PublishProjectWithReferences_PublishSingleFile_GeneratesPublishJsonManifestAndCopiesPublishAssets()
        {
            var testAsset = "RazorAppWithPackageAndP2PReference";
            ProjectDirectory = CreateAspNetSdkTestAsset(testAsset);

            var publish = new PublishCommand(ProjectDirectory, "AppWithPackageAndP2PReference");
            publish.Execute("/p:PublishSingleFile=true", $"/p:RuntimeIdentifier={RuntimeInformation.RuntimeIdentifier}").Should().Pass();

            var intermediateOutputPath = publish.GetIntermediateDirectory(DefaultTfm, "Debug", RuntimeInformation.RuntimeIdentifier).ToString();
            var publishPath = publish.GetOutputDirectory(DefaultTfm, "Debug").ToString();

            // GenerateStaticWebAssetsManifest should generate the manifest file.
            var path = Path.Combine(intermediateOutputPath, "staticwebassets.build.json");
            new FileInfo(path).Should().Exist();
            AssertManifest(
                StaticWebAssetsManifest.FromJsonBytes(File.ReadAllBytes(path)),
                LoadBuildManifest(),
                runtimeIdentifier: RuntimeInformation.RuntimeIdentifier);

            // GenerateStaticWebAssetsManifest should not copy the file to the output folder.
            var finalPath = Path.Combine(publishPath, "AppWithPackageAndP2PReference.staticwebassets.runtime.json");
            new FileInfo(finalPath).Should().NotExist();

            // GenerateStaticWebAssetsPublishManifest should generate the publish manifest file.
            var intermediatePublishManifestPath = Path.Combine(intermediateOutputPath, "staticwebassets.publish.json");
            new FileInfo(path).Should().Exist();
            var publishManifest = StaticWebAssetsManifest.FromJsonBytes(File.ReadAllBytes(intermediatePublishManifestPath));
            AssertManifest(publishManifest, LoadPublishManifest(), runtimeIdentifier: RuntimeInformation.RuntimeIdentifier);

            AssertPublishAssets(
                publishManifest,
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
            build.Execute().Should().Pass();

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
                publishManifest,
                publishPath,
            intermediateOutputPath);
        }

        [Fact]
        public void PublishProjectWithReferences_AppendTargetFrameworkToOutputPathFalse_GeneratesPublishJsonManifestAndCopiesPublishAssets()
        {
            var testAsset = "RazorAppWithPackageAndP2PReference";
            ProjectDirectory = CreateAspNetSdkTestAsset(testAsset);

            var restore = new RestoreCommand(Log, Path.Combine(ProjectDirectory.TestRoot, "AppWithPackageAndP2PReference"));
            restore.Execute().Should().Pass();

            var publish = new PublishCommand(ProjectDirectory, "AppWithPackageAndP2PReference");
            publish.WithWorkingDirectory(ProjectDirectory.Path);
            publish.Execute("/p:AppendTargetFrameworkToOutputPath=false").Should().Pass();

            //  Hard code output paths here to account for AppendTargetFrameworkToOutputPath=false
            var intermediateOutputPath = Path.Combine(ProjectDirectory.Path, "AppWithPackageAndP2PReference", "obj", "Debug");
            var publishPath = Path.Combine(ProjectDirectory.Path, "AppWithPackageAndP2PReference", "bin", "Debug", "publish");

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
            var publishManifest = StaticWebAssetsManifest.FromJsonBytes(File.ReadAllBytes(intermediatePublishManifestPath));
            AssertManifest(
                publishManifest,
                LoadPublishManifest());

            AssertPublishAssets(
                publishManifest,
                publishPath,
                intermediateOutputPath);
        }

        [Fact]
        public void BuildProjectWithReferences_DeployOnBuild_GeneratesPublishJsonManifestAndCopiesPublishAssets()
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

            // GenerateStaticWebAssetsManifest should generate the publish manifest file.
            var intermediatePublishManifestPath = Path.Combine(intermediateOutputPath, "staticwebassets.publish.json");
            new FileInfo(path).Should().Exist();
            var publishManifest = StaticWebAssetsManifest.FromJsonBytes(File.ReadAllBytes(intermediatePublishManifestPath));
            AssertManifest(publishManifest, LoadPublishManifest());

            AssertPublishAssets(
                publishManifest,
                Path.Combine(outputPath, "publish"),
                intermediateOutputPath);
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

            var pack = new MSBuildCommand(projectDirectory, "Pack", "PackageLibraryDirectDependency");
            pack.WithWorkingDirectory(projectDirectory.Path);
            var result = pack.Execute();

            result.Should().Pass();

            var outputPath = pack.GetOutputDirectory(DefaultTfm, "Debug").ToString();

            new FileInfo(Path.Combine(outputPath, "PackageLibraryDirectDependency.dll")).Should().Exist();

            result.Should().NuPkgContain(
                Path.Combine(pack.GetPackageDirectory().FullName, "PackageLibraryDirectDependency.1.0.0.nupkg"),
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
        public void Pack_NoAssets_DoesNothing()
        {
            var testAsset = "PackageLibraryNoStaticAssets";
            var projectDirectory = CreateAspNetSdkTestAsset(testAsset, subdirectory: "TestPackages");

            var pack = new MSBuildCommand(projectDirectory, "Pack");
            pack.WithWorkingDirectory(projectDirectory.Path);
            var result = pack.Execute();

            result.Should().Pass();

            var outputPath = pack.GetOutputDirectory(DefaultTfm, "Debug").ToString();

            new FileInfo(Path.Combine(outputPath, "PackageLibraryNoStaticAssets.dll")).Should().Exist();

            result.Should().NuPkgDoesNotContain(
                Path.Combine(pack.GetPackageDirectory().FullName, "PackageLibraryNoStaticAssets.1.0.0.nupkg"),
                filePaths: new[]
                {
                    Path.Combine("staticwebassets"),
                    Path.Combine("build", "Microsoft.AspNetCore.StaticWebAssets.props"),
                    Path.Combine("build", "PackageLibraryNoStaticAssets.props"),
                    Path.Combine("buildMultiTargeting", "PackageLibraryNoStaticAssets.props"),
                    Path.Combine("buildTransitive", "PackageLibraryNoStaticAssets.props")
                });
        }

        [Fact]
        public void Pack_NoAssets_Multitargeting_DoesNothing()
        {
            var testAsset = "PackageLibraryNoStaticAssets";
            var projectDirectory = CreateAspNetSdkTestAsset(testAsset, subdirectory: "TestPackages");

            projectDirectory.WithProjectChanges(project =>
            {
                var tfm = project.Root.Descendants("TargetFramework").Single();
                tfm.Name = "TargetFrameworks";
                tfm.Value="net6.0;" + DefaultTfm;
            });

            var pack = new MSBuildCommand(Log, "Pack", projectDirectory.Path);
            pack.WithWorkingDirectory(projectDirectory.Path);
            var result = pack.Execute();

            result.Should().Pass();

            var outputPath = pack.GetOutputDirectory(DefaultTfm, "Debug").ToString();

            new FileInfo(Path.Combine(outputPath, "PackageLibraryNoStaticAssets.dll")).Should().Exist();

            result.Should().NuPkgDoesNotContain(
                Path.Combine(projectDirectory.Path, "bin", "Debug", "PackageLibraryNoStaticAssets.1.0.0.nupkg"),
                filePaths: new[]
                {
                    Path.Combine("staticwebassets"),
                    Path.Combine("build", "Microsoft.AspNetCore.StaticWebAssets.props"),
                    Path.Combine("build", "PackageLibraryNoStaticAssets.props"),
                    Path.Combine("buildMultiTargeting", "PackageLibraryNoStaticAssets.props"),
                    Path.Combine("buildTransitive", "PackageLibraryNoStaticAssets.props")
                });
        }

        [Fact]
        public void Pack_Incremental_IncludesStaticWebAssets()
        {
            var testAsset = "PackageLibraryDirectDependency";
            var projectDirectory = CreateAspNetSdkTestAsset(testAsset, subdirectory: "TestPackages");

            var pack = new MSBuildCommand(projectDirectory, "Pack", "PackageLibraryDirectDependency");
            pack.WithWorkingDirectory(projectDirectory.Path);
            var result = pack.Execute();

            result.Should().Pass();

            var pack2 = new MSBuildCommand(projectDirectory, "Pack", "PackageLibraryDirectDependency");
            pack2.WithWorkingDirectory(projectDirectory.Path);
            var result2 = pack2.Execute();

            result2.Should().Pass();

            var outputPath = pack2.GetOutputDirectory(DefaultTfm, "Debug").ToString();

            new FileInfo(Path.Combine(outputPath, "PackageLibraryDirectDependency.dll")).Should().Exist();

            result2.Should().NuPkgContain(
                Path.Combine(pack2.GetPackageDirectory().FullName, "PackageLibraryDirectDependency.1.0.0.nupkg"),
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
        public void Pack_StaticWebAssets_WithoutFileExtension_AreCorrectlyPacked()
        {
            var testAsset = "PackageLibraryDirectDependency";
            var projectDirectory = CreateAspNetSdkTestAsset(testAsset, subdirectory: "TestPackages");

            File.WriteAllText(Path.Combine(projectDirectory.Path, "PackageLibraryDirectDependency", "wwwroot", "LICENSE"), "license file contents");

            var pack = new MSBuildCommand(projectDirectory, "Pack", "PackageLibraryDirectDependency");
            pack.WithWorkingDirectory(projectDirectory.Path);
            var result = pack.Execute();

            result.Should().Pass();

            var outputPath = pack.GetOutputDirectory(DefaultTfm, "Debug").ToString();

            new FileInfo(Path.Combine(outputPath, "PackageLibraryDirectDependency.dll")).Should().Exist();

            result.Should().NuPkgContain(
                Path.Combine(pack.GetPackageDirectory().FullName, "PackageLibraryDirectDependency.1.0.0.nupkg"),
                filePaths: new[]
                {
                    Path.Combine("staticwebassets", "js", "pkg-direct-dep.js"),
                    Path.Combine("staticwebassets", "css", "site.css"),
                    Path.Combine("staticwebassets", "LICENSE"),
                    Path.Combine("staticwebassets", "PackageLibraryDirectDependency.bundle.scp.css"),
                    Path.Combine("build", "Microsoft.AspNetCore.StaticWebAssets.props"),
                    Path.Combine("build", "PackageLibraryDirectDependency.props"),
                    Path.Combine("buildMultiTargeting", "PackageLibraryDirectDependency.props"),
                    Path.Combine("buildTransitive", "PackageLibraryDirectDependency.props")
                });
        }

        [Fact]
        public void Build_StaticWebAssets_GeneratePackageOnBuild_PacksStaticWebAssets()
        {
            var testAsset = "PackageLibraryDirectDependency";
            var projectDirectory = CreateAspNetSdkTestAsset(testAsset, subdirectory: "TestPackages");

            File.WriteAllText(Path.Combine(projectDirectory.Path, "PackageLibraryDirectDependency", "wwwroot", "LICENSE"), "license file contents");

            var buildCommand = new BuildCommand(projectDirectory, "PackageLibraryDirectDependency");
            buildCommand.WithWorkingDirectory(projectDirectory.Path);
            var result = buildCommand.Execute("/p:GeneratePackageOnBuild=true");

            result.Should().Pass();

            var outputPath = buildCommand.GetOutputDirectory(DefaultTfm, "Debug").ToString();

            new FileInfo(Path.Combine(outputPath, "PackageLibraryDirectDependency.dll")).Should().Exist();

            result.Should().NuPkgContain(
                Path.Combine(buildCommand.GetPackageDirectory().FullName, "PackageLibraryDirectDependency.1.0.0.nupkg"),
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
        public void Build_StaticWebAssets_GeneratePackageOnBuild_DoesNotIncludeAssetsAsContent()
        {
            var testAsset = "PackageLibraryDirectDependency";
            var projectDirectory = CreateAspNetSdkTestAsset(testAsset, subdirectory: "TestPackages");

            File.WriteAllText(Path.Combine(projectDirectory.Path, "PackageLibraryDirectDependency", "wwwroot", "LICENSE"), "license file contents");

            var buildCommand = new BuildCommand(projectDirectory, "PackageLibraryDirectDependency");
            buildCommand.WithWorkingDirectory(projectDirectory.Path);
            var result = buildCommand.Execute("/p:GeneratePackageOnBuild=true");

            result.Should().Pass();

            var outputPath = buildCommand.GetOutputDirectory(DefaultTfm, "Debug").ToString();

            new FileInfo(Path.Combine(outputPath, "PackageLibraryDirectDependency.dll")).Should().Exist();

            result.Should().NuPkgDoesNotContain(
                Path.Combine(buildCommand.GetPackageDirectory().FullName, "PackageLibraryDirectDependency.1.0.0.nupkg"),
                filePaths: new[]
                {
                    Path.Combine("content", "js", "pkg-direct-dep.js"),
                    Path.Combine("content", "css", "site.css"),
                    Path.Combine("content", "PackageLibraryDirectDependency.bundle.scp.css"),
                    Path.Combine("contentFiles", "js", "pkg-direct-dep.js"),
                    Path.Combine("contentFiles", "css", "site.css"),
                    Path.Combine("contentFiles", "PackageLibraryDirectDependency.bundle.scp.css"),
                });
        }

        [Fact]
        public void Pack_MultipleTargetFrameworks_Works()
        {
            var testAsset = "PackageLibraryDirectDependency";
            var projectDirectory = CreateAspNetSdkTestAsset(testAsset, subdirectory: "TestPackages");

            projectDirectory.WithProjectChanges((project, document) =>
            {
                var tfm = document.Descendants("TargetFramework").Single();
                tfm.Name = "TargetFrameworks";
                tfm.FirstNode.ReplaceWith(tfm.FirstNode.ToString() + ";netstandard2.1");

                document.Descendants("AddRazorSupportForMvc").SingleOrDefault()?.Remove();
                document.Descendants("FrameworkReference").SingleOrDefault()?.Remove();
            });

            Directory.Delete(Path.Combine(projectDirectory.Path, "PackageLibraryDirectDependency", "Components"), recursive: true);

            var pack = new MSBuildCommand(projectDirectory, "Pack", "PackageLibraryDirectDependency");
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
                    Path.Combine("build", "Microsoft.AspNetCore.StaticWebAssets.props"),
                    Path.Combine("build", "PackageLibraryDirectDependency.props"),
                    Path.Combine("buildMultiTargeting", "PackageLibraryDirectDependency.props"),
                    Path.Combine("buildTransitive", "PackageLibraryDirectDependency.props")
                });
        }

        [Fact]
        public void Pack_MultipleTargetFrameworks_NoBuild_IncludesStaticWebAssets()
        {
            var testAsset = "PackageLibraryDirectDependency";
            var projectDirectory = CreateAspNetSdkTestAsset(testAsset, subdirectory: "TestPackages");

            projectDirectory.WithProjectChanges((project, document) =>
            {
                var tfm = document.Descendants("TargetFramework").Single();
                tfm.Name = "TargetFrameworks";
                tfm.FirstNode.ReplaceWith(tfm.FirstNode.ToString() + ";netstandard2.1");

                document.Descendants("AddRazorSupportForMvc").SingleOrDefault()?.Remove();
                document.Descendants("FrameworkReference").SingleOrDefault()?.Remove();
            });

            Directory.Delete(Path.Combine(projectDirectory.Path, "PackageLibraryDirectDependency", "Components"), recursive: true);

            var build = new BuildCommand(projectDirectory, "PackageLibraryDirectDependency");
            build.WithWorkingDirectory(projectDirectory.Path);
            var buildResult = build.Execute();

            var pack = new MSBuildCommand(projectDirectory, "Pack", "PackageLibraryDirectDependency");
            pack.WithWorkingDirectory(projectDirectory.Path);
            var result = pack.Execute("/p:NoBuild=true");

            result.Should().Pass();

            var outputPath = pack.GetOutputDirectory(DefaultTfm, "Debug").ToString();

            new FileInfo(Path.Combine(outputPath, "PackageLibraryDirectDependency.dll")).Should().Exist();

            result.Should().NuPkgContain(
                Path.Combine(projectDirectory.Path, "PackageLibraryDirectDependency", "bin", "Debug", "PackageLibraryDirectDependency.1.0.0.nupkg"),
                filePaths: new[]
                {
                    Path.Combine("staticwebassets", "js", "pkg-direct-dep.js"),
                    Path.Combine("staticwebassets", "css", "site.css"),
                    Path.Combine("build", "Microsoft.AspNetCore.StaticWebAssets.props"),
                    Path.Combine("build", "PackageLibraryDirectDependency.props"),
                    Path.Combine("buildMultiTargeting", "PackageLibraryDirectDependency.props"),
                    Path.Combine("buildTransitive", "PackageLibraryDirectDependency.props")
                });
        }

        [Fact]
        public void Pack_MultipleTargetFrameworks_NoBuild_DoesNotIncludeAssetsAsContent()
        {
            var testAsset = "PackageLibraryDirectDependency";
            var projectDirectory = CreateAspNetSdkTestAsset(testAsset, subdirectory: "TestPackages");

            projectDirectory.WithProjectChanges((project, document) =>
            {
                var tfm = document.Descendants("TargetFramework").Single();
                tfm.Name = "TargetFrameworks";
                tfm.FirstNode.ReplaceWith(tfm.FirstNode.ToString() + ";netstandard2.1");

                document.Descendants("AddRazorSupportForMvc").SingleOrDefault()?.Remove();
                document.Descendants("FrameworkReference").SingleOrDefault()?.Remove();
            });

            Directory.Delete(Path.Combine(projectDirectory.Path, "PackageLibraryDirectDependency", "Components"), recursive: true);

            var build = new BuildCommand(Log, projectDirectory.Path, "PackageLibraryDirectDependency");
            build.WithWorkingDirectory(projectDirectory.Path);
            var buildResult = build.Execute();

            var pack = new MSBuildCommand(projectDirectory, "Pack", "PackageLibraryDirectDependency");
            pack.WithWorkingDirectory(projectDirectory.Path);
            var result = pack.Execute("/p:NoBuild=true");

            result.Should().Pass();

            var outputPath = pack.GetOutputDirectory(DefaultTfm, "Debug").ToString();

            new FileInfo(Path.Combine(outputPath, "PackageLibraryDirectDependency.dll")).Should().Exist();

            result.Should().NuPkgDoesNotContain(
                Path.Combine(projectDirectory.Path, "PackageLibraryDirectDependency", "bin", "Debug", "PackageLibraryDirectDependency.1.0.0.nupkg"),
                filePaths: new[]
                {
                    Path.Combine("content", "wwwroot", "js", "pkg-direct-dep.js"),
                    Path.Combine("content", "wwwroot", "css", "site.css"),
                    Path.Combine("contentFiles", "wwwroot", "js", "pkg-direct-dep.js"),
                    Path.Combine("contentFiles", "wwwroot", "css", "site.css"),
                });
        }

        [Fact]
        public void Pack_MultipleTargetFrameworks_GeneratePackageOnBuild_IncludesStaticWebAssets()
        {
            var testAsset = "PackageLibraryDirectDependency";
            var projectDirectory = CreateAspNetSdkTestAsset(testAsset, subdirectory: "TestPackages");

            projectDirectory.WithProjectChanges((project, document) =>
            {
                var tfm = document.Descendants("TargetFramework").Single();
                tfm.Name = "TargetFrameworks";
                tfm.FirstNode.ReplaceWith(tfm.FirstNode.ToString() + ";netstandard2.1");

                document.Descendants("AddRazorSupportForMvc").SingleOrDefault()?.Remove();
                document.Descendants("FrameworkReference").SingleOrDefault()?.Remove();
            });

            Directory.Delete(Path.Combine(projectDirectory.Path, "PackageLibraryDirectDependency", "Components"), recursive: true);

            var build = new BuildCommand(projectDirectory, "PackageLibraryDirectDependency");
            build.WithWorkingDirectory(projectDirectory.Path);
            var result = build.Execute("/p:GeneratePackageOnBuild=true");

            result.Should().Pass();

            var outputPath = build.GetOutputDirectory(DefaultTfm, "Debug").ToString();

            new FileInfo(Path.Combine(outputPath, "PackageLibraryDirectDependency.dll")).Should().Exist();

            result.Should().NuPkgContain(
                Path.Combine(projectDirectory.Path, "PackageLibraryDirectDependency", "bin", "Debug", "PackageLibraryDirectDependency.1.0.0.nupkg"),
                filePaths: new[]
                {
                    Path.Combine("staticwebassets", "js", "pkg-direct-dep.js"),
                    Path.Combine("staticwebassets", "css", "site.css"),
                    Path.Combine("build", "Microsoft.AspNetCore.StaticWebAssets.props"),
                    Path.Combine("build", "PackageLibraryDirectDependency.props"),
                    Path.Combine("buildMultiTargeting", "PackageLibraryDirectDependency.props"),
                    Path.Combine("buildTransitive", "PackageLibraryDirectDependency.props")
                });
        }

        [Fact]
        public void Pack_MultipleTargetFrameworks_GeneratePackageOnBuild_DoesNotIncludeAssetsAsContent()
        {
            var testAsset = "PackageLibraryDirectDependency";
            var projectDirectory = CreateAspNetSdkTestAsset(testAsset, subdirectory: "TestPackages");

            projectDirectory.WithProjectChanges((project, document) =>
            {
                var tfm = document.Descendants("TargetFramework").Single();
                tfm.Name = "TargetFrameworks";
                tfm.FirstNode.ReplaceWith(tfm.FirstNode.ToString() + ";netstandard2.1");

                document.Descendants("AddRazorSupportForMvc").SingleOrDefault()?.Remove();
                document.Descendants("FrameworkReference").SingleOrDefault()?.Remove();
            });

            Directory.Delete(Path.Combine(projectDirectory.Path, "PackageLibraryDirectDependency", "Components"), recursive: true);

            var build = new BuildCommand(projectDirectory, "PackageLibraryDirectDependency");
            build.WithWorkingDirectory(projectDirectory.Path);
            var result = build.Execute("/p:GeneratePackageOnBuild=true");

            result.Should().Pass();

            var outputPath = build.GetOutputDirectory(DefaultTfm, "Debug").ToString();

            new FileInfo(Path.Combine(outputPath, "PackageLibraryDirectDependency.dll")).Should().Exist();

            result.Should().NuPkgDoesNotContain(
                Path.Combine(projectDirectory.Path, "PackageLibraryDirectDependency", "bin", "Debug", "PackageLibraryDirectDependency.1.0.0.nupkg"),
                filePaths: new[]
                {
                    Path.Combine("content", "wwwroot", "js", "pkg-direct-dep.js"),
                    Path.Combine("content", "wwwroot", "css", "site.css"),
                    Path.Combine("contentFiles", "wwwroot", "js", "pkg-direct-dep.js"),
                    Path.Combine("contentFiles", "wwwroot", "css", "site.css"),
                });
        }

        [Fact]
        public void Pack_BeforeNet60_MultipleTargetFrameworks_WithScopedCss_IncludesAssetsAndProjectBundle()
        {
            var testAsset = "PackageLibraryTransitiveDependency";
            var projectDirectory = CreateAspNetSdkTestAsset(testAsset, subdirectory: "TestPackages");

            projectDirectory.WithProjectChanges(document =>
            {
                var parse = XDocument.Parse($@"<Project Sdk=""Microsoft.NET.Sdk.Razor"">

  <PropertyGroup>
    <TargetFrameworks>netstandard2.0;net5.0</TargetFrameworks>
    <RazorLangVersion>3.0</RazorLangVersion>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Condition=""'$(TargetFramework)' == 'net5.0'"" Include=""Microsoft.AspNetCore.Components.Web"" Version=""{DefaultPackageVersion}"" />
    <PackageReference Condition=""'$(TargetFramework)' == 'netstandard2.0'"" Include=""Microsoft.AspNetCore.Components.Web"" Version=""3.1.0"" />
  </ItemGroup>

</Project>
");
                document.Root.ReplaceWith(parse.Root);
            });

            Directory.Delete(Path.Combine(projectDirectory.Path, "wwwroot"), recursive: true);

            var componentText = @"<div class=""my-component"">
    This component is defined in the <strong>razorclasslibrarypack</strong> library.
</div>";

            // This mimics the structure of our default template project
            Directory.CreateDirectory(Path.Combine(projectDirectory.Path, "wwwroot"));
            File.WriteAllText(Path.Combine(projectDirectory.Path, "_Imports.razor"), "@using Microsoft.AspNetCore.Components.Web" + Environment.NewLine);
            File.WriteAllText(Path.Combine(projectDirectory.Path, "Component1.razor"), componentText);
            File.WriteAllText(Path.Combine(projectDirectory.Path, "Component1.razor.css"), "");
            File.WriteAllText(Path.Combine(projectDirectory.Path, "ExampleJsInterop.cs"), "");
            File.WriteAllText(Path.Combine(projectDirectory.Path, "wwwroot", "background.png"), "");
            File.WriteAllText(Path.Combine(projectDirectory.Path, "wwwroot", "exampleJsInterop.js"), "");

            var pack = new MSBuildCommand(Log, "Pack", projectDirectory.Path);
            pack.WithWorkingDirectory(projectDirectory.Path);
            var result = pack.Execute("/bl");

            result.Should().Pass();

            var outputPath = pack.GetOutputDirectory("net5.0", "Debug").ToString();

            new FileInfo(Path.Combine(outputPath, "PackageLibraryTransitiveDependency.dll")).Should().Exist();

            var packagePath = Path.Combine(
                projectDirectory.Path,
                "bin",
                "Debug",
                "PackageLibraryTransitiveDependency.1.0.0.nupkg");

            result.Should().NuPkgContain(
                packagePath,
                filePaths: new[]
                {
                    Path.Combine("staticwebassets", "exampleJsInterop.js"),
                    Path.Combine("staticwebassets", "background.png"),
                    Path.Combine("staticwebassets", "PackageLibraryTransitiveDependency.bundle.scp.css"),
                    Path.Combine("build", "Microsoft.AspNetCore.StaticWebAssets.props"),
                    Path.Combine("build", "PackageLibraryTransitiveDependency.props"),
                    Path.Combine("buildMultiTargeting", "PackageLibraryTransitiveDependency.props"),
                    Path.Combine("buildTransitive", "PackageLibraryTransitiveDependency.props")
                });
        }

        [Fact]
        public void Pack_BeforeNet60_MultipleTargetFrameworks_WithScopedCss_DoesNotIncludeAssetsAsContent()
        {
            var testAsset = "PackageLibraryTransitiveDependency";
            var projectDirectory = CreateAspNetSdkTestAsset(testAsset, subdirectory: "TestPackages");

            projectDirectory.WithProjectChanges(document =>
            {
                var parse = XDocument.Parse($@"<Project Sdk=""Microsoft.NET.Sdk.Razor"">

  <PropertyGroup>
    <TargetFrameworks>netstandard2.0;net5.0</TargetFrameworks>
    <RazorLangVersion>3.0</RazorLangVersion>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Condition=""'$(TargetFramework)' == 'net5.0'"" Include=""Microsoft.AspNetCore.Components.Web"" Version=""{DefaultPackageVersion}"" />
    <PackageReference Condition=""'$(TargetFramework)' == 'netstandard2.0'"" Include=""Microsoft.AspNetCore.Components.Web"" Version=""3.1.0"" />
  </ItemGroup>

</Project>
");
                document.Root.ReplaceWith(parse.Root);
            });

            Directory.Delete(Path.Combine(projectDirectory.Path, "wwwroot"), recursive: true);

            var componentText = @"<div class=""my-component"">
    This component is defined in the <strong>razorclasslibrarypack</strong> library.
</div>";

            // This mimics the structure of our default template project
            Directory.CreateDirectory(Path.Combine(projectDirectory.Path, "wwwroot"));
            File.WriteAllText(Path.Combine(projectDirectory.Path, "_Imports.razor"), "@using Microsoft.AspNetCore.Components.Web" + Environment.NewLine);
            File.WriteAllText(Path.Combine(projectDirectory.Path, "Component1.razor"), componentText);
            File.WriteAllText(Path.Combine(projectDirectory.Path, "Component1.razor.css"), "");
            File.WriteAllText(Path.Combine(projectDirectory.Path, "ExampleJsInterop.cs"), "");
            File.WriteAllText(Path.Combine(projectDirectory.Path, "wwwroot", "background.png"), "");
            File.WriteAllText(Path.Combine(projectDirectory.Path, "wwwroot", "exampleJsInterop.js"), "");

            var pack = new MSBuildCommand(Log, "Pack", projectDirectory.Path);
            pack.WithWorkingDirectory(projectDirectory.Path);
            var result = pack.Execute();

            result.Should().Pass();

            var outputPath = pack.GetOutputDirectory("net5.0", "Debug").ToString();

            new FileInfo(Path.Combine(outputPath, "PackageLibraryTransitiveDependency.dll")).Should().Exist();

            var packagePath = Path.Combine(
                projectDirectory.Path,
                "bin",
                "Debug",
                "PackageLibraryTransitiveDependency.1.0.0.nupkg");

            result.Should().NuPkgDoesNotContain(
                packagePath,
                filePaths: new[]
                {
                    Path.Combine("content", "exampleJsInterop.js"),
                    Path.Combine("content", "background.png"),
                    Path.Combine("content", "PackageLibraryTransitiveDependency.bundle.scp.css"),
                    Path.Combine("contentFiles", "exampleJsInterop.js"),
                    Path.Combine("contentFiles", "background.png"),
                    Path.Combine("contentFiles", "PackageLibraryTransitiveDependency.bundle.scp.css"),
                });
        }

        [Fact]
        public void Pack_BeforeNet60_MultipleTargetFrameworks_NoBuild_WithScopedCss_IncludesAssetsAndProjectBundle()
        {
            var testAsset = "PackageLibraryTransitiveDependency";
            var projectDirectory = CreateAspNetSdkTestAsset(testAsset, subdirectory: "TestPackages");

            projectDirectory.WithProjectChanges(document =>
            {
                var parse = XDocument.Parse($@"<Project Sdk=""Microsoft.NET.Sdk.Razor"">

  <PropertyGroup>
    <TargetFrameworks>netstandard2.0;net5.0</TargetFrameworks>
    <RazorLangVersion>3.0</RazorLangVersion>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Condition=""'$(TargetFramework)' == 'net5.0'"" Include=""Microsoft.AspNetCore.Components.Web"" Version=""{DefaultPackageVersion}"" />
    <PackageReference Condition=""'$(TargetFramework)' == 'netstandard2.0'"" Include=""Microsoft.AspNetCore.Components.Web"" Version=""3.1.0"" />
  </ItemGroup>

</Project>
");
                document.Root.ReplaceWith(parse.Root);
            });

            Directory.Delete(Path.Combine(projectDirectory.Path, "wwwroot"), recursive: true);

            var componentText = @"<div class=""my-component"">
    This component is defined in the <strong>razorclasslibrarypack</strong> library.
</div>";

            // This mimics the structure of our default template project
            Directory.CreateDirectory(Path.Combine(projectDirectory.Path, "wwwroot"));
            File.WriteAllText(Path.Combine(projectDirectory.Path, "_Imports.razor"), "@using Microsoft.AspNetCore.Components.Web" + Environment.NewLine);
            File.WriteAllText(Path.Combine(projectDirectory.Path, "Component1.razor"), componentText);
            File.WriteAllText(Path.Combine(projectDirectory.Path, "Component1.razor.css"), "");
            File.WriteAllText(Path.Combine(projectDirectory.Path, "ExampleJsInterop.cs"), "");
            File.WriteAllText(Path.Combine(projectDirectory.Path, "wwwroot", "background.png"), "");
            File.WriteAllText(Path.Combine(projectDirectory.Path, "wwwroot", "exampleJsInterop.js"), "");

            var build = new BuildCommand(Log, projectDirectory.Path);
            build.WithWorkingDirectory(projectDirectory.Path);
            var buildResult = build.Execute();

            buildResult.Should().Pass();

            var pack = new MSBuildCommand(Log, "Pack", projectDirectory.Path);
            pack.WithWorkingDirectory(projectDirectory.Path);
            var result = pack.Execute("/p:NoBuild=true");

            result.Should().Pass();

            var outputPath = pack.GetOutputDirectory("net5.0", "Debug").ToString();

            new FileInfo(Path.Combine(outputPath, "PackageLibraryTransitiveDependency.dll")).Should().Exist();

            var packagePath = Path.Combine(
                projectDirectory.Path,
                "bin",
                "Debug",
                "PackageLibraryTransitiveDependency.1.0.0.nupkg");

            result.Should().NuPkgContain(
                packagePath,
                filePaths: new[]
                {
                    Path.Combine("staticwebassets", "exampleJsInterop.js"),
                    Path.Combine("staticwebassets", "background.png"),
                    Path.Combine("staticwebassets", "PackageLibraryTransitiveDependency.bundle.scp.css"),
                    Path.Combine("build", "Microsoft.AspNetCore.StaticWebAssets.props"),
                    Path.Combine("build", "PackageLibraryTransitiveDependency.props"),
                    Path.Combine("buildMultiTargeting", "PackageLibraryTransitiveDependency.props"),
                    Path.Combine("buildTransitive", "PackageLibraryTransitiveDependency.props")
                });
        }

        [Fact]
        public void Pack_BeforeNet60_MultipleTargetFrameworks_NoBuild_WithScopedCss_DoesNotIncludeAssetsAsContent()
        {
            var testAsset = "PackageLibraryTransitiveDependency";
            var projectDirectory = CreateAspNetSdkTestAsset(testAsset, subdirectory: "TestPackages");

            projectDirectory.WithProjectChanges(document =>
            {
                var parse = XDocument.Parse($@"<Project Sdk=""Microsoft.NET.Sdk.Razor"">

  <PropertyGroup>
    <TargetFrameworks>netstandard2.0;net5.0</TargetFrameworks>
    <RazorLangVersion>3.0</RazorLangVersion>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Condition=""'$(TargetFramework)' == 'net5.0'"" Include=""Microsoft.AspNetCore.Components.Web"" Version=""{DefaultPackageVersion}"" />
    <PackageReference Condition=""'$(TargetFramework)' == 'netstandard2.0'"" Include=""Microsoft.AspNetCore.Components.Web"" Version=""3.1.0"" />
  </ItemGroup>

</Project>
");
                document.Root.ReplaceWith(parse.Root);
            });

            Directory.Delete(Path.Combine(projectDirectory.Path, "wwwroot"), recursive: true);

            var componentText = @"<div class=""my-component"">
    This component is defined in the <strong>razorclasslibrarypack</strong> library.
</div>";

            // This mimics the structure of our default template project
            Directory.CreateDirectory(Path.Combine(projectDirectory.Path, "wwwroot"));
            File.WriteAllText(Path.Combine(projectDirectory.Path, "_Imports.razor"), "@using Microsoft.AspNetCore.Components.Web" + Environment.NewLine);
            File.WriteAllText(Path.Combine(projectDirectory.Path, "Component1.razor"), componentText);
            File.WriteAllText(Path.Combine(projectDirectory.Path, "Component1.razor.css"), "");
            File.WriteAllText(Path.Combine(projectDirectory.Path, "ExampleJsInterop.cs"), "");
            File.WriteAllText(Path.Combine(projectDirectory.Path, "wwwroot", "background.png"), "");
            File.WriteAllText(Path.Combine(projectDirectory.Path, "wwwroot", "exampleJsInterop.js"), "");

            var build = new BuildCommand(Log, projectDirectory.Path);
            build.WithWorkingDirectory(projectDirectory.Path);
            var buildResult = build.Execute();

            buildResult.Should().Pass();

            var pack = new MSBuildCommand(Log, "Pack", projectDirectory.Path);
            pack.WithWorkingDirectory(projectDirectory.Path);
            var result = pack.Execute("/p:NoBuild=true");

            result.Should().Pass();

            var outputPath = pack.GetOutputDirectory("net5.0", "Debug").ToString();

            new FileInfo(Path.Combine(outputPath, "PackageLibraryTransitiveDependency.dll")).Should().Exist();

            var packagePath = Path.Combine(
                projectDirectory.Path,
                "bin",
                "Debug",
                "PackageLibraryTransitiveDependency.1.0.0.nupkg");

            result.Should().NuPkgDoesNotContain(
                packagePath,
                filePaths: new[]
                {
                    Path.Combine("content", "exampleJsInterop.js"),
                    Path.Combine("content", "background.png"),
                    Path.Combine("content", "PackageLibraryTransitiveDependency.bundle.scp.css"),
                    Path.Combine("contentFiles", "exampleJsInterop.js"),
                    Path.Combine("contentFiles", "background.png"),
                    Path.Combine("contentFiles", "PackageLibraryTransitiveDependency.bundle.scp.css"),
                });
        }

        [Fact]
        public void Pack_BeforeNet60_MultipleTargetFrameworks_GeneratePackageOnBuild_WithScopedCss_IncludesAssetsAndProjectBundle()
        {
            var testAsset = "PackageLibraryTransitiveDependency";
            var projectDirectory = CreateAspNetSdkTestAsset(testAsset, subdirectory: "TestPackages");

            projectDirectory.WithProjectChanges(document =>
            {
                var parse = XDocument.Parse($@"<Project Sdk=""Microsoft.NET.Sdk.Razor"">

  <PropertyGroup>
    <TargetFrameworks>netstandard2.0;net5.0</TargetFrameworks>
    <RazorLangVersion>3.0</RazorLangVersion>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Condition=""'$(TargetFramework)' == 'net5.0'"" Include=""Microsoft.AspNetCore.Components.Web"" Version=""{DefaultPackageVersion}"" />
    <PackageReference Condition=""'$(TargetFramework)' == 'netstandard2.0'"" Include=""Microsoft.AspNetCore.Components.Web"" Version=""3.1.0"" />
  </ItemGroup>

</Project>
");
                document.Root.ReplaceWith(parse.Root);
            });

            Directory.Delete(Path.Combine(projectDirectory.Path, "wwwroot"), recursive: true);

            var componentText = @"<div class=""my-component"">
    This component is defined in the <strong>razorclasslibrarypack</strong> library.
</div>";

            // This mimics the structure of our default template project
            Directory.CreateDirectory(Path.Combine(projectDirectory.Path, "wwwroot"));
            File.WriteAllText(Path.Combine(projectDirectory.Path, "_Imports.razor"), "@using Microsoft.AspNetCore.Components.Web" + Environment.NewLine);
            File.WriteAllText(Path.Combine(projectDirectory.Path, "Component1.razor"), componentText);
            File.WriteAllText(Path.Combine(projectDirectory.Path, "Component1.razor.css"), "");
            File.WriteAllText(Path.Combine(projectDirectory.Path, "ExampleJsInterop.cs"), "");
            File.WriteAllText(Path.Combine(projectDirectory.Path, "wwwroot", "background.png"), "");
            File.WriteAllText(Path.Combine(projectDirectory.Path, "wwwroot", "exampleJsInterop.js"), "");

            var build = new BuildCommand(Log, projectDirectory.Path);
            build.WithWorkingDirectory(projectDirectory.Path);
            var result = build.Execute("/p:GeneratePackageOnBuild=true");

            result.Should().Pass();

            var outputPath = build.GetOutputDirectory("net5.0", "Debug").ToString();

            new FileInfo(Path.Combine(outputPath, "PackageLibraryTransitiveDependency.dll")).Should().Exist();

            var packagePath = Path.Combine(
                projectDirectory.Path,
                "bin",
                "Debug",
                "PackageLibraryTransitiveDependency.1.0.0.nupkg");

            result.Should().NuPkgContain(
                packagePath,
                filePaths: new[]
                {
                    Path.Combine("staticwebassets", "exampleJsInterop.js"),
                    Path.Combine("staticwebassets", "background.png"),
                    Path.Combine("staticwebassets", "PackageLibraryTransitiveDependency.bundle.scp.css"),
                    Path.Combine("build", "Microsoft.AspNetCore.StaticWebAssets.props"),
                    Path.Combine("build", "PackageLibraryTransitiveDependency.props"),
                    Path.Combine("buildMultiTargeting", "PackageLibraryTransitiveDependency.props"),
                    Path.Combine("buildTransitive", "PackageLibraryTransitiveDependency.props")
                });
        }

        [Fact]
        public void Pack_BeforeNet60_MultipleTargetFrameworks_GeneratePackageOnBuild_WithScopedCss_DoesNotIncludeAssetsAsContent()
        {
            var testAsset = "PackageLibraryTransitiveDependency";
            var projectDirectory = CreateAspNetSdkTestAsset(testAsset, subdirectory: "TestPackages");

            projectDirectory.WithProjectChanges(document =>
            {
                var parse = XDocument.Parse($@"<Project Sdk=""Microsoft.NET.Sdk.Razor"">

  <PropertyGroup>
    <TargetFrameworks>netstandard2.0;net5.0</TargetFrameworks>
    <RazorLangVersion>3.0</RazorLangVersion>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Condition=""'$(TargetFramework)' == 'net5.0'"" Include=""Microsoft.AspNetCore.Components.Web"" Version=""{DefaultPackageVersion}"" />
    <PackageReference Condition=""'$(TargetFramework)' == 'netstandard2.0'"" Include=""Microsoft.AspNetCore.Components.Web"" Version=""3.1.0"" />
  </ItemGroup>

</Project>
");
                document.Root.ReplaceWith(parse.Root);
            });

            Directory.Delete(Path.Combine(projectDirectory.Path, "wwwroot"), recursive: true);

            var componentText = @"<div class=""my-component"">
    This component is defined in the <strong>razorclasslibrarypack</strong> library.
</div>";

            // This mimics the structure of our default template project
            Directory.CreateDirectory(Path.Combine(projectDirectory.Path, "wwwroot"));
            File.WriteAllText(Path.Combine(projectDirectory.Path, "_Imports.razor"), "@using Microsoft.AspNetCore.Components.Web" + Environment.NewLine);
            File.WriteAllText(Path.Combine(projectDirectory.Path, "Component1.razor"), componentText);
            File.WriteAllText(Path.Combine(projectDirectory.Path, "Component1.razor.css"), "");
            File.WriteAllText(Path.Combine(projectDirectory.Path, "ExampleJsInterop.cs"), "");
            File.WriteAllText(Path.Combine(projectDirectory.Path, "wwwroot", "background.png"), "");
            File.WriteAllText(Path.Combine(projectDirectory.Path, "wwwroot", "exampleJsInterop.js"), "");

            var build = new BuildCommand(Log, projectDirectory.Path);
            build.WithWorkingDirectory(projectDirectory.Path);
            var result = build.Execute("/p:GeneratePackageOnBuild=true");

            result.Should().Pass();

            var outputPath = build.GetOutputDirectory("net5.0", "Debug").ToString();

            new FileInfo(Path.Combine(outputPath, "PackageLibraryTransitiveDependency.dll")).Should().Exist();

            var packagePath = Path.Combine(
                projectDirectory.Path,
                "bin",
                "Debug",
                "PackageLibraryTransitiveDependency.1.0.0.nupkg");

            result.Should().NuPkgDoesNotContain(
                packagePath,
                filePaths: new[]
                {
                    Path.Combine("content", "exampleJsInterop.js"),
                    Path.Combine("content", "background.png"),
                    Path.Combine("content", "PackageLibraryTransitiveDependency.bundle.scp.css"),
                    Path.Combine("contentFiles", "exampleJsInterop.js"),
                    Path.Combine("contentFiles", "background.png"),
                    Path.Combine("contentFiles", "PackageLibraryTransitiveDependency.bundle.scp.css"),
                });
        }

        [Fact]
        public void Pack_Net50_WithScopedCss_IncludesAssetsAndProjectBundle()
        {
            var testAsset = "PackageLibraryTransitiveDependency";
            var projectDirectory = CreateAspNetSdkTestAsset(testAsset, subdirectory: "TestPackages");

            projectDirectory.WithProjectChanges(document =>
            {
                var parse = XDocument.Parse($@"<Project Sdk=""Microsoft.NET.Sdk.Razor"">

  <PropertyGroup>
    <TargetFramework>net5.0</TargetFramework>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Condition=""'$(TargetFramework)' == 'net5.0'"" Include=""Microsoft.AspNetCore.Components.Web"" Version=""{DefaultPackageVersion}"" />
  </ItemGroup>

</Project>
");
                document.Root.ReplaceWith(parse.Root);
            });

            Directory.Delete(Path.Combine(projectDirectory.Path, "wwwroot"), recursive: true);

            var componentText = @"<div class=""my-component"">
    This component is defined in the <strong>razorclasslibrarypack</strong> library.
</div>";

            // This mimics the structure of our default template project
            Directory.CreateDirectory(Path.Combine(projectDirectory.Path, "wwwroot"));
            File.WriteAllText(Path.Combine(projectDirectory.Path, "_Imports.razor"), "@using Microsoft.AspNetCore.Components.Web" + Environment.NewLine);
            File.WriteAllText(Path.Combine(projectDirectory.Path, "Component1.razor"), componentText);
            File.WriteAllText(Path.Combine(projectDirectory.Path, "Component1.razor.css"), "");
            File.WriteAllText(Path.Combine(projectDirectory.Path, "ExampleJsInterop.cs"), "");
            File.WriteAllText(Path.Combine(projectDirectory.Path, "wwwroot", "background.png"), "");
            File.WriteAllText(Path.Combine(projectDirectory.Path, "wwwroot", "exampleJsInterop.js"), "");

            var pack = new MSBuildCommand(Log, "Pack", projectDirectory.Path);
            pack.WithWorkingDirectory(projectDirectory.Path);
            var result = pack.Execute();

            result.Should().Pass();

            var outputPath = pack.GetOutputDirectory("net5.0", "Debug").ToString();

            new FileInfo(Path.Combine(outputPath, "PackageLibraryTransitiveDependency.dll")).Should().Exist();

            var packagePath = Path.Combine(
                projectDirectory.Path,
                "bin",
                "Debug",
                "PackageLibraryTransitiveDependency.1.0.0.nupkg");

            result.Should().NuPkgContain(
                packagePath,
                filePaths: new[]
                {
                    Path.Combine("staticwebassets", "exampleJsInterop.js"),
                    Path.Combine("staticwebassets", "background.png"),
                    Path.Combine("staticwebassets", "PackageLibraryTransitiveDependency.bundle.scp.css"),
                    Path.Combine("build", "Microsoft.AspNetCore.StaticWebAssets.props"),
                    Path.Combine("build", "PackageLibraryTransitiveDependency.props"),
                    Path.Combine("buildMultiTargeting", "PackageLibraryTransitiveDependency.props"),
                    Path.Combine("buildTransitive", "PackageLibraryTransitiveDependency.props")
                });
        }

        [Fact]
        public void Pack_Net50_WithScopedCss_DoesNotIncludeAssetsAsContent()
        {
            var testAsset = "PackageLibraryTransitiveDependency";
            var projectDirectory = CreateAspNetSdkTestAsset(testAsset, subdirectory: "TestPackages");

            projectDirectory.WithProjectChanges(document =>
            {
                var parse = XDocument.Parse($@"<Project Sdk=""Microsoft.NET.Sdk.Razor"">

  <PropertyGroup>
    <TargetFramework>net5.0</TargetFramework>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Condition=""'$(TargetFramework)' == 'net5.0'"" Include=""Microsoft.AspNetCore.Components.Web"" Version=""{DefaultPackageVersion}"" />
  </ItemGroup>

</Project>
");
                document.Root.ReplaceWith(parse.Root);
            });

            Directory.Delete(Path.Combine(projectDirectory.Path, "wwwroot"), recursive: true);

            var componentText = @"<div class=""my-component"">
    This component is defined in the <strong>razorclasslibrarypack</strong> library.
</div>";

            // This mimics the structure of our default template project
            Directory.CreateDirectory(Path.Combine(projectDirectory.Path, "wwwroot"));
            File.WriteAllText(Path.Combine(projectDirectory.Path, "_Imports.razor"), "@using Microsoft.AspNetCore.Components.Web" + Environment.NewLine);
            File.WriteAllText(Path.Combine(projectDirectory.Path, "Component1.razor"), componentText);
            File.WriteAllText(Path.Combine(projectDirectory.Path, "Component1.razor.css"), "");
            File.WriteAllText(Path.Combine(projectDirectory.Path, "ExampleJsInterop.cs"), "");
            File.WriteAllText(Path.Combine(projectDirectory.Path, "wwwroot", "background.png"), "");
            File.WriteAllText(Path.Combine(projectDirectory.Path, "wwwroot", "exampleJsInterop.js"), "");

            var pack = new MSBuildCommand(Log, "Pack", projectDirectory.Path);
            pack.WithWorkingDirectory(projectDirectory.Path);
            var result = pack.Execute();

            result.Should().Pass();

            var outputPath = pack.GetOutputDirectory("net5.0", "Debug").ToString();

            new FileInfo(Path.Combine(outputPath, "PackageLibraryTransitiveDependency.dll")).Should().Exist();

            var packagePath = Path.Combine(
                projectDirectory.Path,
                "bin",
                "Debug",
                "PackageLibraryTransitiveDependency.1.0.0.nupkg");

            result.Should().NuPkgDoesNotContain(
                packagePath,
                filePaths: new[]
                {
                    Path.Combine("content", "exampleJsInterop.js"),
                    Path.Combine("content", "background.png"),
                    Path.Combine("content", "PackageLibraryTransitiveDependency.bundle.scp.css"),
                    Path.Combine("contentFiles", "exampleJsInterop.js"),
                    Path.Combine("contentFiles", "background.png"),
                    Path.Combine("contentFiles", "PackageLibraryTransitiveDependency.bundle.scp.css"),
                });
        }

        [Fact]
        public void Pack_Net50_NoBuild_WithScopedCss_IncludesAssetsAndProjectBundle()
        {
            var testAsset = "PackageLibraryTransitiveDependency";
            var projectDirectory = CreateAspNetSdkTestAsset(testAsset, subdirectory: "TestPackages");

            projectDirectory.WithProjectChanges(document =>
            {
                var parse = XDocument.Parse($@"<Project Sdk=""Microsoft.NET.Sdk.Razor"">

  <PropertyGroup>
    <TargetFramework>net5.0</TargetFramework>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Condition=""'$(TargetFramework)' == 'net5.0'"" Include=""Microsoft.AspNetCore.Components.Web"" Version=""{DefaultPackageVersion}"" />
  </ItemGroup>

</Project>
");
                document.Root.ReplaceWith(parse.Root);
            });

            Directory.Delete(Path.Combine(projectDirectory.Path, "wwwroot"), recursive: true);

            var componentText = @"<div class=""my-component"">
    This component is defined in the <strong>razorclasslibrarypack</strong> library.
</div>";

            // This mimics the structure of our default template project
            Directory.CreateDirectory(Path.Combine(projectDirectory.Path, "wwwroot"));
            File.WriteAllText(Path.Combine(projectDirectory.Path, "_Imports.razor"), "@using Microsoft.AspNetCore.Components.Web" + Environment.NewLine);
            File.WriteAllText(Path.Combine(projectDirectory.Path, "Component1.razor"), componentText);
            File.WriteAllText(Path.Combine(projectDirectory.Path, "Component1.razor.css"), "");
            File.WriteAllText(Path.Combine(projectDirectory.Path, "ExampleJsInterop.cs"), "");
            File.WriteAllText(Path.Combine(projectDirectory.Path, "wwwroot", "background.png"), "");
            File.WriteAllText(Path.Combine(projectDirectory.Path, "wwwroot", "exampleJsInterop.js"), "");

            var build = new BuildCommand(Log, projectDirectory.Path);
            build.WithWorkingDirectory(projectDirectory.Path);
            var buildResult = build.Execute();

            buildResult.Should().Pass();

            var pack = new MSBuildCommand(Log, "Pack", projectDirectory.Path);
            pack.WithWorkingDirectory(projectDirectory.Path);
            var result = pack.Execute("/p:NoBuild=true");

            result.Should().Pass();

            var outputPath = pack.GetOutputDirectory("net5.0", "Debug").ToString();

            new FileInfo(Path.Combine(outputPath, "PackageLibraryTransitiveDependency.dll")).Should().Exist();

            var packagePath = Path.Combine(
                projectDirectory.Path,
                "bin",
                "Debug",
                "PackageLibraryTransitiveDependency.1.0.0.nupkg");

            result.Should().NuPkgContain(
                packagePath,
                filePaths: new[]
                {
                    Path.Combine("staticwebassets", "exampleJsInterop.js"),
                    Path.Combine("staticwebassets", "background.png"),
                    Path.Combine("staticwebassets", "PackageLibraryTransitiveDependency.bundle.scp.css"),
                    Path.Combine("build", "Microsoft.AspNetCore.StaticWebAssets.props"),
                    Path.Combine("build", "PackageLibraryTransitiveDependency.props"),
                    Path.Combine("buildMultiTargeting", "PackageLibraryTransitiveDependency.props"),
                    Path.Combine("buildTransitive", "PackageLibraryTransitiveDependency.props")
                });
        }

        [Fact]
        public void Pack_Net50_NoBuild_WithScopedCss_DoesNotIncludeAssetsAsContent()
        {
            var testAsset = "PackageLibraryTransitiveDependency";
            var projectDirectory = CreateAspNetSdkTestAsset(testAsset, subdirectory: "TestPackages");

            projectDirectory.WithProjectChanges(document =>
            {
                var parse = XDocument.Parse($@"<Project Sdk=""Microsoft.NET.Sdk.Razor"">

  <PropertyGroup>
    <TargetFramework>net5.0</TargetFramework>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Condition=""'$(TargetFramework)' == 'net5.0'"" Include=""Microsoft.AspNetCore.Components.Web"" Version=""{DefaultPackageVersion}"" />
  </ItemGroup>

</Project>
");
                document.Root.ReplaceWith(parse.Root);
            });

            Directory.Delete(Path.Combine(projectDirectory.Path, "wwwroot"), recursive: true);

            var componentText = @"<div class=""my-component"">
    This component is defined in the <strong>razorclasslibrarypack</strong> library.
</div>";

            // This mimics the structure of our default template project
            Directory.CreateDirectory(Path.Combine(projectDirectory.Path, "wwwroot"));
            File.WriteAllText(Path.Combine(projectDirectory.Path, "_Imports.razor"), "@using Microsoft.AspNetCore.Components.Web" + Environment.NewLine);
            File.WriteAllText(Path.Combine(projectDirectory.Path, "Component1.razor"), componentText);
            File.WriteAllText(Path.Combine(projectDirectory.Path, "Component1.razor.css"), "");
            File.WriteAllText(Path.Combine(projectDirectory.Path, "ExampleJsInterop.cs"), "");
            File.WriteAllText(Path.Combine(projectDirectory.Path, "wwwroot", "background.png"), "");
            File.WriteAllText(Path.Combine(projectDirectory.Path, "wwwroot", "exampleJsInterop.js"), "");

            var build = new BuildCommand(Log, projectDirectory.Path);
            build.WithWorkingDirectory(projectDirectory.Path);
            var buildResult = build.Execute();

            buildResult.Should().Pass();

            var pack = new MSBuildCommand(Log, "Pack", projectDirectory.Path);
            pack.WithWorkingDirectory(projectDirectory.Path);
            var result = pack.Execute("/p:NoBuild=true");

            result.Should().Pass();

            var outputPath = pack.GetOutputDirectory("net5.0", "Debug").ToString();

            new FileInfo(Path.Combine(outputPath, "PackageLibraryTransitiveDependency.dll")).Should().Exist();

            var packagePath = Path.Combine(
                projectDirectory.Path,
                "bin",
                "Debug",
                "PackageLibraryTransitiveDependency.1.0.0.nupkg");

            result.Should().NuPkgDoesNotContain(
                packagePath,
                filePaths: new[]
                {
                    Path.Combine("content", "exampleJsInterop.js"),
                    Path.Combine("content", "background.png"),
                    Path.Combine("content", "PackageLibraryTransitiveDependency.bundle.scp.css"),
                    Path.Combine("contentFiles", "exampleJsInterop.js"),
                    Path.Combine("contentFiles", "background.png"),
                    Path.Combine("contentFiles", "PackageLibraryTransitiveDependency.bundle.scp.css"),
                });
        }

        [Fact]
        public void Pack_Net50_GeneratePackageOnBuild_WithScopedCss_IncludesAssetsAndProjectBundle()
        {
            var testAsset = "PackageLibraryTransitiveDependency";
            var projectDirectory = CreateAspNetSdkTestAsset(testAsset, subdirectory: "TestPackages");

            projectDirectory.WithProjectChanges(document =>
            {
                var parse = XDocument.Parse($@"<Project Sdk=""Microsoft.NET.Sdk.Razor"">

  <PropertyGroup>
    <TargetFramework>net5.0</TargetFramework>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Condition=""'$(TargetFramework)' == 'net5.0'"" Include=""Microsoft.AspNetCore.Components.Web"" Version=""{DefaultPackageVersion}"" />
  </ItemGroup>

</Project>
");
                document.Root.ReplaceWith(parse.Root);
            });

            Directory.Delete(Path.Combine(projectDirectory.Path, "wwwroot"), recursive: true);

            var componentText = @"<div class=""my-component"">
    This component is defined in the <strong>razorclasslibrarypack</strong> library.
</div>";

            // This mimics the structure of our default template project
            Directory.CreateDirectory(Path.Combine(projectDirectory.Path, "wwwroot"));
            File.WriteAllText(Path.Combine(projectDirectory.Path, "_Imports.razor"), "@using Microsoft.AspNetCore.Components.Web" + Environment.NewLine);
            File.WriteAllText(Path.Combine(projectDirectory.Path, "Component1.razor"), componentText);
            File.WriteAllText(Path.Combine(projectDirectory.Path, "Component1.razor.css"), "");
            File.WriteAllText(Path.Combine(projectDirectory.Path, "ExampleJsInterop.cs"), "");
            File.WriteAllText(Path.Combine(projectDirectory.Path, "wwwroot", "background.png"), "");
            File.WriteAllText(Path.Combine(projectDirectory.Path, "wwwroot", "exampleJsInterop.js"), "");

            var build = new BuildCommand(Log, projectDirectory.Path);
            build.WithWorkingDirectory(projectDirectory.Path);
            var result = build.Execute("/p:GeneratePackageOnBuild=true");

            result.Should().Pass();

            var outputPath = build.GetOutputDirectory("net5.0", "Debug").ToString();

            new FileInfo(Path.Combine(outputPath, "PackageLibraryTransitiveDependency.dll")).Should().Exist();

            var packagePath = Path.Combine(
                projectDirectory.Path,
                "bin",
                "Debug",
                "PackageLibraryTransitiveDependency.1.0.0.nupkg");

            result.Should().NuPkgContain(
                packagePath,
                filePaths: new[]
                {
                    Path.Combine("staticwebassets", "exampleJsInterop.js"),
                    Path.Combine("staticwebassets", "background.png"),
                    Path.Combine("staticwebassets", "PackageLibraryTransitiveDependency.bundle.scp.css"),
                    Path.Combine("build", "Microsoft.AspNetCore.StaticWebAssets.props"),
                    Path.Combine("build", "PackageLibraryTransitiveDependency.props"),
                    Path.Combine("buildMultiTargeting", "PackageLibraryTransitiveDependency.props"),
                    Path.Combine("buildTransitive", "PackageLibraryTransitiveDependency.props")
                });
        }

        [Fact]
        public void Pack_Net50_GeneratePackageOnBuild_WithScopedCss_DoesNotIncludeAssetsAsContent()
        {
            var testAsset = "PackageLibraryTransitiveDependency";
            var projectDirectory = CreateAspNetSdkTestAsset(testAsset, subdirectory: "TestPackages");

            projectDirectory.WithProjectChanges(document =>
            {
                var parse = XDocument.Parse($@"<Project Sdk=""Microsoft.NET.Sdk.Razor"">

  <PropertyGroup>
    <TargetFramework>net5.0</TargetFramework>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Condition=""'$(TargetFramework)' == 'net5.0'"" Include=""Microsoft.AspNetCore.Components.Web"" Version=""{DefaultPackageVersion}"" />
  </ItemGroup>

</Project>
");
                document.Root.ReplaceWith(parse.Root);
            });

            Directory.Delete(Path.Combine(projectDirectory.Path, "wwwroot"), recursive: true);

            var componentText = @"<div class=""my-component"">
    This component is defined in the <strong>razorclasslibrarypack</strong> library.
</div>";

            // This mimics the structure of our default template project
            Directory.CreateDirectory(Path.Combine(projectDirectory.Path, "wwwroot"));
            File.WriteAllText(Path.Combine(projectDirectory.Path, "_Imports.razor"), "@using Microsoft.AspNetCore.Components.Web" + Environment.NewLine);
            File.WriteAllText(Path.Combine(projectDirectory.Path, "Component1.razor"), componentText);
            File.WriteAllText(Path.Combine(projectDirectory.Path, "Component1.razor.css"), "");
            File.WriteAllText(Path.Combine(projectDirectory.Path, "ExampleJsInterop.cs"), "");
            File.WriteAllText(Path.Combine(projectDirectory.Path, "wwwroot", "background.png"), "");
            File.WriteAllText(Path.Combine(projectDirectory.Path, "wwwroot", "exampleJsInterop.js"), "");

            var build = new BuildCommand(Log, projectDirectory.Path);
            build.WithWorkingDirectory(projectDirectory.Path);
            var result = build.Execute("/p:GeneratePackageOnBuild=true");

            result.Should().Pass();

            var outputPath = build.GetOutputDirectory("net5.0", "Debug").ToString();

            new FileInfo(Path.Combine(outputPath, "PackageLibraryTransitiveDependency.dll")).Should().Exist();

            var packagePath = Path.Combine(
                projectDirectory.Path,
                "bin",
                "Debug",
                "PackageLibraryTransitiveDependency.1.0.0.nupkg");

            result.Should().NuPkgDoesNotContain(
                packagePath,
                filePaths: new[]
                {
                    Path.Combine("content", "exampleJsInterop.js"),
                    Path.Combine("content", "background.png"),
                    Path.Combine("content", "PackageLibraryTransitiveDependency.bundle.scp.css"),
                    Path.Combine("contentFiles", "exampleJsInterop.js"),
                    Path.Combine("contentFiles", "background.png"),
                    Path.Combine("contentFiles", "PackageLibraryTransitiveDependency.bundle.scp.css"),
                });
        }

        [Fact]
        public void Pack_MultipleTargetFrameworks_WithScopedCssAndJsModules_IncludesAssetsAndProjectBundle()
        {
            var testAsset = "PackageLibraryTransitiveDependency";
            var projectDirectory = CreateAspNetSdkTestAsset(testAsset, subdirectory: "TestPackages");

            projectDirectory.WithProjectChanges(document =>
            {
                var parse = XDocument.Parse($@"<Project Sdk=""Microsoft.NET.Sdk.Razor"">

  <PropertyGroup>
    <TargetFrameworks>net8.0;{ToolsetInfo.CurrentTargetFramework};net6.0;net5.0</TargetFrameworks>
    <Nullable>enable</Nullable>
  </PropertyGroup>

  <ItemGroup>
    <SupportedPlatform Condition=""'$(TargetFramework)' == 'net6.0' OR '$(TargetFramework)' == 'net8.0' OR '$(TargetFramework)' == '{ToolsetInfo.CurrentTargetFramework}'"" Include=""browser"" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include=""Microsoft.AspNetCore.Components.Web"" Version=""{DefaultPackageVersion}"" />
  </ItemGroup>

</Project>
");
                document.Root.ReplaceWith(parse.Root);
            });

            Directory.Delete(Path.Combine(projectDirectory.Path, "wwwroot"), recursive: true);

            var componentText = @"<div class=""my-component"">
    This component is defined in the <strong>razorclasslibrarypack</strong> library.
</div>";

            // This mimics the structure of our default template project
            Directory.CreateDirectory(Path.Combine(projectDirectory.Path, "wwwroot"));
            File.WriteAllText(Path.Combine(projectDirectory.Path, "_Imports.razor"), "@using Microsoft.AspNetCore.Components.Web" + Environment.NewLine);
            File.WriteAllText(Path.Combine(projectDirectory.Path, "Component1.razor"), componentText);
            File.WriteAllText(Path.Combine(projectDirectory.Path, "Component1.razor.css"), "");
            File.WriteAllText(Path.Combine(projectDirectory.Path, "Component1.razor.js"), "");
            File.WriteAllText(Path.Combine(projectDirectory.Path, "ExampleJsInterop.cs"), "");
            File.WriteAllText(Path.Combine(projectDirectory.Path, "wwwroot", "background.png"), "");
            File.WriteAllText(Path.Combine(projectDirectory.Path, "wwwroot", "PackageLibraryTransitiveDependency.lib.module.js"), "");
            File.WriteAllText(Path.Combine(projectDirectory.Path, "wwwroot", "exampleJsInterop.js"), "");

            var pack = new MSBuildCommand(Log, "Pack", projectDirectory.Path);
            pack.WithWorkingDirectory(projectDirectory.Path);
            var result = pack.Execute();

            result.Should().Pass();

            var outputPath = pack.GetOutputDirectory(DefaultTfm, "Debug").ToString();

            new FileInfo(Path.Combine(outputPath, "PackageLibraryTransitiveDependency.dll")).Should().Exist();

            var packagePath = Path.Combine(
                projectDirectory.Path,
                "bin",
                "Debug",
                "PackageLibraryTransitiveDependency.1.0.0.nupkg");

            result.Should().NuPkgContain(
                packagePath,
                filePaths: new[]
                {
                    Path.Combine("staticwebassets", "exampleJsInterop.js"),
                    Path.Combine("staticwebassets", "background.png"),
                    Path.Combine("staticwebassets", "Component1.razor.js"),
                    Path.Combine("staticwebassets", "PackageLibraryTransitiveDependency.bundle.scp.css"),
                    Path.Combine("staticwebassets", "PackageLibraryTransitiveDependency.lib.module.js"),
                    Path.Combine("build", "Microsoft.AspNetCore.StaticWebAssets.props"),
                    Path.Combine("build", "PackageLibraryTransitiveDependency.props"),
                    Path.Combine("buildMultiTargeting", "PackageLibraryTransitiveDependency.props"),
                    Path.Combine("buildTransitive", "PackageLibraryTransitiveDependency.props")
                });
        }

        [Fact]
        public void Pack_Incremental_MultipleTargetFrameworks_WithScopedCssAndJsModules_IncludesAssetsAndProjectBundle()
        {
            var testAsset = "PackageLibraryTransitiveDependency";
            var projectDirectory = CreateAspNetSdkTestAsset(testAsset, subdirectory: "TestPackages");

            projectDirectory.WithProjectChanges(document =>
            {
                var parse = XDocument.Parse($@"<Project Sdk=""Microsoft.NET.Sdk.Razor"">

  <PropertyGroup>
    <TargetFrameworks>net8.0;{ToolsetInfo.CurrentTargetFramework};net6.0;net5.0</TargetFrameworks>
    <Nullable>enable</Nullable>
  </PropertyGroup>

  <ItemGroup>
    <SupportedPlatform Condition=""'$(TargetFramework)' == 'net6.0' OR '$(TargetFramework)' == 'net8.0' OR '$(TargetFramework)' == '{ToolsetInfo.CurrentTargetFramework}'"" Include=""browser"" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include=""Microsoft.AspNetCore.Components.Web"" Version=""{DefaultPackageVersion}"" />
  </ItemGroup>

</Project>
");
                document.Root.ReplaceWith(parse.Root);
            });

            Directory.Delete(Path.Combine(projectDirectory.Path, "wwwroot"), recursive: true);

            var componentText = @"<div class=""my-component"">
    This component is defined in the <strong>razorclasslibrarypack</strong> library.
</div>";

            // This mimics the structure of our default template project
            Directory.CreateDirectory(Path.Combine(projectDirectory.Path, "wwwroot"));
            File.WriteAllText(Path.Combine(projectDirectory.Path, "_Imports.razor"), "@using Microsoft.AspNetCore.Components.Web" + Environment.NewLine);
            File.WriteAllText(Path.Combine(projectDirectory.Path, "Component1.razor"), componentText);
            File.WriteAllText(Path.Combine(projectDirectory.Path, "Component1.razor.css"), "");
            File.WriteAllText(Path.Combine(projectDirectory.Path, "Component1.razor.js"), "");
            File.WriteAllText(Path.Combine(projectDirectory.Path, "ExampleJsInterop.cs"), "");
            File.WriteAllText(Path.Combine(projectDirectory.Path, "wwwroot", "background.png"), "");
            File.WriteAllText(Path.Combine(projectDirectory.Path, "wwwroot", "PackageLibraryTransitiveDependency.lib.module.js"), "");
            File.WriteAllText(Path.Combine(projectDirectory.Path, "wwwroot", "exampleJsInterop.js"), "");

            var pack = new MSBuildCommand(Log, "Pack", projectDirectory.Path);

            var pack2 = new MSBuildCommand(Log, "Pack", projectDirectory.Path);
            pack2.WithWorkingDirectory(projectDirectory.Path);
            var result2 = pack2.Execute();

            result2.Should().Pass();

            var outputPath = pack2.GetOutputDirectory(DefaultTfm, "Debug").ToString();

            new FileInfo(Path.Combine(outputPath, "PackageLibraryTransitiveDependency.dll")).Should().Exist();

            var packagePath = Path.Combine(
                projectDirectory.Path,
                "bin",
                "Debug",
                "PackageLibraryTransitiveDependency.1.0.0.nupkg");

            result2.Should().NuPkgContain(
                packagePath,
                filePaths: new[]
                {
                    Path.Combine("staticwebassets", "exampleJsInterop.js"),
                    Path.Combine("staticwebassets", "background.png"),
                    Path.Combine("staticwebassets", "Component1.razor.js"),
                    Path.Combine("staticwebassets", "PackageLibraryTransitiveDependency.bundle.scp.css"),
                    Path.Combine("staticwebassets", "PackageLibraryTransitiveDependency.lib.module.js"),
                    Path.Combine("build", "Microsoft.AspNetCore.StaticWebAssets.props"),
                    Path.Combine("build", "PackageLibraryTransitiveDependency.props"),
                    Path.Combine("buildMultiTargeting", "PackageLibraryTransitiveDependency.props"),
                    Path.Combine("buildTransitive", "PackageLibraryTransitiveDependency.props")
                });
        }

        [Fact]
        public void Pack_MultipleTargetFrameworks_WithScopedCssAndJsModules_DoesNotIncludeApplicationBundleNorModulesManifest()
        {
            var testAsset = "PackageLibraryTransitiveDependency";
            var projectDirectory = CreateAspNetSdkTestAsset(testAsset, subdirectory: "TestPackages");

            projectDirectory.WithProjectChanges(document =>
            {
                var parse = XDocument.Parse($@"<Project Sdk=""Microsoft.NET.Sdk.Razor"">

  <PropertyGroup>
    <TargetFrameworks>net8.0;{ToolsetInfo.CurrentTargetFramework};net6.0;net5.0</TargetFrameworks>
    <Nullable>enable</Nullable>
  </PropertyGroup>

  <ItemGroup>
    <SupportedPlatform Condition=""'$(TargetFramework)' == 'net6.0' OR '$(TargetFramework)' == 'net8.0' OR '$(TargetFramework)' == '{ToolsetInfo.CurrentTargetFramework}'"" Include=""browser"" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include=""Microsoft.AspNetCore.Components.Web"" Version=""{DefaultPackageVersion}"" />
  </ItemGroup>

</Project>
");
                document.Root.ReplaceWith(parse.Root);
            });

            Directory.Delete(Path.Combine(projectDirectory.Path, "wwwroot"), recursive: true);

            var componentText = @"<div class=""my-component"">
    This component is defined in the <strong>razorclasslibrarypack</strong> library.
</div>";

            // This mimics the structure of our default template project
            Directory.CreateDirectory(Path.Combine(projectDirectory.Path, "wwwroot"));
            File.WriteAllText(Path.Combine(projectDirectory.Path, "_Imports.razor"), "@using Microsoft.AspNetCore.Components.Web" + Environment.NewLine);
            File.WriteAllText(Path.Combine(projectDirectory.Path, "Component1.razor"), componentText);
            File.WriteAllText(Path.Combine(projectDirectory.Path, "Component1.razor.css"), "");
            File.WriteAllText(Path.Combine(projectDirectory.Path, "ExampleJsInterop.cs"), "");
            File.WriteAllText(Path.Combine(projectDirectory.Path, "wwwroot", "background.png"), "");
            File.WriteAllText(Path.Combine(projectDirectory.Path, "wwwroot", "exampleJsInterop.js"), "");

            var pack = new MSBuildCommand(Log, "Pack", projectDirectory.Path);
            pack.WithWorkingDirectory(projectDirectory.Path);
            var result = pack.Execute();

            result.Should().Pass();

            var outputPath = pack.GetOutputDirectory(DefaultTfm, "Debug").ToString();

            new FileInfo(Path.Combine(outputPath, "PackageLibraryTransitiveDependency.dll")).Should().Exist();

            var packagePath = Path.Combine(
                projectDirectory.Path,
                "bin",
                "Debug",
                "PackageLibraryTransitiveDependency.1.0.0.nupkg");

            result.Should().NuPkgDoesNotContain(
                packagePath,
                filePaths: new[]
                {
                    Path.Combine("staticwebassets", "PackageLibraryTransitiveDependency.styles.css"),
                    Path.Combine("staticwebassets", "PackageLibraryTransitiveDependency.modules.json"),
                });
        }

        [Fact]
        public void Pack_MultipleTargetFrameworks_DoesNotIncludeAssetsAsContent()
        {
            var testAsset = "PackageLibraryDirectDependency";
            var projectDirectory = CreateAspNetSdkTestAsset(testAsset, subdirectory: "TestPackages");

            projectDirectory.WithProjectChanges((project, document) =>
            {
                var tfm = document.Descendants("TargetFramework").Single();
                tfm.Name = "TargetFrameworks";
                tfm.FirstNode.ReplaceWith(tfm.FirstNode.ToString() + ";netstandard2.1");

                document.Descendants("AddRazorSupportForMvc").SingleOrDefault()?.Remove();
                document.Descendants("FrameworkReference").SingleOrDefault()?.Remove();
            });

            Directory.Delete(Path.Combine(projectDirectory.Path, "PackageLibraryDirectDependency", "Components"), recursive: true);

            var pack = new MSBuildCommand(projectDirectory, "Pack", "PackageLibraryDirectDependency");
            pack.WithWorkingDirectory(projectDirectory.Path);
            var result = pack.Execute();

            result.Should().Pass();

            var outputPath = pack.GetOutputDirectory(DefaultTfm, "Debug").ToString();

            new FileInfo(Path.Combine(outputPath, "PackageLibraryDirectDependency.dll")).Should().Exist();

            result.Should().NuPkgDoesNotContain(
                Path.Combine(projectDirectory.Path, "PackageLibraryDirectDependency", "bin", "Debug", "PackageLibraryDirectDependency.1.0.0.nupkg"),
                filePaths: new[]
                {
                    Path.Combine("content", "wwwroot", "js", "pkg-direct-dep.js"),
                    Path.Combine("content", "wwwroot", "css", "site.css"),
                    Path.Combine("contentFiles", "wwwroot", "js", "pkg-direct-dep.js"),
                    Path.Combine("contentFiles", "wwwroot", "css", "site.css"),
                });
        }

        [Fact]
        public void Pack_DoesNotInclude_TransitiveBundleOrScopedCssAsStaticWebAsset()
        {
            var testAsset = "PackageLibraryDirectDependency";
            var projectDirectory = CreateAspNetSdkTestAsset(testAsset, subdirectory: "TestPackages");

            var pack = new MSBuildCommand(projectDirectory, "Pack", "PackageLibraryDirectDependency");
            pack.WithWorkingDirectory(projectDirectory.TestRoot);
            var result = pack.Execute();

            result.Should().Pass();

            var outputPath = pack.GetOutputDirectory(DefaultTfm, "Debug").ToString();

            new FileInfo(Path.Combine(outputPath, "PackageLibraryDirectDependency.dll")).Should().Exist();

            result.Should().NuPkgDoesNotContain(
                Path.Combine(pack.GetPackageDirectory().FullName, "PackageLibraryDirectDependency.1.0.0.nupkg"),
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

            var pack = new MSBuildCommand(projectDirectory, "Pack", "PackageLibraryDirectDependency");
            var result = pack.Execute();

            result.Should().Pass();

            var outputPath = pack.GetOutputDirectory(DefaultTfm, "Debug").ToString();

            new FileInfo(Path.Combine(outputPath, "PackageLibraryDirectDependency.dll")).Should().Exist();

            result.Should().NuPkgDoesNotContain(
                Path.Combine(pack.GetPackageDirectory().FullName, "PackageLibraryDirectDependency.1.0.0.nupkg"),
                filePaths: new[]
                {
                    Path.Combine("content", "wwwroot", "js", "pkg-direct-dep.js"),
                    Path.Combine("content", "wwwroot", "css", "site.css"),
                    Path.Combine("content", "Components", "App.razor.css"),
                    // This is to make sure we don't include the unscoped css file on the package.
                    Path.Combine("content", "Components", "App.razor.css"),
                    Path.Combine("content", "Components", "App.razor.rz.scp.css"),
                    Path.Combine("contentFiles", "wwwroot", "js", "pkg-direct-dep.js"),
                    Path.Combine("contentFiles", "wwwroot", "css", "site.css"),
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

            var pack = new MSBuildCommand(projectDirectory, "Pack", "PackageLibraryDirectDependency");
            pack.WithWorkingDirectory(projectDirectory.TestRoot);
            var result = pack.Execute("/p:NoBuild=true");

            var outputPath = pack.GetOutputDirectory(DefaultTfm, "Debug").ToString();

            new FileInfo(Path.Combine(outputPath, "PackageLibraryDirectDependency.dll")).Should().Exist();

            result.Should().NuPkgContain(
                Path.Combine(build.GetPackageDirectory().FullName, "PackageLibraryDirectDependency.1.0.0.nupkg"),
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
        public void Pack_NoBuild_DoesNotIncludeFilesAsContent()
        {
            var testAsset = "PackageLibraryDirectDependency";
            var projectDirectory = CreateAspNetSdkTestAsset(testAsset, subdirectory: "TestPackages");

            var build = new BuildCommand(projectDirectory, "PackageLibraryDirectDependency");
            build.Execute().Should().Pass();

            var pack = new MSBuildCommand(projectDirectory, "Pack", "PackageLibraryDirectDependency");
            pack.WithWorkingDirectory(projectDirectory.TestRoot);
            var result = pack.Execute("/p:NoBuild=true");

            var outputPath = pack.GetOutputDirectory(DefaultTfm, "Debug").ToString();

            new FileInfo(Path.Combine(outputPath, "PackageLibraryDirectDependency.dll")).Should().Exist();

            result.Should().NuPkgDoesNotContain(
                Path.Combine(pack.GetPackageDirectory().FullName, "PackageLibraryDirectDependency.1.0.0.nupkg"),
                filePaths: new[]
                {
                    Path.Combine("content", "wwwroot", "js", "pkg-direct-dep.js"),
                    Path.Combine("content", "PackageLibraryDirectDependency.bundle.scp.css"),
                    Path.Combine("content", "wwwroot", "css", "site.css"),
                    Path.Combine("contentFiles", "wwwroot", "js", "pkg-direct-dep.js"),
                    Path.Combine("contentFiles", "PackageLibraryDirectDependency.bundle.scp.css"),
                    Path.Combine("contentFiles", "wwwroot", "css", "site.css"),
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

            var pack = new MSBuildCommand(projectDirectory, "Pack");
            pack.WithWorkingDirectory(projectDirectory.TestRoot);
            var result = pack.Execute();

            var intermediateOutputPath = pack.GetIntermediateDirectory(DefaultTfm, "Debug").ToString();
            var outputPath = pack.GetOutputDirectory(DefaultTfm, "Debug").ToString();

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
