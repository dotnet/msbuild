// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.IO.Compression;
using System.Threading;
using Microsoft.Build.BackEnd;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Logging
{
    /// <summary>
    /// Provides a method to read a binary log file (*.binlog) and replay all stored BuildEventArgs
    /// by implementing IEventSource and raising corresponding events.
    /// </summary>
    /// <remarks>The class is public so that we can call it from MSBuild.exe when replaying a log file.</remarks>
    public sealed class BinaryLogReplayEventSource : BinaryLogReplayEventSourceBase, IEventSource, IEmbeddedContentSource
    {
        public BinaryLogReplayEventSource()
            : base(true) { }

        public bool AllowForwardCompatibility
        {
            get => AllowForwardCompatibilityInternal;
            set => AllowForwardCompatibilityInternal = value;
        }

        public event Action<string>? OnError
        {
            add => OnErrorInternal += value;
            remove => OnErrorInternal -= value;
        }

        public event Action<BuildEventArgs>? BuildEventReceived
        {
            add => BuildEventReceivedInternal += value;
            remove => BuildEventReceivedInternal -= value;
        }

        /// <summary>
        /// This event is raised for all BuildEventArgs objects after a more type-specific event
        /// </summary>
        public event AnyEventHandler? AnyEventRaised;

        /// <summary>
        /// Raised for BuildStatusEventArgs instances
        /// </summary>
        public event BuildStatusEventHandler? StatusEventRaised;

        /// <summary>
        /// Raised for CustomBuildEventArgs instances
        /// </summary>
        public event CustomBuildEventHandler? CustomEventRaised;

        /// <summary>
        /// Raised for BuildStartedEventArgs instances
        /// </summary>
        public event BuildStartedEventHandler? BuildStarted;

        /// <summary>
        /// Raised for BuildFinishedEventArgs instances
        /// </summary>
        public event BuildFinishedEventHandler? BuildFinished;

        /// <summary>
        /// Raised for ProjectStartedEventArgs instances
        /// </summary>
        public event ProjectStartedEventHandler? ProjectStarted;

        /// <summary>
        /// Raised for ProjectFinishedEventArgs instances
        /// </summary>
        public event ProjectFinishedEventHandler? ProjectFinished;

        /// <summary>
        /// Raised for TargetStartedEventArgs instances
        /// </summary>
        public event TargetStartedEventHandler? TargetStarted;

        /// <summary>
        /// Raised for TargetFinishedEventArgs instances
        /// </summary>
        public event TargetFinishedEventHandler? TargetFinished;

        /// <summary>
        /// Raised for TaskStartedEventArgs instances
        /// </summary>
        public event TaskStartedEventHandler? TaskStarted;

        /// <summary>
        /// Raised for TaskFinishedEventArgs instances
        /// </summary>
        public event TaskFinishedEventHandler? TaskFinished;

        /// <summary>
        /// Raised for BuildErrorEventArgs instances
        /// </summary>
        public event BuildErrorEventHandler? ErrorRaised;

        /// <summary>
        /// Raised for BuildWarningEventArgs instances
        /// </summary>
        public event BuildWarningEventHandler? WarningRaised;

        /// <summary>
        /// Raised for BuildMessageEventArgs instances
        /// </summary>
        public event BuildMessageEventHandler? MessageRaised;

        /// <summary>
        /// Raise one of the events that is appropriate for the type of the BuildEventArgs
        /// </summary>
        public void Dispatch(BuildEventArgs buildEvent)
        {
            if (buildEvent is BuildMessageEventArgs buildMessageEventArgs)
            {
                MessageRaised?.Invoke(null, buildMessageEventArgs);
            }
            else if (buildEvent is TaskStartedEventArgs taskStartedEventArgs)
            {
                TaskStarted?.Invoke(null, taskStartedEventArgs);
            }
            else if (buildEvent is TaskFinishedEventArgs taskFinishedEventArgs)
            {
                TaskFinished?.Invoke(null, taskFinishedEventArgs);
            }
            else if (buildEvent is TargetStartedEventArgs targetStartedEventArgs)
            {
                TargetStarted?.Invoke(null, targetStartedEventArgs);
            }
            else if (buildEvent is TargetFinishedEventArgs targetFinishedEventArgs)
            {
                TargetFinished?.Invoke(null, targetFinishedEventArgs);
            }
            else if (buildEvent is ProjectStartedEventArgs projectStartedEventArgs)
            {
                ProjectStarted?.Invoke(null, projectStartedEventArgs);
            }
            else if (buildEvent is ProjectFinishedEventArgs projectFinishedEventArgs)
            {
                ProjectFinished?.Invoke(null, projectFinishedEventArgs);
            }
            else if (buildEvent is BuildStartedEventArgs buildStartedEventArgs)
            {
                BuildStarted?.Invoke(null, buildStartedEventArgs);
            }
            else if (buildEvent is BuildFinishedEventArgs buildFinishedEventArgs)
            {
                BuildFinished?.Invoke(null, buildFinishedEventArgs);
            }
            else if (buildEvent is CustomBuildEventArgs customBuildEventArgs)
            {
                CustomEventRaised?.Invoke(null, customBuildEventArgs);
            }
            else if (buildEvent is BuildStatusEventArgs buildStatusEventArgs)
            {
                StatusEventRaised?.Invoke(null, buildStatusEventArgs);
            }
            else if (buildEvent is BuildWarningEventArgs buildWarningEventArgs)
            {
                WarningRaised?.Invoke(null, buildWarningEventArgs);
            }
            else if (buildEvent is BuildErrorEventArgs buildErrorEventArgs)
            {
                ErrorRaised?.Invoke(null, buildErrorEventArgs);
            }

            AnyEventRaised?.Invoke(null, buildEvent);
        }
    }

    public interface IRawLogEventsSource
    {
        public event Action<ArraySegment<byte>>? BuildEventReceived;
    }

    public sealed class BinaryLogReplayRawEventSource : BinaryLogReplayEventSourceBase, IRawLogEventsSource
    {
        public BinaryLogReplayRawEventSource()
            : base(false) { }

        public event Action<ArraySegment<byte>>? BuildEventReceived
        {
            add => RawBuildEventReceivedInternal += value;
            remove => RawBuildEventReceivedInternal -= value;
        }
    }

    public class BinaryLogReplayEventSourceBase : IEmbeddedContentSource
    {
        /// Touches the <see cref="ItemGroupLoggingHelper"/> static constructor
        /// to ensure it initializes <see cref="TaskParameterEventArgs.MessageGetter"/>
        /// and <see cref="TaskParameterEventArgs.DictionaryFactory"/>
        static BinaryLogReplayEventSourceBase()
        {
            _ = ItemGroupLoggingHelper.ItemGroupIncludeLogMessagePrefix;
        }

        internal BinaryLogReplayEventSourceBase(bool isStructured)
            => _isStructuredReader = isStructured;

        private bool _isStructuredReader;
        protected bool AllowForwardCompatibilityInternal { get; set; } = true;
        protected event Action<string>? OnErrorInternal;
        protected event Action<BuildEventArgs>? BuildEventReceivedInternal;
        protected event Action<ArraySegment<byte>>? RawBuildEventReceivedInternal;

        /// <summary>
        /// Creates a <see cref="BinaryReader"/> for the provided binary log file.
        /// Performs decompression and buffering in the optimal way.
        /// Caller is responsible for disposing the returned reader.
        /// </summary>
        /// <param name="sourceFilePath"></param>
        /// <returns>BinaryReader of the given binlog file.</returns>
        public static BinaryReader OpenReader(string sourceFilePath)
        {
            Stream? stream = null;
            try
            {
                stream = new FileStream(sourceFilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                var gzipStream = new GZipStream(stream, CompressionMode.Decompress, leaveOpen: false);

                // wrapping the GZipStream in a buffered stream significantly improves performance
                // and the max throughput is reached with a 32K buffer. See details here:
                // https://github.com/dotnet/runtime/issues/39233#issuecomment-745598847
                var bufferedStream = new BufferedStream(gzipStream, 32768);
                return new BinaryReader(bufferedStream.ToReadableSeekableStream());
            }
            catch (Exception)
            {
                stream?.Dispose();
                throw;
            }
        }

        /// <summary>
        /// Creates a <see cref="BuildEventArgsReader"/> for the provided binary reader over binary log file.
        /// Caller is responsible for disposing the returned reader.
        /// </summary>
        /// <param name="binaryReader"></param>
        /// <param name="closeInput">Indicates whether the passed BinaryReader should be closed on disposing.</param>
        /// <param name="allowForwardCompatibility">Indicates whether reading of future versions of logs should be allowed.</param>
        /// <returns>BuildEventArgsReader over the given binlog file binary reader.</returns>
        public static BuildEventArgsReader OpenBuildEventsReader(BinaryReader binaryReader, bool closeInput, bool allowForwardCompatibility = true)
        {
            int fileFormatVersion = binaryReader.ReadInt32();

            // the log file is written using a newer version of file format
            // that we don't know how to read
            if (!allowForwardCompatibility && fileFormatVersion > BinaryLogger.FileFormatVersion)
            {
                var text = ResourceUtilities.FormatResourceStringStripCodeAndKeyword("UnsupportedLogFileFormat", fileFormatVersion, BinaryLogger.FileFormatVersion);
                throw new NotSupportedException(text);
            }

            return new BuildEventArgsReader(binaryReader, fileFormatVersion) { CloseInput = closeInput };
        }

        /// <summary>
        /// Creates a <see cref="BinaryReader"/> for the provided binary log file.
        /// Performs decompression and buffering in the optimal way.
        /// Caller is responsible for disposing the returned reader.
        /// </summary>
        /// <param name="sourceFilePath"></param>
        /// <returns>BinaryReader of the given binlog file.</returns>
        public static BuildEventArgsReader OpenBuildEventsReader(string sourceFilePath)
            => OpenBuildEventsReader(OpenReader(sourceFilePath), true);

        /// <summary>
        /// Read the provided binary log file and raise corresponding events for each BuildEventArgs
        /// </summary>
        /// <param name="sourceFilePath">The full file path of the binary log file</param>
        public void Replay(string sourceFilePath)
        {
            Replay(sourceFilePath, CancellationToken.None);
        }

        /// <summary>
        /// Read the provided binary log file and raise corresponding events for each BuildEventArgs
        /// </summary>
        /// <param name="sourceFilePath">The full file path of the binary log file</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> indicating the replay should stop as soon as possible.</param>
        public void Replay(string sourceFilePath, CancellationToken cancellationToken)
        {
            using var eventsReader = BinaryLogReplayEventSource.OpenBuildEventsReader(sourceFilePath);
            Replay(eventsReader, cancellationToken);
        }

        /// <summary>
        /// Read the provided binary log file and raise corresponding events for each BuildEventArgs
        /// </summary>
        /// <param name="binaryReader">The binary log content binary reader - caller is responsible for disposing.</param>
        /// <param name="closeInput">Indicates whether the passed BinaryReader should be closed on disposing.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> indicating the replay should stop as soon as possible.</param>
        public void Replay(BinaryReader binaryReader, bool closeInput, CancellationToken cancellationToken)
        {
            using var reader = BinaryLogReplayEventSource.OpenBuildEventsReader(binaryReader, closeInput, AllowForwardCompatibilityInternal);
            Replay(reader, cancellationToken);
        }

        public void Replay(BuildEventArgsReader reader, CancellationToken cancellationToken)
        {
            _fileFormatVersionRead?.Invoke(reader.FileFormatVersion);
            reader.EmbeddedContentRead += _embeddedContentRead;

            if (_isStructuredReader)
            {
                ReplayStructured(reader, cancellationToken);
            }
            else
            {
                ReplayRaw(reader, cancellationToken);
            }
        }

        private void ReplayStructured(BuildEventArgsReader reader, CancellationToken cancellationToken)
        {
            while (
                !cancellationToken.IsCancellationRequested &&
                reader.Read(AllowForwardCompatibilityInternal, AllowForwardCompatibilityInternal, OnErrorInternal ?? (_ => { }))
                    is { } instance)
            {
                BuildEventReceivedInternal?.Invoke(instance);
            }
        }

        private void ReplayRaw(BuildEventArgsReader reader, CancellationToken cancellationToken)
        {
            _fileFormatVersionRead?.Invoke(reader.FileFormatVersion);
            reader.EmbeddedContentRead += _embeddedContentRead;

            while (!cancellationToken.IsCancellationRequested && reader.ReadRaw() is { Count: > 0 } instance)
            {
                RawBuildEventReceivedInternal?.Invoke(instance);
            }
        }

        private Action<int>? _fileFormatVersionRead;
        event Action<int> ILogVersionInfo.FileFormatVersionRead
        {
            add => _fileFormatVersionRead += value;
            remove => _fileFormatVersionRead -= value;
        }
        private Action<EmbeddedContentEventArgs>? _embeddedContentRead;
        /// <inheritdoc cref="IEmbeddedContentSource.EmbeddedContentRead"/>
        event Action<EmbeddedContentEventArgs>? IEmbeddedContentSource.EmbeddedContentRead
        {
            // Explicitly implemented event has to declare explicit add/remove accessors
            //  https://stackoverflow.com/a/2268472/2308106
            add => _embeddedContentRead += value;
            remove => _embeddedContentRead -= value;
        }
    }
}
