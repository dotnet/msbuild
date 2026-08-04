// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Build.BackEnd;
using Microsoft.Build.Experimental.BuildCheck;
using Microsoft.Build.Framework;
using Microsoft.Build.Framework.Telemetry;

#nullable disable

namespace Microsoft.Build.Shared
{
    #region Enumerations
    /// <summary>
    /// An enumeration of all the types of BuildEventArgs that can be
    /// packaged by this logMessagePacket
    /// </summary>
    /// <remarks>
    /// Several of these values must be kept in sync with MSBuildTaskHost's LoggingEventType.
    /// </remarks>
    internal enum LoggingEventType : int
    {
        /// <summary>
        /// An invalid eventId, used during initialization of a <see cref="LoggingEventType"/>.
        /// </summary>
        Invalid = -1,

        /// <summary>
        /// Event is a CustomEventArgs.
        /// </summary>
        CustomEvent = 0,

        /// <summary>
        /// Event is a <see cref="BuildErrorEventArgs"/>.
        /// </summary>
        BuildErrorEvent = 1,

        /// <summary>
        /// Event is a <see cref="BuildFinishedEventArgs"/>.
        /// </summary>
        BuildFinishedEvent = 2,

        /// <summary>
        /// Event is a <see cref="BuildMessageEventArgs"/>.
        /// </summary>
        BuildMessageEvent = 3,

        /// <summary>
        /// Event is a <see cref="BuildStartedEventArgs"/>.
        /// </summary>
        BuildStartedEvent = 4,

        /// <summary>
        /// Event is a <see cref="BuildWarningEventArgs"/>.
        /// </summary>
        BuildWarningEvent = 5,

        /// <summary>
        /// Event is a <see cref="ProjectFinishedEventArgs"/>.
        /// </summary>
        ProjectFinishedEvent = 6,

        /// <summary>
        /// Event is a <see cref="ProjectStartedEventArgs"/>.
        /// </summary>
        ProjectStartedEvent = 7,

        /// <summary>
        /// Event is a <see cref="TargetStartedEventArgs"/>.
        /// </summary>
        TargetStartedEvent = 8,

        /// <summary>
        /// Event is a <see cref="TargetFinishedEventArgs"/>.
        /// </summary>
        TargetFinishedEvent = 9,

        /// <summary>
        /// Event is a <see cref="TaskStartedEventArgs"/>.
        /// </summary>
        TaskStartedEvent = 10,

        /// <summary>
        /// Event is a <see cref="TaskFinishedEventArgs"/>.
        /// </summary>
        TaskFinishedEvent = 11,

        /// <summary>
        /// Event is a <see cref="TaskCommandLineEventArgs"/>.
        /// </summary>
        TaskCommandLineEvent = 12,

        /// <summary>
        /// Event is a <see cref="TaskParameterEventArgs"/>.
        /// </summary>
        TaskParameterEvent = 13,

        /// <summary>
        /// Event is a <see cref="ProjectEvaluationStartedEventArgs"/>.
        /// </summary>
        ProjectEvaluationStartedEvent = 14,

        /// <summary>
        /// Event is a <see cref="ProjectEvaluationFinishedEventArgs"/>.
        /// </summary>
        ProjectEvaluationFinishedEvent = 15,

        /// <summary>
        /// Event is a <see cref="ProjectImportedEventArgs"/>.
        /// </summary>
        ProjectImportedEvent = 16,

        /// <summary>
        /// Event is a <see cref="TargetSkippedEventArgs"/>.
        /// </summary>
        TargetSkipped = 17,

        /// <summary>
        /// Event is a <see cref="TelemetryEventArgs"/>.
        /// </summary>
        Telemetry = 18,

        /// <summary>
        /// Event is an <see cref="EnvironmentVariableReadEventArgs"/>.
        /// </summary>
        EnvironmentVariableReadEvent = 19,

        /// <summary>
        /// Event is a <see cref="ResponseFileUsedEventArgs"/>.
        /// </summary>
        ResponseFileUsedEvent = 20,

        /// <summary>
        /// Event is an <see cref="AssemblyLoadBuildEventArgs"/>.
        /// </summary>
        AssemblyLoadEvent = 21,

        /// <summary>
        /// Event is <see cref="ExternalProjectStartedEventArgs"/>.
        /// </summary>
        ExternalProjectStartedEvent = 22,

        /// <summary>
        /// Event is <see cref="ExternalProjectFinishedEventArgs"/>.
        /// </summary>
        ExternalProjectFinishedEvent = 23,

