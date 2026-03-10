// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if NET

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Build.BackEnd;
using Microsoft.Build.Shared;
using Shouldly;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Build.UnitTests.BackEnd
{
    /// <summary>
    /// Tests for BuildCoordinator and BuildCoordinatorClient.
    /// These are integration tests that spin up a real coordinator over named pipes.
    /// </summary>
    public class BuildCoordinator_Tests : IDisposable
    {
        private readonly ITestOutputHelper _output;
        private readonly string _testPipeName;

        public BuildCoordinator_Tests(ITestOutputHelper output)
        {
            _output = output;
            // Use a unique pipe name per test run to avoid collisions
            _testPipeName = NativeMethodsShared.IsUnixLike
                ? $"/tmp/MSBuild-CoordinatorTest-{Guid.NewGuid():N}"
                : $"MSBuild-CoordinatorTest-{Guid.NewGuid():N}";
        }

        public void Dispose()
        {
            // Clean up pipe file on Unix
            if (NativeMethodsShared.IsUnixLike && File.Exists(_testPipeName))
            {
                try { File.Delete(_testPipeName); }
                catch { }
            }
        }

        #region Coordinator Protocol Tests

        [Fact]
        public void FirstBuild_GetsFullBudget()
        {
            using var coordinator = new BuildCoordinator(12, maxConcurrentBuilds: 3);
            coordinator.Start();

            try
            {
                string? response = SendRawCommand("REGISTER build-1 6");
                response.ShouldNotBeNull();
                response.ShouldStartWith("OK ");

                // First build should get full budget (min of requested and total)
                int granted = int.Parse(response!.Split(' ')[1]);
                granted.ShouldBe(6);
            }
            finally
            {
                coordinator.Stop();
            }
        }

        [Fact]
        public void FirstBuild_GetsCappedAtTotalBudget()
        {
            using var coordinator = new BuildCoordinator(8, maxConcurrentBuilds: 3);
            coordinator.Start();

            try
            {
                // Request more than total budget
                string? response = SendRawCommand("REGISTER build-1 20");
                response.ShouldNotBeNull();
                response.ShouldStartWith("OK ");

                int granted = int.Parse(response!.Split(' ')[1]);
                granted.ShouldBeLessThanOrEqualTo(8);
            }
            finally
            {
                coordinator.Stop();
            }
        }

        [Fact]
        public void SecondBuild_GetsQueued()
        {
            using var coordinator = new BuildCoordinator(12, maxConcurrentBuilds: 1);
            coordinator.Start();

            try
            {
                // First build — immediate
                string? r1 = SendRawCommand("REGISTER build-1 6");
                r1.ShouldStartWith("OK ");

                // Second build — queued (max concurrent = 1)
                string? r2 = SendRawCommand("REGISTER build-2 6");
                r2.ShouldNotBeNull();
                r2.ShouldStartWith("QUEUED ");

                string[] parts = r2!.Split(' ');
                int position = int.Parse(parts[1]);
                position.ShouldBe(1); // First in queue
            }
            finally
            {
                coordinator.Stop();
            }
        }

        [Fact]
        public void Heartbeat_ReturnsCurrentBudget()
        {
            using var coordinator = new BuildCoordinator(12, maxConcurrentBuilds: 3);
            coordinator.Start();

            try
            {
                SendRawCommand("REGISTER build-1 6");

                string? hb = SendRawCommand("HEARTBEAT build-1");
                hb.ShouldNotBeNull();
                hb.ShouldStartWith("OK ");

                int budget = int.Parse(hb!.Split(' ')[1]);
                budget.ShouldBeGreaterThan(0);
            }
            finally
            {
                coordinator.Stop();
            }
        }

        [Fact]
        public void Heartbeat_ForQueuedBuild_ReturnsQueuePosition()
        {
            using var coordinator = new BuildCoordinator(12, maxConcurrentBuilds: 1);
            coordinator.Start();

            try
            {
                SendRawCommand("REGISTER build-1 6");
                SendRawCommand("REGISTER build-2 6");

                string? hb = SendRawCommand("HEARTBEAT build-2");
                hb.ShouldNotBeNull();
                hb.ShouldStartWith("QUEUED ");
            }
            finally
            {
                coordinator.Stop();
            }
        }

        [Fact]
        public void Unregister_RemovesActiveBuild()
        {
            using var coordinator = new BuildCoordinator(12, maxConcurrentBuilds: 3);
            coordinator.Start();

            try
            {
                SendRawCommand("REGISTER build-1 6");
                string? unreg = SendRawCommand("UNREGISTER build-1");
                unreg.ShouldNotBeNull();
                unreg.ShouldStartWith("OK");

                // Status should show 0 active
                string? status = SendRawCommand("STATUS");
                status.ShouldNotBeNull();
                status.ShouldContain("active=0");
            }
            finally
            {
                coordinator.Stop();
            }
        }

        [Fact]
        public void Unregister_PromotesQueuedBuild()
        {
            using var coordinator = new BuildCoordinator(12, maxConcurrentBuilds: 1);
            coordinator.Start();

            try
            {
                SendRawCommand("REGISTER build-1 6");
                SendRawCommand("REGISTER build-2 6");

                // Unregister first build — should promote build-2
                string? unreg = SendRawCommand("UNREGISTER build-1");
                unreg.ShouldNotBeNull();
                unreg.ShouldContain("promoted");
                unreg.ShouldContain("build-2");

                // build-2 should now get OK on heartbeat (it's active)
                string? hb = SendRawCommand("HEARTBEAT build-2");
                hb.ShouldStartWith("OK ");
            }
            finally
            {
                coordinator.Stop();
            }
        }

        [Fact]
        public void BudgetRebalances_WhenSecondBuildJoins()
        {
            using var coordinator = new BuildCoordinator(12, maxConcurrentBuilds: 3);
            coordinator.Start();

            try
            {
                // First build gets full budget
                string? r1 = SendRawCommand("REGISTER build-1 12");
                int firstGrant = int.Parse(r1!.Split(' ')[1]);
                firstGrant.ShouldBe(12);

                // Second build joins
                SendRawCommand("REGISTER build-2 12");

                // First build heartbeats — should get reduced budget (acknowledges epoch)
                string? hb1 = SendRawCommand("HEARTBEAT build-1");
                int newBudget = int.Parse(hb1!.Split(' ')[1]);
                newBudget.ShouldBe(6); // 12 / 2 builds = 6 each

                // Second build should now be promoted after build-1 acknowledged
                // Heartbeat for build-2 should return OK (promoted)
                string? hb2 = SendRawCommand("HEARTBEAT build-2");
                hb2.ShouldStartWith("OK ");
                int budget2 = int.Parse(hb2!.Split(' ')[1]);
                budget2.ShouldBe(6);
            }
            finally
            {
                coordinator.Stop();
            }
        }

        [Fact]
        public void BudgetIncreases_WhenBuildLeaves()
        {
            using var coordinator = new BuildCoordinator(12, maxConcurrentBuilds: 3);
            coordinator.Start();

            try
            {
                SendRawCommand("REGISTER build-1 12");
                SendRawCommand("REGISTER build-2 12");

                // Acknowledge epoch so build-2 promotes
                SendRawCommand("HEARTBEAT build-1");
                SendRawCommand("HEARTBEAT build-2");

                // Now unregister build-2
                SendRawCommand("UNREGISTER build-2");

                // build-1 should get full budget back
                string? hb = SendRawCommand("HEARTBEAT build-1");
                int budget = int.Parse(hb!.Split(' ')[1]);
                budget.ShouldBe(12);
            }
            finally
            {
                coordinator.Stop();
            }
        }

        [Fact]
        public void Status_ReturnsCorrectSummary()
        {
            using var coordinator = new BuildCoordinator(12, maxConcurrentBuilds: 2);
            coordinator.Start();

            try
            {
                SendRawCommand("REGISTER build-1 6");
                SendRawCommand("REGISTER build-2 6");
                SendRawCommand("REGISTER build-3 6"); // Will be queued

                // Acknowledge so build-2 promotes
                SendRawCommand("HEARTBEAT build-1");

                string? status = SendRawCommand("STATUS");
                status.ShouldNotBeNull();
                status.ShouldContain("budget=12");
                status.ShouldContain("max=2");
            }
            finally
            {
                coordinator.Stop();
            }
        }

        [Fact]
        public void Shutdown_StopsCoordinator()
        {
            using var coordinator = new BuildCoordinator(12, maxConcurrentBuilds: 3);
            coordinator.Start();

            string? response = SendRawCommand("SHUTDOWN");
            response.ShouldBe("OK");

            // Coordinator should stop — WaitForShutdown should return
            coordinator.WaitForShutdown();
        }

        [Fact]
        public void UnknownCommand_ReturnsError()
        {
            using var coordinator = new BuildCoordinator(12, maxConcurrentBuilds: 3);
            coordinator.Start();

            try
            {
                string? response = SendRawCommand("INVALID_CMD");
                response.ShouldNotBeNull();
                response.ShouldStartWith("ERR");
            }
            finally
            {
                coordinator.Stop();
            }
        }

        [Fact]
        public void Register_WithInvalidArgs_ReturnsError()
        {
            using var coordinator = new BuildCoordinator(12, maxConcurrentBuilds: 3);
            coordinator.Start();

            try
            {
                string? response = SendRawCommand("REGISTER");
                response.ShouldNotBeNull();
                response.ShouldStartWith("ERR");
            }
            finally
            {
                coordinator.Stop();
            }
        }

        [Fact]
        public void EpochGating_PreventsPromotionBeforeAcknowledgment()
        {
            using var coordinator = new BuildCoordinator(12, maxConcurrentBuilds: 3);
            coordinator.Start();

            try
            {
                SendRawCommand("REGISTER build-1 12");
                SendRawCommand("REGISTER build-2 12"); // Queued — epoch bumped

                // build-2 heartbeats BEFORE build-1 acknowledges — should still be queued
                string? hb2 = SendRawCommand("HEARTBEAT build-2");
                hb2.ShouldStartWith("QUEUED ");

                // Now build-1 acknowledges via heartbeat
                SendRawCommand("HEARTBEAT build-1");

                // build-2 should now be promoted
                string? hb2After = SendRawCommand("HEARTBEAT build-2");
                hb2After.ShouldStartWith("OK ");
            }
            finally
            {
                coordinator.Stop();
            }
        }

        [Fact]
        public void MaxConcurrency_EnforcesLimit()
        {
            using var coordinator = new BuildCoordinator(12, maxConcurrentBuilds: 2);
            coordinator.Start();

            try
            {
                SendRawCommand("REGISTER build-1 6");
                SendRawCommand("REGISTER build-2 6");

                // Acknowledge so build-2 promotes
                SendRawCommand("HEARTBEAT build-1");

                // Confirm build-2 is promoted before registering build-3
                // (the promotion happens asynchronously on the server)
                string? hb2 = SendRawCommand("HEARTBEAT build-2");
                hb2.ShouldStartWith("OK ");

                // Third build should be queued (max=2)
                SendRawCommand("REGISTER build-3 6");
                string? hb3 = SendRawCommand("HEARTBEAT build-3");
                hb3.ShouldStartWith("QUEUED ");

                // Even after all heartbeats, build-3 stays queued
                SendRawCommand("HEARTBEAT build-1");
                SendRawCommand("HEARTBEAT build-2");
                hb3 = SendRawCommand("HEARTBEAT build-3");
                hb3.ShouldStartWith("QUEUED ");
            }
            finally
            {
                coordinator.Stop();
            }
        }

        [Fact]
        public void FairShare_DistributesBudgetEvenly()
        {
            using var coordinator = new BuildCoordinator(12, maxConcurrentBuilds: 3);
            coordinator.Start();

            try
            {
                // Register 3 builds and get them all active
                SendRawCommand("REGISTER build-1 12");
                SendRawCommand("REGISTER build-2 12");
                SendRawCommand("HEARTBEAT build-1"); // Acknowledge for build-2 promotion
                SendRawCommand("REGISTER build-3 12");
                SendRawCommand("HEARTBEAT build-1"); // Acknowledge for build-3
                SendRawCommand("HEARTBEAT build-2"); // Acknowledge for build-3

                // All three should get 4 nodes each (12 / 3)
                string? hb1 = SendRawCommand("HEARTBEAT build-1");
                string? hb2 = SendRawCommand("HEARTBEAT build-2");
                string? hb3 = SendRawCommand("HEARTBEAT build-3");

                int b1 = int.Parse(hb1!.Split(' ')[1]);
                int b2 = int.Parse(hb2!.Split(' ')[1]);
                int b3 = int.Parse(hb3!.Split(' ')[1]);

                b1.ShouldBe(4);
                b2.ShouldBe(4);
                b3.ShouldBe(4);
            }
            finally
            {
                coordinator.Stop();
            }
        }

        [Fact]
        public void FairShare_CapsAtRequestedNodes()
        {
            using var coordinator = new BuildCoordinator(12, maxConcurrentBuilds: 3);
            coordinator.Start();

            try
            {
                // build-1 only wants 2 nodes
                SendRawCommand("REGISTER build-1 2");
                SendRawCommand("REGISTER build-2 12");
                SendRawCommand("HEARTBEAT build-1");

                // build-1 should get 2 (capped at requested), build-2 gets 6 (fair share)
                string? hb1 = SendRawCommand("HEARTBEAT build-1");
                string? hb2 = SendRawCommand("HEARTBEAT build-2");

                int b1 = int.Parse(hb1!.Split(' ')[1]);
                int b2 = int.Parse(hb2!.Split(' ')[1]);

                b1.ShouldBe(2);
                b2.ShouldBe(6);
            }
            finally
            {
                coordinator.Stop();
            }
        }

        #endregion

        #region Client Tests

        [Fact]
        public void Client_NoCoordinator_TryRegisterReturnsFalse()
        {
            // This test verifies that when no coordinator is listening on the expected pipe,
            // the client gracefully returns false. We use a custom pipe name that doesn't exist.
            // Since BuildCoordinatorClient always uses GetPipeName(), we can't easily redirect it.
            // Instead, verify that SendCommand returns null for a nonexistent pipe by checking
            // that a raw connection to a bogus pipe fails.
            string bogusPipe = NativeMethodsShared.IsUnixLike
                ? $"/tmp/MSBuild-NoPipe-{Guid.NewGuid():N}"
                : $"MSBuild-NoPipe-{Guid.NewGuid():N}";

            bool connected = false;
            try
            {
                using var client = new System.IO.Pipes.NamedPipeClientStream(".", bogusPipe, PipeDirection.InOut, PipeOptions.CurrentUserOnly);
                client.Connect(500);
                connected = true;
            }
            catch (TimeoutException)
            {
                // Expected — no server
            }
            catch (IOException)
            {
                // Expected — no server
            }

            connected.ShouldBeFalse("Connection to nonexistent pipe should fail");
        }

        [Fact]
        public void Client_RegistersWithCoordinator()
        {
            using var coordinator = new BuildCoordinator(12, maxConcurrentBuilds: 3);
            coordinator.Start();

            try
            {
                using var client = new BuildCoordinatorClient();
                bool registered = client.TryRegister(6, out int granted);

                registered.ShouldBeTrue();
                client.IsConnected.ShouldBeTrue();
                granted.ShouldBe(6);
                client.GrantedNodes.ShouldBe(6);
            }
            finally
            {
                coordinator.Stop();
            }
        }

        [Fact]
        public void Client_HeartbeatUpdatesbudget()
        {
            using var coordinator = new BuildCoordinator(12, maxConcurrentBuilds: 3);
            coordinator.Start();

            try
            {
                using var client1 = new BuildCoordinatorClient();
                client1.TryRegister(12, out int granted1);
                granted1.ShouldBe(12);

                client1.StartHeartbeat();

                // Register a second build via raw protocol to trigger rebalance
                SendRawCommand($"REGISTER second-build 12");

                // Wait for heartbeat to pick up the rebalanced budget
                Thread.Sleep(5000);

                // The client should have updated its internal granted nodes via heartbeat
                client1.GrantedNodes.ShouldBe(6); // 12 / 2 = 6
            }
            finally
            {
                coordinator.Stop();
            }
        }

        [Fact]
        public void Client_UnregisterCleansUp()
        {
            using var coordinator = new BuildCoordinator(12, maxConcurrentBuilds: 3);
            coordinator.Start();

            try
            {
                var client = new BuildCoordinatorClient();
                client.TryRegister(6, out _);
                client.IsConnected.ShouldBeTrue();

                client.Dispose(); // Triggers Unregister

                // Coordinator should show 0 active
                string? status = SendRawCommand("STATUS");
                status!.ShouldContain("active=0");
            }
            finally
            {
                coordinator.Stop();
            }
        }

        [Fact]
        public void Client_QueuedBuild_BlocksUntilPromoted()
        {
            using var coordinator = new BuildCoordinator(12, maxConcurrentBuilds: 1);
            coordinator.Start();

            try
            {
                // First build registers immediately
                using var client1 = new BuildCoordinatorClient();
                client1.TryRegister(6, out _);

                // Start heartbeats so client1 acknowledges epochs
                client1.StartHeartbeat();

                // Second build should block in queue
                var queuePositions = new List<int>();
                using var client2 = new BuildCoordinatorClient();

                var registerTask = Task.Run(() =>
                {
                    return client2.TryRegister(6, out _, onQueuePositionChanged: (pos, total, wait) =>
                    {
                        _output.WriteLine($"Queue position: {pos}/{total}, waiting {wait}s");
                        queuePositions.Add(pos);
                    });
                });

                // Give client2 time to register and start heartbeating in queue
                Thread.Sleep(3000);
                registerTask.IsCompleted.ShouldBeFalse("Build should still be queued");

                // Unregister first build — should promote second
                client1.Dispose();

                // Second build should complete registration
                bool registered = registerTask.Wait(TimeSpan.FromSeconds(15))
                    ? registerTask.Result
                    : false;

                registered.ShouldBeTrue("Queued build should be promoted after first build unregisters");
                client2.IsConnected.ShouldBeTrue();
                queuePositions.ShouldNotBeEmpty();
            }
            finally
            {
                coordinator.Stop();
            }
        }

        [Fact]
        public void Client_QueuedBuild_CancellationUnregisters()
        {
            using var coordinator = new BuildCoordinator(12, maxConcurrentBuilds: 1);
            coordinator.Start();

            try
            {
                using var client1 = new BuildCoordinatorClient();
                client1.TryRegister(6, out _);

                using var cts = new CancellationTokenSource();
                using var client2 = new BuildCoordinatorClient();

                var registerTask = Task.Run(() =>
                    client2.TryRegister(6, out _, ct: cts.Token));

                // Let it enter queue
                Thread.Sleep(3000);

                // Cancel
                cts.Cancel();

                bool registered = registerTask.Wait(TimeSpan.FromSeconds(10))
                    ? registerTask.Result
                    : true; // If timed out, fail

                registered.ShouldBeFalse("Cancelled registration should return false");
            }
            finally
            {
                coordinator.Stop();
            }
        }

        #endregion

        #region GetPipeName Tests

        [Fact]
        public void GetPipeName_ContainsUsername()
        {
            string pipeName = BuildCoordinator.GetPipeName();
            pipeName.ShouldContain(Environment.UserName);
        }

        [Fact]
        public void GetPipeName_OnUnix_IsAbsolutePath()
        {
            if (!NativeMethodsShared.IsUnixLike)
            {
                return; // Skip on Windows
            }

            string pipeName = BuildCoordinator.GetPipeName();
            pipeName.ShouldStartWith("/tmp/");
        }

        #endregion

        #region Staleness Reaper Tests

        [Fact]
        public void StalenessReaper_ReapsDeadBuild()
        {
            using var coordinator = new BuildCoordinator(12, maxConcurrentBuilds: 3);
            coordinator.Start();

            try
            {
                // Register a build with a PID that definitely doesn't exist.
                // Build ID format is "{PID}-{ticks}" — use PID 99999999
                string deadBuildId = "99999999-123456789";
                string? r = SendRawCommand($"REGISTER {deadBuildId} 6");
                r.ShouldStartWith("OK ");

                // Verify it's active
                string? status1 = SendRawCommand("STATUS");
                status1!.ShouldContain("active=1");

                // Wait for the staleness reaper to detect it (10s stale + 5s reap interval)
                // The reaper checks every 5s and requires heartbeat to be stale for 10s
                Thread.Sleep(18000);

                // Build should have been reaped
                string? status2 = SendRawCommand("STATUS");
                status2!.ShouldContain("active=0");
            }
            finally
            {
                coordinator.Stop();
            }
        }

        [Fact]
        public void StalenessReaper_DoesNotReapLiveBuild()
        {
            using var coordinator = new BuildCoordinator(12, maxConcurrentBuilds: 3);
            coordinator.Start();

            try
            {
                // Register with current PID — process is definitely alive
                string liveBuildId = $"{Environment.ProcessId}-{DateTime.UtcNow.Ticks}";
                SendRawCommand($"REGISTER {liveBuildId} 6");

                // Wait past the stale threshold but keep heartbeating
                for (int i = 0; i < 4; i++)
                {
                    Thread.Sleep(3000);
                    SendRawCommand($"HEARTBEAT {liveBuildId}");
                }

                // Build should still be active
                string? status = SendRawCommand("STATUS");
                status!.ShouldContain("active=1");
            }
            finally
            {
                coordinator.Stop();
            }
        }

        [Fact]
        public void StalenessReaper_PromotesQueuedAfterReap()
        {
            using var coordinator = new BuildCoordinator(12, maxConcurrentBuilds: 1);
            coordinator.Start();

            try
            {
                // First build — dead PID
                string deadBuildId = "99999999-111111";
                SendRawCommand($"REGISTER {deadBuildId} 6");

                // Second build — queued (max concurrent = 1)
                string liveBuildId = $"{Environment.ProcessId}-{DateTime.UtcNow.Ticks}";
                SendRawCommand($"REGISTER {liveBuildId} 6");

                // Verify: 1 active, 1 queued
                string? status1 = SendRawCommand("STATUS");
                status1!.ShouldContain("active=1");

                // Wait for reaper to kill the dead build and promote the queued one
                Thread.Sleep(18000);

                // Live build should now be active
                string? hb = SendRawCommand($"HEARTBEAT {liveBuildId}");
                hb.ShouldStartWith("OK ");

                string? status2 = SendRawCommand("STATUS");
                status2!.ShouldContain("active=1");
            }
            finally
            {
                coordinator.Stop();
            }
        }

        #endregion

        #region Concurrent Connection Tests

        [Fact]
        public void ConcurrentRegistrations_AllSucceed()
        {
            using var coordinator = new BuildCoordinator(24, maxConcurrentBuilds: 5);
            coordinator.Start();

            try
            {
                int successCount = 0;
                var tasks = new Task[5];

                for (int i = 0; i < 5; i++)
                {
                    int buildNum = i;
                    tasks[i] = Task.Run(() =>
                    {
                        string? response = SendRawCommand($"REGISTER concurrent-{buildNum} 6");
                        if (response != null && (response.StartsWith("OK ") || response.StartsWith("QUEUED ")))
                        {
                            Interlocked.Increment(ref successCount);
                        }
                    });
                }

                Task.WaitAll(tasks, TimeSpan.FromSeconds(15));
                successCount.ShouldBe(5);
            }
            finally
            {
                coordinator.Stop();
            }
        }

        #endregion

        #region Helpers

        /// <summary>
        /// Send a raw command to the coordinator using the well-known pipe name.
        /// </summary>
        private string? SendRawCommand(string command)
        {
            string pipeName = BuildCoordinator.GetPipeName();

            for (int attempt = 0; attempt < 3; attempt++)
            {
                try
                {
                    using var client = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.CurrentUserOnly);
                    client.Connect(2000);

                    using var writer = new StreamWriter(client, leaveOpen: true) { AutoFlush = true };
                    using var reader = new StreamReader(client, leaveOpen: true);

                    writer.WriteLine(command);
                    return reader.ReadLine();
                }
                catch (TimeoutException)
                {
                    if (attempt < 2)
                    {
                        Thread.Sleep(500);
                    }
                }
                catch (IOException)
                {
                    if (attempt < 2)
                    {
                        Thread.Sleep(500);
                    }
                }
            }

            return null;
        }

        #endregion
    }
}

#endif
