// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Build.Shared;
using Microsoft.Build.UnitTests;
using Microsoft.Build.UnitTests.Shared;
using Shouldly;
using Xunit;

#nullable enable

namespace Microsoft.Build.Engine.UnitTests.BackEnd;

public sealed class TaskHostNonLifoReentrancy_Tests(ITestOutputHelper output)
{
    private readonly ITestOutputHelper _output = output;
    private const int TimeoutMilliseconds = 120_000;
    private static readonly string[] s_roles = ["A", "B", "A-child", "B-before-nested", "B-child"];
    private static readonly string[] s_graphFiles =
        ["Root.proj", "WrapperA.proj", "WrapperB.proj", "P.proj", "ChildA.proj", "ChildB.proj", "Common.props"];
    private static readonly string[] s_resultFiles = ["A-child-ready", "B-child-ready", "A.result", "B.result", "root.results"];

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NestedBuildCallbacks_CompleteForOriginalTask(bool multiThreaded)
    {
        string mode = multiThreaded ? "mt" : "normal";
        (string evidenceDir, string binlog) = CreateEvidenceDirectory(multiThreaded);
        var recordingOutput = new RecordingOutput(_output, Path.Combine(evidenceDir, "transcript.txt"));
        using TestEnvironment env = TestEnvironment.Create(_output);
        env.SetEnvironmentVariable("MSBUILDUSESERVER", "0");
        env.SetEnvironmentVariable("MSBUILDFORCEMULTITHREADED", "0");
        env.SetEnvironmentVariable("MSBUILDFORCEALLTASKSOUTOFPROC", null);
        env.SetEnvironmentVariable("MSBUILDDISABLEFEATURESFROMVERSION", null);
        env.SetEnvironmentVariable("MSBUILDDISABLENODEREUSE", null);
        string salt = Guid.NewGuid().ToString("N");
        env.SetEnvironmentVariable("MSBUILDNODEHANDSHAKESALT", salt);
        string runDir = Path.GetFullPath(env.CreateFolder().Path);
        string assembly = Path.GetFullPath(typeof(TaskHostNonLifoNestedBuild).Assembly.Location);
        string diagnosticLog = Path.Combine(evidenceDir, "diagnostic.log");
        string arguments = $"\"{Path.Combine(runDir, "Root.proj")}\" /m:2 /nr:false /v:diag " +
            $"{(multiThreaded ? "-mt" : "-mt:false")} /p:RunDir=\"{runDir}\" /p:ProbeAssembly=\"{assembly}\" " +
            $"/bl:\"{binlog}\" /flp:\"logfile={diagnosticLog};verbosity=diagnostic;encoding=UTF-8\"";
        string invocation = $"Mode={mode}; timeout={TimeoutMilliseconds}ms; handshake salt={salt}{Environment.NewLine}" +
            $"Assembly={assembly}{Environment.NewLine}RunDir={runDir}{Environment.NewLine}" +
            $"Evidence={evidenceDir}{Environment.NewLine}Binlog={binlog}{Environment.NewLine}" +
            $"{RunnerUtilities.BootstrapMSBuildCommand} {arguments}";
        string outcome = "Execution did not complete.";
        bool completedSuccessfully = false;

        try
        {
            recordingOutput.WriteLine(invocation);
            File.WriteAllText(Path.Combine(evidenceDir, "invocation.txt"), invocation);
            // Any nonempty value disables the runner's timeout through cached traits.
            string.IsNullOrEmpty(Environment.GetEnvironmentVariable("MSBuildDebugUnitTests"))
                .ShouldBeTrue("Unset MSBuildDebugUnitTests before starting the test process (not \"0\").");
            CreateGraph(runDir);

            string log = RunnerUtilities.ExecBootstrapedMSBuild(
                arguments, out bool success, outputHelper: recordingOutput, timeoutMilliseconds: TimeoutMilliseconds);
            File.WriteAllText(Path.Combine(evidenceDir, "runner-output.txt"), log);
            success.ShouldBeTrue($"Evidence: {evidenceDir}{Environment.NewLine}{log}");
            log.ShouldContain("PROBE ROOT COMPLETED");
            log.ShouldContain("PROBE B CHILD A REQUEST COMPLETED");
            (int aPid, int bPid) = AssertParentBoundResults(runDir, log);
            if (multiThreaded)
            {
                aPid.ShouldBe(bPid, "Both real nested invocations must use the same TaskHost.");
                AssertTaskHostIdentity(log, aPid);
            }

            outcome = "PASS: graph, parent-bound results, traces and applicable TaskHost assertions completed.";
            completedSuccessfully = true;
        }
        catch (TimeoutException ex)
        {
            outcome = $"TIMEOUT: mode={mode}, bound={TimeoutMilliseconds}ms, evidence={evidenceDir}.{Environment.NewLine}{ex}";
            // PreserveEvidence reports gate/return stages, observed PIDs and parent outputs before cleanup.
            throw;
        }
        catch (Exception ex)
        {
            outcome = $"FAIL: {ex}";
            throw;
        }
        finally
        {
            if (!completedSuccessfully || Environment.GetEnvironmentVariable("MSBUILD_TASKHOST_NONLIFO_EVIDENCE_DIR") is not null)
            {
                // Archive failures before TestEnvironment removes the graph; successful retention is opt-in.
                PreserveEvidence(runDir, evidenceDir, binlog, invocation, outcome, recordingOutput);
            }
            else
            {
                Directory.Delete(evidenceDir, recursive: true);
            }
        }
    }

