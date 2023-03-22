// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using FluentAssertions;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Xunit.Abstractions;

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
