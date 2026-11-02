// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using Microsoft.Build.BackEnd;
using Microsoft.Build.CommandLine;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Microsoft.Build.Logging;
using Microsoft.Build.UnitTests.Shared;
using Shouldly;
using Xunit;
#if NETFRAMEWORK
using System.Runtime.Remoting.Messaging;
#endif

namespace Microsoft.Build.UnitTests;

public sealed class TaskHostCancellation_Tests
{
    private const string ReadyMessage = "TaskHostCancellationReady";
#if NETFRAMEWORK
    private const string ContextIdSlot = "MSBuild.TaskHost.TaskContextId";
#endif
    private const BindingFlags InstanceFields = BindingFlags.Instance | BindingFlags.NonPublic;
    private readonly ITestOutputHelper _output;

    public TaskHostCancellation_Tests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Theory]
    [InlineData(nameof(CancellationContextTask), false)]
#if NETFRAMEWORK
    [InlineData(nameof(IsolatedCancellationContextTask), true)]
#endif
    public void CancelCallbackCanSetAllowFailureWithoutError(string taskName, bool isolated)
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        env.SetEnvironmentVariable("MSBUILDUSESERVER", "0");
        TransientTestFile cancelled = env.CreateFile("cancelled.txt", string.Empty);
        TransientTestFile binlog = env.CreateFile("cancellation.binlog", string.Empty);
        TransientTestFile project = env.CreateFile("cancellation.proj", $"""
            <Project>
              <UsingTask TaskName="{taskName}" AssemblyFile="{typeof(CancellationContextTask).Assembly.Location}" TaskFactory="TaskHostFactory" />
              <Target Name="Build">
                <{taskName} ResultFile="{cancelled.Path}" />
              </Target>
            </Project>
            """);

        string output = RunnerUtilities.ExecBootstrapedMSBuild(
            $"\"{project.Path}\" -m:1 -nr:false -bl:\"{binlog.Path}\" -logger:{typeof(CancelOnReadyLogger).FullName},\"{typeof(CancelOnReadyLogger).Assembly.Location}\"",
            out bool success,
            outputHelper: _output,
            timeoutMilliseconds: 30_000);

        Match taskPid = Regex.Match(output, @"CancellationTaskPid=(\d+)");
        if (taskPid.Success)
        {
            env.WithTransientProcess(int.Parse(taskPid.Groups[1].Value, CultureInfo.InvariantCulture));
        }

        success.ShouldBeFalse(output);
        taskPid.Success.ShouldBeTrue(output);
        Match clientPid = Regex.Match(output, @"Process ID is (\d+)");
        clientPid.Success.ShouldBeTrue(output);
        taskPid.Groups[1].Value.ShouldNotBe(clientPid.Groups[1].Value);
        try
        {
            using Process child = Process.GetProcessById(int.Parse(taskPid.Groups[1].Value, CultureInfo.InvariantCulture));
            child.WaitForExit(10_000).ShouldBeTrue("the cancelled TaskHost must exit");
        }
        catch (ArgumentException)
        {
            // The TaskHost already exited before the client returned.
        }
        output.ShouldContain($"CancellationTaskIsolated={isolated}");
        File.ReadAllText(cancelled.Path).ShouldBe("cancelled");

        MockLogger logger = new(_output);
        BinaryLogReplayEventSource replay = new();
        logger.Initialize(replay);
        replay.Replay(binlog.Path);
        logger.AssertNoErrors();
        logger.AssertLogContains(ReadyMessage);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void CancelTaskRestoresPreviousContext(bool hasPreviousContext, bool throws)
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        ExecutionContext.Run(ExecutionContext.Capture()!, _ =>
        {
            OutOfProcTaskHostNode node = new();
            using TaskExecutionContext context = CreateContext(1);
            using TaskExecutionContext? previous = hasPreviousContext ? CreateContext(2) : null;
            AsyncLocal<TaskExecutionContext> current = GetField<AsyncLocal<TaskExecutionContext>>(node, "_currentTaskContext");
            current.Value = previous!;
#if NETFRAMEWORK
            object? previousId = hasPreviousContext ? 2 : null;
            if (previousId is null)
            {
                CallContext.FreeNamedDataSlot(ContextIdSlot);
            }
            else
            {
                CallContext.LogicalSetData(ContextIdSlot, previousId);
            }
#endif
            int calls = 0;
            InvalidOperationException failure = new("Cancellation callback failure");
            context.TaskWrapper = CreateWrapper(() =>
            {
                calls++;
                current.Value.ShouldBeSameAs(context);
#if NETFRAMEWORK
                CallContext.LogicalGetData(ContextIdSlot).ShouldBe(context.TaskId);
#endif
                node.AllowFailureWithoutError.ShouldBeFalse();
                node.AllowFailureWithoutError = true;
                if (throws)
                {
                    throw failure;
                }
            });
            GetField<ConcurrentDictionary<int, TaskExecutionContext>>(node, "_taskContexts")[context.TaskId] = context;
            Action cancel = GetCancelAction(node);

            if (throws)
            {
                Should.Throw<InvalidOperationException>(cancel).ShouldBeSameAs(failure);
            }
            else
            {
                cancel();
            }

            calls.ShouldBe(1);
            context.AllowFailureWithoutError.ShouldBeTrue();
            current.Value.ShouldBeSameAs(previous);
            previous?.AllowFailureWithoutError.ShouldBeFalse();
#if NETFRAMEWORK
            CallContext.LogicalGetData(ContextIdSlot).ShouldBe(previousId);
#endif
        }, null);
    }

    [Fact]
    public void CancelTaskKeepsActiveAndBlockedTaskContextsIsolated()
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        OutOfProcTaskHostNode node = new();
        using TaskExecutionContext parent = CreateContext(1);
        using TaskExecutionContext nested = CreateContext(2);
        parent.State = TaskExecutionState.BlockedOnCallback;
        parent.AllowFailureWithoutError = true;
        nested.State = TaskExecutionState.Executing;
        int parentCalls = 0;
        int nestedCalls = 0;
        parent.TaskWrapper = CreateWrapper(() =>
        {
            parentCalls++;
            node.AllowFailureWithoutError.ShouldBeTrue();
            node.AllowFailureWithoutError = false;
        });
        nested.TaskWrapper = CreateWrapper(() =>
        {
            nestedCalls++;
            node.AllowFailureWithoutError.ShouldBeFalse();
            node.AllowFailureWithoutError = true;
        });
        ConcurrentDictionary<int, TaskExecutionContext> contexts = GetField<ConcurrentDictionary<int, TaskExecutionContext>>(node, "_taskContexts");
        contexts[parent.TaskId] = parent;
        contexts[nested.TaskId] = nested;

        GetCancelAction(node)();
        GetCancelAction(node)();

        parentCalls.ShouldBe(1);
        nestedCalls.ShouldBe(1);
        parent.AllowFailureWithoutError.ShouldBeFalse();
        nested.AllowFailureWithoutError.ShouldBeTrue();
        GetField<AsyncLocal<TaskExecutionContext>>(node, "_currentTaskContext").Value.ShouldBeNull();
    }

    [Fact]
    public void CancelTaskBeforeWrapperInitializationIsSafe()
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        OutOfProcTaskHostNode node = new();
        Action cancel = GetCancelAction(node);
        cancel();

        OutOfProcTaskAppDomainWrapper wrapper = new();
        cancel();
        wrapper.CancelPending.ShouldBeFalse();

        using TaskExecutionContext context = CreateContext(1);
        GetField<ConcurrentDictionary<int, TaskExecutionContext>>(node, "_taskContexts")[context.TaskId] = context;
        wrapper.CancelPending = false;
        cancel();

        wrapper.CancelPending.ShouldBeFalse("a wrapper being initialized must not be cancelled without its task context");
        context.TaskWrapper = wrapper;
        cancel();
        wrapper.CancelPending.ShouldBeTrue();
        GetField<AsyncLocal<TaskExecutionContext>>(node, "_currentTaskContext").Value.ShouldBeNull();
    }

    [Fact]
    public void CancelTaskIgnoresCompletedContexts()
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        OutOfProcTaskHostNode node = new();
        using TaskExecutionContext completed = CreateContext(1);
        completed.State = TaskExecutionState.Completed;
        int calls = 0;
        completed.TaskWrapper = CreateWrapper(() => calls++);
        ConcurrentDictionary<int, TaskExecutionContext> contexts = GetField<ConcurrentDictionary<int, TaskExecutionContext>>(node, "_taskContexts");
        contexts[completed.TaskId] = completed;

        GetCancelAction(node)();
        contexts.Clear();
        GetCancelAction(node)();

        calls.ShouldBe(0);
    }

    private static TaskExecutionContext CreateContext(int id) =>
        new(id, (TaskHostConfiguration)Activator.CreateInstance(typeof(TaskHostConfiguration), nonPublic: true)!);

    private static T GetField<T>(OutOfProcTaskHostNode node, string name) =>
        (T)typeof(OutOfProcTaskHostNode).GetField(name, InstanceFields)!.GetValue(node)!;

    private static Action GetCancelAction(OutOfProcTaskHostNode node) =>
        (Action)typeof(OutOfProcTaskHostNode).GetMethod("CancelTask", InstanceFields)!.CreateDelegate(typeof(Action), node);

    private static OutOfProcTaskAppDomainWrapper CreateWrapper(Action cancel)
    {
        OutOfProcTaskAppDomainWrapper wrapper = new();
        typeof(OutOfProcTaskAppDomainWrapperBase).GetField("wrappedTask", InstanceFields)!.SetValue(wrapper, new CallbackTask(cancel));
        return wrapper;
    }

    private sealed class CallbackTask(Action cancel) : ICancelableTask
    {
        public IBuildEngine BuildEngine { get; set; } = null!;
        public ITaskHost HostObject { get; set; } = null!;
        public bool Execute() => throw new NotSupportedException();
        public void Cancel() => cancel();
    }

    public sealed class CancelOnReadyLogger : ILogger
    {
        private IEventSource? _eventSource;
        private int _cancelRequested;

        public LoggerVerbosity Verbosity { get; set; } = LoggerVerbosity.Normal;
        public string? Parameters { get; set; }

        public void Initialize(IEventSource eventSource)
        {
            _eventSource = eventSource;
            eventSource.MessageRaised += OnMessage;
        }

        private void OnMessage(object sender, BuildMessageEventArgs args)
        {
            if (args.Message == ReadyMessage && Interlocked.Exchange(ref _cancelRequested, 1) == 0)
            {
                ThreadPool.QueueUserWorkItem(_ => BuildManager.DefaultBuildManager.CancelAllSubmissions());
            }
        }

        public void Shutdown()
        {
            if (_eventSource is not null)
            {
                _eventSource.MessageRaised -= OnMessage;
            }
        }
    }

    public class CancellationContextTask : MarshalByRefObject, ICancelableTask
    {
        private readonly ManualResetEventSlim _cancelled = new();
        private int _cancelRequested;

        public IBuildEngine BuildEngine { get; set; } = null!;
        public ITaskHost HostObject { get; set; } = null!;
        public string ResultFile { get; set; } = null!;

        public bool Execute()
        {
            Microsoft.Build.Utilities.TaskLoggingHelper log = new(this);
            using Process process = Process.GetCurrentProcess();
            log.LogMessage(MessageImportance.High, "CancellationTaskPid={0}", process.Id);
            log.LogMessage(MessageImportance.High, "CancellationTaskIsolated={0}", !AppDomain.CurrentDomain.IsDefaultAppDomain());
            log.LogMessage(MessageImportance.High, ReadyMessage);
            if (!_cancelled.Wait(TimeSpan.FromSeconds(15)))
            {
                log.LogError("Cancellation callback did not complete.");
                return false;
            }

            if (!((IBuildEngine7)BuildEngine).AllowFailureWithoutError)
            {
                log.LogError("Cancellation did not update the executing task's failure state.");
                return false;
            }

            File.WriteAllText(ResultFile, "cancelled");
            return true;
        }

        public void Cancel()
        {
            if (Interlocked.Exchange(ref _cancelRequested, 1) != 0)
            {
                return;
            }

            IBuildEngine7 engine = (IBuildEngine7)BuildEngine;
            if (engine.AllowFailureWithoutError)
            {
                throw new InvalidOperationException("Cancellation inherited another task's failure state.");
            }

            engine.AllowFailureWithoutError = true;
            _cancelled.Set();
        }
    }

    [LoadInSeparateAppDomain]
    public sealed class IsolatedCancellationContextTask : CancellationContextTask
    {
    }
}
