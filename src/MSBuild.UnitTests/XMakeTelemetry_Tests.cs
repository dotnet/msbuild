// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Threading;
using System.Threading.Tasks;
using Microsoft.Build.CommandLine;
using Microsoft.Build.Framework;
using Microsoft.Build.Framework.Telemetry;
using Shouldly;
using Xunit;

namespace Microsoft.Build.UnitTests;

public sealed class XMakeTelemetry_Tests
{
    // NodeMode is internal, so cases are passed as the /nodemode integer values.
    [Theory]
    [InlineData(null, true)]
    [InlineData(8, true)] // OutOfProcServerNode
    [InlineData(1, false)] // OutOfProcNode
    [InlineData(2, false)] // OutOfProcTaskHostNode
    [InlineData(3, false)] // OutOfProcRarNode
    public void OnlyProcessesThatReportBuildsOwnATelemetrySession(int? nodeMode, bool expected)
    {
        MSBuildApp.OwnsProcessTelemetrySession((NodeMode?)nodeMode).ShouldBe(expected);
    }

    /// <summary>
    /// The MSBuild server node runs <see cref="MSBuildApp.Execute(string[])"/> once per request in a single process,
    /// so a request must never shut down the process-wide telemetry session.
    /// </summary>
    [Fact]
    public void BuildRequestsDoNotShutDownProcessTelemetry()
    {
        using TestEnvironment env = TestEnvironment.Create();
        TransientTestFile successProject = env.CreateFile("success.proj", """
            <Project>
              <Target Name="Build"><Message Text="ok" /></Target>
            </Project>
            """);
        TransientTestFile failureProject = env.CreateFile("failure.proj", """
            <Project>
              <Target Name="Build"><Error Text="intentional" /></Target>
            </Project>
            """);

        int shutdownCalls = 0;
        TelemetryManager.ResetForTest();
        TelemetryManager.SetOwnedSessionShutdownForTest((_, _) =>
        {
            Interlocked.Increment(ref shutdownCalls);
            return Task.CompletedTask;
        });

        try
        {
            MSBuildApp.Execute([@"c:\bin\msbuild.exe", successProject.Path, "-nologo"]).ShouldBe(MSBuildApp.ExitType.Success);
            MSBuildApp.Execute([@"c:\bin\msbuild.exe", failureProject.Path, "-nologo"]).ShouldBe(MSBuildApp.ExitType.BuildError);
            MSBuildApp.Execute([@"c:\bin\msbuild.exe", successProject.Path, "-nologo"]).ShouldBe(MSBuildApp.ExitType.Success);

            shutdownCalls.ShouldBe(0);
            TelemetryManager.IsDisposed.ShouldBeFalse();

            // The process-level shutdown happens exactly once, after the last request.
            TelemetryManager.Instance.Dispose();
            shutdownCalls.ShouldBe(1);
        }
        finally
        {
            TelemetryManager.ResetForTest();
        }
    }
}
