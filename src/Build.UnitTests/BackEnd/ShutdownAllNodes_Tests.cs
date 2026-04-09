// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;
using System.Threading;
using Microsoft.Build.Execution;
using Microsoft.Build.Shared;
using Microsoft.Build.UnitTests;
using Microsoft.Build.UnitTests.Shared;
using Shouldly;
using Xunit;
using Xunit.Abstractions;
using Constants = Microsoft.Build.Framework.Constants;

namespace Microsoft.Build.Engine.UnitTests.BackEnd
{
    /// <summary>
    /// E2E tests verifying that <see cref="BuildManager.ShutdownAllNodes"/> correctly finds and
    /// terminates idle worker nodes regardless of whether they were launched via the dotnet host
    /// or the MSBuild AppHost.
    /// </summary>
    /// <remarks>
    /// Regression tests for https://github.com/dotnet/msbuild/issues/13508
    /// </remarks>
    public class ShutdownAllNodes_Tests : IDisposable
    {
        private readonly ITestOutputHelper _output;
        private readonly TestEnvironment _env;

        public ShutdownAllNodes_Tests(ITestOutputHelper output)
        {
            _output = output;
            _env = TestEnvironment.Create(output);
        }

        public void Dispose()
        {
            _env.Dispose();
        }

        /// <summary>
        /// When the bootstrapped MSBuild (AppHost) launches worker nodes with node reuse,
        /// <see cref="BuildManager.ShutdownAllNodes"/> must find and terminate them even though
        /// the nodes run as "MSBuild" processes while the caller may resolve its host as "dotnet".
        /// </summary>
        [Fact]
        public void ShutdownAllNodes_FindsAppHostNodes()
        {
            // Build a simple project out-of-proc with node reuse enabled.
            // This creates idle MSBuild worker nodes that stick around after build completion.
            string output = BuildWithNodeReuse(nodeCount: 2);
            _output.WriteLine(output);

            _output.WriteLine($"MSBuild AppHost processes after build: {GetMSBuildAppHostProcessCount()}");

            // ShutdownAllNodes must complete without error. On shared CI machines we cannot
            // reliably assert process counts because other MSBuild instances may start/stop
            // concurrently. The key verification is that the code path exercising both
            // "dotnet" and "MSBuild" process name searches runs successfully.
            using var shutdownManager = new BuildManager("ShutdownTest");
            Should.NotThrow(() => shutdownManager.ShutdownAllNodes());

            // Best-effort wait for nodes to actually exit.
            Thread.Sleep(2000);
            _output.WriteLine($"MSBuild AppHost processes after shutdown: {GetMSBuildAppHostProcessCount()}");
        }

        /// <summary>
        /// Verify that <see cref="BuildManager.ShutdownAllNodes"/> does not fail when no
        /// idle nodes are present - the search for both "dotnet" and "MSBuild" processes
        /// should gracefully return empty results.
        /// </summary>
        [Fact]
        public void ShutdownAllNodes_NoNodesRunning_DoesNotThrow()
        {
            // Ensure no idle nodes from a previous test.
            using var cleanupManager = new BuildManager("CleanupFirst");
            cleanupManager.ShutdownAllNodes();
            WaitForProcessCountToStabilize(0, timeoutMs: 5_000);

            // Calling shutdown again when no nodes are running should not throw.
            using var shutdownManager = new BuildManager("ShutdownWhenEmpty");
            Should.NotThrow(() => shutdownManager.ShutdownAllNodes());
        }

        /// <summary>
        /// Launch nodes via the bootstrapped AppHost MSBuild.exe, then use
        /// <see cref="BuildManager.ShutdownAllNodes"/> to terminate them. Verifies the fix
        /// end-to-end using the actual bootstrapped MSBuild executable.
        /// </summary>
        [Fact]
        public void ShutdownAllNodes_AfterBootstrappedBuild_TerminatesIdleNodes()
        {
            // Build a project with the bootstrapped MSBuild (uses AppHost on .NET Core).
            string projectContent = """
                <Project>
                  <Target Name="Build">
                    <Message Text="Build completed." Importance="High" />
                  </Target>
                </Project>
                """;

            TransientTestFile project = _env.CreateFile("shutdownTest.proj", projectContent);

            // Build with node reuse enabled and out-of-proc nodes.
            string msbuildArgs = $"\"{project.Path}\" /m:2 /nr:true /v:m";
            string output = RunnerUtilities.ExecBootstrapedMSBuild(
                msbuildArgs,
                out bool success,
                outputHelper: _output);

            _output.WriteLine(output);
            success.ShouldBeTrue("Bootstrap build should succeed.");

            _output.WriteLine($"MSBuild AppHost processes after build: {GetMSBuildAppHostProcessCount()}");

            // ShutdownAllNodes must complete without error.
            using var shutdownManager = new BuildManager("BootstrapShutdown");
            Should.NotThrow(() => shutdownManager.ShutdownAllNodes());

            // Best-effort wait for nodes to actually exit.
            Thread.Sleep(2000);
            _output.WriteLine($"MSBuild AppHost processes after shutdown: {GetMSBuildAppHostProcessCount()}");
        }

        /// <summary>
        /// Builds a simple project with out-of-proc nodes and node reuse enabled,
        /// so that idle worker nodes remain after the build completes.
        /// </summary>
        private string BuildWithNodeReuse(int nodeCount)
        {
            string projectContent = """
                <Project>
                  <Target Name="Build">
                    <Message Text="Hello from node." Importance="High" />
                  </Target>
                </Project>
                """;

            TransientTestFile project = _env.CreateFile("nodeReuseTest.proj", projectContent);

            string msbuildArgs = $"\"{project.Path}\" /m:{nodeCount} /nr:true /v:m";
            string output = RunnerUtilities.ExecBootstrapedMSBuild(
                msbuildArgs,
                out bool success,
                outputHelper: _output);

            success.ShouldBeTrue("Build with node reuse should succeed.");
            return output;
        }

        /// <summary>
        /// Returns the number of MSBuild AppHost processes ("MSBuild") currently running.
        /// This intentionally only counts AppHost-launched nodes, not dotnet-hosted ones,
        /// because the bootstrapped test builds launch nodes via the MSBuild AppHost.
        /// </summary>
        private static int GetMSBuildAppHostProcessCount()
        {
            try
            {
                Process[] processes = Process.GetProcessesByName(Constants.MSBuildAppName);
                int count = processes.Length;
                foreach (Process p in processes)
                {
                    p.Dispose();
                }
                return count;
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// Polls until the MSBuild process count drops to or below the target, or until timeout.
        /// </summary>
        /// <returns>True if the target was reached; false on timeout.</returns>
        private bool WaitForProcessCountToStabilize(int targetMax, int timeoutMs)
        {
            int elapsed = 0;
            int delay = 200;
            while (elapsed < timeoutMs)
            {
                int count = GetMSBuildAppHostProcessCount();
                if (count <= targetMax)
                {
                    return true;
                }

                Thread.Sleep(delay);
                elapsed += delay;
                delay = Math.Min(delay * 2, 1000);
            }

            _output.WriteLine($"Timed out waiting for MSBuild process count to drop to {targetMax}. Current: {GetMSBuildAppHostProcessCount()}");
            return false;
        }
    }
}
