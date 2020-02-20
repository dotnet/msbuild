// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using FluentAssertions;
using Microsoft.DotNet.Tools.Test.Utilities;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Commands;
using Xunit;
using Xunit.Abstractions;

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
                cmd.StdErr.Should().StartWith("No templates matched the input template name: Web1.1.");
            }
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
