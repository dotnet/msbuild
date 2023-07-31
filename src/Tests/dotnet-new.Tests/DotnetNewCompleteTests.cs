// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Cli.New.IntegrationTests
{
    [UsesVerify]
    public class DotnetNewCompleteTests : BaseIntegrationTest
    {
        private readonly ITestOutputHelper _log;

        public DotnetNewCompleteTests(ITestOutputHelper log) : base(log)
        {
            _log = log;
        }

        [Fact]
        public Task CanDoTabCompletion()
        {
            string homeDir = CreateTemporaryFolder();
            CommandResult commandResult = new DotnetCommand(_log, "complete", $"new --debug:custom-hive {homeDir} ").Execute();

            commandResult
                .Should()
                .ExitWith(0)
                .And.NotHaveStdErr();

            return Verify(commandResult.StdOut).UniqueForOSPlatform();
        }

#pragma warning disable xUnit1004 // Test methods should not be skipped
        [Fact(Skip = "https://github.com/dotnet/command-line-api/issues/1519")]
#pragma warning restore xUnit1004 // Test methods should not be skipped
        public void CanDoTabCompletionAtGivenPosition()
        {
            string homeDir = CreateTemporaryFolder();
            CommandResult commandResult = new DotnetCommand(_log, "complete", $"new co --debug:custom-hive {homeDir} --language C#", "--position", "7")
                .Execute();

            commandResult
                .Should()
                .ExitWith(0)
                .And.NotHaveStdErr()
                .And.HaveStdOut("console");
        }
    }
}
