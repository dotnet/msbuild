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

        [Theory]
        [InlineData(false, false)]
        [InlineData(true, false)]
        [InlineData(true, true)]
        public void EventsLoggedAfterTaskCompletesAreDropped(bool taskCompleted, bool otherTaskActive)
        {
            using TestEnvironment env = TestEnvironment.Create(_output);
            RecordingEndpoint endpoint = env.WithTransientTestState(new RecordingEndpoint());
            RedirectedNodeState nodeState = env.WithTransientTestState(new RedirectedNodeState(_ => { }, _ => { }));
            nodeState.SetField("_nodeEndpoint", endpoint.Node);
            using TaskExecutionContext context = CreateTaskContext(1, taskCompleted ? TaskExecutionState.Completed : TaskExecutionState.Executing, nodeState.Node);
            using TaskExecutionContext otherContext = CreateTaskContext(2, TaskExecutionState.Executing, nodeState.Node);
            if (otherTaskActive)
            {
                ((ConcurrentDictionary<int, TaskExecutionContext>)typeof(OutOfProcTaskHostNode)
                    .GetField("_taskContexts", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .GetValue(nodeState.Node)!).TryAdd(otherContext.TaskId, otherContext).ShouldBeTrue();
            }

            // Log from a separate thread holding the first task's IBuildEngine proxy, as a background
            // thread started by the task would. Even when another task is active on the reused TaskHost,
            // events from the completed task must be dropped rather than attributed to the active task.
            Thread logger = new(() =>
            {
                ((AsyncLocal<TaskExecutionContext>)typeof(OutOfProcTaskHostNode)
                    .GetField("_currentTaskContext", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .GetValue(nodeState.Node)!).Value = context;
                context.LogMessageEvent(new BuildMessageEventArgs("late message", null, "LateLogger", MessageImportance.Low));
                nodeState.Node.LogErrorEvent(new BuildErrorEventArgs(null, "LATE0001", "project.proj", 1, 1, 0, 0, "late error", null, "LateLogger"));
            });
            logger.Start();
            logger.Join();

            endpoint.Packets.Count.ShouldBe(taskCompleted ? 0 : 2);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void TaskLoggingHelperAfterTaskCompletesDoesNotThrowOrLeak(bool suppressExecutionContextFlow)
        {
            using TestEnvironment env = TestEnvironment.Create(_output);
            RecordingEndpoint endpoint = env.WithTransientTestState(new RecordingEndpoint());
            RedirectedNodeState nodeState = env.WithTransientTestState(new RedirectedNodeState(_ => { }, _ => { }));
            nodeState.SetField("_nodeEndpoint", endpoint.Node);

            using TaskExecutionContext completedContext = CreateTaskContext(
                1,
                TaskExecutionState.Executing,
                nodeState.Node,
                projectFileOfTask: "task1.proj",
                lineNumberOfTask: 17,
                warningsAsErrors: new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase) { "WARN0001" });
            using TaskExecutionContext activeContext = CreateTaskContext(
                2,
                TaskExecutionState.Executing,
                nodeState.Node,
                projectFileOfTask: "task2.proj",
                lineNumberOfTask: 99);

            ((ConcurrentDictionary<int, TaskExecutionContext>)typeof(OutOfProcTaskHostNode)
                .GetField("_taskContexts", BindingFlags.Instance | BindingFlags.NonPublic)!
                .GetValue(nodeState.Node)!).TryAdd(activeContext.TaskId, activeContext).ShouldBeTrue();

            Microsoft.Build.Utilities.TaskLoggingHelper log = new(completedContext, "LateTask");
            completedContext.MarkAsInactive();
            completedContext.ReleaseTaskReferences();

            Exception? caught = null;
            using ManualResetEvent finished = new(false);

            AsyncFlowControl? flowControl = suppressExecutionContextFlow ? ExecutionContext.SuppressFlow() : null;
            try
            {
                ThreadPool.QueueUserWorkItem(_ =>
                {
                    try
                    {
                        log.LogMessage(MessageImportance.High, "late message");
                        log.LogWarning(subcategory: null, warningCode: "WARN0001", helpKeyword: null, file: null, lineNumber: 0, columnNumber: 0, endLineNumber: 0, endColumnNumber: 0, message: "late warning");
                        log.LogError("late error");
                        _ = ((IBuildEngine10)completedContext).EngineServices.IsTaskInputLoggingEnabled;
                    }
                    catch (Exception ex)
                    {
                        caught = ex;
                    }
                    finally
                    {
                        finished.Set();
                    }
                });
            }
            finally
            {
                flowControl?.Undo();
            }

            finished.WaitOne(10_000).ShouldBeTrue();
            caught.ShouldBeNull();
            endpoint.Packets.ShouldBeEmpty();
        }

        [Fact]
        public void SharedWorkerThreadCreatedByCompletedTaskLogsWithCurrentTaskProxy()
        {
            using TestEnvironment env = TestEnvironment.Create(_output);
            RecordingEndpoint endpoint = env.WithTransientTestState(new RecordingEndpoint());
            RedirectedNodeState nodeState = env.WithTransientTestState(new RedirectedNodeState(_ => { }, _ => { }));
            nodeState.SetField("_nodeEndpoint", endpoint.Node);

            using TaskExecutionContext context1 = CreateTaskContext(
                1,
                TaskExecutionState.Executing,
                nodeState.Node,
                projectFileOfTask: "task1.proj",
                lineNumberOfTask: 10);
            using TaskExecutionContext context2 = CreateTaskContext(
                2,
                TaskExecutionState.Executing,
                nodeState.Node,
                projectFileOfTask: "task2.proj",
                lineNumberOfTask: 42,
                warningsAsErrors: new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase) { "MSB9999" });

            Microsoft.Build.Utilities.TaskLoggingHelper task1Log = new(context1, "Task1");
            Microsoft.Build.Utilities.TaskLoggingHelper task2Log = new(context2, "Task2");

            using AutoResetEvent workerReady = new(false);
            using AutoResetEvent runWork = new(false);
            Exception? workerException = null;

            // Start a reusable worker thread while Task 1's AsyncLocal context is current, then complete Task 1.
            ((AsyncLocal<TaskExecutionContext>)typeof(OutOfProcTaskHostNode)
                .GetField("_currentTaskContext", BindingFlags.Instance | BindingFlags.NonPublic)!
                .GetValue(nodeState.Node)!).Value = context1;

            Thread sharedWorker = new(() =>
            {
                workerReady.Set();
                runWork.WaitOne();
                try
                {
                    // Stale logging on Task 1's proxy must be dropped.
                    task1Log.LogError("stale error from task 1");

                    // Current logging via Task 2's proxy must succeed and use Task 2's location and warning promotion.
                    task2Log.LogWarning(subcategory: null, warningCode: "MSB9999", helpKeyword: null, file: null, lineNumber: 0, columnNumber: 0, endLineNumber: 0, endColumnNumber: 0, message: "promoted warning from task 2");
                }
                catch (Exception ex)
                {
                    workerException = ex;
                }
            });
            sharedWorker.Start();
            workerReady.WaitOne(10_000).ShouldBeTrue();

            ((AsyncLocal<TaskExecutionContext>)typeof(OutOfProcTaskHostNode)
                .GetField("_currentTaskContext", BindingFlags.Instance | BindingFlags.NonPublic)!
                .GetValue(nodeState.Node)!).Value = null!;
            context1.MarkAsInactive();
            context1.ReleaseTaskReferences();

            runWork.Set();
            sharedWorker.Join(10_000).ShouldBeTrue();
            workerException.ShouldBeNull();

            INodePacket singlePacket = endpoint.Packets.ShouldHaveSingleItem();
            LogMessagePacketBase logPacket = singlePacket.ShouldBeAssignableTo<LogMessagePacketBase>()!;
            BuildErrorEventArgs promotedError = logPacket.NodeBuildEvent!.Value.Value.ShouldBeOfType<BuildErrorEventArgs>();
            promotedError.Code.ShouldBe("MSB9999");
            promotedError.File.ShouldBe("task2.proj");
            promotedError.LineNumber.ShouldBe(42);
        }

        [Fact]
        public void CallbacksOnCompletedTaskContextThrowWithoutCorruptingNodeCounts()
        {
            using TestEnvironment env = TestEnvironment.Create(_output);
            RecordingEndpoint endpoint = env.WithTransientTestState(new RecordingEndpoint());
            RedirectedNodeState nodeState = env.WithTransientTestState(new RedirectedNodeState(_ => { }, _ => { }));
            nodeState.SetField("_nodeEndpoint", endpoint.Node);

            using TaskExecutionContext context = CreateTaskContext(1, TaskExecutionState.Executing, nodeState.Node);
            context.MarkAsInactive();
            context.ReleaseTaskReferences();

            IBuildEngine9 engine = context;
            Should.Throw<InternalErrorException>(() => _ = engine.ContinueOnError);
            Should.Throw<InternalErrorException>(() => _ = engine.IsRunningMultipleNodes);
            Should.Throw<InternalErrorException>(() => engine.BuildProjectFile("project.proj", ["Build"], null, null));
            Should.Throw<InternalErrorException>(() => engine.RequestCores(1));
            Should.Throw<InternalErrorException>(() => engine.ReleaseCores(1));
            Should.Throw<InternalErrorException>(() => ((IBuildEngine4)context).RegisterTaskObject("key", new object(), RegisteredTaskObjectLifetime.Build, allowEarlyCollection: false));

            typeof(OutOfProcTaskHostNode).GetField("_activeTaskCount", BindingFlags.Instance | BindingFlags.NonPublic)!
                .GetValue(nodeState.Node).ShouldBe(0);
            typeof(OutOfProcTaskHostNode).GetField("_blockedTaskCount", BindingFlags.Instance | BindingFlags.NonPublic)!
                .GetValue(nodeState.Node).ShouldBe(0);
            endpoint.Packets.ShouldBeEmpty();
        }

        [Fact]
        public void AbandonedInFlightBuildCallbackOnTaskCompletion_PreservesNodeCountsAndDropsLateResponse()
        {
            using TestEnvironment env = TestEnvironment.Create(_output);
            RecordingEndpoint endpoint = env.WithTransientTestState(new RecordingEndpoint());
            RedirectedNodeState nodeState = env.WithTransientTestState(new RedirectedNodeState(_ => { }, _ => { }));
            nodeState.SetField("_nodeEndpoint", endpoint.Node);
            nodeState.SetField("_parentPacketVersion", NodePacketTypeExtensions.PacketVersion);
            nodeState.SetField("_activeTaskCount", 1);

            using TaskExecutionContext context = CreateTaskContext(1, TaskExecutionState.Executing, nodeState.Node);
            Exception? callbackException = null;

            Thread callbackThread = new(() =>
            {
                try
                {
                    ((IBuildEngine)context).BuildProjectFile("nested.proj", ["Build"], null, null);
                }
                catch (Exception ex)
                {
                    callbackException = ex;
                }
            });
            callbackThread.Start();

            // Wait until the callback request packet is enqueued and the task has transitioned to BlockedOnCallback.
            Stopwatch sw = Stopwatch.StartNew();
            while (endpoint.Packets.IsEmpty && sw.ElapsedMilliseconds < 10_000)
            {
                Thread.Sleep(5);
            }

            INodePacket sentPacket = endpoint.Packets.ShouldHaveSingleItem();
            TaskHostBuildRequest buildRequest = sentPacket.ShouldBeOfType<TaskHostBuildRequest>();

            // Simulate Execute() returning while the background thread is still blocked in BuildProjectFile.
            context.MarkAsInactive(reclaimNodeCounts: true);
            callbackThread.Join(10_000).ShouldBeTrue();

            callbackException.ShouldBeOfType<InvalidOperationException>();
            typeof(OutOfProcTaskHostNode).GetField("_activeTaskCount", BindingFlags.Instance | BindingFlags.NonPublic)!
                .GetValue(nodeState.Node).ShouldBe(0);
            typeof(OutOfProcTaskHostNode).GetField("_blockedTaskCount", BindingFlags.Instance | BindingFlags.NonPublic)!
                .GetValue(nodeState.Node).ShouldBe(0);

            // A late response arriving from the parent for that abandoned requestId must be ignored without throwing.
            typeof(OutOfProcTaskHostNode).GetMethod("HandleCallbackResponse", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(nodeState.Node, [new TaskHostBuildResponse(buildRequest.RequestId, true, null)]);
        }

        private static TaskExecutionContext CreateTaskContext(
            int taskId,
            TaskExecutionState state,
            OutOfProcTaskHostNode? owner = null,
            string projectFileOfTask = "project.proj",
            int lineNumberOfTask = 1,
            System.Collections.Generic.ICollection<string>? warningsAsErrors = null) =>
            new(
                taskId,
                new TaskHostConfiguration(
                    nodeId: 1,
                    startupDirectory: Directory.GetCurrentDirectory(),
                    buildProcessEnvironment: null,
                    culture: CultureInfo.CurrentCulture,
                    uiCulture: CultureInfo.CurrentUICulture,
                    hostServices: null,
#if FEATURE_APPDOMAIN
                    appDomainSetup: null,
#endif
                    lineNumberOfTask: lineNumberOfTask,
                    columnNumberOfTask: 1,
                    projectFileOfTask: projectFileOfTask,
                    continueOnError: false,
                    taskName: "LateLogger",
                    taskLocation: "LateLogger.dll",
                    targetName: "Build",
                    projectFile: projectFileOfTask,
                    isTaskInputLoggingEnabled: false,
                    taskParameters: null,
                    globalParameters: null,
                    warningsAsErrors: warningsAsErrors,
                    warningsNotAsErrors: null,
                    warningsAsMessages: null),
                owner)
            {
                State = state,
            };

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

            internal ConcurrentQueue<TaskHostTaskComplete> PendingCompletions =>
                (ConcurrentQueue<TaskHostTaskComplete>)typeof(OutOfProcTaskHostNode).GetField("_taskCompletePackets", InstanceMembers)!.GetValue(Node)!;

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

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void LateBackgroundLoggingInTaskHost_DoesNotCrashOrFailSubsequentTask(bool runSecondTask)
        {
            using TestEnvironment env = TestEnvironment.Create(_output);
            env.SetEnvironmentVariable("MSBUILDUSESERVER", "0");
            TransientTestFolder syncFolder = env.CreateFolder(createFolder: true);

            string secondTaskXml = runSecondTask
                ? $"""<LateBackgroundLoggingTask Mode="TriggerAndVerify" SyncDirectory="{syncFolder.Path}" />"""
                : string.Empty;
            string firstTaskMode = runSecondTask ? "SpawnAndReturn" : "SingleTaskIdle";

            TransientTestFile project = env.CreateFile("lateBackgroundLogging.proj", $"""
                <Project>
                  <UsingTask TaskName="{nameof(LateBackgroundLoggingTask)}" AssemblyFile="{typeof(LateBackgroundLoggingTask).Assembly.Location}" TaskFactory="TaskHostFactory" />
                  <Target Name="Build">
                    <{nameof(LateBackgroundLoggingTask)} Mode="{firstTaskMode}" SyncDirectory="{syncFolder.Path}" />
                    {secondTaskXml}
                  </Target>
                </Project>
                """);

            string output = RunnerUtilities.ExecBootstrapedMSBuild(
                $"\"{project.Path}\" -m:1 -nr:false", out bool success, outputHelper: _output);
            MatchCollection taskPids = Regex.Matches(output, @"LateLoggingTaskPid=(\d+)");
            foreach (Match match in taskPids)
            {
                env.WithTransientProcess(int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture));
            }

            success.ShouldBeTrue(output);
            output.ShouldNotContain("MSB0001");
            output.ShouldNotContain("LateErrorFromTask1");
            output.ShouldNotContain("LateWarningFromTask1");
            File.Exists(Path.Combine(syncFolder.Path, "unexpected.error")).ShouldBeFalse(
                File.Exists(Path.Combine(syncFolder.Path, "unexpected.error"))
                    ? File.ReadAllText(Path.Combine(syncFolder.Path, "unexpected.error"))
                    : string.Empty);

            taskPids.Count.ShouldBe(runSecondTask ? 2 : 1);
            if (runSecondTask)
            {
                output.ShouldNotContain("LateMessageFromTask1");
                taskPids[1].Groups[1].Value.ShouldBe(taskPids[0].Groups[1].Value, "both tasks must share the same TaskHost process");
                output.ShouldContain("SecondTaskCompletedCleanly");
            }
        }

#if NETFRAMEWORK
        [Fact]
        public void IsolatedTask_LateBackgroundLoggingDuringAppDomainUnload_DoesNotDeadlockOrFailBuild()
        {
            using TestEnvironment env = TestEnvironment.Create(_output);
            env.SetEnvironmentVariable("MSBUILDUSESERVER", "0");
            TransientTestFolder syncFolder = env.CreateFolder(createFolder: true);

            TransientTestFile project = env.CreateFile("isolatedLateLogging.proj", $"""
                <Project>
                  <UsingTask TaskName="{nameof(IsolatedLateBackgroundLoggingTask)}" AssemblyFile="{typeof(IsolatedLateBackgroundLoggingTask).Assembly.Location}" TaskFactory="TaskHostFactory" />
                  <Target Name="Build">
                    <{nameof(IsolatedLateBackgroundLoggingTask)} SpawnUnloadLogger="true" SyncDirectory="{syncFolder.Path}" />
                    <{nameof(IsolatedLateBackgroundLoggingTask)} SpawnUnloadLogger="false" SyncDirectory="{syncFolder.Path}" />
                  </Target>
                </Project>
                """);

            string output = RunnerUtilities.ExecBootstrapedMSBuild(
                $"\"{project.Path}\" -m:1 -nr:false", out bool success, outputHelper: _output);
            MatchCollection taskPids = Regex.Matches(output, @"IsolatedLateLoggingTaskPid=(\d+)");
            foreach (Match match in taskPids)
            {
                env.WithTransientProcess(int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture));
            }

            success.ShouldBeTrue(output);
            output.ShouldNotContain("MSB0001");
            output.ShouldNotContain("IsolatedUnloadError");
            File.Exists(Path.Combine(syncFolder.Path, "isolated.error")).ShouldBeFalse(
                File.Exists(Path.Combine(syncFolder.Path, "isolated.error"))
                    ? File.ReadAllText(Path.Combine(syncFolder.Path, "isolated.error"))
                    : string.Empty);

            taskPids.Count.ShouldBe(2);
            taskPids[1].Groups[1].Value.ShouldBe(taskPids[0].Groups[1].Value, "both isolated tasks must share the same TaskHost process");
            output.ShouldContain("IsolatedSecondTaskSucceeded");
        }
