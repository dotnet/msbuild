// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Microsoft.Build.Framework;
using Microsoft.Build.TaskHost.Exceptions;
using Microsoft.Build.TaskHost.Utilities;

namespace Microsoft.Build.TaskHost.BackEnd;

// On .NET Framework 3.5, Microsoft.Build.Framework includes the following concrete event args types:
//
// - BuildErrorEventArgs
// - BuildFinishedEventArgs
// - BuildMessageEventArgs
// - BuildStartedEventArgs
// - BuildWarningEventArgs
// - ExternalProjectFinishedEventArgs
// - ExternalProjectStartedEventArgs
// - ProjectFinishedEventArgs
// - ProjectStartedEventArgs
// - TargetFinishedEventArgs
// - TargetStartedEventArgs
// - TaskCommandLineEventArgs
// - TaskFinishedEventArgs
// - TaskStartedEventArgs

/// <summary>
/// An enumeration of all the types of BuildEventArgs that can be
/// packaged by this logMessagePacket
/// </summary>
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
    /// Event is <see cref="ExternalProjectStartedEventArgs"/>.
    /// </summary>
    ExternalProjectStartedEvent = 22,

    /// <summary>
    /// Event is <see cref="ExternalProjectFinishedEventArgs"/>.
    /// </summary>
    ExternalProjectFinishedEvent = 23,
}

/// <summary>
/// A packet to encapsulate a BuildEventArg logging message.
/// Contents:
/// Build Event Type
/// Build Event Args
/// </summary>
internal sealed class LogMessagePacketBase : INodePacket
{
    private const string WriteToStreamMethodName = "WriteToStream";
    private const string CreateFromStreamMethodName = "CreateFromStream";

    private const BindingFlags NonPublicInstance = BindingFlags.NonPublic | BindingFlags.Instance;

    /// <summary>
    /// The packet version, which is based on the CLR version. Cached because querying Environment.Version each time becomes an allocation bottleneck.
    /// </summary>
    private static readonly int s_defaultPacketVersion = (Environment.Version.Major * 10) + Environment.Version.Minor;

    /// <summary>
    /// Dictionary of methods used to read BuildEventArgs.
    /// </summary>
    private static readonly Dictionary<LoggingEventType, MethodInfo> s_readMethodCache = [];

    /// <summary>
    /// Dictionary of methods used to write BuildEventArgs.
    /// </summary>
    private static readonly Dictionary<LoggingEventType, MethodInfo> s_writeMethodCache = [];

    /// <summary>
    /// The event type of the buildEventArg based on the
    /// LoggingEventType enumeration
    /// </summary>
    private LoggingEventType _eventType = LoggingEventType.Invalid;

    /// <summary>
    /// The buildEventArg which is encapsulated by the packet.
    /// </summary>
    private BuildEventArgs? _buildEvent;

    /// <summary>
    /// The sink id
    /// </summary>
    private int _sinkId;

    /// <summary>
    /// Encapsulates the buildEventArg in this packet.
    /// </summary>
    public LogMessagePacketBase(KeyValuePair<int, BuildEventArgs>? nodeBuildEvent)
    {
        ErrorUtilities.VerifyThrow(nodeBuildEvent != null, "nodeBuildEvent was null");
        _buildEvent = nodeBuildEvent.Value.Value;
        _sinkId = nodeBuildEvent.Value.Key;
        _eventType = GetLoggingEventId(_buildEvent);
    }

    /// <summary>
    /// Delegate representing a method on the BuildEventArgs classes used to write to a stream.
    /// </summary>
    private delegate void WriteToStreamMethod(BinaryWriter writer);

    /// <summary>
    /// Delegate representing a method on the BuildEventArgs classes used to read from a stream.
    /// </summary>
    private delegate void CreateFromStreamMethod(BinaryReader reader);

    /// <summary>
    /// Gets the nodePacket Type, in this case the packet is a Logging Message.
    /// </summary>
    public NodePacketType Type => NodePacketType.LogMessage;

