// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using FluentAssertions;
using Microsoft.DotNet.Tools.Test.Utilities;
using Xunit;

namespace Microsoft.DotNet.Cli.Install.Tests
{
    public class GivenDotnetInstallTool : TestBase
    {
        [Fact]
        public void ItRunsWithQuietVerbosityByDefault()
        {
            var result = new ToolCommand()
                .ExecuteWithCapturedOutput("install -g nonexistent_tool_package");

            result
                .Should()
                .Fail()
                .And
                .NotHaveStdOutContaining("Restoring");
        }

        [Fact]
        public void ItRunsWithTheSpecifiedVerbosity()
        {
            var result = new ToolCommand()
                .ExecuteWithCapturedOutput("install -g -v:n nonexistent_tool_package");

            result
                .Should()
                .Fail();

            result
                .StdOut
                .Should()
                .ContainVisuallySameFragmentIfNotLocalized("Restoring");
        }
    }
}