    private static void CreateGraph(string runDir)
    {
        File.WriteAllText(Path.Combine(runDir, "Common.props"), $"""
            <Project>
              <UsingTask TaskName="{typeof(TaskHostNonLifoNestedBuild).FullName}" AssemblyFile="$(ProbeAssembly)" />
              <UsingTask TaskName="{typeof(TaskHostNonLifoFileGate).FullName}" AssemblyFile="$(ProbeAssembly)" />
            </Project>
            """);
        File.WriteAllText(Path.Combine(runDir, "Root.proj"), """
            <Project DefaultTargets="Build">
              <ItemGroup>
                <Wrapper Include="$([MSBuild]::NormalizePath('$(MSBuildThisFileDirectory)', 'WrapperA.proj'))" />
                <Wrapper Include="$([MSBuild]::NormalizePath('$(MSBuildThisFileDirectory)', 'WrapperB.proj'))" />
              </ItemGroup>
              <Target Name="Build">
                <MSBuild Projects="@(Wrapper)" Targets="Build" BuildInParallel="true">
                  <Output TaskParameter="TargetOutputs" ItemName="RootResults" />
                </MSBuild>
                <WriteLinesToFile File="$([MSBuild]::NormalizePath('$(RunDir)', 'root.results'))" Lines="@(RootResults)" Overwrite="true" />
                <Message Text="PROBE ROOT COMPLETED" Importance="high" />
              </Target>
            </Project>
            """);
        string[] wrapperRoles = ["A", "B"];
        foreach (string role in wrapperRoles)
        {
            File.WriteAllText(Path.Combine(runDir, $"Wrapper{role}.proj"), $"""
                <Project DefaultTargets="Build">
                  <Target Name="Build" Returns="@(WrapperResults)">
                    <MSBuild Projects="$([MSBuild]::NormalizePath('$(MSBuildThisFileDirectory)', 'P.proj'))" Targets="{role}">
                      <Output TaskParameter="TargetOutputs" ItemName="WrapperResults" />
                    </MSBuild>
                  </Target>
                </Project>
                """);
        }

        // A and B are targets of ONE project configuration. Role is never a global property.
        File.WriteAllText(Path.Combine(runDir, "P.proj"), """
            <Project>
              <Import Project="Common.props" />
              <Target Name="A" Returns="$(AResult)">
                <TaskHostNonLifoNestedBuild Role="A" RunDir="$(RunDir)" ChildProject="$([MSBuild]::NormalizePath('$(MSBuildThisFileDirectory)', 'ChildA.proj'))">
                  <Output TaskParameter="CompletedRole" PropertyName="AResult" />
                  <Output TaskParameter="ProcessId" PropertyName="APid" />
                </TaskHostNonLifoNestedBuild>
                <WriteLinesToFile File="$([MSBuild]::NormalizePath('$(RunDir)', 'A.result'))" Lines="$(AResult)|$(APid)" Overwrite="true" />
                <Message Text="PROBE P TARGET A PARENT COMPLETED role=$(AResult) pid=$(APid)" Importance="high" />
              </Target>
              <Target Name="B" Returns="$(BResult)">
                <TaskHostNonLifoFileGate Role="B-before-nested" RunDir="$(RunDir)" WaitFor="$([MSBuild]::NormalizePath('$(RunDir)', 'A-child-ready'))" />
                <TaskHostNonLifoNestedBuild Role="B" RunDir="$(RunDir)" ChildProject="$([MSBuild]::NormalizePath('$(MSBuildThisFileDirectory)', 'ChildB.proj'))">
                  <Output TaskParameter="CompletedRole" PropertyName="BResult" />
                  <Output TaskParameter="ProcessId" PropertyName="BPid" />
                </TaskHostNonLifoNestedBuild>
                <WriteLinesToFile File="$([MSBuild]::NormalizePath('$(RunDir)', 'B.result'))" Lines="$(BResult)|$(BPid)" Overwrite="true" />
                <Message Text="PROBE P TARGET B PARENT COMPLETED role=$(BResult) pid=$(BPid)" Importance="high" />
              </Target>
            </Project>
            """);
        File.WriteAllText(Path.Combine(runDir, "ChildA.proj"), """
            <Project DefaultTargets="Build">
              <Import Project="Common.props" />
              <Target Name="Build">
                <TaskHostNonLifoFileGate Role="A-child" RunDir="$(RunDir)" Signal="$([MSBuild]::NormalizePath('$(RunDir)', 'A-child-ready'))" WaitFor="$([MSBuild]::NormalizePath('$(RunDir)', 'B-child-ready'))" />
              </Target>
            </Project>
            """);
        // B's signal releases A; B still needs the SAME P:A result. No extra release dependency.
        File.WriteAllText(Path.Combine(runDir, "ChildB.proj"), """
            <Project DefaultTargets="Build">
              <Import Project="Common.props" />
              <Target Name="Build">
                <TaskHostNonLifoFileGate Role="B-child" RunDir="$(RunDir)" Signal="$([MSBuild]::NormalizePath('$(RunDir)', 'B-child-ready'))" />
                <MSBuild Projects="$([MSBuild]::NormalizePath('$(MSBuildThisFileDirectory)', 'P.proj'))" Targets="A" />
                <Message Text="PROBE B CHILD A REQUEST COMPLETED" Importance="high" />
              </Target>
            </Project>
            """);
    }

