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
    /// Pure domain tests for BuildCoordinator — no pipes, no I/O.
    /// </summary>
    public class BuildCoordinator_Tests
    {
        [Fact]
        public void Register_FirstBuild_GetsGranted()
        {
            using var coord = new BuildCoordinator(new FairShareBudgetPolicy(12, 3));
            var result = coord.Register("build-1", 6);

            result.Outcome.ShouldBe(RegisterOutcome.Granted);
            // remaining=12, slots=3, fairShare=4, capped at requested=6 → 4
            result.GrantedNodes.ShouldBe(4);
        }

        [Fact]
        public void Register_AtCapacity_GetsQueued()
        {
            using var coord = new BuildCoordinator(new FairShareBudgetPolicy(12, 2));
            coord.Register("build-1", 4);
            coord.Register("build-2", 4);

            var result = coord.Register("build-3", 4);

            result.Outcome.ShouldBe(RegisterOutcome.Queued);
            result.QueuePosition.ShouldBe(1);
            result.QueueTotal.ShouldBe(1);
        }

        [Fact]
        public void Register_MultipleQueued_PositionsCorrect()
        {
            using var coord = new BuildCoordinator(new FairShareBudgetPolicy(12, 1));
            coord.Register("build-1", 4);

            var r2 = coord.Register("build-2", 4);
            var r3 = coord.Register("build-3", 4);

            r2.QueuePosition.ShouldBe(1);
            r3.QueuePosition.ShouldBe(2);
            r3.QueueTotal.ShouldBe(2);
        }

        [Fact]
        public void Register_FillsToMaxConcurrent()
        {
            using var coord = new BuildCoordinator(new FairShareBudgetPolicy(12, 3));
            var r1 = coord.Register("build-1", 4);
            var r2 = coord.Register("build-2", 4);
            var r3 = coord.Register("build-3", 4);

            r1.Outcome.ShouldBe(RegisterOutcome.Granted);
            r2.Outcome.ShouldBe(RegisterOutcome.Granted);
            r3.Outcome.ShouldBe(RegisterOutcome.Granted);
        }

        [Fact]
        public void Heartbeat_UpdatesLastHeartbeat()
        {
            using var coord = new BuildCoordinator(new FairShareBudgetPolicy(12, 3));
            coord.Register("build-1", 6);

            // Should not throw
            coord.Heartbeat("build-1");

            // Heartbeat for unknown build should also not throw
            coord.Heartbeat("unknown-build");
        }

        [Fact]
        public void Unregister_Active_PromotesQueued()
        {
            var promoted = new List<(string buildId, int granted)>();
            using var coord = new BuildCoordinator(new FairShareBudgetPolicy(12, 2), onBuildPromoted: (id, nodes) => promoted.Add((id, nodes)));

            coord.Register("build-1", 6);
            coord.Register("build-2", 6);
            coord.Register("build-3", 6); // queued

            int promotedCount = coord.Unregister("build-1");

            promotedCount.ShouldBe(1);
            promoted.Count.ShouldBe(1);
            promoted[0].buildId.ShouldBe("build-3");
            promoted[0].granted.ShouldBeGreaterThan(0);
        }

        [Fact]
        public void Unregister_Queued_NoPromotion()
        {
            var promoted = new List<string>();
            using var coord = new BuildCoordinator(new FairShareBudgetPolicy(12, 2), onBuildPromoted: (id, _) => promoted.Add(id));

            coord.Register("build-1", 6);
            coord.Register("build-2", 6);
            coord.Register("build-3", 6); // queued

            int promotedCount = coord.Unregister("build-3");

            promotedCount.ShouldBe(0);
            promoted.Count.ShouldBe(0);
        }

        [Fact]
        public void Unregister_MultipleSlotsOpen_PromotesMultiple()
        {
            var promoted = new List<string>();
            using var coord = new BuildCoordinator(new FairShareBudgetPolicy(12, 3), onBuildPromoted: (id, _) => promoted.Add(id));

            coord.Register("build-1", 4);
            coord.Register("build-2", 4);
            coord.Register("build-3", 4);
            coord.Register("build-4", 4); // queued
            coord.Register("build-5", 4); // queued

            // Unregister build-1 → promotes build-4 (1 slot opens)
            coord.Unregister("build-1");
            promoted.Count.ShouldBe(1);
            promoted[0].ShouldBe("build-4");
        }

        [Fact]
        public void GetStatus_ReflectsState()
        {
            using var coord = new BuildCoordinator(new FairShareBudgetPolicy(12, 2));
            coord.Register("build-1", 6);
            coord.Register("build-2", 6);
            coord.Register("build-3", 6); // queued

            var status = coord.GetStatus();

            status.TotalBudget.ShouldBe(12);
            status.ActiveCount.ShouldBe(2);
            status.QueuedCount.ShouldBe(1);
            status.MaxConcurrentBuilds.ShouldBe(2);
        }

        [Fact]
        public void GetStatus_AfterUnregister()
        {
            using var coord = new BuildCoordinator(new FairShareBudgetPolicy(12, 2));
            coord.Register("build-1", 6);
            coord.Register("build-2", 6);

            coord.Unregister("build-1");

            var status = coord.GetStatus();
            status.ActiveCount.ShouldBe(1);
            status.QueuedCount.ShouldBe(0);
        }

        [Fact]
        public void StartupDelay_QueuesFirst_ThenBatchPromotes()
        {
            var promoted = new List<string>();
            using var coord = new BuildCoordinator(new FairShareBudgetPolicy(12, 3), startupDelayMs: 100, onBuildPromoted: (id, _) => promoted.Add(id));

            // All should be queued during startup delay
            var r1 = coord.Register("build-1", 4);
            var r2 = coord.Register("build-2", 4);
            var r3 = coord.Register("build-3", 4);

            r1.Outcome.ShouldBe(RegisterOutcome.Queued);
            r2.Outcome.ShouldBe(RegisterOutcome.Queued);
            r3.Outcome.ShouldBe(RegisterOutcome.Queued);

            // Wait for startup delay to fire
            Thread.Sleep(300);

            // All 3 should be promoted (maxConcurrent=3)
            promoted.Count.ShouldBe(3);

            var status = coord.GetStatus();
            status.ActiveCount.ShouldBe(3);
            status.QueuedCount.ShouldBe(0);
        }

        [Fact]
        public void StartupDelay_MoreThanCapacity_OnlyPromotesMax()
        {
            var promoted = new List<string>();
            using var coord = new BuildCoordinator(new FairShareBudgetPolicy(12, 2), startupDelayMs: 100, onBuildPromoted: (id, _) => promoted.Add(id));

            coord.Register("build-1", 4);
            coord.Register("build-2", 4);
            coord.Register("build-3", 4);

            Thread.Sleep(300);

            promoted.Count.ShouldBe(2);

            var status = coord.GetStatus();
            status.ActiveCount.ShouldBe(2);
            status.QueuedCount.ShouldBe(1);
        }

        [Fact]
        public void GrantedNodes_FairShare()
        {
            using var coord = new BuildCoordinator(new FairShareBudgetPolicy(12, 3));

            // remaining=12, slots=3, fair=4, capped at 8 → 4
            var r1 = coord.Register("build-1", 8);
            r1.GrantedNodes.ShouldBe(4);

            // remaining=8, slots=2, fair=4, capped at 6 → 4
            var r2 = coord.Register("build-2", 6);
            r2.GrantedNodes.ShouldBe(4);

            // remaining=4, slots=1, fair=4, capped at 4 → 4
            var r3 = coord.Register("build-3", 4);
            r3.GrantedNodes.ShouldBe(4);
        }
    }

    /// <summary>
    /// Integration tests for NamedPipeCoordinatorHost — exercises the real pipe protocol.
    /// </summary>
    public class NamedPipeCoordinatorHost_Tests : IDisposable
    {
        private readonly NamedPipeCoordinatorHost _host;
        private readonly string _pipeName;

        public NamedPipeCoordinatorHost_Tests()
        {
            _pipeName = $"/tmp/MSBuild-Test-{Guid.NewGuid():N}";
            _host = new NamedPipeCoordinatorHost(new FairShareBudgetPolicy(12, 3), pipeName: _pipeName);
            _host.Start();
        }

        public void Dispose()
        {
            _host.Dispose();
            try { File.Delete(_pipeName); } catch { }
        }

        [Fact]
        public void Register_ReturnsGranted()
        {
            string? response = SendCommand("REGISTER test-1 4");

            response.ShouldNotBeNull();
            response.ShouldStartWith("OK ");
            int.TryParse(response.AsSpan(3), out int granted).ShouldBeTrue();
            granted.ShouldBeGreaterThan(0);
        }

        [Fact]
        public void Register_AtCapacity_ReturnsQueued()
        {
            SendCommand("REGISTER test-1 4");
            SendCommand("REGISTER test-2 4");
            SendCommand("REGISTER test-3 4");

            string? response = SendCommand("REGISTER test-4 4");

            response.ShouldNotBeNull();
            response.ShouldStartWith("QUEUED ");
        }

        [Fact]
        public void Heartbeat_ReturnsOk()
        {
            SendCommand("REGISTER test-1 4");

            string? response = SendCommand("HEARTBEAT test-1");

            response.ShouldBe("OK");
        }

        [Fact]
        public void Unregister_ReturnsOk()
        {
            SendCommand("REGISTER test-1 4");

            string? response = SendCommand("UNREGISTER test-1");

            response.ShouldNotBeNull();
            response.ShouldStartWith("OK");
        }

        [Fact]
        public void Unregister_WithQueued_ReturnsPromotedCount()
        {
            SendCommand("REGISTER test-1 4");
            SendCommand("REGISTER test-2 4");
            SendCommand("REGISTER test-3 4");

            // Queue one more
            Task.Run(() =>
            {
                // Create a wait pipe for the queued build to receive promotion
                string waitPipe = NamedPipeCoordinatorHost.GetWaitPipeName(_pipeName, "test-4");
                using var server = new NamedPipeServerStream(waitPipe, PipeDirection.In, 1, PipeTransmissionMode.Byte, PipeOptions.CurrentUserOnly);
                server.WaitForConnectionAsync(CancellationToken.None).Wait(TimeSpan.FromSeconds(5));
                using var reader = new StreamReader(server);
                reader.ReadLine();
            });

            SendCommand("REGISTER test-4 4");
            Thread.Sleep(200); // Let wait pipe get set up

            string? response = SendCommand("UNREGISTER test-1");

            response.ShouldNotBeNull();
            response.ShouldContain("promoted");
        }

        [Fact]
        public void Status_ReturnsBudgetInfo()
        {
            SendCommand("REGISTER test-1 4");

            string? response = SendCommand("STATUS");

            response.ShouldNotBeNull();
            response.ShouldStartWith("OK ");
            response.ShouldContain("budget=12");
            response.ShouldContain("active=1");
            response.ShouldContain("max=3");
        }

        [Fact]
        public void Shutdown_StopsHost()
        {
            string? response = SendCommand("SHUTDOWN");

            response.ShouldBe("OK");

            // Should not be able to connect after shutdown
            Thread.Sleep(200);
            string? afterShutdown = SendCommand("STATUS");
            afterShutdown.ShouldBeNull();
        }

        [Fact]
        public void UnknownCommand_ReturnsError()
        {
            string? response = SendCommand("FOOBAR");

            response.ShouldNotBeNull();
            response.ShouldStartWith("ERR");
        }

        [Fact]
        public void Register_InvalidArgs_ReturnsError()
        {
            string? response = SendCommand("REGISTER");

            response.ShouldNotBeNull();
            response.ShouldStartWith("ERR");
        }

        [Fact]
        public void Register_InvalidNodeCount_ReturnsError()
        {
            string? response = SendCommand("REGISTER test-1 notanumber");

            response.ShouldNotBeNull();
            response.ShouldStartWith("ERR");
        }

        [Fact]
        public void Client_Register_Heartbeat_Unregister()
        {
            using var client = new NamedPipeCoordinatorClient(_pipeName);

            bool registered = client.TryRegister(4, out int granted);

            registered.ShouldBeTrue();
            granted.ShouldBeGreaterThan(0);
            client.IsConnected.ShouldBeTrue();

            // Heartbeat
            client.StartHeartbeat();
            Thread.Sleep(500);

            // Unregister
            client.Unregister();
            client.IsConnected.ShouldBeFalse();
        }

        [Fact]
        public void Client_QueuedBuild_BlocksUntilPromoted()
        {
            // Fill capacity
            SendCommand("REGISTER fill-1 4");
            SendCommand("REGISTER fill-2 4");
            SendCommand("REGISTER fill-3 4");

            using var client = new NamedPipeCoordinatorClient(_pipeName);
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

            bool registered = false;
            int grantedNodes = 0;

            // Register client in background (will block on wait pipe)
            var registerTask = Task.Run(() =>
            {
                registered = client.TryRegister(4, out grantedNodes, cts.Token);
            });

            Thread.Sleep(500); // Let the client get queued

            // Unregister one build to open a slot
            SendCommand("UNREGISTER fill-1");

            registerTask.Wait(TimeSpan.FromSeconds(10)).ShouldBeTrue("Register task should complete after promotion");

            registered.ShouldBeTrue();
            grantedNodes.ShouldBeGreaterThan(0);
        }

        [Fact]
        public void Client_QueuedBuild_CancelledWhileWaiting()
        {
            // Fill capacity
            SendCommand("REGISTER fill-1 4");
            SendCommand("REGISTER fill-2 4");
            SendCommand("REGISTER fill-3 4");

            using var client = new NamedPipeCoordinatorClient(_pipeName);
            using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));

            bool registered = false;

            var registerTask = Task.Run(() =>
            {
                registered = client.TryRegister(4, out _, cts.Token);
            });

            registerTask.Wait(TimeSpan.FromSeconds(5)).ShouldBeTrue("Register task should complete after cancellation");
            registered.ShouldBeFalse();
        }

        [Fact]
        public void ConcurrentRegistrations()
        {
            var tasks = new Task<string?>[6];

            for (int i = 0; i < tasks.Length; i++)
            {
                int idx = i;
                tasks[i] = Task.Run(() => SendCommand($"REGISTER concurrent-{idx} 4"));
            }

            Task.WaitAll(tasks, TimeSpan.FromSeconds(10)).ShouldBeTrue();

            int granted = 0;
            int queued = 0;
            foreach (var task in tasks)
            {
                task.Result.ShouldNotBeNull();
                if (task.Result!.StartsWith("OK ", StringComparison.Ordinal))
                {
                    granted++;
                }
                else if (task.Result!.StartsWith("QUEUED ", StringComparison.Ordinal))
                {
                    queued++;
                }
            }

            granted.ShouldBe(3); // maxConcurrent = 3
            queued.ShouldBe(3);
        }

        private string? SendCommand(string command)
        {
            try
            {
                using var client = new NamedPipeClientStream(".", _pipeName, PipeDirection.InOut, PipeOptions.CurrentUserOnly);
                client.Connect(5000);
                using var writer = new StreamWriter(client, leaveOpen: true) { AutoFlush = true };
                using var reader = new StreamReader(client, leaveOpen: true);
                writer.WriteLine(command);
                return reader.ReadLine();
            }
            catch
            {
                return null;
            }
        }
    }
}

#endif
