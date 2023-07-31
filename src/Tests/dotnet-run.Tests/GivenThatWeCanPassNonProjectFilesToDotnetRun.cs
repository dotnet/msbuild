// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Cli.Run.Tests
{
    public class GivenThatWeCanPassNonProjectFilesToDotnetRun : SdkTest
    {
        public GivenThatWeCanPassNonProjectFilesToDotnetRun(ITestOutputHelper log) : base(log)
        {
        }

        [Fact]
        public void ItFailsWithAnAppropriateErrorMessage()
        {
            var projectDirectory = _testAssetsManager
                .CopyTestAsset("SlnFileWithNoProjectReferences")
                .WithSource()
                .Path;

            var slnFullPath = Path.Combine(projectDirectory, "SlnFileWithNoProjectReferences.sln");

            new DotnetCommand(Log, "run")
                .Execute($"-p", slnFullPath)
                .Should().Fail()
                .And.HaveStdErrContaining(
                    string.Format(
                        Microsoft.DotNet.Tools.Run.LocalizableStrings.RunCommandSpecifiecFileIsNotAValidProject,
                        slnFullPath));
        }
    }
}