        /// <summary>
        /// Event is <see cref="ExtendedCustomBuildEventArgs"/>.
        /// </summary>
        ExtendedCustomEvent = 24,

        /// <summary>
        /// Event is <see cref="ExtendedBuildErrorEventArgs"/>.
        /// </summary>
        ExtendedBuildErrorEvent = 25,

        /// <summary>
        /// Event is <see cref="ExtendedBuildWarningEventArgs"/>.
        /// </summary>
        ExtendedBuildWarningEvent = 26,

        /// <summary>
        /// Event is <see cref="ExtendedBuildMessageEventArgs"/>.
        /// </summary>
        ExtendedBuildMessageEvent = 27,

        /// <summary>
        /// Event is <see cref="CriticalBuildMessageEventArgs"/>.
        /// </summary>
        CriticalBuildMessage = 28,

        /// <summary>
        /// Event is <see cref="MetaprojectGeneratedEventArgs"/>.
        /// </summary>
        MetaprojectGenerated = 29,

        /// <summary>
        /// Event is <see cref="PropertyInitialValueSetEventArgs"/>.
        /// </summary>
        PropertyInitialValueSet = 30,

        /// <summary>
        /// Event is <see cref="PropertyReassignmentEventArgs"/>.
        /// </summary>
        PropertyReassignment = 31,

        /// <summary>
        /// Event is <see cref="UninitializedPropertyReadEventArgs"/>.
        /// </summary>
        UninitializedPropertyRead = 32,

        /// <summary>
        /// Event is <see cref="ExtendedCriticalBuildMessageEventArgs"/>.
        /// </summary>
        ExtendedCriticalBuildMessageEvent = 33,

        /// <summary>
        /// Event is a <see cref="GeneratedFileUsedEventArgs"/>.
        /// </summary>
        GeneratedFileUsedEvent = 34,

        /// <summary>
        /// Event is <see cref="BuildCheckResultMessage"/>.
        /// </summary>
        BuildCheckMessageEvent = 35,

        /// <summary>
        /// Event is <see cref="BuildCheckResultWarning"/>.
        /// </summary>
        BuildCheckWarningEvent = 36,

        /// <summary>
        /// Event is <see cref="BuildCheckResultError"/>.
        /// </summary>
        BuildCheckErrorEvent = 37,

        /// <summary>
        /// Event is <see cref="BuildCheckTracingEventArgs"/>.
        /// </summary>
        BuildCheckTracingEvent = 38,

        /// <summary>
        /// Event is <see cref="BuildCheckAcquisitionEventArgs"/>.
        /// </summary>
        BuildCheckAcquisitionEvent = 39,

        /// <summary>
        /// Event is <see cref="BuildSubmissionStartedEventArgs"/>.
        /// </summary>
        BuildSubmissionStartedEvent = 40,

        /// <summary>
        /// Event is <see cref="BuildCanceledEventArgs"/>
        /// </summary>
        BuildCanceledEvent = 41,

        /// <summary>
        /// Event is <see cref="WorkerNodeTelemetryEventArgs"/>
        /// </summary>
        WorkerNodeTelemetryEvent = 42,

        /// <summary>
        /// Event is <see cref="LoggersRegisteredEventArgs"/>
        /// </summary>
        LoggersRegisteredEvent = 43,

        /// <summary>
        /// Event is <see cref="MSBuildServerLifecycleEventArgs"/>
        /// </summary>
        MSBuildServerLifecycleEvent = 44,
    }
    #endregion

    /// <summary>
    /// A packet to encapsulate a BuildEventArg logging message.
    /// Contents:
    /// Build Event Type
    /// Build Event Args
    /// </summary>
    internal class LogMessagePacketBase : INodePacket
    {
        /// <summary>
        /// The packet version, which is based on the CLR version. Cached because querying Environment.Version each time becomes an allocation bottleneck.
        /// </summary>
        private static readonly int s_defaultPacketVersion = (Environment.Version.Major * 10) + Environment.Version.Minor;

        #region Data

        /// <summary>
        /// The event type of the buildEventArg based on the
        /// LoggingEventType enumeration
        /// </summary>
        private LoggingEventType _eventType = LoggingEventType.Invalid;

        /// <summary>
        /// The buildEventArg which is encapsulated by the packet
        /// </summary>
        private BuildEventArgs _buildEvent;

        /// <summary>
        /// The sink id
        /// </summary>
        private int _sinkId;

        #endregion

        #region Constructors

