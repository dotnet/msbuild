// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Build.BackEnd;
using Microsoft.Build.BackEnd.Logging;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Microsoft.Build.UnitTests.BackEnd;
using Shouldly;
using Xunit;

namespace Microsoft.Build.UnitTests.Logging;

public class TaskCacheDiagnosticCapture_Tests
{
    private readonly ITestOutputHelper _output;

    public TaskCacheDiagnosticCapture_Tests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, true, false)]
    [InlineData(false, false, true)]
    [InlineData(true, false, false)]
    [InlineData(true, true, false)]
    [InlineData(true, false, true)]
    public void CapturesBeforeAsynchronousDispatchAndWarningPolicy(bool forwarded, bool demote, bool promote)
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        using TestLoggingService test = new(_output, LoggerMode.Asynchronous);
        LoggingService service = test.Service;
        if (demote)
        {
            service.WarningsAsMessages = new HashSet<string> { "CS0001" };
        }
        else if (promote)
        {
            service.WarningsAsErrors = new HashSet<string> { "CS0001" };
        }

        service.LogBuildEvent(new BuildMessageEventArgs("block", null, "test", MessageImportance.Normal));
        test.BlockingLogger.Entered.Wait(TimeSpan.FromSeconds(30)).ShouldBeTrue();

        using TaskCacheDiagnosticCapture capture =
            ((ITaskCacheDiagnosticSource)service).CaptureTaskDiagnostics(test.Context);
        BuildWarningEventArgs warning = Warning(test.Context);
        if (forwarded)
        {
            service.PacketReceived(1, new LogMessagePacket(new KeyValuePair<int, BuildEventArgs>(0, warning)));
        }
        else
        {
            service.LogBuildEvent(warning);
        }

        // The logging thread is still blocked on the preceding message.
        capture.HasDiagnostics.ShouldBeTrue();
        test.Logger.Warnings.ShouldBeEmpty();
        test.Logger.Errors.ShouldBeEmpty();

        test.BlockingLogger.Release.Set();
        service.WaitForLoggingToProcessEvents();
        if (!forwarded)
        {
            test.Logger.Warnings.Count.ShouldBe(demote || promote ? 0 : 1);
            test.Logger.Errors.Count.ShouldBe(promote ? 1 : 0);
            test.Logger.AllBuildEvents.ShouldContain(e =>
                e.Message == "diagnostic" && (demote ? e is BuildMessageEventArgs : true));
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CapturesErrorsSynchronously(bool forwarded)
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        using TestLoggingService test = new(_output);
        using TaskCacheDiagnosticCapture capture = test.Service.CaptureTaskDiagnostics(test.Context);
        BuildErrorEventArgs error = new(null, "CS0001", "test.cs", 1, 1, 1, 1, "diagnostic", null, "Csc")
        {
            BuildEventContext = test.Context
        };

        if (forwarded)
        {
            test.Service.PacketReceived(1, new LogMessagePacket(new KeyValuePair<int, BuildEventArgs>(0, error)));
        }
        else
        {
            test.Service.LogBuildEvent(error);
        }

        capture.HasDiagnostics.ShouldBeTrue();
        capture.HasBlockingDiagnostics.ShouldBeTrue();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ReplayableWarningIsSnapshottedBeforeAsyncMutationAndPolicy(bool promote)
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        using TestLoggingService test = new(_output, LoggerMode.Asynchronous);
        test.Service.WarningsAsErrors = promote ? new HashSet<string> { "CS0001" } : null;
        test.Service.WarningsAsMessages = promote ? null : new HashSet<string> { "CS0001" };
        test.Service.LogBuildEvent(new BuildMessageEventArgs("block", null, "test", MessageImportance.Normal));
        test.BlockingLogger.Entered.Wait(TimeSpan.FromSeconds(30)).ShouldBeTrue();
        using TaskCacheDiagnosticCapture capture = test.Service.CaptureTaskDiagnostics(test.Context);
        capture.BeginReplayablePhase();
        MutableArgument argument = new();
        BuildWarningEventArgs warning = new("subcategory", "CS0001", "test.cs", 1, 2, 3, 4,
            "message {0}", "help", "sender", "https://example.invalid/help", DateTime.UtcNow, argument)
        {
            BuildEventContext = test.Context,
        };
        test.Service.LogBuildEvent(warning);
        argument.Value = "mutated";
        warning.BuildEventContext = BuildEventContext.Invalid;
        capture.HasDiagnostics.ShouldBeTrue();
        capture.HasBlockingDiagnostics.ShouldBeFalse();
        TaskCacheWarning[] warnings = capture.GetWarnings();
        warnings.Length.ShouldBe(1);
        warnings[0].Message.ShouldBe("message original");
        warnings[0].ToEvent(test.Context).BuildEventContext.ShouldBe(test.Context);
        // Restore context before releasing the unrelated asynchronous logging test.
        warning.BuildEventContext = test.Context;
        test.BlockingLogger.Release.Set();
    }

    [Fact]
    public void CleanupWarningBlocksPublicationWithoutJoiningReplayPayload()
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        using TestLoggingService test = new(_output);
        using TaskCacheDiagnosticCapture capture = test.Service.CaptureTaskDiagnostics(test.Context);
        capture.BeginReplayablePhase();
        test.Service.LogBuildEvent(Warning(test.Context));
        capture.HasBlockingDiagnostics.ShouldBeFalse();
        capture.EndReplayablePhase();
        test.Service.LogBuildEvent(Warning(test.Context));
        capture.HasBlockingDiagnostics.ShouldBeTrue();
        capture.GetWarnings().Length.ShouldBe(1);
        test.Logger.Warnings.Count.ShouldBe(2);
    }

    [Fact]
    public void TaskHostEndsReplayCaptureBeforeInvokingFactoryCleanup()
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        using TestLoggingService test = new(_output);
        using TaskCacheDiagnosticCapture capture = test.Service.CaptureTaskDiagnostics(test.Context);
        using TaskExecutionHost host = new();
        CleanupFactory factory = new(() => test.Service.LogBuildEvent(Warning(test.Context)));
        host._UNITTESTONLY_TaskFactoryWrapper = new TaskFactoryWrapper(factory, null!, "Cleanup", TaskHostParameters.Empty);
        typeof(TaskExecutionHost).GetProperty("TaskInstance", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(host, new DeclaredIOEchoTask());
        capture.BeginReplayablePhase();
        host.CleanupForBatch(diagnostics: capture);
        capture.HasBlockingDiagnostics.ShouldBeTrue();
        capture.GetWarnings().ShouldBeEmpty();
        test.Logger.Warnings.Count.ShouldBe(1);
    }

    [Fact]
    public void RawErrorNotificationCannotBecomeReplayableAfterContinueOnError()
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        using TestLoggingService test = new(_output);
        using TaskCacheDiagnosticCapture capture = test.Service.CaptureTaskDiagnostics(test.Context);
        capture.BeginReplayablePhase();
        test.Service.RecordTaskError(test.Context);
        test.Service.LogBuildEvent(Warning(test.Context));
        capture.HasBlockingDiagnostics.ShouldBeTrue();
        capture.GetWarnings().ShouldBeEmpty();
    }

    [Fact]
    public void RawErrorBlocksEvenInsideReplayablePhase()
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        using TestLoggingService test = new(_output);
        using TaskCacheDiagnosticCapture capture = test.Service.CaptureTaskDiagnostics(test.Context);
        capture.BeginReplayablePhase();
        test.Service.LogBuildEvent(new BuildErrorEventArgs(null, "ERR1", "test.cs", 1, 1, 1, 1, "error", null, "task")
        {
            BuildEventContext = test.Context,
        });
        capture.HasBlockingDiagnostics.ShouldBeTrue();
        capture.GetWarnings().ShouldBeEmpty();
    }

    [Fact]
    public void TooManyWarningsDisablePublicationButDoNotSuppressLogging()
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        using TestLoggingService test = new(_output);
        using TaskCacheDiagnosticCapture capture = test.Service.CaptureTaskDiagnostics(test.Context);
        capture.BeginReplayablePhase();
        for (int i = 0; i <= TaskCacheWarning.MaximumCount; i++)
        {
            test.Service.LogBuildEvent(Warning(test.Context));
        }
        capture.HasBlockingDiagnostics.ShouldBeTrue();
        capture.GetWarnings().Length.ShouldBe(TaskCacheWarning.MaximumCount);
        test.Logger.Warnings.Count.ShouldBe(TaskCacheWarning.MaximumCount + 1);
    }

    private sealed class MutableArgument
    {
        internal string Value = "original";
        public override string ToString() => Value;
    }

    private sealed class CleanupFactory(Action cleanup) : ITaskFactory
    {
        public string FactoryName => "Cleanup";
        public Type TaskType => typeof(DeclaredIOEchoTask);
        public bool Initialize(string taskName, IDictionary<string, TaskPropertyInfo> parameterGroup, string taskBody, IBuildEngine taskFactoryLoggingHost) => true;
        public TaskPropertyInfo[] GetTaskParameters() => [];
        public ITask CreateTask(IBuildEngine taskFactoryLoggingHost) => new DeclaredIOEchoTask();
        public void CleanupTask(ITask task) => cleanup();
    }

    [Fact]
    public void MatchesNodeProjectTargetAndTask()
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        using TestLoggingService test = new(_output);
        using TaskCacheDiagnosticCapture capture = test.Service.CaptureTaskDiagnostics(test.Context);
        BuildEventContext context = test.Context;
        test.Service.ProcessLoggingEvent(Warning(new BuildEventContext(context.NodeId + 1, context.TargetId, context.ProjectContextId, 1)));
        test.Service.ProcessLoggingEvent(Warning(new BuildEventContext(context.NodeId, context.TargetId, context.ProjectContextId + 1, 1)));
        test.Service.ProcessLoggingEvent(Warning(new BuildEventContext(context.NodeId, context.TargetId + 1, context.ProjectContextId, 1)));
        test.Service.ProcessLoggingEvent(Warning(null));
        test.Service.ProcessLoggingEvent(new BuildMessageEventArgs("message", null, "test", MessageImportance.Normal)
        {
            BuildEventContext = context
        });
        test.Service.ProcessLoggingEvent(Warning(new BuildEventContext(context.NodeId, context.TargetId, context.ProjectContextId, 123)));
        capture.HasDiagnostics.ShouldBeFalse();

        test.Service.ProcessLoggingEvent(Warning(context));
        capture.HasDiagnostics.ShouldBeTrue();
    }

    [Fact]
    public void OverlappingCapturesDisposeIndependentlyAndRetainTheirResult()
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        using TestLoggingService test = new(_output);
        using TaskCacheDiagnosticCapture first = test.Service.CaptureTaskDiagnostics(test.Context);
        using TaskCacheDiagnosticCapture second = test.Service.CaptureTaskDiagnostics(test.Context);
        first.Dispose();
        first.Dispose();
        test.Service.LogBuildEvent(Warning(test.Context));
        first.HasDiagnostics.ShouldBeFalse();
        second.HasDiagnostics.ShouldBeTrue();
        second.Dispose();
        second.HasDiagnostics.ShouldBeTrue();

        using TaskCacheDiagnosticCapture subsequent = test.Service.CaptureTaskDiagnostics(test.Context);
        subsequent.HasDiagnostics.ShouldBeFalse();
        test.Service.LogBuildEvent(Warning(test.Context));
        subsequent.HasDiagnostics.ShouldBeTrue();
    }

    [Fact]
    public void ConcurrentCapturesAndDiagnosticsDoNotLoseRegistrations()
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        using TestLoggingService test = new(_output);
        using TaskCacheDiagnosticCapture survivor = test.Service.CaptureTaskDiagnostics(test.Context);

        Parallel.For(0, 32, i =>
        {
            BuildEventContext context = new(test.Context.NodeId, i + 10, test.Context.ProjectContextId, i);
            using TaskCacheDiagnosticCapture capture = test.Service.CaptureTaskDiagnostics(context);
            test.Service.LogBuildEvent(Warning(context));
            capture.HasDiagnostics.ShouldBeTrue();
        });

        survivor.HasDiagnostics.ShouldBeFalse();
        Parallel.For(0, 32, _ => test.Service.LogBuildEvent(Warning(test.Context)));
        survivor.HasDiagnostics.ShouldBeTrue();
    }

    [Fact]
    public void RejectsMissingTaskContext()
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        using TestLoggingService test = new(_output);
        Should.Throw<ArgumentNullException>(() => test.Service.CaptureTaskDiagnostics(null!));
        Should.Throw<ArgumentException>(() => test.Service.CaptureTaskDiagnostics(BuildEventContext.Invalid));
        Should.Throw<ArgumentException>(() => test.Service.CaptureTaskDiagnostics(
            new BuildEventContext(test.Context.NodeId, test.Context.TargetId, test.Context.ProjectContextId, BuildEventContext.InvalidTaskId)));
    }

    private static BuildWarningEventArgs Warning(BuildEventContext? context) =>
        new(null, "CS0001", "test.cs", 1, 1, 1, 1, "diagnostic", null, "Csc")
        {
            BuildEventContext = context
        };

    private sealed class TestLoggingService : IDisposable
    {
        public LoggingService Service { get; }
        public BuildEventContext Context { get; }
        public MockLogger Logger { get; }
        public BlockingLogger BlockingLogger { get; } = new();

        public TestLoggingService(ITestOutputHelper output, LoggerMode mode = LoggerMode.Synchronous)
        {
            MockHost host = new();
            BuildRequestData request = new("test.proj", new Dictionary<string, string?>(), "Current", ["Build"], null);
            ((ConfigCache)host.GetComponent(BuildComponentType.ConfigCache)).AddConfiguration(new BuildRequestConfiguration(1, request, request.ExplicitlySpecifiedToolsVersion));
            Service = (LoggingService)LoggingService.CreateLoggingService(mode, 1);
            Service.InitializeComponent(host);
            Logger = new MockLogger(output);
            Service.RegisterLogger(Logger);
            if (mode == LoggerMode.Asynchronous)
            {
                Service.RegisterLogger(BlockingLogger);
            }

            BuildEventContext project = Service.LogProjectStarted(
                new BuildEventContext(1, 1, 1, 1), 0, 1, BuildEventContext.Invalid, "test.proj", "Build", [], []);
            Context = new BuildEventContext(project.SubmissionId, project.NodeId, project.ProjectInstanceId, project.ProjectContextId, 1, 1);
        }

        public void Dispose()
        {
            BlockingLogger.Release.Set();
            Service.ShutdownComponent();
            BlockingLogger.Entered.Dispose();
            BlockingLogger.Release.Dispose();
        }
    }

    private sealed class BlockingLogger : ILogger
    {
        public ManualResetEventSlim Entered { get; } = new();
        public ManualResetEventSlim Release { get; } = new();
        public LoggerVerbosity Verbosity { get; set; } = LoggerVerbosity.Diagnostic;
        public string? Parameters { get; set; }

        public void Initialize(IEventSource eventSource)
        {
            eventSource.MessageRaised += (_, e) =>
            {
                if (e.Message == "block")
                {
                    Entered.Set();
                    Release.Wait(TimeSpan.FromSeconds(30));
                }
            };
        }

        public void Shutdown()
        {
        }
    }
}
