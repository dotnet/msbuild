// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.AspNetCore.StaticWebAssets.Tasks;

namespace Microsoft.NET.Sdk.Razor.Tests
{
    public class StaticWebAssetsCrossTargetingTests : AspNetSdkBaselineTest
    {
        public StaticWebAssetsCrossTargetingTests(ITestOutputHelper log) : base(log, GenerateBaselines) { }

        // Build Standalone project
        [Fact]
        public void Build_CrosstargetingTests_CanIncludeBrowserAssets()
        {
            var expectedManifest = LoadBuildManifest();
            var testAsset = "RazorComponentAppMultitarget";
            ProjectDirectory = CreateAspNetSdkTestAsset(testAsset);

            ProjectDirectory.WithProjectChanges(d =>
            {
                d.Root.Element("PropertyGroup").Add(
                    XElement.Parse("""<StaticWebAssetBasePath>/</StaticWebAssetBasePath>"""));

                d.Root.LastNode.AddBeforeSelf(
                    XElement.Parse("""
                        <ItemGroup>
                          <StaticWebAssetsEmbeddedConfiguration
                            Include="$(TargetFramework)"
                            Condition="'$([MSBuild]::GetTargetPlatformIdentifier($(TargetFramework)))' == '' And $([MSBuild]::VersionGreaterThanOrEquals(8.0, $([MSBuild]::GetTargetFrameworkVersion($(TargetFramework)))))">
                            <Platform>browser</Platform>
                          </StaticWebAssetsEmbeddedConfiguration>
                        </ItemGroup>
                        """));
            });

            var wwwroot = Directory.CreateDirectory(Path.Combine(ProjectDirectory.TestRoot, "wwwroot"));
            File.WriteAllText(Path.Combine(wwwroot.FullName, "test.js"), "console.log('hello')");

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
            AssertBuildAssets(manifest, outputPath, intermediateOutputPath);

            // GenerateStaticWebAssetsManifest should copy the file to the output folder.
            var finalPath = Path.Combine(outputPath, "RazorComponentAppMultitarget.staticwebassets.runtime.json");
            new FileInfo(finalPath).Should().Exist();
        }

        [Fact]
        public void Publish_CrosstargetingTests_CanIncludeBrowserAssets()
        {
            var testAsset = "RazorComponentAppMultitarget";
            ProjectDirectory = CreateAspNetSdkTestAsset(testAsset);

            ProjectDirectory.WithProjectChanges(d =>
            {
                d.Root.Element("PropertyGroup").Add(
                    XElement.Parse("""<StaticWebAssetBasePath>/</StaticWebAssetBasePath>"""));

                d.Root.LastNode.AddBeforeSelf(
                    XElement.Parse("""
                        <ItemGroup>
                          <StaticWebAssetsEmbeddedConfiguration
                            Include="$(TargetFramework)"
                            Condition="'$([MSBuild]::GetTargetPlatformIdentifier($(TargetFramework)))' == '' And $([MSBuild]::VersionGreaterThanOrEquals(8.0, $([MSBuild]::GetTargetFrameworkVersion($(TargetFramework)))))">
                            <Platform>browser</Platform>
                          </StaticWebAssetsEmbeddedConfiguration>
                        </ItemGroup>
                        """));
            });

            var wwwroot = Directory.CreateDirectory(Path.Combine(ProjectDirectory.TestRoot, "wwwroot"));
            File.WriteAllText(Path.Combine(wwwroot.FullName, "test.js"), "console.log('hello')");

            var restore = new RestoreCommand(ProjectDirectory);
            restore.WithWorkingDirectory(ProjectDirectory.TestRoot);
            restore.Execute().Should().Pass();

            var publish = new PublishCommand(ProjectDirectory);
            publish.WithWorkingDirectory(ProjectDirectory.TestRoot);
            publish.ExecuteWithoutRestore("/bl", "/p:TargetFramework=net8.0").Should().Pass();

            var publishPath = publish.GetOutputDirectory(DefaultTfm).ToString();
            var intermediateOutputPath = publish.GetIntermediateDirectory(DefaultTfm, "Debug").ToString();

            // GenerateStaticWebAssetsManifest should generate the manifest file.
            var path = Path.Combine(intermediateOutputPath, "staticwebassets.publish.json");
            new FileInfo(path).Should().Exist();
            var manifest = StaticWebAssetsManifest.FromJsonBytes(File.ReadAllBytes(path));
            AssertManifest(manifest, LoadPublishManifest());

            AssertPublishAssets(
                manifest,
                publishPath,
                intermediateOutputPath);
        }
    }
}