        /// <summary>
        /// Encapsulates the buildEventArg in this packet.
        /// </summary>
        internal LogMessagePacketBase(KeyValuePair<int, BuildEventArgs>? nodeBuildEvent)
        {
            Assumed.NotNull(nodeBuildEvent, "nodeBuildEvent was null");
            _buildEvent = nodeBuildEvent.Value.Value;
            _sinkId = nodeBuildEvent.Value.Key;
            _eventType = GetLoggingEventId(_buildEvent);
        }

        /// <summary>
        /// Constructor for deserialization
        /// </summary>
        internal LogMessagePacketBase(ITranslator translator) => Translate(translator);

        #endregion

        #region Delegates

        /// <summary>
        /// Delegate representing a method on the BuildEventArgs classes used to read from a stream.
        /// </summary>
        private delegate void ArgsReaderDelegate(BinaryReader reader, int version);

        #endregion

        #region Properties

        /// <summary>
        /// The nodePacket Type, in this case the packet is a Logging Message
        /// </summary>
        public NodePacketType Type
        {
            get { return NodePacketType.LogMessage; }
        }

        /// <summary>
        /// The buildEventArg wrapped by this packet
        /// </summary>
        internal KeyValuePair<int, BuildEventArgs>? NodeBuildEvent
        {
            get
            {
                return new KeyValuePair<int, BuildEventArgs>(_sinkId, _buildEvent);
            }
        }

        /// <summary>
        /// The event type of the wrapped buildEventArg
        /// based on the LoggingEventType enumeration
        /// </summary>
        internal LoggingEventType EventType
        {
            get
            {
                return _eventType;
            }
        }
        #endregion

        #region INodePacket Methods

        /// <summary>
        /// Reads/writes this packet
        /// </summary>
        public void Translate(ITranslator translator)
        {
            translator.TranslateEnum(ref _eventType, (int)_eventType);
            translator.Translate(ref _sinkId);
            if (translator.Mode == TranslationDirection.ReadFromStream)
            {
                ReadFromStream(translator);
            }
            else
            {
                WriteToStream(translator);
            }
        }

        #endregion

        /// <summary>
        /// Writes the logging packet to the translator.
        /// </summary>
        internal void WriteToStream(ITranslator translator)
        {
            Assumed.NotEqual(_eventType, LoggingEventType.CustomEvent, "_eventType should not be a custom event");

            int packetVersion = s_defaultPacketVersion;

            // Make sure the other side knows what sort of serialization is coming
            translator.Translate(ref packetVersion);

            bool eventCanSerializeItself = EventCanSerializeItself(_eventType);

            translator.Translate(ref eventCanSerializeItself);

            if (eventCanSerializeItself)
            {
                // 3.5 or later -- the event has a WriteToStream method, so let it serialize itself.
                // This is a direct virtual call (mirroring ReadFromStream's CreateFromStream call) rather
                // than a reflective lookup, so it is trimming- and Native AOT-safe.
                _buildEvent.WriteToStream(translator.Writer);

                TranslateAdditionalProperties(translator, _eventType, _buildEvent);
            }
            else
            {
                WriteEventToStream(_buildEvent, _eventType, translator);
            }
        }

        /// <summary>
        /// Reads the logging packet from the translator.
        /// </summary>
        internal void ReadFromStream(ITranslator translator)
        {
            Assumed.NotEqual(_eventType, LoggingEventType.CustomEvent, "_eventType should not be a custom event");

            _buildEvent = GetBuildEventArgFromId();

            // The other side is telling us whether the event knows how to log itself, or whether we're going to have
            // to do it manually
            int packetVersion = s_defaultPacketVersion;
            translator.Translate(ref packetVersion);

            bool eventCanSerializeItself = true;
            translator.Translate(ref eventCanSerializeItself);

            if (eventCanSerializeItself)
            {
                _buildEvent.CreateFromStream(translator.Reader, packetVersion);

                TranslateAdditionalProperties(translator, _eventType, _buildEvent);
            }
            else
            {
                _buildEvent = ReadEventFromStream(_eventType, translator);
                Assumed.NotNull(_buildEvent, $"Not Supported LoggingEventType {_eventType}");
            }

            _eventType = GetLoggingEventId(_buildEvent);
        }

        /// <summary>
        /// Returns whether to use the event's own serialization method, if found.
        /// If false, defers to overridable implementations in <see cref="WriteEventToStream"/> and
        /// <see cref="ReadEventFromStream"/>.
        /// </summary>
        /// <remarks>
        /// Every <see cref="BuildEventArgs"/> defines <c>WriteToStream</c> / <c>CreateFromStream</c>, so the
        /// default is to let the event serialize itself. Overrides return <see langword="false"/> for event
        /// types whose inherited <c>WriteToStream</c> should not be used.
        /// </remarks>
        protected virtual bool EventCanSerializeItself(LoggingEventType eventType)
            => true;