#endif

        public sealed class LateBackgroundLoggingTask : ITask
        {
            public IBuildEngine BuildEngine { get; set; } = null!;
            public ITaskHost HostObject { get; set; } = null!;
            public string Mode { get; set; } = string.Empty;
            public string SyncDirectory { get; set; } = string.Empty;

            public bool Execute()
            {
                Microsoft.Build.Utilities.TaskLoggingHelper log = new(this);
                using Process process = Process.GetCurrentProcess();
                log.LogMessage(MessageImportance.High, "LateLoggingTaskPid={0}", process.Id);

                if (string.Equals(Mode, "SingleTaskIdle", StringComparison.Ordinal))
                {
                    object gate = new();
                    bool executeReturning = false;
                    int startedCount = 0;

                    void Worker()
                    {
                        lock (gate)
                        {
                            if (++startedCount == 2)
                            {
                                Monitor.PulseAll(gate);
                            }

                            while (!executeReturning)
                            {
                                Monitor.Wait(gate, 10_000);
                            }
                        }

                        try
                        {
                            // Straddle the exact Execute() return + TaskHost shutdown window (#15612);
                            // messages logged before MarkAsInactive() are valid, while messages logged
                            // after MarkAsInactive() must be dropped without crashing with MSB0001.
                            for (int i = 0; i < 25; i++)
                            {
                                log.LogMessage(MessageImportance.High, "BoundaryMessageFromTask");
                                Thread.Yield();
                            }
                        }
                        catch (Exception ex)
                        {
                            WriteAtomicFile(Path.Combine(SyncDirectory, "unexpected.error"), ex.ToString());
                        }
                    }

                    Thread flowedThread = new(Worker) { IsBackground = true };
                    flowedThread.Start();

                    AsyncFlowControl suppressedFlow = ExecutionContext.SuppressFlow();
                    try
                    {
                        Thread suppressedThread = new(Worker) { IsBackground = true };
                        suppressedThread.Start();
                    }
                    finally
                    {
                        suppressedFlow.Undo();
                    }

                    lock (gate)
                    {
                        while (startedCount < 2)
                        {
                            Monitor.Wait(gate, 10_000);
                        }

                        executeReturning = true;
                        Monitor.PulseAll(gate);
                    }

                    return true;
                }

                if (string.Equals(Mode, "SpawnAndReturn", StringComparison.Ordinal))
                {
                    object gate = new();
                    int startedCount = 0;
                    string releaseGate = Path.Combine(SyncDirectory, "release.gate");

                    void SpawnedWorker(string resultFileName)
                    {
                        lock (gate)
                        {
                            if (++startedCount == 2)
                            {
                                Monitor.PulseAll(gate);
                            }
                        }

                        if (!WaitForFile(releaseGate, timeoutMs: 15_000))
                        {
                            WriteAtomicFile(Path.Combine(SyncDirectory, resultFileName), "Timed out waiting for release.gate");
                            return;
                        }

                        try
                        {
                            log.LogMessage(MessageImportance.High, "LateMessageFromTask1");
                            log.LogWarning("LateWarningFromTask1");
                            log.LogError("LateErrorFromTask1");
                            WriteAtomicFile(Path.Combine(SyncDirectory, resultFileName), "OK");
                        }
                        catch (Exception ex)
                        {
                            WriteAtomicFile(Path.Combine(SyncDirectory, "unexpected.error"), ex.ToString());
                            WriteAtomicFile(Path.Combine(SyncDirectory, resultFileName), ex.ToString());
                        }
                    }

                    Thread flowedThread = new(() => SpawnedWorker("done1.result")) { IsBackground = true };
                    flowedThread.Start();

                    AsyncFlowControl suppressedFlow = ExecutionContext.SuppressFlow();
                    try
                    {
                        Thread suppressedThread = new(() => SpawnedWorker("done2.result")) { IsBackground = true };
                        suppressedThread.Start();
                    }
                    finally
                    {
                        suppressedFlow.Undo();
                    }

                    lock (gate)
                    {
                        DateTime deadline = DateTime.UtcNow.AddSeconds(10);
                        while (startedCount < 2 && DateTime.UtcNow < deadline)
                        {
                            Monitor.Wait(gate, 1_000);
                        }

                        if (startedCount < 2)
                        {
                            log.LogError("Timed out waiting for background threads to start.");
                            return false;
                        }
                    }

                    return true;
                }

                if (string.Equals(Mode, "TriggerAndVerify", StringComparison.Ordinal))
                {
                    WriteAtomicFile(Path.Combine(SyncDirectory, "release.gate"), "GO");

                    string done1 = Path.Combine(SyncDirectory, "done1.result");
                    string done2 = Path.Combine(SyncDirectory, "done2.result");
                    if (!WaitForFile(done1, timeoutMs: 15_000) || !WaitForFile(done2, timeoutMs: 15_000))
                    {
                        log.LogError("Timed out waiting for Task 1 background threads to finish logging.");
                        return false;
                    }

                    string result1 = File.ReadAllText(done1);
                    string result2 = File.ReadAllText(done2);
                    if (!string.Equals(result1, "OK", StringComparison.Ordinal) || !string.Equals(result2, "OK", StringComparison.Ordinal))
                    {
                        log.LogError("Background threads failed: {0} / {1}", result1, result2);
                        return false;
                    }

                    log.LogMessage(MessageImportance.High, "SecondTaskCompletedCleanly");
                    return true;
                }

                log.LogError("Unexpected Mode '{0}'.", Mode);
                return false;
            }

            private static bool WaitForFile(string path, int timeoutMs)
            {
                Stopwatch sw = Stopwatch.StartNew();
                while (sw.ElapsedMilliseconds < timeoutMs)
                {
                    if (File.Exists(path))
                    {
                        return true;
                    }

                    Thread.Sleep(5);
                }

                return File.Exists(path);
            }

            private static void WriteAtomicFile(string path, string content)
            {
                string tempPath = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
                File.WriteAllText(tempPath, content);
                if (File.Exists(path))
                {
                    File.Delete(path);
                }

                File.Move(tempPath, path);
            }
        }