    /// <summary>
    /// Reads/writes this packet.
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

    /// <summary>
    /// Writes the logging packet to the translator.
    /// </summary>
    private void WriteToStream(ITranslator translator)
    {
        ErrorUtilities.VerifyThrow(_eventType != LoggingEventType.CustomEvent, "_eventType should not be a custom event");

        MethodInfo? methodInfo = null;
        lock (s_writeMethodCache)
        {
            if (!s_writeMethodCache.TryGetValue(_eventType, out methodInfo))
            {
                Type eventDerivedType = _buildEvent!.GetType();
                methodInfo = eventDerivedType.GetMethod(WriteToStreamMethodName, NonPublicInstance);
                s_writeMethodCache.Add(_eventType, methodInfo);
            }
        }

        int packetVersion = s_defaultPacketVersion;

        // Make sure the other side knows what sort of serialization is coming
        translator.Translate(ref packetVersion);

        // Note: This should always be true in MSBuildTaskHost (and methodInfo should never be null).
        // The .NET 3.5 event args have correct "WriteToStream" methods.
        bool eventCanSerializeItself = methodInfo != null;

        translator.Translate(ref eventCanSerializeItself);

        if (eventCanSerializeItself)
        {
            WriteToStreamMethod writerMethod = CreateDelegateRobust<WriteToStreamMethod>(_buildEvent!, methodInfo!);
            writerMethod(translator.Writer);
        }
        else
        {
            WriteEventToStream(_buildEvent!, _eventType, translator);
        }
    }

    /// <summary>
    /// Reads the logging packet from the translator.
    /// </summary>
    private void ReadFromStream(ITranslator translator)
    {
        ErrorUtilities.VerifyThrow(_eventType != LoggingEventType.CustomEvent, "_eventType should not be a custom event");

        _buildEvent = GetBuildEventArgFromId();

        // The other side is telling us whether the event knows how to log itself, or whether we're going to have
        // to do it manually
        int packetVersion = s_defaultPacketVersion;
        translator.Translate(ref packetVersion);

        bool eventCanSerializeItself = true;
        translator.Translate(ref eventCanSerializeItself);

        // Note: This should always be true in MSBuildTaskHost (and methodInfo should never be null).
        // The .NET 3.5 event args have correct "CreateFromStream" methods.
        if (eventCanSerializeItself)
        {
            MethodInfo? methodInfo = null;
            lock (s_readMethodCache)
            {
                if (!s_readMethodCache.TryGetValue(_eventType, out methodInfo))
                {
                    Type eventDerivedType = _buildEvent.GetType();
                    methodInfo = eventDerivedType.GetMethod(CreateFromStreamMethodName, NonPublicInstance);
                    s_readMethodCache.Add(_eventType, methodInfo);
                }
            }

            CreateFromStreamMethod readerMethod = CreateDelegateRobust<CreateFromStreamMethod>(_buildEvent, methodInfo);

            readerMethod(translator.Reader);
        }
        else
        {
            _buildEvent = ReadEventFromStream(_eventType, translator);
            ErrorUtilities.VerifyThrow(_buildEvent is not null, $"Unsupported LoggingEventType {_eventType}");
        }

        _eventType = GetLoggingEventId(_buildEvent);
    }

    /// <summary>
    /// Wrapper for Delegate.CreateDelegate with retries.
    /// </summary>
    private static T CreateDelegateRobust<T>(object firstArgument, MethodInfo methodInfo)
        where T : class, Delegate
    {
        Type type = typeof(T);

        for (int i = 0; i < 5; i++)
        {
            try
            {
                return (T)Delegate.CreateDelegate(type, firstArgument, methodInfo);
            }
            catch (FileLoadException)
            {
                // Sometimes, in 64-bit processes, the fusion load of Microsoft.Build.Framework.dll
                // spontaneously fails when trying to bind to the delegate.  However, it seems to
                // not repeat on additional tries -- so we'll try again a few times.  However, if
                // it keeps happening, it's probably a real problem, so we want to go ahead and
                // throw to let the user know what's up.
            }
        }

        ErrorUtilities.ThrowInternalErrorUnreachable();
        return null;
    }