        /// <summary>
        /// Translates additional properties that are not handled by the default serialization.
        /// </summary>
        protected virtual void TranslateAdditionalProperties(ITranslator translator, LoggingEventType eventType, BuildEventArgs buildEvent)
        {
        }

        #region Private Methods

        /// <summary>
        /// Takes in a id (LoggingEventType as an int) and creates the correct specific logging class
        /// </summary>
        private BuildEventArgs GetBuildEventArgFromId()
            => _eventType switch
            {
                LoggingEventType.BuildErrorEvent => new BuildErrorEventArgs(null, null, null, -1, -1, -1, -1, null, null, null),
                LoggingEventType.BuildFinishedEvent => new BuildFinishedEventArgs(null, null, false),
                LoggingEventType.BuildMessageEvent => new BuildMessageEventArgs(null, null, null, MessageImportance.Normal),
                LoggingEventType.BuildStartedEvent => new BuildStartedEventArgs(null, null),
                LoggingEventType.BuildWarningEvent => new BuildWarningEventArgs(null, null, null, -1, -1, -1, -1, null, null, null),
                LoggingEventType.ProjectFinishedEvent => new ProjectFinishedEventArgs(null, null, null, false),
                LoggingEventType.ProjectStartedEvent => new ProjectStartedEventArgs(null, null, null, null, null, null),
                LoggingEventType.TargetStartedEvent => new TargetStartedEventArgs(null, null, null, null, null),
                LoggingEventType.TargetFinishedEvent => new TargetFinishedEventArgs(null, null, null, null, null, false),
                LoggingEventType.TaskStartedEvent => new TaskStartedEventArgs(null, null, null, null, null),
                LoggingEventType.TaskFinishedEvent => new TaskFinishedEventArgs(null, null, null, null, null, false),
                LoggingEventType.TaskCommandLineEvent => new TaskCommandLineEventArgs(null, null, MessageImportance.Normal),
                LoggingEventType.ResponseFileUsedEvent => new ResponseFileUsedEventArgs(null),
                LoggingEventType.AssemblyLoadEvent => new AssemblyLoadBuildEventArgs(),
                LoggingEventType.TaskParameterEvent => new TaskParameterEventArgs(0, null, null, true, default),
                LoggingEventType.ProjectEvaluationStartedEvent => new ProjectEvaluationStartedEventArgs(),
                LoggingEventType.ProjectEvaluationFinishedEvent => new ProjectEvaluationFinishedEventArgs(),
                LoggingEventType.ProjectImportedEvent => new ProjectImportedEventArgs(),
                LoggingEventType.TargetSkipped => new TargetSkippedEventArgs(),
                LoggingEventType.Telemetry => new TelemetryEventArgs(),
                LoggingEventType.ExtendedCustomEvent => new ExtendedCustomBuildEventArgs(),
                LoggingEventType.ExtendedBuildErrorEvent => new ExtendedBuildErrorEventArgs(),
                LoggingEventType.ExtendedBuildWarningEvent => new ExtendedBuildWarningEventArgs(),
                LoggingEventType.ExtendedBuildMessageEvent => new ExtendedBuildMessageEventArgs(),
                LoggingEventType.ExtendedCriticalBuildMessageEvent => new ExtendedCriticalBuildMessageEventArgs(),
                LoggingEventType.ExternalProjectStartedEvent => new ExternalProjectStartedEventArgs(null, null, null, null, null),
                LoggingEventType.ExternalProjectFinishedEvent => new ExternalProjectFinishedEventArgs(null, null, null, null, false),
                LoggingEventType.CriticalBuildMessage => new CriticalBuildMessageEventArgs(null, null, null, -1, -1, -1, -1, null, null, null),
                LoggingEventType.MetaprojectGenerated => new MetaprojectGeneratedEventArgs(null, null, null),
                LoggingEventType.PropertyInitialValueSet => new PropertyInitialValueSetEventArgs(),
                LoggingEventType.PropertyReassignment => new PropertyReassignmentEventArgs(),
                LoggingEventType.UninitializedPropertyRead => new UninitializedPropertyReadEventArgs(),
                LoggingEventType.GeneratedFileUsedEvent => new GeneratedFileUsedEventArgs(),
                LoggingEventType.BuildCheckMessageEvent => new BuildCheckResultMessage(),
                LoggingEventType.BuildCheckWarningEvent => new BuildCheckResultWarning(),
                LoggingEventType.BuildCheckErrorEvent => new BuildCheckResultError(),
                LoggingEventType.BuildCheckAcquisitionEvent => new BuildCheckAcquisitionEventArgs(),
                LoggingEventType.BuildCheckTracingEvent => new BuildCheckTracingEventArgs(),
                LoggingEventType.EnvironmentVariableReadEvent => new EnvironmentVariableReadEventArgs(),
                LoggingEventType.BuildSubmissionStartedEvent => new BuildSubmissionStartedEventArgs(),
                LoggingEventType.BuildCanceledEvent => new BuildCanceledEventArgs("Build canceled."),
                LoggingEventType.WorkerNodeTelemetryEvent => new WorkerNodeTelemetryEventArgs(),
                LoggingEventType.LoggersRegisteredEvent => new LoggersRegisteredEventArgs(),
                LoggingEventType.MSBuildServerLifecycleEvent => new MSBuildServerLifecycleEventArgs(),

                _ => Assumed.Unreachable<BuildEventArgs>($"Should not get to the default of GetBuildEventArgFromId ID: {_eventType}")
            };

