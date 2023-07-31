// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Cli.Fsi.Tests
{
    public class GivenDotnetFsiExecutesAndGeneratesHelpText : SdkTest
    {
        public GivenDotnetFsiExecutesAndGeneratesHelpText(ITestOutputHelper log) : base(log)
        {
        }

        [Fact]
        public void ItRuns()
        {
            new DotnetCommand(Log, "fsi")
                .Execute("--help")
                .Should().Pass();
        }
    }
}