    private static (int APid, int BPid) AssertParentBoundResults(string runDir, string log)
    {
        string[] traces = Directory.GetFiles(runDir, "*.trace");
        traces.Length.ShouldBe(s_roles.Length, "Exactly two nested invocations and three gates should execute.");
        int aPid = AssertParent("A");
        int bPid = AssertParent("B");
        foreach (string role in s_roles)
        {
            string trace = File.ReadAllText(traces.Where(path => TraceBelongsToRole(path, role)).ShouldHaveSingleItem());
            foreach (string stage in RequiredStages(role))
            {
                (trace.IndexOf($"stage={stage}", StringComparison.Ordinal) >= 0).ShouldBeTrue($"Missing {stage} for {role}.");
            }
            trace.ShouldNotContain("GATE_TIMEOUT");
        }

        File.Exists(Path.Combine(runDir, "A-child-ready")).ShouldBeTrue();
        File.Exists(Path.Combine(runDir, "B-child-ready")).ShouldBeTrue();
        string[] rootResults = File.ReadAllLines(Path.Combine(runDir, "root.results"));
        rootResults.Length.ShouldBe(2, "Do not deduplicate results: an extra invocation is a failure.");
        rootResults.OrderBy(value => value, StringComparer.Ordinal).ToArray().ShouldBe(["A", "B"]);
        return (aPid, bPid);

        int AssertParent(string role)
        {
            string result = File.ReadAllLines(Path.Combine(runDir, $"{role}.result")).ShouldHaveSingleItem();
            string[] fields = result.Split('|');
            fields.Length.ShouldBe(2);
            fields[0].ShouldBe(role, $"Parent {role} must receive its own task's output, not its peer's.");
            int.TryParse(fields[1], NumberStyles.None, CultureInfo.InvariantCulture, out int pid).ShouldBeTrue(result);
            pid.ShouldBeGreaterThan(0);
            string tracePath = traces.Where(path => TraceBelongsToRole(path, role)).ShouldHaveSingleItem();
            (File.ReadAllText(tracePath).IndexOf(
                $"stage=RUNTIME corelib={typeof(object).Assembly.GetName().Name}", StringComparison.Ordinal) >= 0)
                .ShouldBeTrue("The task must execute on the same runtime as this test's bootstrap MSBuild.");
            int tracePid = int.Parse(Path.GetFileName(tracePath).Split('-')[1], CultureInfo.InvariantCulture);
            pid.ShouldBe(tracePid, $"Parent {role}'s bound PID must match its own real invocation.");
            foreach (string line in File.ReadAllLines(tracePath))
            {
                line.ShouldContain($" role={role} pid={pid} ");
            }
            log.ShouldContain($"PROBE P TARGET {role} PARENT COMPLETED role={role} pid={pid}");
            return pid;
        }
    }

