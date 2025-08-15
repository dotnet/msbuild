// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using Microsoft.Build.Framework;
using Microsoft.Build.Framework.Telemetry;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Logging;


/// <summary>
/// The ReusableLogger wraps a <see cref="ILogger"/> and allows it to be used for both design-time and build-time.  It internally swaps
/// between the design-time and build-time event sources in response to <see cref="ILogger.Initialize"/> and <see cref="ILogger.Shutdown"/> events.
/// Use this if you'd like to provide the same instance of an <see cref="ILogger"/> to both a
/// <see cref="Evaluation.ProjectCollection"/>/<see cref="Evaluation.Project"/> _and_
/// directly during a call to one of the <see cref="ILogger"/>-accepting overloads of <see cref="Evaluation.Project.Build()"/>.
/// </summary>
/// <remarks>
/// This class needs to always implement the most-recent IEventSource interface so that it doesn't act as a limiter on the capabilities
/// of ILoggers passed to it.
/// </remarks>
[DebuggerDisplay("{OriginalLogger}")]
internal class ReusableLogger : INodeLogger, IEventSource5
{
    /// <summary>
    /// The logger we are wrapping.
    /// </summary>
    private readonly ILogger _originalLogger;

    /// <summary>
    /// Returns the logger we are wrapping.
    /// </summary>
    internal ILogger OriginalLogger => _originalLogger;

    /// <summary>
    /// The design-time event source
    /// </summary>
    private IEventSource? _designTimeEventSource;

    /// <summary>
    /// The build-time event source
    /// </summary>
    private IEventSource? _buildTimeEventSource;

    /// <summary>
    /// The Any event handler
    /// </summary>
    private AnyEventHandler? _anyEventHandler;

    /// <summary>
    /// The BuildFinished event handler
    /// </summary>
    private BuildFinishedEventHandler? _buildFinishedEventHandler;

    /// <summary>
    /// The BuildStarted event handler
    /// </summary>
    private BuildStartedEventHandler? _buildStartedEventHandler;

    /// <summary>
    /// The Custom event handler
    /// </summary>
    private CustomBuildEventHandler? _customBuildEventHandler;

    /// <summary>
    /// The Error event handler
    /// </summary>
    private BuildErrorEventHandler? _buildErrorEventHandler;

    /// <summary>
    /// The Message event handler
    /// </summary>
    private BuildMessageEventHandler? _buildMessageEventHandler;

    /// <summary>
    /// The ProjectFinished event handler
    /// </summary>
    private ProjectFinishedEventHandler? _projectFinishedEventHandler;

    /// <summary>
    /// The ProjectStarted event handler
    /// </summary>
    private ProjectStartedEventHandler? _projectStartedEventHandler;

    /// <summary>
    /// The Status event handler
    /// </summary>
    private BuildStatusEventHandler? _buildStatusEventHandler;

    /// <summary>
    /// The TargetFinished event handler
    /// </summary>
    private TargetFinishedEventHandler? _targetFinishedEventHandler;

    /// <summary>
    /// The TargetStarted event handler
    /// </summary>
    private TargetStartedEventHandler? _targetStartedEventHandler;

    /// <summary>
    /// The TaskFinished event handler
    /// </summary>
    private TaskFinishedEventHandler? _taskFinishedEventHandler;

    /// <summary>
    /// The TaskStarted event handler
    /// </summary>
    private TaskStartedEventHandler? _taskStartedEventHandler;

    /// <summary>
    /// The Warning event handler
    /// </summary>
    private BuildWarningEventHandler? _buildWarningEventHandler;

    /// <summary>
    ///  The telemetry event handler.
    /// </summary>
    private TelemetryEventHandler? _telemetryEventHandler;

    /// <summary>
    /// The worker node telemetry logged event handler.
    /// </summary>
    private WorkerNodeTelemetryEventHandler? _workerNodeTelemetryLoggedHandler;

    private bool _includeEvaluationMetaprojects;

    private bool _includeEvaluationProfiles;