        /// <summary>
        /// Based on the type of the BuildEventArg to be wrapped
        /// generate an Id which identifies which concrete type the
        /// BuildEventArg is.
        /// </summary>
        /// <param name="eventArg">Argument to get the type Id for</param>
        /// <returns>An enumeration entry which represents the type</returns>
        private LoggingEventType GetLoggingEventId(BuildEventArgs eventArg)
        {
            Type eventType = eventArg.GetType();
            if (eventType == typeof(BuildMessageEventArgs))
            {
                return LoggingEventType.BuildMessageEvent;
            }
            else if (eventType == typeof(TaskCommandLineEventArgs))
            {
                return LoggingEventType.TaskCommandLineEvent;
            }
            else if (eventType == typeof(TaskParameterEventArgs))
            {
                return LoggingEventType.TaskParameterEvent;
            }
            else if (eventType == typeof(ProjectFinishedEventArgs))
            {
                return LoggingEventType.ProjectFinishedEvent;
            }
            else if (eventType == typeof(ProjectStartedEventArgs))
            {
                return LoggingEventType.ProjectStartedEvent;
            }
            else if (eventType == typeof(ExternalProjectStartedEventArgs))
            {
                return LoggingEventType.ExternalProjectStartedEvent;
            }
            else if (eventType == typeof(ExternalProjectFinishedEventArgs))
            {
                return LoggingEventType.ExternalProjectFinishedEvent;
            }
            else if (eventType == typeof(ProjectEvaluationFinishedEventArgs))
            {
                return LoggingEventType.ProjectEvaluationFinishedEvent;
            }
            else if (eventType == typeof(ProjectEvaluationStartedEventArgs))
            {
                return LoggingEventType.ProjectEvaluationStartedEvent;
            }
            else if (eventType == typeof(ProjectImportedEventArgs))
            {
                return LoggingEventType.ProjectImportedEvent;
            }
            else if (eventType == typeof(TargetSkippedEventArgs))
            {
                return LoggingEventType.TargetSkipped;
            }
            else if (eventType == typeof(TelemetryEventArgs))
            {
                return LoggingEventType.Telemetry;
            }
            else if (eventType == typeof(AssemblyLoadBuildEventArgs))
            {
                return LoggingEventType.AssemblyLoadEvent;
            }
            else if (eventType == typeof(ExtendedCustomBuildEventArgs))
            {
                return LoggingEventType.ExtendedCustomEvent;
            }
            else if (eventType == typeof(ExtendedBuildErrorEventArgs))
            {
                return LoggingEventType.ExtendedBuildErrorEvent;
            }
            else if (eventType == typeof(ExtendedBuildWarningEventArgs))
            {
                return LoggingEventType.ExtendedBuildWarningEvent;
            }
            else if (eventType == typeof(ExtendedBuildMessageEventArgs))
            {
                return LoggingEventType.ExtendedBuildMessageEvent;
            }
            else if (eventType == typeof(CriticalBuildMessageEventArgs))
            {
                return LoggingEventType.CriticalBuildMessage;
            }
            else if (eventType == typeof(ExtendedCriticalBuildMessageEventArgs))
            {
                return LoggingEventType.ExtendedCriticalBuildMessageEvent;
            }
            else if (eventType == typeof(MetaprojectGeneratedEventArgs))
            {
                return LoggingEventType.MetaprojectGenerated;
            }
            else if (eventType == typeof(PropertyInitialValueSetEventArgs))
            {
                return LoggingEventType.PropertyInitialValueSet;
            }
            else if (eventType == typeof(PropertyReassignmentEventArgs))
            {
                return LoggingEventType.PropertyReassignment;
            }
            else if (eventType == typeof(UninitializedPropertyReadEventArgs))
            {
                return LoggingEventType.UninitializedPropertyRead;
            }
            else if (eventType == typeof(GeneratedFileUsedEventArgs))
            {
                return LoggingEventType.GeneratedFileUsedEvent;
            }
            else if (eventType == typeof(BuildCheckResultMessage))
            {
                return LoggingEventType.BuildCheckMessageEvent;
            }
            else if (eventType == typeof(BuildCheckResultWarning))
            {
                return LoggingEventType.BuildCheckWarningEvent;
            }
            else if (eventType == typeof(BuildCheckResultError))
            {
                return LoggingEventType.BuildCheckErrorEvent;
            }
            else if (eventType == typeof(BuildCheckAcquisitionEventArgs))
            {
                return LoggingEventType.BuildCheckAcquisitionEvent;
            }
            else if (eventType == typeof(BuildCheckTracingEventArgs))
            {
                return LoggingEventType.BuildCheckTracingEvent;
            }
            else if (eventType == typeof(EnvironmentVariableReadEventArgs))
            {
                return LoggingEventType.EnvironmentVariableReadEvent;
            }
            else if (eventType == typeof(BuildSubmissionStartedEventArgs))
            {
                return LoggingEventType.BuildSubmissionStartedEvent;
            }
            else if (eventType == typeof(BuildCanceledEventArgs))
            {
                return LoggingEventType.BuildCanceledEvent;
            }
            else if (eventType == typeof(WorkerNodeTelemetryEventArgs))
            {
                return LoggingEventType.WorkerNodeTelemetryEvent;
            }
            else if (eventType == typeof(LoggersRegisteredEventArgs))
            {
                return LoggingEventType.LoggersRegisteredEvent;
            }
            else if (eventType == typeof(MSBuildServerLifecycleEventArgs))
            {
                return LoggingEventType.MSBuildServerLifecycleEvent;
            }
            else if (eventType == typeof(TargetStartedEventArgs))
            {
                return LoggingEventType.TargetStartedEvent;
            }
            else if (eventType == typeof(TargetFinishedEventArgs))
            {
                return LoggingEventType.TargetFinishedEvent;
            }
            else if (eventType == typeof(TaskStartedEventArgs))
            {
                return LoggingEventType.TaskStartedEvent;
            }
            else if (eventType == typeof(TaskFinishedEventArgs))
            {
                return LoggingEventType.TaskFinishedEvent;
            }
            else if (eventType == typeof(BuildFinishedEventArgs))
            {
                return LoggingEventType.BuildFinishedEvent;
            }
            else if (eventType == typeof(BuildStartedEventArgs))
            {
                return LoggingEventType.BuildStartedEvent;
            }
            else if (eventType == typeof(BuildWarningEventArgs))
            {
                return LoggingEventType.BuildWarningEvent;
            }
            else if (eventType == typeof(BuildErrorEventArgs))
            {
                return LoggingEventType.BuildErrorEvent;
            }
            else if (eventType == typeof(ResponseFileUsedEventArgs))
            {
                return LoggingEventType.ResponseFileUsedEvent;
            }
            else
            {
                return LoggingEventType.CustomEvent;
            }
        }

