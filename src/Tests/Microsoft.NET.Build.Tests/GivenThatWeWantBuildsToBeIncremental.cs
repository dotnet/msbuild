// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.NET.Build.Tests
{
    public class GivenThatWeWantBuildsToBeIncremental : SdkTest
    {
        public GivenThatWeWantBuildsToBeIncremental(ITestOutputHelper log) : base(log)
        {
        }

        [Theory]
        [InlineData("netcoreapp1.1")]
        [InlineData(ToolsetInfo.CurrentTargetFramework)]
        public void GenerateBuildRuntimeConfigurationFiles_runs_incrementally(string targetFramework)
        {
            var testAsset = _testAssetsManager
                .CopyTestAsset("HelloWorld", identifier: targetFramework)
                .WithSource()
                .WithTargetFramework(targetFramework);

            var buildCommand = new BuildCommand(testAsset);
            var outputDirectory = buildCommand.GetOutputDirectory(targetFramework).FullName;
            var runtimeConfigDevJsonPath = Path.Combine(outputDirectory, "HelloWorld.runtimeconfig.dev.json");

            buildCommand.Execute().Should().Pass();
            DateTime runtimeConfigDevJsonFirstModifiedTime = File.GetLastWriteTimeUtc(runtimeConfigDevJsonPath);

            buildCommand.Execute().Should().Pass();
            DateTime runtimeConfigDevJsonSecondModifiedTime = File.GetLastWriteTimeUtc(runtimeConfigDevJsonPath);

            runtimeConfigDevJsonSecondModifiedTime.Should().Be(runtimeConfigDevJsonFirstModifiedTime);
        }

        [Theory]
        [InlineData("netcoreapp1.1")]
        [InlineData(ToolsetInfo.CurrentTargetFramework)]
        public void ResolvePackageAssets_runs_incrementally(string targetFramework)
        {
            var testAsset = _testAssetsManager
                .CopyTestAsset("HelloWorld", identifier: targetFramework)
                .WithSource()
                .WithTargetFramework(targetFramework);

            var buildCommand = new BuildCommand(testAsset);
            var outputDirectory = buildCommand.GetOutputDirectory(targetFramework).FullName;
            var baseIntermediateOutputDirectory = buildCommand.GetBaseIntermediateDirectory().FullName;
            var intermediateDirectory = buildCommand.GetIntermediateDirectory(targetFramework).FullName;

            var assetsJsonPath = Path.Combine(baseIntermediateOutputDirectory, "project.assets.json");
            var assetsCachePath = Path.Combine(intermediateDirectory, "HelloWorld.assets.cache");

            // initial build
            buildCommand.Execute().Should().Pass();
            var cacheWriteTime1 = File.GetLastWriteTimeUtc(assetsCachePath);

            // build with no change to project.assets.json
            WaitForUtcNowToAdvance();
            buildCommand.Execute().Should().Pass();
            var cacheWriteTime2 = File.GetLastWriteTimeUtc(assetsCachePath);
            cacheWriteTime2.Should().Be(cacheWriteTime1);

            // build with modified project
            WaitForUtcNowToAdvance();
            File.SetLastWriteTimeUtc(assetsJsonPath, DateTime.UtcNow);
            buildCommand.Execute().Should().Pass();
            var cacheWriteTime3 = File.GetLastWriteTimeUtc(assetsCachePath);
            cacheWriteTime3.Should().NotBe(cacheWriteTime2);

            // build with modified settings
            WaitForUtcNowToAdvance();
            buildCommand.Execute("/p:DisableLockFileFrameworks=true").Should().Pass();
            var cacheWriteTime4 = File.GetLastWriteTimeUtc(assetsCachePath);
            cacheWriteTime4.Should().NotBe(cacheWriteTime3);
        }
    }
}
