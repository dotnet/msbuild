// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using NuGet.Common;
using NuGet.ProjectModel;

namespace Microsoft.NET.Clean.Tests
{
    public class GivenThatWeWantToCleanAHelloWorldProject : SdkTest
    {
        public GivenThatWeWantToCleanAHelloWorldProject(ITestOutputHelper log) : base(log)
        {
        }

        [RequiresMSBuildVersionFact("17.8.0")]
        public void It_cleans_without_logging_assets_message()
        {
            var testAsset = _testAssetsManager
                .CopyTestAsset("HelloWorld", "CleanHelloWorld")
                .WithSource()
                .Restore(Log);

            var lockFilePath = Path.Combine(testAsset.TestRoot, "obj", "project.assets.json");
            LockFile lockFile = LockFileUtilities.GetLockFile(lockFilePath, NullLogger.Instance);

            lockFile.LogMessages.Add(
                new AssetsLogMessage(
                    LogLevel.Warning,
                    NuGetLogCode.NU1500,
                    "a test warning",
                    null));

            new LockFileFormat().Write(lockFilePath, lockFile);

            var cleanCommand = new CleanCommand(Log, testAsset.TestRoot);

            cleanCommand
                .Execute("/p:CheckEolTargetFramework=false")
                .Should()
                .Pass()
                .And
                .NotHaveStdOutContaining("warning");
        }

        [Fact]
        public void It_cleans_without_assets_file_present()
        {
            var testAsset = _testAssetsManager
                .CopyTestAsset("HelloWorld")
                .WithSource();

            var assetsFilePath = Path.Combine(testAsset.TestRoot, "obj", "project.assets.json");
            File.Exists(assetsFilePath).Should().BeFalse();

            var cleanCommand = new CleanCommand(Log, testAsset.TestRoot);

            cleanCommand
                .Execute()
                .Should()
                .Pass();
        }

        // Related to https://github.com/dotnet/sdk/issues/2233
        // This test will fail if the naive fix for not reading assets file during clean is attempted
        [Fact]
        public void It_can_clean_and_build_without_using_rebuild()
        {
            var testAsset = _testAssetsManager
              .CopyTestAsset("HelloWorld")
              .WithSource();

            var cleanAndBuildCommand = new MSBuildCommand(Log, "Clean;Build", testAsset.TestRoot);

            cleanAndBuildCommand
                .Execute()
                .Should()
                .Pass();
        }
    }
}