        /// <summary>
        /// Given a build event that is presumed to be 2.0 (due to its lack of a "WriteToStream" method) and its
        /// LoggingEventType, serialize that event to the stream.
        /// </summary>
        /// <remarks>
        /// Override to customize serialization per-assembly without relying on compile directives.
        /// </remarks>
        protected virtual void WriteEventToStream(BuildEventArgs buildEvent, LoggingEventType eventType, ITranslator translator)
        {
            string message = buildEvent.Message;
            string helpKeyword = buildEvent.HelpKeyword;
            string senderName = buildEvent.SenderName;

            translator.Translate(ref message);
            translator.Translate(ref helpKeyword);
            translator.Translate(ref senderName);

            // It is essential that you translate in the same order during writing and reading
            switch (eventType)
            {
                case LoggingEventType.BuildMessageEvent:
                    WriteBuildMessageEventToStream((BuildMessageEventArgs)buildEvent, translator);
                    break;
                case LoggingEventType.ResponseFileUsedEvent:
                    WriteResponseFileUsedEventToStream((ResponseFileUsedEventArgs)buildEvent, translator);
                    break;
                case LoggingEventType.TaskCommandLineEvent:
                    WriteTaskCommandLineEventToStream((TaskCommandLineEventArgs)buildEvent, translator);
                    break;
                case LoggingEventType.BuildErrorEvent:
                    WriteBuildErrorEventToStream((BuildErrorEventArgs)buildEvent, translator);
                    break;
                case LoggingEventType.BuildWarningEvent:
                    WriteBuildWarningEventToStream((BuildWarningEventArgs)buildEvent, translator);
                    break;
                default:
                    InternalError.Throw($"Not Supported LoggingEventType {eventType}");
                    break;
            }
        }

