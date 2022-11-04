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
            var cmd = new DotnetCommand(Log).Execute("new", "Web1.1");

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
            var cmd = new DotnetCommand(Log).Execute("new", "console", "-o", tempDir.Path);
            cmd.Should().Pass();
        }

        [Fact]
        public void ItCanShowHelp()
        {
            var tempDir = _testAssetsManager.CreateTestDirectory();
            var cmd = new DotnetCommand(Log).Execute("new", "--help");
            cmd.Should().Pass()
                .And.HaveStdOutContaining("Usage: new [options]");
        }

        [Fact]
        public void ItCanShowHelpForTemplate()
        {
            var tempDir = _testAssetsManager.CreateTestDirectory();
            var cmd = new DotnetCommand(Log).Execute("new", "classlib", "--help");
            cmd.Should().Pass()
                .And.NotHaveStdOutContaining("Usage: new [options]")
                .And.HaveStdOutContaining("Class Library (C#)")
                .And.HaveStdOutContaining("--framework");
        }

        [Fact(Skip = "https://github.com/dotnet/templating/issues/1971")]
        public void WhenTemplateNameIsNotUniquelyMatchedThenItIndicatesProblemToUser()
        {
            var cmd = new DotnetCommand(Log).Execute("new", "c");

            cmd.ExitCode.Should().NotBe(0);

            if (!TestContext.IsLocalized())
            {
                cmd.StdErr.Should().StartWith("Unable to determine the desired template from the input template name: c.");
            }
        }
    }
}
