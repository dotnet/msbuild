// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using NuGet.Common;
using NuGet.ProjectModel;

namespace Microsoft.NET.Rebuild.Tests
{
    public class GivenThatWeWantToRebuildAHelloWorldProject : SdkTest
    {
        public GivenThatWeWantToRebuildAHelloWorldProject(ITestOutputHelper log) : base(log)
        {
        }

        [Fact]
        public void It_rebuilds_with_logging_assets_message()
        {
            var testAsset = _testAssetsManager
                .CopyTestAsset("HelloWorld", "RebuildHelloWorld")
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

            var rebuildCommand = new RebuildCommand(Log, testAsset.TestRoot);

            rebuildCommand
                .ExecuteWithoutRestore()
                .Should()
                .Pass()
                .And
                .HaveStdOutContaining("warning");
        }
    }
}
