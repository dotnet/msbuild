// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

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
                .And.HaveStdErrContaining("Required argument missing for option: '-v'")
                .And.HaveStdOutContaining(dotnetNewHelpOutput.StdOut);
        }
    }
}
