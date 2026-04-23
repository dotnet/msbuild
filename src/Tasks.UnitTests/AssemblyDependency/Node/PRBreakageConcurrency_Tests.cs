// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Build.BackEnd;
using Microsoft.Build.Framework;
using Microsoft.Build.Internal;
using Microsoft.Build.Tasks;
using Microsoft.Build.Tasks.AssemblyDependency;
using Microsoft.Build.Utilities;
using Shouldly;
using Xunit;

// Probabilistic / harness-driven regression tests for the concurrency findings
// of PR #13319 ("Enlighten RAR task"). Pairs with the deterministic suite in
// PRBreakage_Tests.cs. These pin the non-deterministic failures the council
// flagged: pool poisoning, lazy-init race, OOP deadlock, GetInstance TOCTOU.
//
// Parity convention: where applicable, each `_Breakage` test is paired with a
// `_Control` test that exercises the SAME scenario through the legacy/MP
// (multi-process / single-threaded) path. The Control test is expected to
// PASS on the PR head, demonstrating the breakage is specific to the new
// MT/OOP path.
//
// Findings without a meaningful MP parity (C1, NEW-2) operate on PR-introduced
// types (OutOfProcRarClient, OutOfProcRarNodeEndpoint) that have no MP
// equivalent — `-multiprocess` mode does not pool pipes nor run an
// out-of-proc RAR endpoint, so there is no comparable codepath to exercise.
// This is documented inline above each such test.
//
// All tests are timing-bounded (CTS) and use modest concurrency so they run in
// a few seconds total, but they may exhibit flakiness on extremely slow agents.
// Each test documents its statistical confidence in its xmldoc.

namespace Microsoft.Build.UnitTests.ResolveAssemblyReference_Tests
{
    /// <summary>
    /// Concurrency / harness suite that demonstrates the non-deterministic
    /// breakages in PR #13319. Each test uses bounded threads + bounded time
    /// so failures are observable within a single test run on the PR head.
    /// </summary>
    public sealed class PRBreakageConcurrency_Tests
    {
        private readonly ITestOutputHelper _output;

        public PRBreakageConcurrency_Tests(ITestOutputHelper output) => _output = output;