    private static void AssertTaskHostIdentity(string log, int pid)
    {
        // Use the localized resources, including the diagnostic task-event suffix, rather than English text.
        string details = ResourceUtilities.FormatResourceStringIgnoreCodeAndKeyword(
            "TaskHostDetails", nameof(TaskHostNonLifoNestedBuild), "HOSTPID", "CALLERPID", "NEWFLAG", "SIDEFLAG", "REUSEFLAG");
        string pattern = Regex.Escape(ResourceUtilities.FormatResourceStringIgnoreCodeAndKeyword("TaskMessageWithId", details, "TASKID"))
            .Replace("HOSTPID", "(?<host>[0-9]+)")
            .Replace("CALLERPID", "(?<caller>[0-9]+)")
            .Replace("TASKID", "(?<task>[0-9]+)")
            .Replace("NEWFLAG", "(?:True|False)")
            .Replace("SIDEFLAG", "(?:True|False)")
            .Replace("REUSEFLAG", "(?:True|False)");
        Match[] dispatches = Regex.Matches(log, pattern).Cast<Match>().ToArray();
        dispatches.Length.ShouldBe(2, log);
        foreach (Match dispatch in dispatches)
        {
            int.Parse(dispatch.Groups["host"].Value, CultureInfo.InvariantCulture).ShouldBe(pid);
            int caller = int.Parse(dispatch.Groups["caller"].Value, CultureInfo.InvariantCulture);
            caller.ShouldBeGreaterThan(0);
            caller.ShouldNotBe(pid, "The shared process must be an external TaskHost, not the caller.");
        }
        dispatches[0].Groups["caller"].Value.ShouldBe(dispatches[1].Groups["caller"].Value);
        dispatches[0].Groups["task"].Value.ShouldNotBe(dispatches[1].Groups["task"].Value);

        // Within this shared host/caller, correlate each role's call and return to one dispatch task ID.
        // Full project/target event contexts remain in the binlog; parent result files are the ownership oracle.
        string taskSuffix = Regex.Escape(ResourceUtilities.FormatResourceStringIgnoreCodeAndKeyword("TaskMessageWithId", "MESSAGE", "TASKID"))
            .Replace("MESSAGE", "PROBEMESSAGE").Replace("TASKID", "(?<task>[0-9]+)");
        string[] roleTaskIds = new string[2];
        for (int i = 0; i < 2; i++)
        {
            string role = i == 0 ? "A" : "B";
            string message = $@"PROBE role={role} pid={pid} tid=[0-9]+ stage=CALL_CHILD[^\r\n]*?";
            Match call = Regex.Matches(log, taskSuffix.Replace("PROBEMESSAGE", message)).Cast<Match>().ShouldHaveSingleItem();
            roleTaskIds[i] = call.Groups["task"].Value;
            dispatches.Count(dispatch => dispatch.Groups["task"].Value == roleTaskIds[i]).ShouldBe(1);
            message = $@"PROBE role={role} pid={pid} tid=[0-9]+ stage=CHILD_RETURN success=True";
            Match returned = Regex.Matches(log, taskSuffix.Replace("PROBEMESSAGE", message)).Cast<Match>().ShouldHaveSingleItem();
            returned.Groups["task"].Value.ShouldBe(roleTaskIds[i], $"Role {role}'s return log must belong to its original task.");
        }
        roleTaskIds[0].ShouldNotBe(roleTaskIds[1]);
    }