        #region Writes to Stream

        /// <summary>
        /// Write Build Warning Log message into the translator
        /// </summary>
        private void WriteBuildWarningEventToStream(BuildWarningEventArgs buildWarningEventArgs, ITranslator translator)
        {
            string code = buildWarningEventArgs.Code;
            translator.Translate(ref code);

            int columnNumber = buildWarningEventArgs.ColumnNumber;
            translator.Translate(ref columnNumber);

            int endColumnNumber = buildWarningEventArgs.EndColumnNumber;
            translator.Translate(ref endColumnNumber);

            int endLineNumber = buildWarningEventArgs.EndLineNumber;
            translator.Translate(ref endLineNumber);

            string file = buildWarningEventArgs.File;
            translator.Translate(ref file);

            int lineNumber = buildWarningEventArgs.LineNumber;
            translator.Translate(ref lineNumber);

            string subCategory = buildWarningEventArgs.Subcategory;
            translator.Translate(ref subCategory);
        }

        /// <summary>
        /// Write a Build Error message into the translator
        /// </summary>
        private void WriteBuildErrorEventToStream(BuildErrorEventArgs buildErrorEventArgs, ITranslator translator)
        {
            string code = buildErrorEventArgs.Code;
            translator.Translate(ref code);

            int columnNumber = buildErrorEventArgs.ColumnNumber;
            translator.Translate(ref columnNumber);

            int endColumnNumber = buildErrorEventArgs.EndColumnNumber;
            translator.Translate(ref endColumnNumber);

            int endLineNumber = buildErrorEventArgs.EndLineNumber;
            translator.Translate(ref endLineNumber);

            string file = buildErrorEventArgs.File;
            translator.Translate(ref file);

            int lineNumber = buildErrorEventArgs.LineNumber;
            translator.Translate(ref lineNumber);

            string subCategory = buildErrorEventArgs.Subcategory;
            translator.Translate(ref subCategory);
        }

        /// <summary>
        /// Write Task Command Line log message into the translator
        /// </summary>
        private void WriteTaskCommandLineEventToStream(TaskCommandLineEventArgs taskCommandLineEventArgs, ITranslator translator)
        {
            MessageImportance importance = taskCommandLineEventArgs.Importance;
            translator.TranslateEnum(ref importance, (int)importance);

            string commandLine = taskCommandLineEventArgs.CommandLine;
            translator.Translate(ref commandLine);

            string taskName = taskCommandLineEventArgs.TaskName;
            translator.Translate(ref taskName);
        }

        /// <summary>
        /// Write a "standard" Message Log the translator
        /// </summary>
        private void WriteBuildMessageEventToStream(BuildMessageEventArgs buildMessageEventArgs, ITranslator translator)
        {
            MessageImportance importance = buildMessageEventArgs.Importance;
            translator.TranslateEnum(ref importance, (int)importance);
        }

        /// <summary>
        /// Write a response file used log message into the translator
        /// </summary>
        private void WriteResponseFileUsedEventToStream(ResponseFileUsedEventArgs responseFileUsedEventArgs, ITranslator translator)
        {
            string filePath = responseFileUsedEventArgs.ResponseFilePath;

            translator.Translate(ref filePath);

            DateTime timestamp = responseFileUsedEventArgs.RawTimestamp;
            translator.Translate(ref timestamp);
        }

        #endregion

        #region Reads from Stream