    /// <summary>
    /// Takes in a id (LoggingEventType as an int) and creates the correct specific logging class
    /// </summary>
    private BuildEventArgs GetBuildEventArgFromId() => _eventType switch
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
        LoggingEventType.ExternalProjectStartedEvent => new ExternalProjectStartedEventArgs(null, null, null, null, null),
        LoggingEventType.ExternalProjectFinishedEvent => new ExternalProjectFinishedEventArgs(null, null, null, null, false),

        _ => throw new InternalErrorException($"Should not get to the default of GetBuildEventArgFromId ID: {_eventType}")
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
    private void WriteEventToStream(BuildEventArgs buildEvent, LoggingEventType eventType, ITranslator translator)
    {
        string? message = buildEvent.Message;
        string? helpKeyword = buildEvent.HelpKeyword;
        string? senderName = buildEvent.SenderName;

        translator.Translate(ref message);
        translator.Translate(ref helpKeyword);
        translator.Translate(ref senderName);

        // It is essential that you translate in the same order during writing and reading
        switch (eventType)
        {
            case LoggingEventType.BuildMessageEvent:
                WriteBuildMessageEventToStream((BuildMessageEventArgs)buildEvent, translator);
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
                ErrorUtilities.ThrowInternalError($"Not Supported LoggingEventType {eventType}");
                break;
        }
    }

    /// <summary>
    /// Write Build Warning Log message into the translator
    /// </summary>
    private void WriteBuildWarningEventToStream(BuildWarningEventArgs buildWarningEventArgs, ITranslator translator)
    {
        string? code = buildWarningEventArgs.Code;
        translator.Translate(ref code);

        int columnNumber = buildWarningEventArgs.ColumnNumber;
        translator.Translate(ref columnNumber);

        int endColumnNumber = buildWarningEventArgs.EndColumnNumber;
        translator.Translate(ref endColumnNumber);

        int endLineNumber = buildWarningEventArgs.EndLineNumber;
        translator.Translate(ref endLineNumber);

        string? file = buildWarningEventArgs.File;
        translator.Translate(ref file);

        int lineNumber = buildWarningEventArgs.LineNumber;
        translator.Translate(ref lineNumber);

        string? subCategory = buildWarningEventArgs.Subcategory;
        translator.Translate(ref subCategory);
    }

    /// <summary>
    /// Write a Build Error message into the translator
    /// </summary>
    private void WriteBuildErrorEventToStream(BuildErrorEventArgs buildErrorEventArgs, ITranslator translator)
    {
        string? code = buildErrorEventArgs.Code;
        translator.Translate(ref code);

        int columnNumber = buildErrorEventArgs.ColumnNumber;
        translator.Translate(ref columnNumber);

        int endColumnNumber = buildErrorEventArgs.EndColumnNumber;
        translator.Translate(ref endColumnNumber);

        int endLineNumber = buildErrorEventArgs.EndLineNumber;
        translator.Translate(ref endLineNumber);

        string? file = buildErrorEventArgs.File;
        translator.Translate(ref file);

        int lineNumber = buildErrorEventArgs.LineNumber;
        translator.Translate(ref lineNumber);

        string? subCategory = buildErrorEventArgs.Subcategory;
        translator.Translate(ref subCategory);
    }

