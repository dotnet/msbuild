// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
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

        [Fact]
        public void TaskIdentityAppliesOnlyToTheNextTask()
        {
            using TestEnvironment env = TestEnvironment.Create(_output);
            RedirectedNodeState state = env.WithTransientTestState(new RedirectedNodeState(_ => { }, _ => { }));
            state.SetField("_parentPacketVersion", NodePacketTypeExtensions.PacketVersion);
            var configuration = (TaskHostConfiguration)Activator.CreateInstance(typeof(TaskHostConfiguration), nonPublic: true)!;

            state.HandlePacket(new TaskHostTaskIdentity(5, yieldEnabled: true));
            using TaskExecutionContext identified = state.CreateTaskContext(configuration);
            using TaskExecutionContext legacy = state.CreateTaskContext(configuration);
            state.HandlePacket(new TaskHostTaskIdentity(6, yieldEnabled: false));
            using TaskExecutionContext noYield = state.CreateTaskContext(configuration);

            identified.ParentTaskId.ShouldBe(5);
            identified.YieldEnabled.ShouldBeTrue();
            legacy.ParentTaskId.ShouldBe(0, "A configuration without an identity uses legacy routing.");
            legacy.YieldEnabled.ShouldBeFalse();
            noYield.ParentTaskId.ShouldBe(6);
            noYield.YieldEnabled.ShouldBeFalse("A worker that cannot yield (no IBuildEngine3) keeps Yield a no-op.");
        }

        [Fact]
        public void YieldIsANoOpWithoutCapableParent()
        {
            using TestEnvironment env = TestEnvironment.Create(_output);
            RedirectedNodeState state = env.WithTransientTestState(new RedirectedNodeState(_ => { }, _ => { }));
            using TaskExecutionContext context = state.AddContext(parentTaskId: 0, yieldEnabled: false);

            // No endpoint exists, so any callback would fail. Legacy sequences are not validated.
            state.RunAs(context, () =>
            {
                state.Node.Yield();
                state.Node.Yield();
                state.Node.Reacquire();
                state.Node.Reacquire();
            });
            context.State.ShouldBe(TaskExecutionState.Executing);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void YieldReleasesTheProcessAndReacquireWaitsForTheWorker(bool disconnect)
        {
            using TestEnvironment env = TestEnvironment.Create(_output);
            RecordingEndpoint endpoint = env.WithTransientTestState(new RecordingEndpoint());
            RedirectedNodeState state = env.WithTransientTestState(new RedirectedNodeState(_ => { }, _ => { }));
            state.SetField("_nodeEndpoint", endpoint.Node);
            state.SetField("_parentPacketVersion", NodePacketTypeExtensions.PacketVersion);
            state.SetField("_activeTaskCount", 1);
            using TaskExecutionContext context = state.AddContext(parentTaskId: 7, yieldEnabled: true);
            ImmediateDisconnectEndpoint failureEndpoint = new();
            state.SubscribeToLinkStatus(failureEndpoint);
            System.Threading.Tasks.Task execution = System.Threading.Tasks.Task.Run(() => state.RunAs(context, () =>
            {
                state.Node.Yield();
                state.Node.Reacquire();
            }));

            try
            {
                TaskHostYieldRequest yield = NextRequest();
                yield.Reacquire.ShouldBeFalse();
                execution.IsCompleted.ShouldBeFalse("Yield must wait for the worker's acknowledgment.");
                context.State.ShouldBe(TaskExecutionState.Yielded, "The process is released before the worker can schedule another task here.");
                state.GetField<int>("_activeTaskCount").ShouldBe(0);
                state.GetField<int>("_yieldedTaskCount").ShouldBe(1);
                state.HandlePacket(new TaskHostYieldResponse(yield.RequestId));

                TaskHostYieldRequest reacquire = NextRequest();
                reacquire.Reacquire.ShouldBeTrue();
                execution.IsCompleted.ShouldBeFalse("Reacquire must not return before the worker reacquires its node.");
                if (disconnect)
                {
                    failureEndpoint.FailConnection();
                }
                else
                {
                    state.HandlePacket(new TaskHostYieldResponse(reacquire.RequestId, new Microsoft.Build.Exceptions.BuildAbortedException()));
                }

                SpinWait.SpinUntil(() => execution.IsCompleted, TimeSpan.FromSeconds(10)).ShouldBeTrue();
                if (disconnect)
                {
                    Should.Throw<InvalidOperationException>(() => execution.GetAwaiter().GetResult());
                }
                else
                {
                    Should.Throw<Microsoft.Build.Exceptions.BuildAbortedException>(() => execution.GetAwaiter().GetResult());
                }

                // The task did not get the process back; it completes as a yielded task.
                context.State.ShouldBe(TaskExecutionState.Yielded);
                state.GetField<int>("_activeTaskCount").ShouldBe(0);
                context.PendingCallbackRequests.ShouldBeEmpty();
            }
            finally
            {
                failureEndpoint.FailConnection();
                SpinWait.SpinUntil(() => execution.IsCompleted, TimeSpan.FromSeconds(10)).ShouldBeTrue();
            }

            TaskHostYieldRequest NextRequest()
            {
                SpinWait.SpinUntil(() => !endpoint.Packets.IsEmpty, TimeSpan.FromSeconds(10)).ShouldBeTrue();
                endpoint.Packets.TryDequeue(out INodePacket? packet).ShouldBeTrue();
                TaskHostTaskEnvelope envelope = packet.ShouldBeOfType<TaskHostTaskEnvelope>();
                envelope.TaskId.ShouldBe(7, "Requests from a shared TaskHost must identify their task.");
                return envelope.Packet.ShouldBeOfType<TaskHostYieldRequest>();
            }
        }

        [Fact]
        public void SuccessfulReacquireRestoresTheTasksEnvironment()
        {
            using TestEnvironment env = TestEnvironment.Create(_output);
            RecordingEndpoint endpoint = env.WithTransientTestState(new RecordingEndpoint());
            RedirectedNodeState state = env.WithTransientTestState(new RedirectedNodeState(_ => { }, _ => { }));
            state.SetField("_nodeEndpoint", endpoint.Node);
            state.SetField("_parentPacketVersion", NodePacketTypeExtensions.PacketVersion);
            state.SetField("_activeTaskCount", 1);
            const string variable = "MSBUILD_TASKHOST_YIELD_UNIT_TEST";
            env.SetEnvironmentVariable(variable, "first");
            using TaskExecutionContext context = state.AddContext(parentTaskId: 3, yieldEnabled: true);
            System.Threading.Tasks.Task execution = System.Threading.Tasks.Task.Run(() => state.RunAs(context, () =>
            {
                state.Node.Yield();
                state.Node.Reacquire();
                Environment.GetEnvironmentVariable(variable).ShouldBe("first");
            }));

            Respond();
            // Another task that ran here while the first was yielded changed the process environment.
            Environment.SetEnvironmentVariable(variable, "second");
            Respond();
            execution.Wait(TimeSpan.FromSeconds(10)).ShouldBeTrue();
            context.State.ShouldBe(TaskExecutionState.Executing);
            state.GetField<int>("_activeTaskCount").ShouldBe(1);
            state.GetField<int>("_yieldedTaskCount").ShouldBe(0);

            void Respond()
            {
                SpinWait.SpinUntil(() => !endpoint.Packets.IsEmpty, TimeSpan.FromSeconds(10)).ShouldBeTrue();
                endpoint.Packets.TryDequeue(out INodePacket? packet).ShouldBeTrue();
                var request = (TaskHostYieldRequest)packet.ShouldBeOfType<TaskHostTaskEnvelope>().Packet;
                state.HandlePacket(new TaskHostYieldResponse(request.RequestId));
            }
        }

        [Theory]
        [InlineData("double-yield")]
        [InlineData("reacquire-first")]
        [InlineData("wrong-thread")]
        public void InvalidYieldSequencesFailLikeInProcTasks(string operation)
        {
            using TestEnvironment env = TestEnvironment.Create(_output);
            RecordingEndpoint endpoint = env.WithTransientTestState(new RecordingEndpoint());
            RedirectedNodeState state = env.WithTransientTestState(new RedirectedNodeState(_ => { }, _ => { }));
            state.SetField("_nodeEndpoint", endpoint.Node);
            state.SetField("_parentPacketVersion", NodePacketTypeExtensions.PacketVersion);
            state.SetField("_activeTaskCount", 1);
            using TaskExecutionContext context = state.AddContext(parentTaskId: 4, yieldEnabled: true);
            if (operation == "reacquire-first")
            {
                Should.Throw<InternalErrorException>(() => state.RunAs(context, state.Node.Reacquire));
                endpoint.Packets.ShouldBeEmpty();
                return;
            }

            System.Threading.Tasks.Task yield = System.Threading.Tasks.Task.Run(() => state.RunAs(context, state.Node.Yield));
            SpinWait.SpinUntil(() => !endpoint.Packets.IsEmpty, TimeSpan.FromSeconds(10)).ShouldBeTrue();
            endpoint.Packets.TryDequeue(out INodePacket? packet).ShouldBeTrue();
            state.HandlePacket(new TaskHostYieldResponse(((TaskHostTaskEnvelope)packet!).Packet.ShouldBeOfType<TaskHostYieldRequest>().RequestId));
            yield.Wait(TimeSpan.FromSeconds(10)).ShouldBeTrue();

            Action invalid = operation switch
            {
                "double-yield" => state.Node.Yield,
                _ => state.Node.Reacquire,
            };
            Should.Throw<InternalErrorException>(() => state.RunAs(context, invalid));
            endpoint.Packets.ShouldBeEmpty("An invalid call must not reach the worker node.");
            context.State.ShouldBe(TaskExecutionState.Yielded);
        }

        [Fact]
        public void BuildProjectFileWhileYieldedReacquiresForTheNestedBuildAndYieldsAgain()
        {
            using TestEnvironment env = TestEnvironment.Create(_output);
            RecordingEndpoint endpoint = env.WithTransientTestState(new RecordingEndpoint());
            RedirectedNodeState state = env.WithTransientTestState(new RedirectedNodeState(_ => { }, _ => { }));
            state.SetField("_nodeEndpoint", endpoint.Node);
            state.SetField("_parentPacketVersion", NodePacketTypeExtensions.PacketVersion);
            state.SetField("_activeTaskCount", 1);
            using TaskExecutionContext context = state.AddContext(parentTaskId: 5, yieldEnabled: true);
            bool built = false;
            System.Threading.Tasks.Task execution = System.Threading.Tasks.Task.Run(() => state.RunAs(context, () =>
            {
                state.Node.Yield();
                built = state.Node.BuildProjectFile("other.proj", null, null, null);
            }));

            Next<TaskHostYieldRequest>(out int yieldId).Reacquire.ShouldBeFalse();
            state.HandlePacket(new TaskHostYieldResponse(yieldId));
            TaskHostYieldRequest reacquire = Next<TaskHostYieldRequest>(out int reacquireId);
            reacquire.Reacquire.ShouldBeTrue("The nested build needs the node, as it did when Yield was a no-op.");
            endpoint.Packets.ShouldBeEmpty("The build request must wait for the node.");
            state.HandlePacket(new TaskHostYieldResponse(reacquireId));

            Next<TaskHostBuildRequest>(out int buildId);
            context.State.ShouldBe(TaskExecutionState.BlockedOnCallback);
            state.HandlePacket(new TaskHostBuildResponse(buildId, true, null));

            Next<TaskHostYieldRequest>(out int yieldAgainId).Reacquire.ShouldBeFalse("The task resumes yielded, as it called it.");
            state.HandlePacket(new TaskHostYieldResponse(yieldAgainId));
            execution.Wait(TimeSpan.FromSeconds(10)).ShouldBeTrue();
            built.ShouldBeTrue();
            context.State.ShouldBe(TaskExecutionState.Yielded);
            state.GetField<int>("_activeTaskCount").ShouldBe(0);
            state.GetField<int>("_blockedTaskCount").ShouldBe(0);
            state.GetField<int>("_yieldedTaskCount").ShouldBe(1);

            T Next<T>(out int requestId)
                where T : INodePacket
            {
                SpinWait.SpinUntil(() => !endpoint.Packets.IsEmpty || execution.IsCompleted, TimeSpan.FromSeconds(10)).ShouldBeTrue();
                execution.Exception.ShouldBeNull();
                endpoint.Packets.TryDequeue(out INodePacket? packet).ShouldBeTrue();
                TaskHostTaskEnvelope envelope = packet.ShouldBeOfType<TaskHostTaskEnvelope>();
                envelope.TaskId.ShouldBe(5);
                T request = envelope.Packet.ShouldBeOfType<T>();
                requestId = ((ITaskHostCallbackPacket)request).RequestId;
                return request;
            }
        }

        [Fact]
        public void CancellationByTaskIdCancelsOnlyThatTask()
        {
            using TestEnvironment env = TestEnvironment.Create(_output);
            RedirectedNodeState state = env.WithTransientTestState(new RedirectedNodeState(_ => { }, _ => { }));
            using TaskExecutionContext yielded = state.AddContext(parentTaskId: 1, yieldEnabled: true);
            yielded.State = TaskExecutionState.Yielded;
            using TaskExecutionContext active = state.AddContext(parentTaskId: 2, yieldEnabled: true);

            state.HandlePacket(new TaskHostTaskCancel(2));
            state.HandlePacket(new TaskHostTaskCancel(99));

            active.CancelRequested.ShouldBeTrue();
            yielded.CancelRequested.ShouldBeFalse("A task that shares the process must keep running.");
            state.GetField<ManualResetEvent>("_taskCancelledEvent").WaitOne(0).ShouldBeFalse("Per-task cancellation must not cancel every task.");
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void CompletionDrainPreservesResultsAndFlushesConcurrentConsoleOutput(bool enqueueDuringDrain)
        {
            using TestEnvironment env = TestEnvironment.Create(_output);
            RecordingEndpoint endpoint = env.WithTransientTestState(new RecordingEndpoint());
            Action? afterErrorFlush = null;
            RedirectedNodeState state = env.WithTransientTestState(new RedirectedNodeState(
                text => endpoint.Node.SendData(new ConsoleWritePacket(text, ConsoleOutput.Standard)),
                text =>
                {
                    endpoint.Node.SendData(new ConsoleWritePacket(text, ConsoleOutput.Error));
                    afterErrorFlush?.Invoke();
                }));
            state.SetField("_nodeEndpoint", endpoint.Node);
            TaskHostTaskComplete first = CreateCompletion(TaskCompleteType.Success);
            TaskHostTaskComplete second = CreateCompletion(TaskCompleteType.Failure);
            state.PendingCompletions.Enqueue(first);
            state.OutWriter.Write("A output");
            state.ErrorWriter.Write("A error");

            if (enqueueDuringDrain)
            {
                afterErrorFlush = () =>
                {
                    afterErrorFlush = null;
                    // Interleave another producer after stdout was flushed for the first completion.
                    state.OutWriter.Write("B output");
                    state.PendingCompletions.Enqueue(second);
                };
            }
            else
            {
                state.PendingCompletions.Enqueue(second);
            }

            state.CompleteTask();

            INodePacket[] packets = endpoint.Packets.ToArray();
            packets.Length.ShouldBe(enqueueDuringDrain ? 5 : 4);
            packets[0].ShouldBeOfType<ConsoleWritePacket>().Text.ShouldBe("A output");
            packets[1].ShouldBeOfType<ConsoleWritePacket>().Text.ShouldBe("A error");
            packets[2].ShouldBeSameAs(first);
            if (enqueueDuringDrain)
            {
                packets[3].ShouldBeOfType<ConsoleWritePacket>().Text.ShouldBe("B output");
            }
            packets[^1].ShouldBeSameAs(second);
            packets.OfType<TaskHostTaskComplete>().Select(packet => packet.TaskResult).ToArray()
                .ShouldBe([TaskCompleteType.Success, TaskCompleteType.Failure]);
            state.PendingCompletions.ShouldBeEmpty();
            state.CompleteTask();
            endpoint.Packets.Count.ShouldBe(packets.Length, "a second drain must not duplicate either completion");
        }

        private static TaskHostTaskComplete CreateCompletion(TaskCompleteType result) =>
            new(new OutOfProcTaskHostTaskResult(result),
#if FEATURE_REPORTFILEACCESSES
                null,
#endif
                null);

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

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void ShutdownAcknowledgmentPreservesReuseUnlessConnectionAlreadyFailed(bool connectionAlreadyFailed)
        {
            using TestEnvironment env = TestEnvironment.Create(_output);
            RedirectedNodeState state = env.WithTransientTestState(new RedirectedNodeState(_ => { }, _ => { }));
            state.ShutdownReason = NodeEngineShutdownReason.BuildCompleteReuse;
            ImmediateDisconnectEndpoint endpoint = new();
            state.SubscribeToLinkStatus(endpoint);
            if (connectionAlreadyFailed)
            {
                endpoint.FailConnection();
            }

            state.Node.SendShutdownNotification(endpoint);

            state.ShutdownReason.ShouldBe(connectionAlreadyFailed
                ? NodeEngineShutdownReason.ConnectionFailed
                : NodeEngineShutdownReason.BuildCompleteReuse);
            endpoint.ClientWillDisconnectCalled.ShouldBe(!connectionAlreadyFailed);
            if (connectionAlreadyFailed)
            {
                endpoint.ShutdownPacket.ShouldBeNull();
            }
            else
            {
                endpoint.ShutdownPacket.ShouldNotBeNull();
                endpoint.ShutdownPacket.Reason.ShouldBe(NodeShutdownReason.Requested);
                endpoint.LinkStatus.ShouldBe(LinkStatus.Failed);
            }
        }

        private sealed class ImmediateDisconnectEndpoint : INodeEndpoint
        {
            public event LinkStatusChangedDelegate? OnLinkStatusChanged;

            public LinkStatus LinkStatus { get; private set; } = LinkStatus.Active;
            internal bool ClientWillDisconnectCalled { get; private set; }
            internal NodeShutdown? ShutdownPacket { get; private set; }

            public void ClientWillDisconnect() => ClientWillDisconnectCalled = true;
            public void Connect(INodePacketFactory factory) => throw new NotSupportedException();
            public void Listen(INodePacketFactory factory) => throw new NotSupportedException();
            public void Disconnect() => throw new NotSupportedException();

            public void SendData(INodePacket packet)
            {
                ShutdownPacket = packet.ShouldBeOfType<NodeShutdown>();
                FailConnection();
            }

            internal void FailConnection()
            {
                LinkStatus = LinkStatus.Failed;
                OnLinkStatusChanged?.Invoke(this, LinkStatus);
            }
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
            internal NodeEngineShutdownReason ShutdownReason
            {
                get => (NodeEngineShutdownReason)typeof(OutOfProcTaskHostNode).GetField("_shutdownReason", InstanceMembers)!.GetValue(Node)!;
                set => SetField("_shutdownReason", value);
            }

            internal ConcurrentQueue<INodePacket> PendingCompletions =>
                (ConcurrentQueue<INodePacket>)typeof(OutOfProcTaskHostNode).GetField("_taskCompletePackets", InstanceMembers)!.GetValue(Node)!;

            internal void HandlePacket(INodePacket packet) => ((Action<INodePacket>)typeof(OutOfProcTaskHostNode)
                .GetMethod("HandlePacket", InstanceMembers)!.CreateDelegate(typeof(Action<INodePacket>), Node))(packet);

            internal TaskExecutionContext CreateTaskContext(TaskHostConfiguration configuration) => ((Func<TaskHostConfiguration, TaskExecutionContext>)typeof(OutOfProcTaskHostNode)
                .GetMethod("CreateTaskContext", InstanceMembers)!.CreateDelegate(typeof(Func<TaskHostConfiguration, TaskExecutionContext>), Node))(configuration);

            internal T GetField<T>(string name) => (T)typeof(OutOfProcTaskHostNode).GetField(name, InstanceMembers)!.GetValue(Node)!;

            /// <summary>
            /// Adds an executing task context, as if the node had received its configuration.
            /// </summary>
            internal TaskExecutionContext AddContext(int parentTaskId, bool yieldEnabled)
            {
                TaskExecutionContext context = CreateTaskContext(
                    (TaskHostConfiguration)Activator.CreateInstance(typeof(TaskHostConfiguration), nonPublic: true)!);
                context.ParentTaskId = parentTaskId;
                context.YieldEnabled = yieldEnabled;
                context.State = TaskExecutionState.Executing;
                return context;
            }

            /// <summary>
            /// Runs <paramref name="action"/> as the task of <paramref name="context"/> on the current thread.
            /// </summary>
            internal void RunAs(TaskExecutionContext context, Action action)
            {
                var current = GetField<AsyncLocal<TaskExecutionContext>>("_currentTaskContext");
                TaskExecutionContext? previous = current.Value;
                current.Value = context;
                try
                {
                    action();
                }
                finally
                {
                    current.Value = previous!;
                }
            }

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

            internal void CompleteTask() => ((Action)typeof(OutOfProcTaskHostNode)
                .GetMethod("CompleteTask", InstanceMembers)!.CreateDelegate(typeof(Action), Node))();

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

            internal void SubscribeToLinkStatus(INodeEndpoint endpoint) =>
                endpoint.OnLinkStatusChanged += (LinkStatusChangedDelegate)typeof(OutOfProcTaskHostNode)
                    .GetMethod("OnLinkStatusChanged", InstanceMembers)!.CreateDelegate(typeof(LinkStatusChangedDelegate), Node);

            private static void StopTimer(RedirectConsoleWriter writer)
            {
                using ManualResetEvent disposed = new(false);
                Timer timer = (Timer)typeof(RedirectConsoleWriter).GetField("_timer", InstanceMembers)!.GetValue(writer)!;
                timer.Dispose(disposed).ShouldBeTrue();
                disposed.WaitOne(10_000).ShouldBeTrue();
            }
        }

        private sealed class RecordingEndpoint : TransientTestState
        {
            private const BindingFlags InstanceMembers = BindingFlags.Instance | BindingFlags.NonPublic;
            private readonly AutoResetEvent _packetAvailable = new(false);

            internal NodeEndpointOutOfProcTaskHost Node { get; } = new(false, NodePacketTypeExtensions.PacketVersion);
            internal ConcurrentQueue<INodePacket> Packets { get; } = new();

            internal RecordingEndpoint()
            {
                typeof(NodeEndpointOutOfProcBase).GetField("_status", InstanceMembers)!.SetValue(Node, LinkStatus.Active);
                typeof(NodeEndpointOutOfProcBase).GetField("_packetQueue", InstanceMembers)!.SetValue(Node, Packets);
                typeof(NodeEndpointOutOfProcBase).GetField("_packetAvailable", InstanceMembers)!.SetValue(Node, _packetAvailable);
            }

            public override void Revert()
            {
                typeof(NodeEndpointOutOfProcBase).GetField("_status", InstanceMembers)!.SetValue(Node, LinkStatus.Inactive);
                _packetAvailable.Dispose();
                ((IDisposable)typeof(NodeEndpointOutOfProcBase).GetField("_pipeServer", InstanceMembers)!.GetValue(Node)!).Dispose();
                ((IDisposable)typeof(NodeEndpointOutOfProcBase).GetField("_binaryWriter", InstanceMembers)!.GetValue(Node)!).Dispose();
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
