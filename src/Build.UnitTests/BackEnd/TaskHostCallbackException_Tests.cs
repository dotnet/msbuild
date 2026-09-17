// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Build.BackEnd;
using Microsoft.Build.BackEnd.Logging;
using Microsoft.Build.Construction;
using Microsoft.Build.Exceptions;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Microsoft.Build.Internal;
using Microsoft.Build.Shared;
using Shouldly;
using Xunit;

#nullable enable

namespace Microsoft.Build.UnitTests.BackEnd;

public sealed class TaskHostCallbackException_Tests(ITestOutputHelper output)
{
    private readonly ITestOutputHelper _output = output;
    private const long PeerInvocationId = long.MaxValue;
    private const string EnvironmentVariable = "MSBUILD_CALLBACK_EXCEPTION_COMPLETED";
    private const string TrailingMessage = "Logging after the failed build callback";

    [Theory]
    [InlineData("build", "success")]
    [InlineData("build", "failure")]
    [InlineData("build", "execution-crash")]
    [InlineData("build", "after-execution-crash")]
    [InlineData("build", "shutdown")]
    [InlineData("multiple-nodes", "success")]
    [InlineData("multiple-nodes", "failure")]
    [InlineData("multiple-nodes", "execution-crash")]
    [InlineData("multiple-nodes", "after-execution-crash")]
    [InlineData("multiple-nodes", "shutdown")]
    [InlineData("request-cores", "success")]
    [InlineData("request-cores", "failure")]
    [InlineData("request-cores", "execution-crash")]
    [InlineData("request-cores", "after-execution-crash")]
    [InlineData("request-cores", "shutdown")]
    [InlineData("release-cores", "success")]
    [InlineData("release-cores", "failure")]
    [InlineData("release-cores", "execution-crash")]
    [InlineData("release-cores", "after-execution-crash")]
    [InlineData("release-cores", "shutdown")]
    public async Task CallbackExceptionRetainsInvocationUntilTerminalPacket(string callback, string terminal)
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        using var harness = new PacketPumpHarness(_output);
        var originalException = new CircularDependencyException("Original callback failure");
        var engine = new ThrowingBuildEngine(originalException, _output, callback);
        TaskEnvironment taskEnvironment = TaskEnvironment.CreateWithProjectDirectoryAndEnvironment(
            env.CreateFolder().Path, new Dictionary<string, string>());
        TaskHostTask task = harness.CreateTask(engine, taskEnvironment);
        Task<Exception?> execution = Task.Run(() => Record.Exception(() => { task.Execute(); }));

