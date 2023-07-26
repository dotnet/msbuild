// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Cli.MSBuild.Tests
{
    public class GivenDotnetVsTestForwardingApp
    {
        [Fact]
        public void ItRunsVsTestApp()
        {
            new VSTestForwardingApp(new string[0])
                .GetProcessStartInfo().Arguments.Should().EndWith("vstest.console.dll");
        }

        [Fact]
        public void ItCanUseEnvironmentVariableToForceCustomPathToVsTestApp()
        {
            string vsTestConsolePath = "VSTEST_CONSOLE_PATH";
            string dummyPath = Path.Join(Path.GetTempPath(), "vstest.custom.console.dll");

            try
            {
                Environment.SetEnvironmentVariable(vsTestConsolePath, dummyPath);
                new VSTestForwardingApp(new string[0])
                    .GetProcessStartInfo().Arguments.Should().EndWith("vstest.custom.console.dll");
            }
            finally
            {
                Environment.SetEnvironmentVariable(vsTestConsolePath, null);
            }
        }
    }
}
