// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO;
using Microsoft.Build.UnitTests;
using Microsoft.Build.UnitTests.Shared;
using Shouldly;
using Xunit;

namespace Microsoft.Build.Engine.UnitTests;

/// <summary>
/// End-to-end tests for the CLR2 task host (MSBuildTaskHost.exe).
/// These tests explicitly force tasks to run out-of-proc in CLR2 via
/// <c>TaskFactory="TaskHostFactory"</c> and <c>Runtime="CLR2"</c>,
/// exercising the CLR2 branch in <c>ResolveNodeLaunchConfiguration</c>.
/// </summary>
public class CLR2TaskHost_E2E_Tests
{
    private readonly ITestOutputHelper _output;

    public CLR2TaskHost_E2E_Tests(ITestOutputHelper output)
    {
        _output = output;
    }

    /// <summary>
    /// Verifies that the CLR2 task host (MSBuildTaskHost.exe) can be launched and connected to
    /// when a task explicitly requests Runtime="CLR2" with TaskHostFactory.
    ///
    /// Regression test for the apphost changes (PR #13175) that replaced the three-branch
    /// ResolveNodeLaunchConfiguration with a two-branch version, losing the CLR2-specific path:
    ///   1. Empty command-line args (MSBuildTaskHost.Main() takes no arguments)
    ///   2. Handshake with toolsDirectory set to the EXE's directory so the pipe name
    ///      salt matches what the child process computes on startup
    /// Without these, the parent and child compute different pipe name hashes → MSB4216.
    /// </summary>
    [WindowsNet35OnlyFact]
    public void ExplicitCLR2TaskHostFactory_RunsTaskSuccessfully()
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        TransientTestFolder testFolder = env.CreateFolder(createFolder: true);

        string projectContent = """
            <Project xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
              <!-- Force Exec to run out-of-proc in the CLR2 task host (MSBuildTaskHost.exe) -->
              <UsingTask TaskName="Microsoft.Build.Tasks.Exec"
                         AssemblyName="Microsoft.Build.Tasks.v3.5, Version=3.5.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a"
                         TaskFactory="TaskHostFactory"
                         Runtime="CLR2" />

              <Target Name="Build">
                <Exec Command="echo CLR2TaskHostSuccess" />
              </Target>
            </Project>
            """;

        string projectPath = Path.Combine(testFolder.Path, "CLR2ExplicitTest.proj");
        File.WriteAllText(projectPath, projectContent);

        string testOutput = RunnerUtilities.ExecBootstrapedMSBuild(
            $"\"{projectPath}\" -v:n",
            out bool success,
            outputHelper: _output);


        // MSB4216 occurs when the parent can't connect to MSBuildTaskHost.exe —
        // either due to handshake salt mismatch (missing toolsDirectory) or wrong process routing.
        testOutput.ShouldNotContain("MSB4216", customMessage: "CLR2 task host connection should succeed with correct handshake salt and empty command-line args");

        success.ShouldBeTrue(customMessage: "Task explicitly requesting CLR2 + TaskHostFactory should execute in MSBuildTaskHost.exe");

        // Verify the task actually ran by checking for its output.
        testOutput.ShouldContain("CLR2TaskHostSuccess", customMessage: "Exec task output should be visible, confirming it ran in CLR2 task host");
    }
}