        try
        {
            TaskHostConfiguration configuration = await Within(harness.Pipe.Configuration.Task);
            long invocationId = configuration.TaskInvocationId;
            invocationId.ShouldBeGreaterThan(0);
            harness.Provider.TryAttachTaskHandler(harness.Context, harness.Peer, PeerInvocationId).ShouldBeTrue();
            harness.Provider.PacketReceived(1, new TaskHostTaskPacket(invocationId, CreateRequest(callback)));

            (await Within(Task.WhenAny(harness.Pipe.Response.Task, execution)))
                .ShouldBeSameAs(harness.Pipe.Response.Task, "Every failed callback must reply before waiting for remote completion.");
            ITaskHostCallbackPacket response = await Within(harness.Pipe.Response.Task);
            response.RequestId.ShouldBe(17);
            switch (callback)
            {
                case "build":
                    var buildResponse = response.ShouldBeOfType<TaskHostBuildResponse>();
                    buildResponse.Success.ShouldBeFalse();
                    buildResponse.TargetOutputsPerProject.ShouldBeNull();
                    break;
                case "multiple-nodes":
                    response.ShouldBeOfType<TaskHostIsRunningMultipleNodesResponse>().IsRunningMultipleNodes.ShouldBeFalse();
                    break;
                default:
                    response.ShouldBeOfType<TaskHostCoresResponse>().GrantedCores.ShouldBe(callback == "request-cores" ? 1 : 0);
                    break;
            }

            // The reply has crossed the real sender. The remote task may now log before completing.
            // On the broken implementation either dispatch rejects the ID, or Execute wins this race.
            harness.Provider.PacketReceived(1, new TaskHostTaskPacket(invocationId, Message(TrailingMessage)));
            (await Within(Task.WhenAny(engine.TrailingMessageReceived.Task, execution)))
                .ShouldBeSameAs(engine.TrailingMessageReceived.Task, "Execute must keep pumping after the callback throws.");
            execution.IsCompleted.ShouldBeFalse();
            harness.Provider.TaskHandlerRegistrationCount.ShouldBe(1, "both handlers share one registered node");
            engine.Log.MessageEvents.ShouldHaveSingleItem().Message.ShouldBe(TrailingMessage);
            harness.Provider.PacketReceived(1, new TaskHostTaskPacket(PeerInvocationId, Message("peer-before-completion")));
            harness.Peer.Packets.ShouldHaveSingleItem().Type.ShouldBe(NodePacketType.LogMessage);

            if (terminal == "shutdown")
            {
                harness.Provider.PacketReceived(1, new NodeShutdown(NodeShutdownReason.ConnectionFailed));
            }
            else
            {
                TaskCompleteType result = terminal switch
                {
                    "success" => TaskCompleteType.Success,
                    "failure" => TaskCompleteType.Failure,
                    "execution-crash" => TaskCompleteType.CrashedDuringExecution,
                    _ => TaskCompleteType.CrashedAfterExecution,
                };
                var remoteException = new InvalidOperationException("Remote completion failure");
                var taskResult = new OutOfProcTaskHostTaskResult(
                    result, new Dictionary<string, object> { ["Result"] = "remote-output" },
                    result is TaskCompleteType.Success or TaskCompleteType.Failure ? null : remoteException, null, null);
                harness.Provider.PacketReceived(1, new TaskHostTaskPacket(invocationId, new TaskHostTaskComplete(
                    taskResult,
#if FEATURE_REPORTFILEACCESSES
                    null,
#endif
                    new Dictionary<string, string> { [EnvironmentVariable] = "applied" })));
            }

            Exception? observed = await Within(execution);
            observed.ShouldBeSameAs(originalException);
            string callbackMethod = callback switch
            {
                "build" => nameof(ThrowingBuildEngine.BuildProjectFilesInParallel),
                "multiple-nodes" => nameof(ThrowingBuildEngine.IsRunningMultipleNodes),
                "request-cores" => nameof(ThrowingBuildEngine.RequestCores),
                _ => nameof(ThrowingBuildEngine.ReleaseCores),
            };
            observed.ShouldNotBeNull().StackTrace.ShouldNotBeNull().ShouldContain(callbackMethod);
            if (terminal == "shutdown")
            {
                harness.Logger.Errors.ShouldHaveSingleItem().Code.ShouldBe("MSB4217");
                harness.Peer.Packets.Count.ShouldBe(2);
                harness.Peer.Packets[1].Type.ShouldBe(NodePacketType.NodeShutdown);
                harness.Provider.TaskHandlerRegistrationCount.ShouldBe(0);
            }
            else
            {
                taskEnvironment.GetEnvironmentVariable(EnvironmentVariable).ShouldBe("applied");
                if (terminal is "success" or "failure")
                {
                    task.GetPropertyValue(new TaskPropertyInfo("Result", typeof(string), true, false)).ShouldBe("remote-output");
                    harness.Logger.Errors.ShouldBeEmpty();
                }
                else
                {
                    harness.Logger.Errors.ShouldHaveSingleItem().Message.ShouldNotBeNull().ShouldContain("Remote completion failure");
                }

                harness.Provider.TaskHandlerRegistrationCount.ShouldBe(1);
                harness.Provider.PacketReceived(1, new TaskHostTaskPacket(PeerInvocationId, Message("peer-after-completion")));
                harness.Peer.Packets.Count.ShouldBe(2);
                harness.Peer.Packets[1].Type.ShouldBe(NodePacketType.LogMessage);
                harness.Provider.ConnectedNodes.Count.ShouldBe(1);
                Should.Throw<InvalidDataException>(() =>
                    harness.Provider.PacketReceived(1, new TaskHostTaskPacket(invocationId, Message("retired invocation"))));
            }
        }
        finally
        {
            harness.Provider.PacketReceived(1, new NodeShutdown(NodeShutdownReason.ConnectionFailed));
            await Within(execution);
            taskEnvironment.Dispose();
        }
    }

    [Theory]
    [InlineData("build", false)]
    [InlineData("multiple-nodes", false)]
    [InlineData("request-cores", false)]
    [InlineData("release-cores", false)]
    [InlineData("build", true)]
    [InlineData("multiple-nodes", true)]
    [InlineData("request-cores", true)]
    [InlineData("release-cores", true)]
    public async Task BuildWideCallbackExceptionIsNotDeferred(string callback, bool loggerFailure)
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        using var harness = new PacketPumpHarness(_output);
        Exception originalException = loggerFailure
            ? new LoggerException("Synthetic logger failure")
            : new OutOfMemoryException("Synthetic critical callback failure");
        var engine = new ThrowingBuildEngine(originalException, _output, callback);
        TaskHostTask task = harness.CreateTask(engine, TaskEnvironmentHelper.CreateForTest());
        Task<Exception?> execution = Task.Run(() => Record.Exception(() => { task.Execute(); }));
        try
        {
            TaskHostConfiguration configuration = await Within(harness.Pipe.Configuration.Task);
            harness.Provider.PacketReceived(1, new TaskHostTaskPacket(configuration.TaskInvocationId, CreateRequest(callback)));
            // If the callback is incorrectly deferred, the next message is processed instead of unwinding.
            task.PacketReceived(1, Message(TrailingMessage));
            (await Within(Task.WhenAny(execution, engine.TrailingMessageReceived.Task)))
                .ShouldBeSameAs(execution, "Build-wide failures must propagate immediately, not wait for a remote task.");
            (await execution).ShouldBeSameAs(originalException);
            harness.Provider.TaskHandlerRegistrationCount.ShouldBe(0);
        }
        finally
        {
            harness.Provider.PacketReceived(1, new NodeShutdown(NodeShutdownReason.ConnectionFailed));
            await Within(execution);
        }
    }

    private static ITaskHostCallbackPacket CreateRequest(string callback)
    {
        ITaskHostCallbackPacket request = callback switch
        {
            "build" => new TaskHostBuildRequest(["child.proj"], ["Build"], null, null, null, true),
            "multiple-nodes" => new TaskHostIsRunningMultipleNodesRequest(),
            "request-cores" => new TaskHostCoresRequest(2, isRelease: false),
            "release-cores" => new TaskHostCoresRequest(2, isRelease: true),
            _ => throw new ArgumentException("Unknown callback.", nameof(callback)),
        };
        request.RequestId = 17;
        return request;
    }

    private static LogMessagePacket Message(string message)
        => new(new KeyValuePair<int, BuildEventArgs>(0, new BuildMessageEventArgs(message, null, "remote", MessageImportance.High)));

    private static async Task<T> Within<T>(Task<T> task)
    {
        using CancellationTokenSource cancellation = new();
        Task timeout = Task.Delay(TimeSpan.FromSeconds(10), cancellation.Token);
        (await Task.WhenAny(task, timeout)).ShouldBeSameAs(task, "The packet-pump test exceeded its safety bound.");
        cancellation.Cancel();
        return await task;
    }

    private sealed class PacketPumpHarness : IBuildComponentHost, IDisposable
    {
        private readonly Process _process = Process.GetCurrentProcess();
        private readonly ManualResetEventSlim _terminated = new();
        public ReplyStream Pipe { get; } = new();
        public RecordingHandler Peer { get; } = new();
        public MockLogger Logger { get; }
        public NodeProviderOutOfProcTaskHost Provider { get; }
        public NodeProviderOutOfProcBase.NodeContext Context { get; }
        public BuildParameters BuildParameters { get; } = new() { EnableNodeReuse = false };
        public LegacyThreadingData LegacyThreadingData { get; } = new();
        public ILoggingService LoggingService { get; }
        public string Name => nameof(PacketPumpHarness);

        public PacketPumpHarness(ITestOutputHelper output)
        {
            Logger = new MockLogger(output);
            LoggingService = (ILoggingService)new LoggingServiceFactory(LoggerMode.Synchronous, 1)
                .CreateInstance(BuildComponentType.LoggingService);
            LoggingService.RegisterLogger(Logger);
            Provider = (NodeProviderOutOfProcTaskHost)NodeProviderOutOfProcTaskHost.CreateComponent(BuildComponentType.OutOfProcTaskHostNodeProvider);
            Provider.InitializeComponent(this);
            HandshakeOptions options = CommunicationsUtilities.GetHandshakeOptions(taskHost: true, TaskHostParameters.Empty, nodeReuse: false);
            // NET framing lets the scripted remote observe invocation IDs on either test TFM.
            Context = new(1, _process, Pipe, Provider, id => { Provider.NodeContextTerminated(id); _terminated.Set(); },
                NodePacketTypeExtensions.TaskHostInvocationMinVersion, HandshakeOptions.TaskHost | HandshakeOptions.NET);
            Provider.NodeContextCreated(Context, new TaskHostNodeKey(options, 1));
        }

        public TaskHostTask CreateTask(IBuildEngine engine, TaskEnvironment environment)
            => new(
                ElementLocation.Create("callback.proj", 1, 1),
                new TaskLoggingContext(LoggingService, BuildEventContext.Invalid),
                this, TaskHostParameters.Empty,
                new LoadedType(typeof(TestTask), AssemblyLoadInfo.Create(typeof(TestTask).Assembly.FullName, null),
                    typeof(TestTask).Assembly, typeof(ITaskItem)),
                false, false, "callback.proj",
#if FEATURE_APPDOMAIN
                null,
#endif
                null, 1, environment) { BuildEngine = engine };

        public IBuildComponent GetComponent(BuildComponentType type)
            => type == BuildComponentType.OutOfProcTaskHostNodeProvider ? Provider : throw new NotSupportedException(type.ToString());
        public TComponent GetComponent<TComponent>(BuildComponentType type) where TComponent : IBuildComponent
            => (TComponent)GetComponent(type);
        public void RegisterFactory(BuildComponentType factoryType, BuildComponentFactoryDelegate factory) => throw new NotSupportedException();

        public void Dispose()
        {
            Pipe.CompleteRead();
            _terminated.Wait(TimeSpan.FromSeconds(10)).ShouldBeTrue();
            Context.WaitForSendCompletion(10_000).ShouldBeTrue();
            LoggingService.ShutdownComponent();
            Pipe.Dispose();
            _terminated.Dispose();
            _process.Dispose();
        }
    }

    private sealed class ReplyStream : MemoryStream
    {
        private readonly TaskCompletionSource<int> _read = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly MemoryStream _written = new();
        private long _consumed;
        public TaskCompletionSource<TaskHostConfiguration> Configuration { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<ITaskHostCallbackPacket> Response { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public void CompleteRead() => _read.TrySetResult(0);
        public override void Write(byte[] buffer, int offset, int count)
        {
            _written.Position = _written.Length;
            _written.Write(buffer, offset, count);
            _written.Position = _consumed;
            while (_written.Length - _written.Position >= 5)
            {
                byte type = (byte)_written.ReadByte();
                using BinaryReader header = new(_written, System.Text.Encoding.UTF8, leaveOpen: true);
                int length = header.ReadInt32();
                if (_written.Length - _written.Position < length)
                {
                    return;
                }

                long end = _written.Position + length;
                byte version = NodePacketTypeExtensions.HasExtendedHeader(type) ? NodePacketTypeExtensions.ReadVersion(_written) : (byte)0;
                ITranslator translator = BinaryTranslator.GetReadTranslator(_written, InterningBinaryReader.CreateSharedBuffer());
                translator.NegotiatedPacketVersion = version;
                switch (NodePacketTypeExtensions.GetNodePacketType(type))
                {
                    case NodePacketType.TaskHostConfiguration:
                        Configuration.TrySetResult((TaskHostConfiguration)TaskHostConfiguration.FactoryForDeserialization(translator));
                        break;
                    case NodePacketType.TaskHostBuildResponse:
                        Response.TrySetResult((TaskHostBuildResponse)TaskHostBuildResponse.FactoryForDeserialization(translator));
                        break;
                    case NodePacketType.TaskHostIsRunningMultipleNodesResponse:
                        Response.TrySetResult((TaskHostIsRunningMultipleNodesResponse)TaskHostIsRunningMultipleNodesResponse.FactoryForDeserialization(translator));
                        break;
                    case NodePacketType.TaskHostCoresResponse:
                        Response.TrySetResult((TaskHostCoresResponse)TaskHostCoresResponse.FactoryForDeserialization(translator));
                        break;
                }

                _written.Position = _consumed = end;
            }
        }

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) => _read.Task;
#if NET
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) => new(_read.Task);
#endif
        public override IAsyncResult BeginRead(byte[] buffer, int offset, int count, AsyncCallback? callback, object? state)
        {
            _ = _read.Task.ContinueWith(_ => callback?.Invoke(_read.Task), TaskScheduler.Default);
            return _read.Task;
        }
        public override int EndRead(IAsyncResult asyncResult) => _read.Task.GetAwaiter().GetResult();
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _written.Dispose();
            }
            base.Dispose(disposing);
        }
    }

    private sealed class RecordingHandler : INodePacketHandler
    {
        public List<INodePacket> Packets { get; } = [];
        public void PacketReceived(int node, INodePacket packet) => Packets.Add(packet);
    }

    private sealed class ThrowingBuildEngine(Exception exception, ITestOutputHelper output, string callback) : IBuildEngine9
    {
        public MockEngine Log { get; } = new(output);
        public TaskCompletionSource<bool> TrailingMessageReceived { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public bool ContinueOnError => false;
        public int LineNumberOfTaskNode => 1;
        public int ColumnNumberOfTaskNode => 1;
        public string ProjectFileOfTaskNode => "callback.proj";
        public bool IsRunningMultipleNodes
        {
            [MethodImpl(MethodImplOptions.NoInlining)]
            get => callback == "multiple-nodes" ? throw exception : true;
        }
        public bool AllowFailureWithoutError { get; set; }
        public IReadOnlyDictionary<string, string> GetGlobalProperties() => new Dictionary<string, string>();
        public bool ShouldTreatWarningAsError(string warningCode) => false;
        public void LogTelemetry(string eventName, IDictionary<string, string> properties) => Log.LogTelemetry(eventName, properties);
        public void RegisterTaskObject(object key, object obj, RegisteredTaskObjectLifetime lifetime, bool allowEarlyCollection)
            => throw new NotSupportedException();
        public object GetRegisteredTaskObject(object key, RegisteredTaskObjectLifetime lifetime) => throw new NotSupportedException();
        public object UnregisterTaskObject(object key, RegisteredTaskObjectLifetime lifetime) => throw new NotSupportedException();
        [MethodImpl(MethodImplOptions.NoInlining)]
        public int RequestCores(int requestedCores) => callback == "request-cores" ? throw exception : requestedCores;
        [MethodImpl(MethodImplOptions.NoInlining)]
        public void ReleaseCores(int coresToRelease)
        {
            if (callback == "release-cores")
            {
                throw exception;
            }
        }
        public void LogErrorEvent(BuildErrorEventArgs e) => Log.LogErrorEvent(e);
        public void LogWarningEvent(BuildWarningEventArgs e) => Log.LogWarningEvent(e);
        public void LogCustomEvent(CustomBuildEventArgs e) => Log.LogCustomEvent(e);
        public void LogMessageEvent(BuildMessageEventArgs e)
        {
            Log.LogMessageEvent(e);
            if (e.Message == TrailingMessage)
            {
                TrailingMessageReceived.TrySetResult(true);
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public BuildEngineResult BuildProjectFilesInParallel(string[] projectFileNames, string[] targetNames,
            IDictionary[] globalProperties, IList<string>[] removeGlobalProperties, string[] toolsVersion, bool returnTargetOutputs)
            => throw exception;
        public bool BuildProjectFile(string projectFileName, string[] targetNames, IDictionary globalProperties, IDictionary targetOutputs)
            => throw new NotSupportedException();
        public bool BuildProjectFile(string projectFileName, string[] targetNames, IDictionary globalProperties, IDictionary targetOutputs, string toolsVersion)
            => throw new NotSupportedException();
        public bool BuildProjectFilesInParallel(string[] projectFileNames, string[] targetNames, IDictionary[] globalProperties,
            IDictionary[] targetOutputsPerProject, string[] toolsVersion, bool useResultsCache, bool unloadProjectsOnCompletion)
            => throw new NotSupportedException();
        public void Yield() => throw new NotSupportedException();
        public void Reacquire() => throw new NotSupportedException();
    }

    private sealed class TestTask : ITask
    {
        public IBuildEngine BuildEngine { get; set; } = null!;
        public ITaskHost HostObject { get; set; } = null!;
        public bool Execute() => true;
    }
}
