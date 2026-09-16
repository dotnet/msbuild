// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using Microsoft.Build.BackEnd;
using Microsoft.Build.CommandLine;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.UnitTests.Shared;
using Shouldly;
using Xunit;

#nullable enable

namespace Microsoft.Build.UnitTests
{
    /// <summary>
    /// Tests for task-scoped state and build cleanup in a task host.
    /// </summary>
    /// <remarks>
    /// A task host that exits at the end of a build is reset by construction: the next build gets a
    /// brand new node. One that stays connected to its owner across builds is not, so it resets in
    /// place. Only state the next build does not re-establish for itself belongs there -- everything
    /// carried by the incoming TaskHostConfiguration is assigned before it is read -- which makes
    /// the few remaining items easy to drop by accident.
    /// </remarks>
    public class OutOfProcTaskHostNode_Tests
    {
        private readonly ITestOutputHelper _output;

        public OutOfProcTaskHostNode_Tests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void CleanupRestoresConsoleWhenBuildObjectDisposalThrows(bool retainConnection)
        {
            using TestEnvironment env = TestEnvironment.Create(_output);
            StringBuilder output = new();
            RedirectedNodeState state = env.WithTransientTestState(new RedirectedNodeState(text => output.Append(text), text => output.Append(text)));
#pragma warning disable CA2000 // The task-host cache owns the registered object.
            state.Node.RegisterTaskObject(nameof(ThrowingBuildObject), new ThrowingBuildObject(), RegisteredTaskObjectLifetime.Build, false);
#pragma warning restore CA2000

            Action cleanup = retainConnection ? state.Node.PrepareForNextBuild : state.Shutdown;
            Should.Throw<CriticalTaskException>(cleanup);

            Console.Out.ShouldBeSameAs(state.OriginalOut);
            Console.Error.ShouldBeSameAs(state.OriginalError);
            string completedOutput = output.ToString();
            state.OutWriter.WriteLine("stale stdout");
            state.ErrorWriter.WriteLine("stale stderr");
            state.OutWriter.Flush();
            state.ErrorWriter.Flush();
            output.ToString().ShouldBe(completedOutput);
        }

        [Fact]
        public void ConsoleShutdownRestoresBothStreamsWhenStdoutFlushThrows()
        {
            using TestEnvironment env = TestEnvironment.Create(_output);
            StringBuilder errorOutput = new();
            RedirectedNodeState state = env.WithTransientTestState(new RedirectedNodeState(
                _ => throw new IOException("stdout callback failed"), text => errorOutput.Append(text)));
            state.OutWriter.Write("pending stdout");
            state.ErrorWriter.Write("pending stderr");

            Should.Throw<IOException>(state.ShutdownConsole);

            Console.Out.ShouldBeSameAs(state.OriginalOut);
            Console.Error.ShouldBeSameAs(state.OriginalError);
            errorOutput.ToString().ShouldBe("pending stderr");
            Should.NotThrow(() => state.OutWriter.WriteLine("stale stdout"));
            state.ErrorWriter.WriteLine("stale stderr");
            state.ErrorWriter.Flush();
            errorOutput.ToString().ShouldBe("pending stderr");
        }

        [Fact]
        public void ShutdownRestoresConsoleBeforeCreatingDebugFile()
        {
            using TestEnvironment env = TestEnvironment.Create(_output);
            RedirectedNodeState state = env.WithTransientTestState(new RedirectedNodeState(_ => { }, _ => { }));
            state.SetField("_debugCommunications", true);
            env.CreateFolder(Path.Combine(FileUtilities.TempFileDirectory, $"MSBuild_NodeShutdown_{EnvironmentUtilities.CurrentProcessId}.txt"));

            Should.Throw<UnauthorizedAccessException>(state.Shutdown);

            Console.Out.ShouldBeSameAs(state.OriginalOut);
            Console.Error.ShouldBeSameAs(state.OriginalError);
        }

        private sealed class ThrowingBuildObject : IDisposable
        {
            public void Dispose()
            {
                Console.Write("cleanup output");
                throw new CriticalTaskException(new InvalidOperationException("disposal failed"));
            }
        }

        private sealed class RedirectedNodeState : TransientTestState
        {
            private const BindingFlags InstanceMembers = BindingFlags.Instance | BindingFlags.NonPublic;

            internal OutOfProcTaskHostNode Node { get; } = new();
            internal TextWriter OriginalOut { get; } = Console.Out;
            internal TextWriter OriginalError { get; } = Console.Error;
            internal RedirectConsoleWriter OutWriter { get; }
            internal RedirectConsoleWriter ErrorWriter { get; }

            internal RedirectedNodeState(Action<string> output, Action<string> error)
            {
                OutWriter = new RedirectConsoleWriter(output);
                ErrorWriter = new RedirectConsoleWriter(error);
                StopTimer(OutWriter);
                StopTimer(ErrorWriter);
                SetField("_originalConsoleOut", OriginalOut);
                SetField("_originalConsoleError", OriginalError);
                SetField("_consoleOutWriter", OutWriter);
                SetField("_consoleErrorWriter", ErrorWriter);
                Console.SetOut(OutWriter);
                Console.SetError(ErrorWriter);
            }

            internal void Shutdown() => ((Func<NodeEngineShutdownReason>)typeof(OutOfProcTaskHostNode)
                .GetMethod("HandleShutdown", InstanceMembers)!.CreateDelegate(typeof(Func<NodeEngineShutdownReason>), Node))();