    private bool _includeTaskInputs;

    private bool _includeEvaluationPropertiesAndItems;

    public ReusableLogger(ILogger? originalLogger)
    {
        ErrorUtilities.VerifyThrowArgumentNull(originalLogger);
        _originalLogger = originalLogger!;
    }

    #region IEventSource Members

    /// <summary>
    /// The Message logging event
    /// </summary>
    public event BuildMessageEventHandler? MessageRaised;

    /// <summary>
    /// The Error logging event
    /// </summary>
    public event BuildErrorEventHandler? ErrorRaised;

    /// <summary>
    /// The Warning logging event
    /// </summary>
    public event BuildWarningEventHandler? WarningRaised;

    /// <summary>
    /// The BuildStarted logging event
    /// </summary>
    public event BuildStartedEventHandler? BuildStarted;

    /// <summary>
    /// The BuildFinished logging event
    /// </summary>
    public event BuildFinishedEventHandler? BuildFinished;

    /// <summary>
    /// The BuildCanceled logging event
    /// </summary>
    public event BuildCanceledEventHandler? BuildCanceled;

    /// <summary>
    /// The ProjectStarted logging event
    /// </summary>
    public event ProjectStartedEventHandler? ProjectStarted;

    /// <summary>
    /// The ProjectFinished logging event
    /// </summary>
    public event ProjectFinishedEventHandler? ProjectFinished;

    /// <summary>
    /// The TargetStarted logging event
    /// </summary>
    public event TargetStartedEventHandler? TargetStarted;

    /// <summary>
    /// The TargetFinished logging event
    /// </summary>
    public event TargetFinishedEventHandler? TargetFinished;

    /// <summary>
    /// The TaskStarted logging event
    /// </summary>
    public event TaskStartedEventHandler? TaskStarted;

    /// <summary>
    /// The TaskFinished logging event
    /// </summary>
    public event TaskFinishedEventHandler? TaskFinished;

    /// <summary>
    /// The Custom logging event
    /// </summary>
    public event CustomBuildEventHandler? CustomEventRaised;

    /// <summary>
    /// The Status logging event
    /// </summary>
    public event BuildStatusEventHandler? StatusEventRaised;

    /// <summary>
    /// The Any logging event
    /// </summary>
    public event AnyEventHandler? AnyEventRaised;

    /// <summary>
    /// The telemetry sent event.
    /// </summary>
    public event TelemetryEventHandler? TelemetryLogged;

    /// <summary>
    /// The worker node telemetry logged event.
    /// </summary>
    public event WorkerNodeTelemetryEventHandler? WorkerNodeTelemetryLogged;

    /// <summary>
    /// Should evaluation events include generated metaprojects?
    /// </summary>
    public void IncludeEvaluationMetaprojects()
    {
        if (_buildTimeEventSource is IEventSource3 buildEventSource3)
        {
            buildEventSource3.IncludeEvaluationMetaprojects();
        }

        if (_designTimeEventSource is IEventSource3 designTimeEventSource3)
        {
            designTimeEventSource3.IncludeEvaluationMetaprojects();
        }

        _includeEvaluationMetaprojects = true;
    }

    /// <summary>
    /// Should evaluation events include profiling information?
    /// </summary>
    public void IncludeEvaluationProfiles()
    {
        if (_buildTimeEventSource is IEventSource3 buildEventSource3)
        {
            buildEventSource3.IncludeEvaluationProfiles();
        }

        if (_designTimeEventSource is IEventSource3 designTimeEventSource3)
        {
            designTimeEventSource3.IncludeEvaluationProfiles();
        }

        _includeEvaluationProfiles = true;
    }

    /// <summary>
    /// Should task events include task inputs?
    /// </summary>
    public void IncludeTaskInputs()
    {
        if (_buildTimeEventSource is IEventSource3 buildEventSource3)
        {
            buildEventSource3.IncludeTaskInputs();
        }

        if (_designTimeEventSource is IEventSource3 designTimeEventSource3)
        {
            designTimeEventSource3.IncludeTaskInputs();
        }

        _includeTaskInputs = true;
    }

