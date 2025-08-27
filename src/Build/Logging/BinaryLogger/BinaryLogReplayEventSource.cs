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
    /// Interface for replaying a binary log file (*.binlog)
    /// </summary>
    internal interface IBinaryLogReplaySource :
        IEventSource,
        IBuildEventArgsReaderNotifications
    {
        /// <summary>
        /// Event raised when non-textual log record is read.
        /// This means all event args and key-value pairs.
        /// Strings and Embedded files are not included.
        /// </summary>
        event Action<BinaryLogRecordKind, Stream>? RawLogRecordReceived;

        /// <summary>
        /// Enables initialization (e.g. subscription to events) - that is deferred until Replay is triggered.
        /// At this point all other possible subscribers should be already subscribed -
        ///  so it can be determined if raw events or structured events should be replayed.
        /// </summary>
        /// <param name="onRawReadingPossible"></param>
        /// <param name="onStructuredReadingOnly"></param>
        void DeferredInitialize(
            Action onRawReadingPossible,
            Action onStructuredReadingOnly);

        /// <summary>
        /// File format version of the binary log file.
        /// </summary>
        int FileFormatVersion { get; }

        /// <summary>
        /// The minimum reader version for the binary log file.
        /// </summary>
        int MinimumReaderVersion { get; }

        /// <summary>
        /// Raised when the log reader encounters a project import archive (embedded content) in the stream.
        /// The subscriber must read the exactly given length of binary data from the stream - otherwise exception is raised.
        /// If no subscriber is attached, the data is skipped.
        /// </summary>
        event Action<EmbeddedContentEventArgs> EmbeddedContentRead;
    }

    /// <summary>
    /// Provides a method to read a binary log file (*.binlog) and replay all stored BuildEventArgs
    /// by implementing IEventSource and raising corresponding events.
    /// </summary>
    /// <remarks>The class is public so that we can call it from MSBuild.exe when replaying a log file.</remarks>
    public sealed class BinaryLogReplayEventSource : EventArgsDispatcher,
        IBinaryLogReplaySource
    {
        private int? _fileFormatVersion;
        private int? _minimumReaderVersion;

        public int FileFormatVersion => _fileFormatVersion ?? throw new InvalidOperationException(ResourceUtilities.GetResourceString("Binlog_Source_VersionUninitialized"));
        public int MinimumReaderVersion => _minimumReaderVersion ?? throw new InvalidOperationException(ResourceUtilities.GetResourceString("Binlog_Source_VersionUninitialized"));

        /// Touches the <see cref="ItemGroupLoggingHelper"/> static constructor
        /// to ensure it initializes <see cref="TaskParameterEventArgs.MessageGetter"/>
        /// and <see cref="TaskParameterEventArgs.DictionaryFactory"/>
        static BinaryLogReplayEventSource()
        {
            _ = ItemGroupLoggingHelper.ItemGroupIncludeLogMessagePrefix;
        }

        /// <summary>
        /// Unknown build events or unknown parts of known build events will be ignored if this is set to true.
        /// </summary>
        public bool AllowForwardCompatibility { private get; init; }

        /// <inheritdoc cref="IBuildEventArgsReaderNotifications.RecoverableReadError"/>
        public event Action<BinaryLogReaderErrorEventArgs>? RecoverableReadError;

        /// <summary>
        /// Read the provided binary log file and raise corresponding events for each BuildEventArgs
        /// </summary>
        /// <param name="sourceFilePath">The full file path of the binary log file</param>
        public void Replay(string sourceFilePath)
        {
            Replay(sourceFilePath, CancellationToken.None);
        }

        /// <summary>
        /// Read the provided binary log file opened as a stream and raise corresponding events for each BuildEventArgs
        /// </summary>
        /// <param name="sourceFileStream">Stream over the binlog content.</param>
        /// <param name="cancellationToken"></param>
        public void Replay(Stream sourceFileStream, CancellationToken cancellationToken)
        {
            using var binaryReader = OpenReader(sourceFileStream);
            Replay(binaryReader, cancellationToken);
        }

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
                return OpenReader(stream);
            }
            catch (Exception)
            {
                stream?.Dispose();
                throw;
            }
        }

        /// <summary>
        /// Creates a <see cref="BinaryReader"/> for the provided binary log file.
        /// Performs decompression and buffering in the optimal way.
        /// Caller is responsible for disposing the returned reader.
        /// </summary>
        /// <param name="sourceFileStream">Stream over the binlog file</param>
        /// <returns>BinaryReader of the given binlog file.</returns>
        public static BinaryReader OpenReader(Stream sourceFileStream)
        {
            var gzipStream = new GZipStream(sourceFileStream, CompressionMode.Decompress, leaveOpen: false);

            // wrapping the GZipStream in a buffered stream significantly improves performance
            // and the max throughput is reached with a 32K buffer. See details here:
            // https://github.com/dotnet/runtime/issues/39233#issuecomment-745598847
            var bufferedStream = new BufferedStream(gzipStream, 32768);
            return new BinaryReader(bufferedStream);
        }

        /// <summary>
        /// Creates a <see cref="BuildEventArgsReader"/> for the provided binary reader over binary log file.
        /// Caller is responsible for disposing the returned reader.
        /// </summary>
        /// <param name="binaryReader"></param>
        /// <param name="closeInput">Indicates whether the passed BinaryReader should be closed on disposing.</param>
        /// <param name="allowForwardCompatibility">Unknown build events or unknown parts of known build events will be ignored if this is set to true.</param>
        /// <returns>BuildEventArgsReader over the given binlog file binary reader.</returns>
        public static BuildEventArgsReader OpenBuildEventsReader(
            BinaryReader binaryReader,
            bool closeInput,
            bool allowForwardCompatibility = false)
        {
            int fileFormatVersion = binaryReader.ReadInt32();
            // Is this the new log format that contains the minimum reader version?
            int minimumReaderVersion = fileFormatVersion >= BinaryLogger.ForwardCompatibilityMinimalVersion
                ? binaryReader.ReadInt32()
                : fileFormatVersion;

            // the log file is written using a newer version of file format
            // that we don't know how to read
            if (fileFormatVersion > BinaryLogger.FileFormatVersion &&
                (!allowForwardCompatibility || minimumReaderVersion > BinaryLogger.FileFormatVersion))
            {
                var text = ResourceUtilities.FormatResourceStringStripCodeAndKeyword("UnsupportedLogFileFormat", fileFormatVersion, minimumReaderVersion, BinaryLogger.FileFormatVersion);
                throw new NotSupportedException(text);
            }

            return new BuildEventArgsReader(binaryReader, fileFormatVersion)
            {
                CloseInput = closeInput,
                MinimumReaderVersion = minimumReaderVersion
            };
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
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> indicating the replay should stop as soon as possible.</param>
        public void Replay(string sourceFilePath, CancellationToken cancellationToken)
        {
            using var eventsReader = OpenBuildEventsReader(sourceFilePath);
            Replay(eventsReader, cancellationToken);
        }

        /// <summary>
        /// Read the provided binary log file and raise corresponding events for each BuildEventArgs
        /// </summary>
        /// <param name="binaryReader">The binary log content binary reader - caller is responsible for disposing.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> indicating the replay should stop as soon as possible.</param>
        public void Replay(BinaryReader binaryReader, CancellationToken cancellationToken)
            => Replay(binaryReader, false, cancellationToken);

        /// <summary>
        /// Read the provided binary log file and raise corresponding events for each BuildEventArgs
        /// </summary>
        /// <param name="binaryReader">The binary log content binary reader - caller is responsible for disposing, unless <paramref name="closeInput"/> is set to true.</param>
        /// <param name="closeInput">Indicates whether the passed BinaryReader should be closed on disposing.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> indicating the replay should stop as soon as possible.</param>
        public void Replay(BinaryReader binaryReader, bool closeInput, CancellationToken cancellationToken)
        {
            using var reader = OpenBuildEventsReader(binaryReader, closeInput, AllowForwardCompatibility);
            Replay(reader, cancellationToken);
        }

        /// <summary>
        /// Read the provided binary log file and raise corresponding events for each BuildEventArgs
        /// </summary>
        /// <param name="reader">The build events reader - caller is responsible for disposing.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> indicating the replay should stop as soon as possible.</param>
        public void Replay(BuildEventArgsReader reader, CancellationToken cancellationToken)
        {
            _fileFormatVersion = reader.FileFormatVersion;
            _minimumReaderVersion = reader.MinimumReaderVersion;
            bool supportsForwardCompatibility = reader.FileFormatVersion >= BinaryLogger.ForwardCompatibilityMinimalVersion;

            // Allow any possible deferred subscriptions to be registered
            if (HasStructuredEventsSubscribers || !supportsForwardCompatibility)
            {
                _onStructuredReadingOnly?.Invoke();
            }
            else
            {
                _onRawReadingPossible?.Invoke();
            }

            reader.EmbeddedContentRead += _embeddedContentRead;
            reader.ArchiveFileEncountered += _archiveFileEncountered;
            reader.StringReadDone += _stringReadDone;

            if (HasStructuredEventsSubscribers || !supportsForwardCompatibility)
            {
                if (this._rawLogRecordReceived != null)
                {
                    throw new NotSupportedException(
                        ResourceUtilities.GetResourceString("Binlog_Source_MultiSubscribeError"));
                }

                // Forward compatible reading makes sense only for structured events reading.
                reader.SkipUnknownEvents = supportsForwardCompatibility && AllowForwardCompatibility;
                reader.SkipUnknownEventParts = supportsForwardCompatibility && AllowForwardCompatibility;
                reader.RecoverableReadError += RecoverableReadError;

                while (!cancellationToken.IsCancellationRequested && reader.Read() is { } instance)
                {
                    Dispatch(instance);
                }
            }
            else
            {
                if (this._rawLogRecordReceived == null &&
                    this._embeddedContentRead == null &&
                    this._stringReadDone == null &&
                    this._archiveFileEncountered == null)
                {
                    throw new NotSupportedException(
                        ResourceUtilities.GetResourceString("Binlog_Source_MissingSubscribeError"));
                }

                while (!cancellationToken.IsCancellationRequested && reader.ReadRaw() is { } instance &&
                       instance.RecordKind != BinaryLogRecordKind.EndOfFile)
                {
                    _rawLogRecordReceived?.Invoke(instance.RecordKind, instance.Stream);
                }
            }

            // Unsubscribe from events for a case if the reader is reused (upon cancellation).
            reader.EmbeddedContentRead -= _embeddedContentRead;
            reader.ArchiveFileEncountered -= _archiveFileEncountered;
            reader.StringReadDone -= _stringReadDone;
            reader.RecoverableReadError -= RecoverableReadError;
        }

        // Following members are explicit implementations of the IBinaryLogReplaySource interface
        //  to avoid exposing them publicly.
        // We want an interface so that BinaryLogger can fine tune its initialization logic
        //  in case the given event source is the replay source. On the other hand we don't want
        //  to expose these members publicly because they are not intended to be used by the consumers.

        private Action? _onRawReadingPossible;
        private Action? _onStructuredReadingOnly;
        /// <inheritdoc cref="IBinaryLogReplaySource.DeferredInitialize"/>
        void IBinaryLogReplaySource.DeferredInitialize(
            Action onRawReadingPossible,
            Action onStructuredReadingOnly)
        {
            this._onRawReadingPossible += onRawReadingPossible;
            this._onStructuredReadingOnly += onStructuredReadingOnly;
        }

        private Action<EmbeddedContentEventArgs>? _embeddedContentRead;
        /// <inheritdoc cref="IBinaryLogReplaySource.EmbeddedContentRead"/>
        event Action<EmbeddedContentEventArgs>? IBinaryLogReplaySource.EmbeddedContentRead
        {
            // Explicitly implemented event has to declare explicit add/remove accessors
            //  https://stackoverflow.com/a/2268472/2308106
            add => _embeddedContentRead += value;
            remove => _embeddedContentRead -= value;
        }

        private Action<StringReadEventArgs>? _stringReadDone;
        /// <inheritdoc cref="IBuildEventArgsReaderNotifications.StringReadDone"/>
        event Action<StringReadEventArgs>? IBuildEventArgsReaderNotifications.StringReadDone
        {
            add => _stringReadDone += value;
            remove => _stringReadDone -= value;
        }

        private Action<ArchiveFileEventArgs>? _archiveFileEncountered;
        /// <inheritdoc cref="IBuildEventArgsReaderNotifications.ArchiveFileEncountered"/>
        event Action<ArchiveFileEventArgs>? IBuildEventArgsReaderNotifications.ArchiveFileEncountered
        {
            add => _archiveFileEncountered += value;
            remove => _archiveFileEncountered -= value;
        }

        private Action<BinaryLogRecordKind, Stream>? _rawLogRecordReceived;
        /// <inheritdoc cref="IBinaryLogReplaySource.RawLogRecordReceived"/>
        event Action<BinaryLogRecordKind, Stream>? IBinaryLogReplaySource.RawLogRecordReceived
        {
            add => _rawLogRecordReceived += value;
            remove => _rawLogRecordReceived -= value;
        }
    }
}