            internal void ShutdownConsole() => ((Action)typeof(OutOfProcTaskHostNode)
                .GetMethod("ShutdownConsoleRedirection", InstanceMembers)!.CreateDelegate(typeof(Action), Node))();

            public override void Revert()
            {
                Console.SetOut(OriginalOut);
                Console.SetError(OriginalError);
                using (ErrorWriter)
                {
                    OutWriter.Dispose();
                }
                string[] fields = ["_packetReceivedEvent", "_shutdownEvent", "_taskCompleteEvent", "_taskCancelledEvent"];
                foreach (string field in fields)
                {
                    ((WaitHandle)typeof(OutOfProcTaskHostNode).GetField(field, InstanceMembers)!.GetValue(Node)!).Dispose();
                }
            }

            internal void SetField(string name, object value) =>
                typeof(OutOfProcTaskHostNode).GetField(name, InstanceMembers)!.SetValue(Node, value);

            private static void StopTimer(RedirectConsoleWriter writer)
            {
                using ManualResetEvent disposed = new(false);
                Timer timer = (Timer)typeof(RedirectConsoleWriter).GetField("_timer", InstanceMembers)!.GetValue(writer)!;
                timer.Dispose(disposed).ShouldBeTrue();
                disposed.WaitOne(10_000).ShouldBeTrue();
            }
        }

        [Theory]
        [InlineData(false, nameof(FailureFlagTask))]
        [InlineData(true, nameof(FailureFlagTask))]
#if NETFRAMEWORK
        [InlineData(false, nameof(IsolatedFailureFlagTask))]
        [InlineData(true, nameof(IsolatedFailureFlagTask))]
#endif
        public void AllowFailureWithoutError_IsolatedForEachTask(bool runNested, string taskName)
        {
            using TestEnvironment env = TestEnvironment.Create(_output);
            env.SetEnvironmentVariable("MSBUILDUSESERVER", "0");
            TransientTestFile project = env.CreateFile("failureFlag.proj", $"""
                <Project>
                  <UsingTask TaskName="{taskName}" AssemblyFile="{typeof(FailureFlagTask).Assembly.Location}" TaskFactory="TaskHostFactory" />
                  <Target Name="Build">
                    <{taskName} Value="true" RunNested="{runNested}" />
                    <{taskName} Value="false" />
                  </Target>
                  <Target Name="Nested">
                    <{taskName} Value="false" />
                  </Target>
                </Project>
                """);

            string output = RunnerUtilities.ExecBootstrapedMSBuild(
                $"\"{project.Path}\" -m:1 -nr:false", out bool success, outputHelper: _output);
            MatchCollection taskPids = Regex.Matches(output, @"FailureFlagTaskPid=(\d+)");
            foreach (Match match in taskPids)
            {
                env.WithTransientProcess(int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture));
            }

            success.ShouldBeTrue(output);
            taskPids.Count.ShouldBe(runNested ? 3 : 2);
            string hostPid = taskPids[0].Groups[1].Value;
            foreach (Match match in taskPids)
            {
                match.Groups[1].Value.ShouldBe(hostPid, "all calls must use the same TaskHost");
            }

            Match clientPid = Regex.Match(output, @"Process ID is (\d+)");
            clientPid.Success.ShouldBeTrue(output);
            hostPid.ShouldNotBe(clientPid.Groups[1].Value);
        }

        [Fact]
        public void PrepareForNextBuild_ResetsStateTheNextBuildDoesNotReestablish()
        {
            using TestEnvironment env = TestEnvironment.Create(_output);
            env.SetCurrentDirectory(env.CreateFolder().Path);
            OutOfProcTaskHostNode node = new();

            // A cancellation arriving as the build ends would otherwise stay signalled and spin the
            // next build's wait loop if this wasn't reset.
            node.TaskCancelledEvent.Set();

            node.PrepareForNextBuild();

            node.TaskCancelledEvent.WaitOne(0).ShouldBeFalse("a cancellation from the previous build must not still be signalled");
            Directory.GetCurrentDirectory().ShouldBe(
                BuildEnvironmentHelper.Instance.CurrentMSBuildToolsDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        }

        public class FailureFlagTask : MarshalByRefObject, ITask
        {
            public IBuildEngine BuildEngine { get; set; } = null!;
            public ITaskHost HostObject { get; set; } = null!;
            public bool Value { get; set; }
            public bool RunNested { get; set; }

            public bool Execute()
            {
                Microsoft.Build.Utilities.TaskLoggingHelper log = new(this);
                using Process process = Process.GetCurrentProcess();
                log.LogMessage(MessageImportance.High, "FailureFlagTaskPid={0}", process.Id);
                IBuildEngine7 engine = (IBuildEngine7)BuildEngine;
                bool initialValue;
                try
                {
                    initialValue = engine.AllowFailureWithoutError;
                }
                catch (Exception ex)
                {
                    log.LogErrorFromException(ex, showStackTrace: true);
                    return false;
                }

                if (initialValue)
                {
                    log.LogError("AllowFailureWithoutError was inherited from another task.");
                    return false;
                }

                engine.AllowFailureWithoutError = Value;
                if (RunNested && !BuildEngine.BuildProjectFile(BuildEngine.ProjectFileOfTaskNode, ["Nested"], null, null))
                {
                    return false;
                }

                if (engine.AllowFailureWithoutError != Value)
                {
                    log.LogError("A nested task changed AllowFailureWithoutError on its caller.");
                    return false;
                }

                return true;
            }
        }

        [LoadInSeparateAppDomain]
        public sealed class IsolatedFailureFlagTask : FailureFlagTask
        {
        }
    }
}
