// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;
using System.IO;
using System.Threading;
using Microsoft.Build.BackEnd;
using Microsoft.Build.BackEnd.Client;
using Microsoft.Build.Experimental;
using Microsoft.Build.UnitTests;
using Shouldly;
using Xunit;

namespace Microsoft.Build.UnitTests.BackEnd
{
    /// <summary>
    /// Tests for the <see cref="MSBuildClient"/> fallback behaviour.
    /// </summary>
    public class MSBuildClient_Tests
    {
        private readonly ITestOutputHelper _output;

        public MSBuildClient_Tests(ITestOutputHelper output)
        {
            _output = output;
        }

        /// <summary>
        /// When the configured msbuild executable does not exist, launching the server fails.
        /// <see cref="MSBuildClient.Execute"/> must return a recoverable exit type rather than
        /// letting an exception escape — it is the host's contract that any failure inside
        /// Execute routes through <see cref="MSBuildClientExitResult"/> so callers (e.g.
        /// <c>MSBuildClientApp</c>) can fall back to in-process execution.
        /// </summary>
        /// <remarks>
        /// Regression coverage for the .NET 10.0.300 / Aspire timeout: when
        /// <c>DOTNET_CLI_USE_MSBUILD_SERVER=true</c> is honoured but the server child cannot
        /// start (e.g. the apphost can't find the .NET runtime), <see cref="MSBuildClient.Execute"/>
        /// must not propagate a <see cref="System.TimeoutException"/> or any other exception.
        /// Pre-fix, an uncaught <c>TimeoutException</c> from <c>NamedPipeClientStream.Connect</c>
        /// escaped past <c>MSBuildClientApp</c> and crashed the CLI; this test locks in the
        /// no-exception-escape contract by simply calling <c>Execute</c> outside any try/catch
        /// and asserting on the structured result.
        /// </remarks>
        [Fact]
        public void Execute_WithUnreachableServer_DoesNotPropagateException()
        {
            // Isolate from any real MSBuild server / DOTNET_CLI_USE_MSBUILD_SERVER state on
            // the dev or CI machine. Without this the named-mutex check in MSBuildClient can
            // observe a warm server from another test/run, which makes the assertions below
            // non-deterministic.
            using TestEnvironment env = TestEnvironment.Create(_output);
            env.SetEnvironmentVariable("DOTNET_CLI_USE_MSBUILD_SERVER", null);
            env.SetEnvironmentVariable("MSBUILDUSESERVER", null);

            string[] commandLine = ["dummy.proj"];
            string nonexistentMsBuild = Path.Combine(
                Path.GetTempPath(),
                "msbuildclient-tests-" + Guid.NewGuid().ToString("N"),
                "MSBuild.dll");

            MSBuildClient client = new MSBuildClient(commandLine, nonexistentMsBuild);

            // The whole point of the regression fix: this must NOT throw. xUnit fails the
            // test with the offending stack if any exception escapes, which is the
            // primary regression contract being verified.
            MSBuildClientExitResult result = client.Execute(CancellationToken.None);

            result.ShouldNotBeNull();

            // The unreachable-server path must produce one of the recoverable failure types
            // so that MSBuildClientApp can fall back to in-process execution. Crucially,
            // ServerBusy is excluded here: ServerBusy is the "another client is racing for
            // the launch mutex" path and is not what an unreachable server should produce —
            // accepting it would mask a real regression where the failure was misclassified.
            result.MSBuildClientExitType.ShouldBeOneOf(
                MSBuildClientExitType.LaunchError,
                MSBuildClientExitType.UnableToConnect,
                MSBuildClientExitType.UnknownServerState);

            // No server child was successfully launched, so the diagnostic helper should not
            // have observed an exit code. (Once issue #13718 lands and the diagnostic helper
            // is plumbed through every connect failure, this gives MSBuildClientApp the right
            // signal to pick the generic "server unavailable" message rather than the more
            // specific "crashed with exit code N" one.)
            result.ServerProcessExitCode.ShouldBeNull();
        }

        /// <summary>
        /// Regression coverage for https://github.com/dotnet/msbuild/issues/14172. On Linux under full CPU
        /// saturation, a warm <c>/mt</c> MSBuild Server build that succeeded would intermittently exit 1 with
        /// the final <c>Build succeeded.</c> summary dropped.
        /// </summary>
        /// <remarks>
        /// Root cause: the client's packet-processing loop waits on
        /// <c>WaitAny([cancel, PacketPumpCompleted, PacketReceivedEvent])</c>. The pump enqueues the final
        /// <see cref="ServerNodeBuildResult"/> (and trailing console writes) and then sets
        /// <c>PacketPumpCompleted</c> - a sticky <see cref="ManualResetEvent"/> that sorts before
        /// <c>PacketReceivedEvent</c>. If the (descheduled) main loop reaches <c>WaitAny</c> after both are
        /// signaled, it took the completed branch, which set <c>_buildFinished</c> WITHOUT draining the queue,
        /// dropping the result. That left <see cref="MSBuildClientExitResult.MSBuildAppExitTypeString"/> null,
        /// which <c>MSBuildClientApp</c> maps to a non-zero process exit. This test arranges that exact event
        /// ordering deterministically (both events pre-signaled with the result already queued, so
        /// <c>WaitAny</c> returns the completed branch) and asserts the result is still processed.
        /// </remarks>
        [Fact]
        public void ProcessPackets_WhenPumpCompletedRacesQueuedResult_DoesNotDropBuildResult()
        {
            using TestEnvironment env = TestEnvironment.Create(_output);

            MSBuildClient client = new MSBuildClient(["dummy.proj"], "MSBuild.dll");

            using MSBuildClientPacketPump pump = new MSBuildClientPacketPump(new MemoryStream());

            // Seed the queue with the trailing console write and the build result, then signal both the
            // received event and the (sticky) completion event, mirroring the server's teardown ordering.
            pump.ReceivedPacketsQueue.Enqueue(new ServerNodeConsoleWrite("Build succeeded." + Environment.NewLine, ConsoleOutput.Standard));
            pump.ReceivedPacketsQueue.Enqueue(new ServerNodeBuildResult(0, "Success"));
            pump.PacketReceivedEvent.Set();
            pump.PacketPumpCompleted.Set();

            // Capture stdout so we can assert the dropped-summary symptom directly (and keep the seeded
            // console write out of the real test-run output).
            MSBuildClientExitResult result;
            using StringWriter capturedConsole = new StringWriter();
            TextWriter originalConsoleOut = Console.Out;
            Console.SetOut(capturedConsole);
            try
            {
                result = client.ProcessSeededPacketsForTests(pump);
            }
            finally
            {
                Console.SetOut(originalConsoleOut);
            }

            // The build result must be surfaced, not dropped: the client stays Success and forwards the
            // server's exit type string (which MSBuildClientApp maps to a zero process exit code).
            result.MSBuildClientExitType.ShouldBe(MSBuildClientExitType.Success);
            result.MSBuildAppExitTypeString.ShouldBe("Success");

            // The trailing console write queued right before the result - the "Build succeeded." summary
            // that #14172 dropped - must also be flushed, not stranded behind the drained result.
            capturedConsole.ToString().ShouldContain("Build succeeded.");
        }
    }
}
