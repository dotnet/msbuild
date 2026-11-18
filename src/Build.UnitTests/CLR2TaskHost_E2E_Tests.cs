// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Security;
using Microsoft.Build.Shared;
#if NET
using Microsoft.Win32;
#endif
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

#if NET
    public static bool Clr2BootstrapAvailable =>
        OperatingSystem.IsWindows()
        && Equals(Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\NET Framework Setup\NDP\v3.5", "Install", null), 1)
        && File.Exists(Path.Combine(RunnerUtilities.BootstrapRootPath, "net472", "MSBuild", "Current", "Bin", "amd64", "MSBuildTaskHost.exe"))
        && File.Exists(Path.Combine(RunnerUtilities.BootstrapRootPath, "net472", "MSBuild", "Current", "Bin", "MSBuildTaskHost.exe"))
        && File.Exists(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Microsoft.NET", "Framework", "v3.5", "Microsoft.Build.Tasks.v3.5.dll"));

    [Theory(Skip = "Requires .NET Framework 3.5 and the desktop bootstrap task host.", SkipUnless = nameof(Clr2BootstrapAvailable))]
    [InlineData(true, "x64", "amd64", false)]
    [InlineData(false, "x86", "", false)]
    [InlineData(true, "x64", "amd64", true)]
    [InlineData(false, "x86", "", true)]
    public void DotNetMSBuildRunsClr2TaskInVisualStudioTaskHost(bool useAssemblyFile, string architecture, string architectureDirectory, bool runtimeOnTask)
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        SetUpVisualStudioTaskHost(env, architectureDirectory);

        string taskAssembly = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Microsoft.NET", "Framework", "v3.5", "Microsoft.Build.Tasks.v3.5.dll");
        string taskAssemblyAttribute = useAssemblyFile
            ? $"""AssemblyFile="{SecurityElement.Escape(taskAssembly)}" """
            : """AssemblyName="Microsoft.Build.Tasks.v3.5, Version=3.5.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a" """;
        string taskFactoryAttribute = useAssemblyFile && !runtimeOnTask ? """TaskFactory="TaskHostFactory" """ : string.Empty;
        string registrationRuntime = runtimeOnTask ? string.Empty : $"""Runtime="CLR2" Architecture="{architecture}" """;
        string invocationRuntime = runtimeOnTask ? $"""MSBuildRuntime="CLR2" MSBuildArchitecture="{architecture}" """ : string.Empty;
        string projectPath = Path.Combine(env.CreateFolder().Path, "Clr2Task.proj");
        File.WriteAllText(projectPath, $"""
            <Project>
              <UsingTask TaskName="Microsoft.Build.Tasks.Exec" {taskAssemblyAttribute}
                         {taskFactoryAttribute}{registrationRuntime} />
              <Target Name="Build">
                <Exec Command="echo CLR2TaskHostSuccess" {invocationRuntime} />
              </Target>
            </Project>
            """);

        string output = RunnerUtilities.ExecBootstrapedMSBuild($"\"{projectPath}\" -v:n", out bool success, outputHelper: _output);
        success.ShouldBeTrue(output);
        output.ShouldContain("CLR2TaskHostSuccess");
    }

 #endif

    [WindowsNet35OnlyFact]
    public void Clr2InvocationWithCaseInsensitiveFactoryDoesNotChangeLaterDefaultRuntime()
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        SetUpVisualStudioTaskHost(env, "amd64");
        string folder = env.CreateFolder().Path;
        string source = Path.Combine(folder, "ProcessTasks.cs");
        string assembly = Path.Combine(folder, "ProcessTasks.dll");
        string windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        string compiler = Path.Combine(windows, "Microsoft.NET", "Framework", "v2.0.50727", "csc.exe");
        string framework = Path.Combine(windows, "assembly", "GAC_MSIL", "Microsoft.Build.Framework", "2.0.0.0__b03f5f7f11d50a3a", "Microsoft.Build.Framework.dll");
        File.WriteAllText(source, """
            using System.Diagnostics;
            using Microsoft.Build.Framework;
            public class ReportProcess : ITask
            {
                private IBuildEngine engine;
                private ITaskHost host;
                private int processId;
                public IBuildEngine BuildEngine { get { return engine; } set { engine = value; } }
                public ITaskHost HostObject { get { return host; } set { host = value; } }
                [Output] public int ProcessId { get { return processId; } set { processId = value; } }
                public bool Execute() { processId = Process.GetCurrentProcess().Id; return true; }
            }
            public class BaselineProcess : ReportProcess { }
            """);
        string compileOutput = RunnerUtilities.ExecMSBuild(
            compiler, $"/nologo /target:library /out:\"{assembly}\" /reference:\"{framework}\" \"{source}\"",
            out bool compiled, outputHelper: _output);
        compiled.ShouldBeTrue(compileOutput);

        string projectPath = Path.Combine(folder, "MixedRuntime.proj");
        string escapedAssembly = SecurityElement.Escape(assembly);
        File.WriteAllText(projectPath, $"""
            <Project>
              <UsingTask TaskName="BaselineProcess" AssemblyFile="{escapedAssembly}" />
              <UsingTask TaskName="ReportProcess" AssemblyFile="{escapedAssembly}" TaskFactory="assemblytaskfactory" />
              <Target Name="Build">
                <BaselineProcess><Output TaskParameter="ProcessId" PropertyName="BaselinePid" /></BaselineProcess>
                <ReportProcess MSBuildRuntime="CLR2" MSBuildArchitecture="x64"><Output TaskParameter="ProcessId" PropertyName="Clr2Pid" /></ReportProcess>
                <ReportProcess><Output TaskParameter="ProcessId" PropertyName="DefaultPid" /></ReportProcess>
                <Error Condition="'$(BaselinePid)' == '$(Clr2Pid)'" Text="CLR2 task ran in the current process." />
                <Error Condition="'$(BaselinePid)' != '$(DefaultPid)'" Text="Default task ran out of process." />
              </Target>
            </Project>
            """);
        string output = RunnerUtilities.ExecBootstrapedMSBuild($"\"{projectPath}\" -v:n", out bool success, outputHelper: _output);
        success.ShouldBeTrue(output);
    }

    private static void SetUpVisualStudioTaskHost(TestEnvironment env, string architectureDirectory)
    {
        string visualStudioRoot = env.CreateFolder().Path;
        string hostDirectory = Path.Combine(visualStudioRoot, "MSBuild", "Current", "Bin", architectureDirectory);
        Directory.CreateDirectory(hostDirectory);

        string bootstrapHostDirectory = Path.Combine(RunnerUtilities.BootstrapRootPath, "net472", "MSBuild", "Current", "Bin", architectureDirectory);
        foreach (string file in Directory.GetFiles(bootstrapHostDirectory, "MSBuildTaskHost.exe*"))
        {
            File.Copy(file, Path.Combine(hostDirectory, Path.GetFileName(file)));
        }
        File.Copy(
            Path.Combine(bootstrapHostDirectory, "Microsoft.NET.StringTools.net35.dll"),
            Path.Combine(hostDirectory, "Microsoft.NET.StringTools.net35.dll"));

        env.SetEnvironmentVariable("VSINSTALLDIR", visualStudioRoot);
        env.SetEnvironmentVariable("VisualStudioVersion", MSBuildConstants.CurrentVisualStudioVersion);
        env.SetEnvironmentVariable("MSBUILDTASKHOST_EXE_NAME", null);
    }
}