    public void IncludeEvaluationPropertiesAndItems()
    {
        if (_buildTimeEventSource is IEventSource4 buildEventSource4)
        {
            buildEventSource4.IncludeEvaluationPropertiesAndItems();
        }

        if (_designTimeEventSource is IEventSource4 designTimeEventSource4)
        {
            designTimeEventSource4.IncludeEvaluationPropertiesAndItems();
        }

        _includeEvaluationPropertiesAndItems = true;
    }

    #endregion

    #region ILogger Members

    /// <summary>
    /// The logger verbosity
    /// </summary>
    public LoggerVerbosity Verbosity
    {
        get => _originalLogger.Verbosity;
        set => _originalLogger.Verbosity = value;
    }

    /// <summary>
    /// The logger parameters
    /// </summary>
    public string? Parameters
    {
        get => _originalLogger.Parameters;

        set => _originalLogger.Parameters = value;
    }

    /// <summary>
    /// If we haven't yet been initialized, we register for design time events and initialize the logger we are holding.
    /// If we are in design-time mode already, we unregister and transition over to build-time mode.
    /// </summary>
    public void Initialize(IEventSource eventSource, int nodeCount)
    {
        if (_designTimeEventSource == null)
        {
            _designTimeEventSource = eventSource;
            RegisterForEvents(_designTimeEventSource);

            if (_originalLogger is INodeLogger logger)
            {
                logger.Initialize(this, nodeCount);
            }
            else
            {
                _originalLogger.Initialize(this);
            }
        }
        else
        {
            ErrorUtilities.VerifyThrow(_buildTimeEventSource == null, "Already registered for build-time.");
            _buildTimeEventSource = eventSource;
            UnregisterForEvents(_designTimeEventSource);
            RegisterForEvents(_buildTimeEventSource);
        }
    }

    /// <summary>
    /// If we haven't yet been initialized, we register for design time events and initialize the logger we are holding.
    /// If we are in design-time mode
    /// </summary>
    public void Initialize(IEventSource eventSource) => Initialize(eventSource, 1);

    /// <summary>
    /// If we are in build-time mode, we unregister for build-time events and re-register for design-time events.
    /// If we are in design-time mode, we unregister for design-time events and shut down the logger we are holding.
    /// </summary>
    /// <remarks>
    /// Invariant: one of _buildTimeEventSource or _designTimeEventSource must be non-null.
    /// </remarks>
    public void Shutdown()
    {
        if (_buildTimeEventSource != null)
        {
            UnregisterForEvents(_buildTimeEventSource);
            RegisterForEvents(_designTimeEventSource!);
            _buildTimeEventSource = null;
        }
        else
        {
            ErrorUtilities.VerifyThrow(_designTimeEventSource != null, "Already unregistered for design-time.");
            UnregisterForEvents(_designTimeEventSource!);
            _originalLogger.Shutdown();
        }
    }

    #endregion

