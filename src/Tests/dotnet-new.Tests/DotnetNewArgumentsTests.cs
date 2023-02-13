// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using FluentAssertions;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Xunit.Abstractions;

namespace Microsoft.DotNet.Cli.New.IntegrationTests
{
    public partial class DotnetNewArgumentsTests
    {
        private readonly ITestOutputHelper _log;

        public DotnetNewArgumentsTests(ITestOutputHelper log)
        {
            _log = log;
        }

        [Fact]
        public void ShowsDetailedOutputOnMissedRequiredParam()
        {
            var dotnetNewHelpOutput = new DotnetNewCommand(_log, "--help")
                .WithoutCustomHive()
                .Execute();

            new DotnetNewCommand(_log, "-v")
                .WithoutCustomHive()
                .Execute()
                .Should()
                .ExitWith(127)
                .And.HaveStdErrContaining("Required argument missing for option: -v")
                .And.HaveStdOutContaining(dotnetNewHelpOutput.StdOut);
        }
    }
}
