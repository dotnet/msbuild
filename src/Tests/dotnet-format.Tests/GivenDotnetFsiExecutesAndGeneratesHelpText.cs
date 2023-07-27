// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Cli.Format.Tests
{
    public class GivenDotnetFormatExecutesAndGeneratesHelpText : SdkTest
    {
        public GivenDotnetFormatExecutesAndGeneratesHelpText(ITestOutputHelper log) : base(log)
        {
        }

        [Fact]
        public void ItRuns()
        {
            new DotnetCommand(Log, "format")
                .Execute("--help")
                .Should().Pass();
        }
    }
}
