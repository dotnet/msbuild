// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using FluentAssertions;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Xunit.Abstractions;

namespace Microsoft.DotNet.Cli.New.IntegrationTests
{
    public class FirstRunTest : BaseIntegrationTest
    {
        private readonly ITestOutputHelper _log;

        public FirstRunTest(ITestOutputHelper log) : base(log)
        {
            _log = log;
        }

        [Fact]
        public void FirstRunSuccess()
        {
            var home = CreateTemporaryFolder(folderName: "Home");
            new DotnetNewCommand(_log)
                .WithCustomHive(home)
                .Execute()
                .Should()
                .ExitWith(0)
                .And.NotHaveStdErr()
                .And.NotHaveStdOutContaining("Error");

            new DotnetNewCommand(_log, "--list")
                .WithCustomHive(home)
                .Execute()
                .Should()
                .ExitWith(0)
                .And.NotHaveStdErr()
                .And.HaveStdOutContaining("classlib");
        }
    }
}