    private static bool TraceBelongsToRole(string path, string role) =>
        Regex.IsMatch(Path.GetFileName(path), $"^{Regex.Escape(role)}-[0-9]+-[0-9a-f]{{32}}\\.trace$");

    private static string[] RequiredStages(string role) => role is "A" or "B"
        ? ["START", "CALL_CHILD", "CHILD_RETURN success=True", "EXECUTE_RETURN success=True"]
        : ["START", "YIELD_BEGIN", "YIELDED", "GATE_OPEN", "REACQUIRE_BEGIN", "REACQUIRED", "EXECUTE_RETURN success=True"];

    private static (string Directory, string Binlog) CreateEvidenceDirectory(bool multiThreaded)
    {
        string? configuredBase = Environment.GetEnvironmentVariable("MSBUILD_TASKHOST_NONLIFO_BINLOG_BASE");
        int binlogBase = 1;
        if (configuredBase is not null &&
            (!int.TryParse(configuredBase, NumberStyles.None, CultureInfo.InvariantCulture, out binlogBase) ||
             binlogBase <= 0 || binlogBase == int.MaxValue))
        {
            throw new ArgumentException("MSBUILD_TASKHOST_NONLIFO_BINLOG_BASE must be a positive integer with room for the MT row.");
        }
        string root = Path.GetFullPath(Environment.GetEnvironmentVariable("MSBUILD_TASKHOST_NONLIFO_EVIDENCE_DIR") ??
            Path.Combine(Path.GetTempPath(), "MSBuild-TaskHostNonLifoReentrancy"));
        string directory = Path.Combine(root, $"{Guid.NewGuid():N}-{(multiThreaded ? "mt" : "normal")}");
        if (Directory.Exists(directory))
        {
            throw new IOException($"Refusing to overwrite existing evidence: {directory}");
        }
        Directory.CreateDirectory(directory);
        string number = (binlogBase + (multiThreaded ? 1 : 0)).ToString(CultureInfo.InvariantCulture);
        return (directory, Path.Combine(directory, number + ".binlog"));
    }