    /// <summary>
    /// Registers for all of the events on the specified event source.
    /// </summary>
    private void RegisterForEvents(IEventSource eventSource)
    {
        // Create the handlers.
        _anyEventHandler = AnyEventRaisedHandler;
        _buildFinishedEventHandler = BuildFinishedHandler;
        _buildStartedEventHandler = BuildStartedHandler;
        _customBuildEventHandler = CustomEventRaisedHandler;
        _buildErrorEventHandler = ErrorRaisedHandler;
        _buildMessageEventHandler = MessageRaisedHandler;
        _projectFinishedEventHandler = ProjectFinishedHandler;
        _projectStartedEventHandler = ProjectStartedHandler;
        _buildStatusEventHandler = StatusEventRaisedHandler;
        _targetFinishedEventHandler = TargetFinishedHandler;
        _targetStartedEventHandler = TargetStartedHandler;
        _taskFinishedEventHandler = TaskFinishedHandler;
        _taskStartedEventHandler = TaskStartedHandler;
        _buildWarningEventHandler = WarningRaisedHandler;
        _telemetryEventHandler = TelemetryLoggedHandler;
        _workerNodeTelemetryLoggedHandler = WorkerNodeTelemetryLoggedHandler;

        // Register for the events.
        eventSource.AnyEventRaised += _anyEventHandler;
        eventSource.BuildFinished += _buildFinishedEventHandler;
        eventSource.BuildStarted += _buildStartedEventHandler;
        eventSource.CustomEventRaised += _customBuildEventHandler;
        eventSource.ErrorRaised += _buildErrorEventHandler;
        eventSource.MessageRaised += _buildMessageEventHandler;
        eventSource.ProjectFinished += _projectFinishedEventHandler;
        eventSource.ProjectStarted += _projectStartedEventHandler;
        eventSource.StatusEventRaised += _buildStatusEventHandler;
        eventSource.TargetFinished += _targetFinishedEventHandler;
        eventSource.TargetStarted += _targetStartedEventHandler;
        eventSource.TaskFinished += _taskFinishedEventHandler;
        eventSource.TaskStarted += _taskStartedEventHandler;
        eventSource.WarningRaised += _buildWarningEventHandler;

        if (eventSource is IEventSource2 eventSource2)
        {
            eventSource2.TelemetryLogged += _telemetryEventHandler;
        }

        if (eventSource is IEventSource3 eventSource3)
        {
            if (_includeEvaluationMetaprojects)
            {
                eventSource3.IncludeEvaluationMetaprojects();
            }

            if (_includeEvaluationProfiles)
            {
                eventSource3.IncludeEvaluationProfiles();
            }

            if (_includeTaskInputs)
            {
                eventSource3.IncludeTaskInputs();
            }
        }

        if (eventSource is IEventSource4 eventSource4)
        {
            if (_includeEvaluationPropertiesAndItems)
            {
                eventSource4.IncludeEvaluationPropertiesAndItems();
            }
        }

        if (eventSource is IEventSource5 eventSource5)
        {
            eventSource5.WorkerNodeTelemetryLogged += _workerNodeTelemetryLoggedHandler;
        }
    }

    /// <summary>
    /// Unregisters for all events on the specified event source.
    /// </summary>
    private void UnregisterForEvents(IEventSource eventSource)
    {
        // Unregister for the events.
        eventSource.AnyEventRaised -= _anyEventHandler;
        eventSource.BuildFinished -= _buildFinishedEventHandler;
        eventSource.BuildStarted -= _buildStartedEventHandler;
        eventSource.CustomEventRaised -= _customBuildEventHandler;
        eventSource.ErrorRaised -= _buildErrorEventHandler;
        eventSource.MessageRaised -= _buildMessageEventHandler;
        eventSource.ProjectFinished -= _projectFinishedEventHandler;
        eventSource.ProjectStarted -= _projectStartedEventHandler;
        eventSource.StatusEventRaised -= _buildStatusEventHandler;
        eventSource.TargetFinished -= _targetFinishedEventHandler;
        eventSource.TargetStarted -= _targetStartedEventHandler;
        eventSource.TaskFinished -= _taskFinishedEventHandler;
        eventSource.TaskStarted -= _taskStartedEventHandler;
        eventSource.WarningRaised -= _buildWarningEventHandler;

        if (eventSource is IEventSource2 eventSource2)
        {
            eventSource2.TelemetryLogged -= _telemetryEventHandler;
        }

        if (eventSource is IEventSource5 eventSource5)
        {
            eventSource5.WorkerNodeTelemetryLogged -= _workerNodeTelemetryLoggedHandler;
        }

        // Null out the handlers.
        _anyEventHandler = null;
        _buildFinishedEventHandler = null;
        _buildStartedEventHandler = null;
        _customBuildEventHandler = null;
        _buildErrorEventHandler = null;
        _buildMessageEventHandler = null;
        _projectFinishedEventHandler = null;
        _projectStartedEventHandler = null;
        _buildStatusEventHandler = null;
        _targetFinishedEventHandler = null;
        _targetStartedEventHandler = null;
        _taskFinishedEventHandler = null;
        _taskStartedEventHandler = null;
        _buildWarningEventHandler = null;
        _telemetryEventHandler = null;
        _workerNodeTelemetryLoggedHandler = null;
    }