        // ============================================================
        // NEW-1 — Strings.Initialize lazy-init data race.
        // The PR enables concurrent RAR invocations (one per OOP endpoint).
        // Strings.Initialize uses a plain `bool initialized` check/write with
        // NO synchronization, then assigns ~30 static string fields. Thread B
        // can observe initialized==true while one or more dependent fields are
        // still null (publication race). On weak-memory architectures (ARM64)
        // the store reordering is directly observable; on x64 it is rare but
        // a hammered loop with N>16 producers exposes it.
        //
        // Demonstration: reset initialized + null all dependent fields between
        // iterations, race N threads through `new ResolveAssemblyReference()`,
        // and immediately read the dependent fields. Any (initialized==true
        // AND any field==null) sample proves the race exists.
        //
        // Confidence: high on ARM64, medium on x64. Bumping ITERATIONS or
        // THREADS makes it more reliable. Marked as a hammer test in the
        // assertion message so retriers know what they're looking at.
        // ============================================================
        [Fact]
        public void NEW1_StringsInitialize_HasPublicationRace()
        {
            const int Iterations = 200;
            const int Threads = 32;

            Type stringsType = typeof(ResolveAssemblyReference)
                .GetNestedType("Strings", BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("Strings nested type not found.");

            FieldInfo initializedField = stringsType.GetField(
                "initialized", BindingFlags.Static | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("Strings.initialized field not found.");

            // Collect every public static string field on Strings. These are the
            // ones that get assigned inside Initialize after `initialized = true`.
            FieldInfo[] dependentFields = stringsType
                .GetFields(BindingFlags.Public | BindingFlags.Static)
                .Where(f => f.FieldType == typeof(string) && !f.IsLiteral && !f.IsInitOnly)
                .ToArray();

            dependentFields.Length.ShouldBeGreaterThan(20,
                "Sanity: expected ~30 lazily-initialized string fields on Strings.");

            int racesObserved = 0;
            string? sampleMissingField = null;

            for (int i = 0; i < Iterations && racesObserved == 0; i++)
            {
                ResetStringsState(initializedField, dependentFields);

                using Barrier barrier = new(Threads);
                Thread[] workers = new Thread[Threads];

                for (int t = 0; t < Threads; t++)
                {
                    workers[t] = new Thread(() =>
                    {
                        barrier.SignalAndWait();

                        // Triggers Strings.Initialize(Log).
                        _ = new ResolveAssemblyReference();

                        // If we observe initialized==true but any dependent field is still
                        // null, we have caught the publication race in flight.
                        bool isInitialized = (bool)initializedField.GetValue(null)!;
                        if (!isInitialized)
                        {
                            return;
                        }

                        foreach (FieldInfo f in dependentFields)
                        {
                            if (f.GetValue(null) is null)
                            {
                                Interlocked.Increment(ref racesObserved);
                                Interlocked.CompareExchange(ref sampleMissingField, f.Name, null);
                                return;
                            }
                        }
                    });
                }

                foreach (Thread w in workers)
                {
                    w.Start();
                }

                foreach (Thread w in workers)
                {
                    w.Join();
                }
            }

            _output.WriteLine($"NEW-1: races observed = {racesObserved}, sample missing field = {sampleMissingField ?? "(none)"}");

            racesObserved.ShouldBeGreaterThan(0,
                "NEW-1 hammer test: no torn observations of Strings.Initialize after " +
                $"{Iterations} iterations × {Threads} threads. Either the race was masked by " +
                "the JIT/CPU on this run (rerun a few times — especially on ARM64), or a fix " +
                "(LazyInitializer / static ctor / volatile + barrier) has landed.");
        }

        private static void ResetStringsState(FieldInfo initializedField, FieldInfo[] dependentFields)
        {
            foreach (FieldInfo f in dependentFields)
            {
                f.SetValue(null, null);
            }

            // Reset LAST so a parallel reader cannot transiently see initialized==false but
            // fields populated; the bug we're hunting is initialized==true + fields==null.
            initializedField.SetValue(null, false);
        }

        /// <summary>
        /// Parity control for NEW-1. Same Strings.Initialize sequence executed
        /// strictly serially (single thread). This is the `-multiprocess`
        /// equivalent: each Execute() runs in its own process, so static-init
        /// is observed by exactly one thread. No race is possible; all
        /// dependent fields must be non-null after Initialize returns.
        /// </summary>
        [Fact]
        public void NEW1_StringsInitialize_SingleThreaded_Control()
        {
            Type? stringsType = typeof(ResolveAssemblyReference)
                .GetNestedType("Strings", BindingFlags.NonPublic);
            stringsType.ShouldNotBeNull("expected nested 'Strings' type on ResolveAssemblyReference");

            FieldInfo? initializedField = stringsType!.GetField(
                "initialized", BindingFlags.Static | BindingFlags.NonPublic);
            initializedField.ShouldNotBeNull();

            FieldInfo[] dependentFields = stringsType.GetFields(
                BindingFlags.Static | BindingFlags.Public)
                .Where(f => f.FieldType == typeof(string) && !f.IsLiteral && !f.IsInitOnly)
                .ToArray();
            dependentFields.Length.ShouldBeGreaterThan(0);

            // Repeated serial init/reset cycles must always end with all fields populated.
            for (int i = 0; i < 50; i++)
            {
                ResetStringsState(initializedField!, dependentFields);
                _ = new ResolveAssemblyReference();

                ((bool)initializedField!.GetValue(null)!).ShouldBeTrue(
                    "Control: serial Initialize must set the flag.");
                foreach (FieldInfo f in dependentFields)
                {
                    f.GetValue(null).ShouldNotBeNull(
                        $"Control: serial Initialize must populate '{f.Name}' before returning. " +
                        "MP path runs each Execute in its own process; no race possible.");
                }
            }
        }

        // ============================================================
        // C1 / O3 — Broken pipe returned to the client pool on exception.
        // OutOfProcRarClient.Execute wraps acquire/use/release in try/finally.
        // The finally calls ReleaseConnection() unconditionally, even when the
        // pipe write/read just threw (server crash, IO error, malformed frame).
        // Subsequent Executes draw the poisoned pipe and fail too.
        //
        // Demonstration: spin a real endpoint, do one successful Execute (pipe
        // pooled), kill the endpoint, do a second Execute that fails — then
        // assert the pool no longer contains a broken NodePipeClient. Today
        // the count is non-zero with a disconnected client.
        //
        // No MP parity Control: OutOfProcRarClient and its pipe pool are
        // PR-introduced; `-multiprocess` mode resolves assemblies in-proc with
        // no pool, so there is no comparable codepath to exercise.
        // ============================================================
        [Fact]
        public void C1_FailedExecute_DoesNotReturnBrokenPipeToPool()
        {
            MockEngine engine = new(_output) { SetIsOutOfProcRarNodeEnabled = true };

            ResolveAssemblyReference rar = new()
            {
                AllowOutOfProcNode = true,
                BuildEngine = engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(),
                Assemblies = new ITaskItem[] { new TaskItem("System") },
                SearchPaths = new[] { System.IO.Path.GetDirectoryName(typeof(object).Module.FullyQualifiedName)! },
            };

            using OutOfProcRarNodeEndpoint endpoint = new(
                endpointId: 0,
                OutOfProcRarNodeEndpoint.CreateConfig(maxNumberOfServerInstances: 1));
            using CancellationTokenSource cts = new();
            System.Threading.Tasks.Task endpointRun = endpoint.RunAsync(cts.Token);

            // First Execute: pipe gets created and pooled.
            rar.Execute().ShouldBeTrue("first OOP Execute should succeed");

            using OutOfProcRarClient? client = engine.GetRegisteredTaskObject(
                OutOfProcRarClient.TaskObjectCacheKey,
                RegisteredTaskObjectLifetime.Build) as OutOfProcRarClient;
            client.ShouldNotBeNull("RAR client should be registered after the first OOP execute");

            int poolCountBefore = GetPoolCount(client!);
            poolCountBefore.ShouldBe(1, "after a successful Execute the connected pipe is back in the pool");

            // Kill the endpoint so the next Execute will hit a broken pipe.
            cts.Cancel();
            try
            {
                endpointRun.Wait(TimeSpan.FromSeconds(5));
            }
            catch (AggregateException) { /* expected on cancel */ }

            // Second Execute: write/read will throw because the server is gone.
            // Suppress the exception; we only care about pool state afterwards.
            // Bound the second Execute with a hard timeout. The bug we're chasing
            // (broken-pipe poisoning) often manifests as a hang when the protocol
            // has no client-side read timeout, so we MUST guard the test itself.
            System.Threading.Tasks.Task<bool> secondExec = System.Threading.Tasks.Task.Run(() =>
            {
                try { return rar.Execute(); } catch { return false; }
            });
            bool completed = secondExec.Wait(TimeSpan.FromSeconds(10));
            if (!completed)
            {
                _output.WriteLine("C1: second Execute did not complete within 10s (consistent with deadlock).");
            }

            int poolCountAfter = GetPoolCount(client!);

            // BREAKAGE: either (a) the broken pipe sits in the pool poisoning the next
            // caller, or (b) the second Execute deadlocks because there is no read
            // timeout. EITHER outcome falsifies the assertion below.
            (completed && poolCountAfter == 0).ShouldBeTrue(
                $"C1 regression: second Execute completed={completed}, pool count after = {poolCountAfter}. " +
                "Expected (completed=true AND pool count=0): broken pipes must be disposed, not pooled, " +
                "and the protocol must not block forever after a server-side failure.");
        }

        private static int GetPoolCount(OutOfProcRarClient client)
        {
            FieldInfo poolField = typeof(OutOfProcRarClient).GetField(
                "_availablePipeClients", BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("_availablePipeClients field not found.");
            object queue = poolField.GetValue(client)!;
            return (int)queue.GetType().GetProperty("Count")!.GetValue(queue)!;
        }

        // ============================================================
        // NEW-2 — OOP node exception path deadlocks the client.
        // OutOfProcRarNodeEndpoint catches every non-cancellation exception
        // thrown while processing a request, traces it, and continues the
        // accept-loop without sending a response or disconnecting. The
        // client (OutOfProcRarClient.Execute / NodePipeBase.ReadPacket)
        // blocks forever in ReadPacket because the protocol has no timeout.
        //
        // Demonstration: attach a NodePipeClient to a real endpoint, send a
        // deliberately-malformed RarNodeExecuteRequest packet that fails to
        // deserialize on the server, then ReadPacketAsync with a bounded
        // CancellationToken. If the read times out, the deadlock is real.
        //
        // No MP parity Control: the OOP endpoint and pipe protocol are
        // PR-introduced; `-multiprocess` mode invokes RAR.Execute directly and
        // any exception propagates synchronously to the caller — no pipe to
        // deadlock on. There is no comparable codepath to exercise.
        // ============================================================
        [Fact]
        public async System.Threading.Tasks.Task NEW2_OopNodeException_LeavesClientReadBlocked()
        {
            OutOfProcRarNodeEndpoint.SharedConfig config =
                OutOfProcRarNodeEndpoint.CreateConfig(maxNumberOfServerInstances: 1);

            using OutOfProcRarNodeEndpoint endpoint = new(endpointId: 0, config);
            using CancellationTokenSource endpointCts = new();
            System.Threading.Tasks.Task endpointRun = endpoint.RunAsync(endpointCts.Token);

            using NodePipeClient pipeClient = new(config.PipeName, config.Handshake);

            // Register a response factory so ReadPacketAsync knows how to decode any reply.
            NodePacketFactory packetFactory = new();
            packetFactory.RegisterPacketHandler(
                NodePacketType.RarNodeExecuteResponse,
                static t => new RarNodeExecuteResponse(t),
                null);
            packetFactory.RegisterPacketHandler(
                NodePacketType.RarNodeBufferedLogEvents,
                static t => new RarNodeBufferedLogEvents(t),
                null);
            pipeClient.RegisterPacketFactory(packetFactory);

            pipeClient.ConnectToServer(timeout: 5000).ShouldBeTrue("client must connect to the test endpoint");

            // Write a syntactically valid RarNodeExecuteRequest with no inputs / no project
            // dir — the endpoint will instantiate MultiThreadedTaskEnvironmentDriver(null!)
            // which throws inside Path.GetFullPath. The endpoint catches the exception in
            // its top-level try/catch and continues, never sending a response.
            ResolveAssemblyReference clientRar = new()
            {
                BuildEngine = new MockEngine(),
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(),
            };
            // ProjectDirectory will be the process CWD — we then null it out via reflection
            // on the request to provoke the server-side exception.
            RarNodeExecuteRequest request = new(clientRar);
            FieldInfo projDir = typeof(RarNodeExecuteRequest).GetField(
                "_projectDirectory", BindingFlags.Instance | BindingFlags.NonPublic)!;
            projDir.SetValue(request, null);

            pipeClient.WritePacket(request);

            // BREAKAGE: ReadPacketAsync should never complete because the endpoint
            // swallowed the exception silently. Bound the wait so the test terminates.
            using CancellationTokenSource readCts = new(TimeSpan.FromSeconds(3));

            Exception? readException = null;
            INodePacket? receivedPacket = null;
            try
            {
                receivedPacket = await pipeClient.ReadPacketAsync(readCts.Token);
            }
            catch (Exception ex)
            {
                readException = ex;
            }

            // Cleanup endpoint regardless of test outcome.
            endpointCts.Cancel();
            try { endpointRun.Wait(TimeSpan.FromSeconds(5)); } catch { }

            // BREAKAGE: a fix would either send a typed failure response or disconnect the
            // pipe. Either path produces a non-cancellation outcome below.
            readException.ShouldBeOfType<OperationCanceledException>(
                "NEW-2 regression: endpoint swallowed an exception during request processing " +
                "and left the client blocked in ReadPacket forever. Expected either a typed " +
                $"failure response or a pipe disconnect. Received instead: " +
                $"exception={readException?.GetType().Name ?? "<none>"}, packet={receivedPacket?.Type.ToString() ?? "<none>"}.");
        }

        // ============================================================
        // NEW-6 — OutOfProcRarClient.GetInstance TOCTOU.
        // GetInstance reads the registered task object, sees null, constructs
        // a new client, and registers it (last-write-wins on a ConcurrentDict).
        // Two concurrent threads both see null, both construct, both register —
        // the loser's instance is RETURNED to the caller (local variable)
        // without ever being registered, so it never gets disposed at end-of-
        // build. Each leak holds a Queue<NodePipeClient> with named-pipe
        // handles; on a long-lived MSBuild server this leaks over time.
        //
        // Demonstration: race N threads through GetInstance with a single
        // engine. After the race, count distinct returned instances; only one
        // is registered. Leak count = distinct_returned − 1 must be 0.
        // ============================================================
        [Fact]
        public void NEW6_GetInstance_TOCTOU_LeaksUnregisteredClients()
        {
            const int Threads = 32;

            MockEngine engine = new(_output) { SetIsOutOfProcRarNodeEnabled = true };

            // We don't have direct access to OutOfProcRarClient.GetInstance from outside
            // (internal), but Microsoft.Build.Tasks.UnitTests has InternalsVisibleTo, so
            // the call is direct.
            using Barrier barrier = new(Threads);
            OutOfProcRarClient[] returned = new OutOfProcRarClient[Threads];
            Thread[] workers = new Thread[Threads];

            for (int t = 0; t < Threads; t++)
            {
                int captured = t;
                workers[t] = new Thread(() =>
                {
                    barrier.SignalAndWait();
                    returned[captured] = OutOfProcRarClient.GetInstance(engine);
                });
            }

            foreach (Thread w in workers)
            {
                w.Start();
            }

            foreach (Thread w in workers)
            {
                w.Join();
            }

            // The instance the engine retains (for build-end disposal):
            OutOfProcRarClient registered = (OutOfProcRarClient)engine.GetRegisteredTaskObject(
                OutOfProcRarClient.TaskObjectCacheKey,
                RegisteredTaskObjectLifetime.Build);
            registered.ShouldNotBeNull();

            int distinct = returned.Where(c => c is not null).Distinct().Count();
            int leaks = distinct - 1;

            _output.WriteLine($"NEW-6: distinct instances returned = {distinct}, registered = 1, leaks = {leaks}");

            // Cleanup: dispose what we can; the leaked clients are not reachable through
            // the engine and would normally just be GC'd in test context. In production
            // they hold pipe handles for the lifetime of the MSBuild server.
            registered.Dispose();

            // BREAKAGE: any leak count > 0 means GetInstance constructed more than one
            // client and only registered the last one.
            leaks.ShouldBe(0,
                $"NEW-6 regression: {Threads} concurrent GetInstance calls produced " +
                $"{distinct} distinct OutOfProcRarClient instances ({leaks} leaked). " +
                "Fix: get-or-add atomically (e.g. ConcurrentDictionary.GetOrAdd or a lock " +
                "around the get/register pair, with a re-read after add).");
        }

        /// <summary>
        /// Parity control for NEW-6. Same GetInstance call sequence executed
        /// strictly serially. This is the `-multiprocess` / single-RAR-call
        /// equivalent: one caller per build, no race. All N calls must return
        /// the SAME instance (the one registered on first call).
        /// </summary>
        [Fact]
        public void NEW6_GetInstance_SingleThreaded_Control()
        {
            const int Calls = 32;

            MockEngine engine = new(_output) { SetIsOutOfProcRarNodeEnabled = true };

            OutOfProcRarClient[] returned = new OutOfProcRarClient[Calls];
            for (int i = 0; i < Calls; i++)
            {
                returned[i] = OutOfProcRarClient.GetInstance(engine);
            }

            int distinct = returned.Where(c => c is not null).Distinct().Count();
            distinct.ShouldBe(1,
                "Control: serial GetInstance calls must return the single registered " +
                $"OutOfProcRarClient instance (got {distinct} distinct). MP / single-caller " +
                "path is by definition race-free.");

            OutOfProcRarClient registered = (OutOfProcRarClient)engine.GetRegisteredTaskObject(
                OutOfProcRarClient.TaskObjectCacheKey,
                RegisteredTaskObjectLifetime.Build);
            registered.ShouldBeSameAs(returned[0],
                "Control: the registered instance must be the one returned to all callers.");

            registered.Dispose();
        }
    }
}
