// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using ApprovalTests;
using FluentAssertions;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.TemplateEngine.TestHelper;
using NuGet.Versioning;
using Xunit;
using Xunit.Abstractions;

namespace Dotnet_new3.IntegrationTests
{
    public class DotnetNew3CompleteTests
    {
        private readonly ITestOutputHelper _log;

        public DotnetNew3CompleteTests(ITestOutputHelper log)
        {
            _log = log;
        }

        [Fact]
        public void CanDoTabCompletion()
        {
            string homeDir = TestUtils.CreateTemporaryFolder();
            var commandResult = new DotnetNewCommand(_log, "complete", $"new3 --debug:custom-hive {homeDir} ")
                .WithoutCustomHive()
                .Execute();

            commandResult
                .Should()
                .ExitWith(0)
                .And.NotHaveStdErr();

            Approvals.Verify(commandResult.StdOut);
        }

        [Fact]
        public void CanDoTabCompletionAtGivenPosition()
        {
            string homeDir = TestUtils.CreateTemporaryFolder();
            var commandResult = new DotnetNewCommand(_log, "complete", $"new3 co --debug:custom-hive {homeDir} --language C#", "--position", "7")
                .WithoutCustomHive()
                .Execute();

            commandResult
                .Should()
                .ExitWith(0)
                .And.NotHaveStdErr()
                .And.HaveStdOut("console");
        }
    }
}