#if NETFRAMEWORK
        [LoadInSeparateAppDomain]
        public sealed class IsolatedLateBackgroundLoggingTask : MarshalByRefObject, ITask
        {
            public IBuildEngine BuildEngine { get; set; } = null!;
            public ITaskHost HostObject { get; set; } = null!;
            public bool SpawnUnloadLogger { get; set; }
            public string SyncDirectory { get; set; } = string.Empty;

            public bool Execute()
            {
                Microsoft.Build.Utilities.TaskLoggingHelper log = new(this);
                using Process process = Process.GetCurrentProcess();
                log.LogMessage(MessageImportance.High, "IsolatedLateLoggingTaskPid={0}", process.Id);

                if (!SpawnUnloadLogger)
                {
                    log.LogMessage(MessageImportance.High, "IsolatedSecondTaskSucceeded");
                    return true;
                }

                object gate = new();
                bool started = false;
                bool executeReturning = false;
                Thread backgroundThread = new(() =>
                {
                    try
                    {
                        lock (gate)
                        {
                            started = true;
                            Monitor.PulseAll(gate);
                            while (!executeReturning)
                            {
                                Monitor.Wait(gate, 10_000);
                            }
                        }

                        // Exercise cross-AppDomain logging across Execute() return and into AppDomain.Unload.
                        while (true)
                        {
                            log.LogMessage(MessageImportance.High, "IsolatedBoundaryMessage");
                            Thread.Yield();
                        }
                    }
                    catch (Exception ex) when (ex is ThreadAbortException or AppDomainUnloadedException)
                    {
                        try
                        {
                            log.LogError("IsolatedUnloadError");
                        }
                        catch (Exception inner) when (inner is ThreadAbortException or AppDomainUnloadedException)
                        {
                            // Expected once the CLR blocks cross-AppDomain calls during AppDomain.Unload.
                        }
                        catch (Exception inner)
                        {
                            File.WriteAllText(Path.Combine(SyncDirectory, "isolated.error"), inner.ToString());
                        }
                    }
                    catch (Exception ex)
                    {
                        File.WriteAllText(Path.Combine(SyncDirectory, "isolated.error"), ex.ToString());
                    }
                })
                {
                    IsBackground = true,
                };

                backgroundThread.Start();
                lock (gate)
                {
                    DateTime deadline = DateTime.UtcNow.AddSeconds(10);
                    while (!started && DateTime.UtcNow < deadline)
                    {
                        Monitor.Wait(gate, 1_000);
                    }

                    if (!started)
                    {
                        log.LogError("Timed out waiting for isolated background thread to start.");
                        return false;
                    }

                    executeReturning = true;
                    Monitor.PulseAll(gate);
                }

                return true;
            }
        }
#endif
    }
}
