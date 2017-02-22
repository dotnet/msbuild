using Microsoft.DotNet.Tools.VSTest;
using FluentAssertions;
using Xunit;
using System;

namespace Microsoft.DotNet.Cli.MSBuild.Tests
{
    public class GivenDotnetVsTestForwardingApp
    {
        [Fact]
        public void ItRunsVsTestApp()
        {
            new VSTestForwardingApp(new string[0])
                .GetProcessStartInfo().FileName.Should().EndWith("vstest.console.dll");
        }
    }
}
