// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using FluentAssertions;
using Microsoft.DotNet.Tools.Test.Utilities;
using Xunit;

namespace Microsoft.DotNet.New.Tests
{
    public class NewCommandTests : TestBase
    {
        [Fact]
        public void WhenSwitchIsSkippedThenItPrintsError()
        {
            var cmd = new DotnetCommand().Execute("new Web1.1");

            cmd.ExitCode.Should().NotBe(0);

            if (!DotnetUnderTest.IsLocalized())
            {
                cmd.StdErr.Should().StartWith("No templates matched the input template name: Web1.1.");
            }
        }

        [Fact(Skip = "https://github.com/dotnet/templating/issues/1971")]
        public void WhenTemplateNameIsNotUniquelyMatchedThenItIndicatesProblemToUser()
        {
            var cmd = new DotnetCommand().Execute("new c");

            cmd.ExitCode.Should().NotBe(0);

            if (!DotnetUnderTest.IsLocalized())
            {
                cmd.StdErr.Should().StartWith("Unable to determine the desired template from the input template name: c.");
            }
        }
    }
}