    /// <summary>
    /// Handler for Warning events.
    /// </summary>
    private void WarningRaisedHandler(object sender, BuildWarningEventArgs e) => WarningRaised?.Invoke(sender, e);

    /// <summary>
    /// Handler for TaskStarted events.
    /// </summary>
    private void TaskStartedHandler(object sender, TaskStartedEventArgs e) => TaskStarted?.Invoke(sender, e);

    /// <summary>
    /// Handler for TaskFinished events.
    /// </summary>
    private void TaskFinishedHandler(object sender, TaskFinishedEventArgs e) => TaskFinished?.Invoke(sender, e);

    /// <summary>
    /// Handler for TargetStarted events.
    /// </summary>
    private void TargetStartedHandler(object sender, TargetStartedEventArgs e) => TargetStarted?.Invoke(sender, e);

    /// <summary>
    /// Handler for TargetFinished events.
    /// </summary>
    private void TargetFinishedHandler(object sender, TargetFinishedEventArgs e) => TargetFinished?.Invoke(sender, e);

    /// <summary>
    /// Handler for Status events.
    /// </summary>
    private void StatusEventRaisedHandler(object sender, BuildStatusEventArgs e) => StatusEventRaised?.Invoke(sender, e);

    /// <summary>
    /// Handler for ProjectStarted events.
    /// </summary>
    private void ProjectStartedHandler(object sender, ProjectStartedEventArgs e) => ProjectStarted?.Invoke(sender, e);

    /// <summary>
    /// Handler for ProjectFinished events.
    /// </summary>
    private void ProjectFinishedHandler(object sender, ProjectFinishedEventArgs e) => ProjectFinished?.Invoke(sender, e);

    /// <summary>
    /// Handler for Message events.
    /// </summary>
    private void MessageRaisedHandler(object sender, BuildMessageEventArgs e) => MessageRaised?.Invoke(sender, e);

    /// <summary>
    /// Handler for Error events.
    /// </summary>
    private void ErrorRaisedHandler(object sender, BuildErrorEventArgs e) => ErrorRaised?.Invoke(sender, e);

    /// <summary>
    /// Handler for Custom events.
    /// </summary>
    private void CustomEventRaisedHandler(object sender, CustomBuildEventArgs e) => CustomEventRaised?.Invoke(sender, e);

    /// <summary>
    /// Handler for BuildStarted events.
    /// </summary>
    private void BuildStartedHandler(object sender, BuildStartedEventArgs e) => BuildStarted?.Invoke(sender, e);

    /// <summary>
    /// Handler for BuildFinished events.
    /// </summary>
    private void BuildFinishedHandler(object sender, BuildFinishedEventArgs e) => BuildFinished?.Invoke(sender, e);

    /// <summary>
    /// Handler for BuildCanceled events.
    /// </summary>
    private void BuildCanceledHandler(object sender, BuildCanceledEventArgs e) => BuildCanceled?.Invoke(sender, e);

    /// <summary>
    /// Handler for Any events.
    /// </summary>
    private void AnyEventRaisedHandler(object sender, BuildEventArgs e) => AnyEventRaised?.Invoke(sender, e);

    /// <summary>
    /// Handler for telemetry events.
    /// </summary>
    private void TelemetryLoggedHandler(object sender, TelemetryEventArgs e) => TelemetryLogged?.Invoke(sender, e);

    /// <summary>
    /// Handler for worker node telemetry logged events.
    /// </summary>
    private void WorkerNodeTelemetryLoggedHandler(object? sender, WorkerNodeTelemetryEventArgs e) => WorkerNodeTelemetryLogged?.Invoke(sender, e);
}
