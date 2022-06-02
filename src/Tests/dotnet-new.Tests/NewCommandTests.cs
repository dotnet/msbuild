// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using FluentAssertions;
using Microsoft.DotNet.Tools.Test.Utilities;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Commands;
using Xunit;
using Xunit.Abstractions;
using Microsoft.NET.TestFramework.Assertions;

namespace Microsoft.DotNet.New.Tests
{
    public class NewCommandTests : SdkTest
    {
        public NewCommandTests(ITestOutputHelper log) : base(log)
        {
        }

        [Fact]
        public void WhenSwitchIsSkippedThenItPrintsError()
        {
            var cmd = new DotnetCommand(Log).Execute("new", "Web1.1", "--debug:ephemeral-hive");

            cmd.ExitCode.Should().NotBe(0);

            if (!TestContext.IsLocalized())
            {
                cmd.StdErr.Should().StartWith("No templates found");
            }
        }

        [Fact]
        public void ItCanCreateTemplate()
        {
            var tempDir = _testAssetsManager.CreateTestDirectory();
            var cmd = new DotnetCommand(Log).Execute("new", "console", "-o", tempDir.Path, "--debug:ephemeral-hive");
            cmd.Should().Pass();
        }

        [Fact]
        public void ItCanShowHelp()
        {
            var cmd = new DotnetCommand(Log).Execute("new", "--help", "--debug:ephemeral-hive");
            cmd.Should().Pass()
                .And.HaveStdOutContaining("Usage:")
                .And.HaveStdOutContaining("dotnet new [command] [options]");
        }

        [Fact]
        public void ItCanShowHelpForTemplate()
        {
            var cmd = new DotnetCommand(Log).Execute("new", "classlib", "--help", "--debug:ephemeral-hive");
            cmd.Should().Pass()
                .And.NotHaveStdOutContaining("Usage: new [options]")
                .And.HaveStdOutContaining("Class Library (C#)")
                .And.HaveStdOutContaining("--framework");
        }

        [Fact]
        public void ItCanShowParseError()
        {
            var cmd = new DotnetCommand(Log).Execute("new", "update", "--bla");
            cmd.Should().ExitWith(127)
                .And.HaveStdErrContaining("Unrecognized command or argument '--bla'")
                .And.HaveStdOutContaining("dotnet new update [options]");
        }

        [Fact]
        public void WhenTemplateNameIsNotUniquelyMatchedThenItIndicatesProblemToUser()
        {
            var cmd = new DotnetCommand(Log).Execute("new", "c");

            cmd.ExitCode.Should().NotBe(0);

            if (!TestContext.IsLocalized())
            {
                cmd.StdErr.Should().StartWith("No templates found matching: 'c'.");
            }
        }
    }
}