    private static void PreserveEvidence(
        string runDir, string evidenceDir, string binlog, string invocation, string outcome, RecordingOutput output)
    {
        var report = new StringBuilder().AppendLine(invocation).AppendLine(outcome);
        string archive = Path.Combine(evidenceDir, "graph");
        Collect(() => Directory.CreateDirectory(archive), "create graph archive");
        string[] files = [];
        Collect(() => files = Directory.GetFiles(runDir), "enumerate graph");
        foreach (string name in s_graphFiles.Concat(s_resultFiles).Concat(files.Select(path => Path.GetFileName(path))).Distinct())
        {
            Collect(() =>
            {
                string source = Path.Combine(runDir, name);
                if (!File.Exists(source))
                {
                    report.AppendLine($"MISSING: {name}");
                    return;
                }
                File.Copy(source, Path.Combine(archive, name), overwrite: false);
            }, $"copy {name}");
        }
        foreach (string name in s_resultFiles)
        {
            Collect(() =>
            {
                string path = Path.Combine(runDir, name);
                report.AppendLine($"{name}: {(File.Exists(path) ? File.ReadAllText(path) : "<MISSING>")}");
            }, $"read {name}");
        }
        foreach (string role in s_roles)
        {
            string[] traces = files.Where(path => TraceBelongsToRole(path, role)).ToArray();
            report.AppendLine($"Role {role}: {traces.Length} invocation traces (expected 1).");
            if (traces.Length == 0)
            {
                report.AppendLine($"MISSING {role} stages: {string.Join(", ", RequiredStages(role))}");
            }
            foreach (string tracePath in traces)
            {
                Collect(() =>
                {
                    string trace = File.ReadAllText(tracePath);
                    report.AppendLine(Path.GetFileName(tracePath)).AppendLine(trace);
                    foreach (string stage in RequiredStages(role))
                    {
                        if (trace.IndexOf($"stage={stage}", StringComparison.Ordinal) < 0)
                        {
                            report.AppendLine($"MISSING {role} stage: {stage}");
                        }
                    }
                }, $"read trace {tracePath}");
            }
        }
        Collect(() =>
        {
            report.AppendLine(File.Exists(binlog)
                ? $"Binlog: {binlog}, {new FileInfo(binlog).Length} bytes; readability NOT verified (forced termination can leave a truncated/header-only file)."
                : $"MISSING binlog: {binlog}");
            report.AppendLine($"Transcript: {output.TranscriptPath}; recording error: {output.RecordingError ?? "<none>"}");
            string[] logFiles = ["diagnostic.log", "runner-output.txt"];
            foreach (string name in logFiles)
            {
                string path = Path.Combine(evidenceDir, name);
                report.AppendLine(File.Exists(path) ? $"Retained: {path} (may be incomplete on timeout)." : $"MISSING: {path}");
            }
            report.AppendLine("The runner's root-process wait does not prove that every descendant has exited.");
        }, "report diagnostic artifacts");
        Collect(() => File.WriteAllText(Path.Combine(evidenceDir, "evidence.txt"), report.ToString()), "write evidence summary");
        Collect(() => output.WriteLine(report.ToString()), "print evidence summary");

        void Collect(Action action, string description)
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                // Evidence is best effort and must never replace the original timeout/assertion.
                report.AppendLine($"EVIDENCE INCOMPLETE ({description}): {ex}");
            }
        }
    }

    private sealed class RecordingOutput(ITestOutputHelper inner, string transcriptPath) : ITestOutputHelper
    {
        private readonly LockType _lock = new();
        private string? _recordingError;

        public string TranscriptPath { get; } = transcriptPath;
        public string Output => inner.Output;
        public string? RecordingError
        {
            get
            {
                lock (_lock)
                {
                    return _recordingError;
                }
            }
        }

        public void Write(string message) => Record(message, newline: false);
        public void Write(string format, params object?[] args) => Write(string.Format(CultureInfo.CurrentCulture, format, args));
        public void WriteLine(string message) => Record(message, newline: true);
        public void WriteLine(string format, params object?[] args) => WriteLine(string.Format(CultureInfo.CurrentCulture, format, args));

        private void Record(string message, bool newline)
        {
            lock (_lock)
            {
                try
                {
                    File.AppendAllText(TranscriptPath, message + (newline ? Environment.NewLine : string.Empty));
                }
                catch (Exception ex)
                {
                    _recordingError = ex.ToString();
                }

                if (newline)
                {
                    inner.WriteLine(message);
                }
                else
                {
                    inner.Write(message);
                }
            }
        }
    }
}