        /// <summary>
        /// Given a build event that is presumed to be 2.0 (due to its lack of a "ReadFromStream" method) and its
        /// LoggingEventType, read that event from the stream.
        /// </summary>
        /// <remarks>
        /// Override to customize serialization per-assembly without relying on compile directives.
        /// </remarks>
        protected virtual BuildEventArgs ReadEventFromStream(LoggingEventType eventType, ITranslator translator)
        {
            string message = null;
            string helpKeyword = null;
            string senderName = null;

            translator.Translate(ref message);
            translator.Translate(ref helpKeyword);
            translator.Translate(ref senderName);

            return eventType switch
            {
                LoggingEventType.TaskCommandLineEvent => ReadTaskCommandLineEventFromStream(translator, message, helpKeyword, senderName),
                LoggingEventType.BuildErrorEvent => ReadTaskBuildErrorEventFromStream(translator, message, helpKeyword, senderName),
                LoggingEventType.BuildMessageEvent => ReadBuildMessageEventFromStream(translator, message, helpKeyword, senderName),
                LoggingEventType.ResponseFileUsedEvent => ReadResponseFileUsedEventFromStream(translator, message, helpKeyword, senderName),
                LoggingEventType.BuildWarningEvent => ReadBuildWarningEventFromStream(translator, message, helpKeyword, senderName),
                _ => null,
            };
        }

        /// <summary>
        /// Read and reconstruct a BuildWarningEventArgs from the stream
        /// </summary>
        private BuildWarningEventArgs ReadBuildWarningEventFromStream(ITranslator translator, string message, string helpKeyword, string senderName)
        {
            string code = null;
            translator.Translate(ref code);

            int columnNumber = -1;
            translator.Translate(ref columnNumber);

            int endColumnNumber = -1;
            translator.Translate(ref endColumnNumber);

            int endLineNumber = -1;
            translator.Translate(ref endLineNumber);

            string file = null;
            translator.Translate(ref file);

            int lineNumber = -1;
            translator.Translate(ref lineNumber);

            string subCategory = null;
            translator.Translate(ref subCategory);

            BuildWarningEventArgs buildEvent =
                new BuildWarningEventArgs(
                        subCategory,
                        code,
                        file,
                        lineNumber,
                        columnNumber,
                        endLineNumber,
                        endColumnNumber,
                        message,
                        helpKeyword,
                        senderName);

            return buildEvent;
        }

        /// <summary>
        /// Read and reconstruct a BuildErrorEventArgs from the stream
        /// </summary>
        private BuildErrorEventArgs ReadTaskBuildErrorEventFromStream(ITranslator translator, string message, string helpKeyword, string senderName)
        {
            string code = null;
            translator.Translate(ref code);

            int columnNumber = -1;
            translator.Translate(ref columnNumber);

            int endColumnNumber = -1;
            translator.Translate(ref endColumnNumber);

            int endLineNumber = -1;
            translator.Translate(ref endLineNumber);

            string file = null;
            translator.Translate(ref file);

            int lineNumber = -1;
            translator.Translate(ref lineNumber);

            string subCategory = null;
            translator.Translate(ref subCategory);

            BuildErrorEventArgs buildEvent =
                new BuildErrorEventArgs(
                        subCategory,
                        code,
                        file,
                        lineNumber,
                        columnNumber,
                        endLineNumber,
                        endColumnNumber,
                        message,
                        helpKeyword,
                        senderName);

            return buildEvent;
        }

        /// <summary>
        /// Read and reconstruct a TaskCommandLineEventArgs from the stream
        /// </summary>
        private TaskCommandLineEventArgs ReadTaskCommandLineEventFromStream(ITranslator translator, string message, string helpKeyword, string senderName)
        {
            MessageImportance importance = MessageImportance.Normal;
            translator.TranslateEnum(ref importance, (int)importance);

            string commandLine = null;
            translator.Translate(ref commandLine);

            string taskName = null;
            translator.Translate(ref taskName);

            TaskCommandLineEventArgs buildEvent = new TaskCommandLineEventArgs(commandLine, taskName, importance);
            return buildEvent;
        }

        /// <summary>
        /// Read and reconstruct a BuildMessageEventArgs from the stream
        /// </summary>
        private BuildMessageEventArgs ReadBuildMessageEventFromStream(ITranslator translator, string message, string helpKeyword, string senderName)
        {
            MessageImportance importance = MessageImportance.Normal;

            translator.TranslateEnum(ref importance, (int)importance);

            BuildMessageEventArgs buildEvent = new BuildMessageEventArgs(message, helpKeyword, senderName, importance);
            return buildEvent;
        }

        private ResponseFileUsedEventArgs ReadResponseFileUsedEventFromStream(ITranslator translator, string message, string helpKeyword, string senderName)
        {
            string responseFilePath = String.Empty;
            translator.Translate(ref responseFilePath);
            ResponseFileUsedEventArgs buildEvent = new ResponseFileUsedEventArgs(responseFilePath);

            DateTime timestamp = default;
            translator.Translate(ref timestamp);
            buildEvent.RawTimestamp = timestamp;

            return buildEvent;
        }

        #endregion

        #endregion
    }
}
