// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using LocalizableStrings = Microsoft.DotNet.Tools.Run.LocalizableStrings;

namespace Microsoft.DotNet.Cli.Run.Tests
{
    public class GivenDotnetRunThrowsAParseError : SdkTest
    {
        public GivenDotnetRunThrowsAParseError(ITestOutputHelper log) : base(log)
        {
        }

        [Fact]
        public void ItFailsWithAnAppropriateErrorMessage()
        {
            new DotnetCommand(Log, "run")
                // executing in a known path, with no project, is a sure way to get run to throw a parse error
                .WithWorkingDirectory(Path.GetTempPath())
                .Execute("--", "1")
                .Should().Fail()
                .And.HaveStdErrContainingOnce(LocalizableStrings.RunCommandExceptionNoProjects);
        }
    }
}