    /// <summary>
    /// Write Task Command Line log message into the translator.
    /// </summary>
    private void WriteTaskCommandLineEventToStream(TaskCommandLineEventArgs taskCommandLineEventArgs, ITranslator translator)
    {
        MessageImportance importance = taskCommandLineEventArgs.Importance;
        translator.TranslateEnum(ref importance, (int)importance);

        string? commandLine = taskCommandLineEventArgs.CommandLine;
        translator.Translate(ref commandLine);

        string? taskName = taskCommandLineEventArgs.TaskName;
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
    /// Given a build event that is presumed to be 2.0 (due to its lack of a "ReadFromStream" method) and its
    /// LoggingEventType, read that event from the stream.
    /// </summary>
    /// <remarks>
    /// Override to customize serialization per-assembly without relying on compile directives.
    /// </remarks>
    private BuildEventArgs? ReadEventFromStream(LoggingEventType eventType, ITranslator translator)
    {
        string? message = null;
        string? helpKeyword = null;
        string? senderName = null;

        translator.Translate(ref message);
        translator.Translate(ref helpKeyword);
        translator.Translate(ref senderName);

        return eventType switch
        {
            LoggingEventType.TaskCommandLineEvent => ReadTaskCommandLineEventFromStream(translator),
            LoggingEventType.BuildErrorEvent => ReadTaskBuildErrorEventFromStream(translator, message, helpKeyword, senderName),
            LoggingEventType.BuildMessageEvent => ReadBuildMessageEventFromStream(translator, message, helpKeyword, senderName),
            LoggingEventType.BuildWarningEvent => ReadBuildWarningEventFromStream(translator, message, helpKeyword, senderName),
            _ => null,
        };
    }

    /// <summary>
    /// Read and reconstruct a BuildWarningEventArgs from the stream.
    /// </summary>
    private BuildWarningEventArgs ReadBuildWarningEventFromStream(ITranslator translator, string? message, string? helpKeyword, string? senderName)
    {
        string? code = null;
        translator.Translate(ref code);

        int columnNumber = -1;
        translator.Translate(ref columnNumber);

        int endColumnNumber = -1;
        translator.Translate(ref endColumnNumber);

        int endLineNumber = -1;
        translator.Translate(ref endLineNumber);

        string? file = null;
        translator.Translate(ref file);

        int lineNumber = -1;
        translator.Translate(ref lineNumber);

        string? subCategory = null;
        translator.Translate(ref subCategory);

        return new BuildWarningEventArgs(
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
    }

    /// <summary>
    /// Read and reconstruct a BuildErrorEventArgs from the stream.
    /// </summary>
    private BuildErrorEventArgs ReadTaskBuildErrorEventFromStream(ITranslator translator, string? message, string? helpKeyword, string? senderName)
    {
        string? code = null;
        translator.Translate(ref code);

        int columnNumber = -1;
        translator.Translate(ref columnNumber);

        int endColumnNumber = -1;
        translator.Translate(ref endColumnNumber);

        int endLineNumber = -1;
        translator.Translate(ref endLineNumber);

        string? file = null;
        translator.Translate(ref file);

        int lineNumber = -1;
        translator.Translate(ref lineNumber);

        string? subCategory = null;
        translator.Translate(ref subCategory);

        return new BuildErrorEventArgs(
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
    }

    /// <summary>
    /// Read and reconstruct a TaskCommandLineEventArgs from the stream.
    /// </summary>
    private TaskCommandLineEventArgs ReadTaskCommandLineEventFromStream(ITranslator translator)
    {
        MessageImportance importance = MessageImportance.Normal;
        translator.TranslateEnum(ref importance, (int)importance);

        string? commandLine = null;
        translator.Translate(ref commandLine);

        string? taskName = null;
        translator.Translate(ref taskName);

        return new TaskCommandLineEventArgs(commandLine, taskName, importance);
    }

    /// <summary>
    /// Read and reconstruct a BuildMessageEventArgs from the stream.
    /// </summary>
    private BuildMessageEventArgs ReadBuildMessageEventFromStream(ITranslator translator, string? message, string? helpKeyword, string? senderName)
    {
        MessageImportance importance = MessageImportance.Normal;

        translator.TranslateEnum(ref importance, (int)importance);

        return new BuildMessageEventArgs(message, helpKeyword, senderName, importance);
    }
}
