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
    internal interface IRawLogEventsSource
    {
        /// <summary>
        /// Event raised when non-textual log record is read.
        /// This means all event args and key-value pairs.
        /// Strings and Embedded files are not included.
        /// </summary>
        event Action<BinaryLogRecordKind, Stream>? LogDataSliceReceived;

        /// <summary>
        /// Enables initialization (e.g. subscription to events) - that is deferred until Replay is triggered.
        /// At this point all other possible subscribers should be already subscribed -
        ///  so it can be determined if raw events or structured events should be replayed.
        /// </summary>
        /// <param name="onFileFormatVersionRead"></param>
        /// <param name="onRawReadingPossible"></param>
        /// <param name="onStructuredReadingOnly"></param>
        void DeferredInitialize(
            Action<int> onFileFormatVersionRead,
            Action onRawReadingPossible,
            Action onStructuredReadingOnly);
    }

    /// <summary>
    /// Interface for replaying a binary log file (*.binlog)
    /// </summary>
    internal interface IBinaryLogReplaySource :
        IEventSource, IRawLogEventsSource, IBuildEventStringsReader, IEmbeddedContentSource
    { }

    /// <summary>
    /// Provides a method to read a binary log file (*.binlog) and replay all stored BuildEventArgs
    /// by implementing IEventSource and raising corresponding events.
    /// </summary>
    /// <remarks>The class is public so that we can call it from MSBuild.exe when replaying a log file.</remarks>
    public sealed class BinaryLogReplayEventSource : EventArgsDispatcher,
        IBinaryLogReplaySource
    {
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
        public bool AllowForwardCompatibility { private get; init; } = true;

        /// <summary>
        /// Receives recoverable errors during reading.
        /// </summary>
        public event Action<string>? OnRecoverableReadError;

        /// <summary>
        /// Read the provided binary log file and raise corresponding events for each BuildEventArgs
        /// </summary>
        /// <param name="sourceFilePath">The full file path of the binary log file</param>
        public void Replay(string sourceFilePath)
        {
            Replay(sourceFilePath, CancellationToken.None);
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
        /// <param name="allowForwardCompatibility">Unknown build events or unknown parts of known build events will be ignored if this is set to true.</param>
        /// <param name="onRecoverableReadError">Optional handler of recoverable errors during reading.</param>
        /// <returns>BuildEventArgsReader over the given binlog file binary reader.</returns>
        public static BuildEventArgsReader OpenBuildEventsReader(
            BinaryReader binaryReader,
            bool closeInput,
            bool allowForwardCompatibility = true,
            Action<string>? onRecoverableReadError = null)
        {
            int fileFormatVersion = binaryReader.ReadInt32();

            // the log file is written using a newer version of file format
            // that we don't know how to read
            if (!allowForwardCompatibility && fileFormatVersion > BinaryLogger.FileFormatVersion)
            {
                var text = ResourceUtilities.FormatResourceStringStripCodeAndKeyword("UnsupportedLogFileFormat", fileFormatVersion, BinaryLogger.FileFormatVersion);
                throw new NotSupportedException(text);
            }

            return new BuildEventArgsReader(binaryReader, fileFormatVersion)
            {
                CloseInput = closeInput,
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
            bool supportsForwardCompatibility = reader.FileFormatVersion >= 18;

            // Allow any possible deferred subscriptions to be registered
            if (HasStructuredEventsSubscribers || !supportsForwardCompatibility)
            {
                _onStructuredReadingOnly?.Invoke();
            }
            else
            {
                _onRawReadingPossible?.Invoke();
            }

            _fileFormatVersionRead?.Invoke(reader.FileFormatVersion);
            reader.EmbeddedContentRead += _embeddedContentRead;
            reader.StringReadDone += _stringReadDone;

            if (HasStructuredEventsSubscribers || !supportsForwardCompatibility)
            {
                if (this._logDataSliceReceived != null)
                {
                    throw new NotSupportedException(
                        "Structured events and raw events cannot be replayed at the same time.");
                }

                // Forward compatibile reading makes sense only for structured events reading.
                reader.SkipUnknownEvents = supportsForwardCompatibility && AllowForwardCompatibility;
                reader.SkipUnknownEventParts = supportsForwardCompatibility && AllowForwardCompatibility;
                reader.OnRecoverableReadError += OnRecoverableReadError;

                while (!cancellationToken.IsCancellationRequested && reader.Read() is { } instance)
                {
                    Dispatch(instance);
                }
            }
            else
            {
                if (this._logDataSliceReceived == null &&
                    this._embeddedContentRead == null &&
                    this._stringReadDone == null)
                {
                    throw new NotSupportedException(
                        "No subscribers for any events.");
                }

                while (!cancellationToken.IsCancellationRequested && reader.ReadRaw() is { } instance &&
                       instance.RecordKind != BinaryLogRecordKind.EndOfFile)
                {
                    _logDataSliceReceived?.Invoke(instance.RecordKind, instance.Stream);
                }
            }
        }

        /// <inheritdoc cref="IRawLogEventsSource.DeferredInitialize"/>
        void IRawLogEventsSource.DeferredInitialize(
            Action<int> onFileFormatVersionRead,
            Action onRawReadingPossible,
            Action onStructuredReadingOnly)
        {
            this._fileFormatVersionRead += onFileFormatVersionRead;
            this._onRawReadingPossible += onRawReadingPossible;
            this._onStructuredReadingOnly += onStructuredReadingOnly;
        }

        private Action? _onRawReadingPossible;
        private Action? _onStructuredReadingOnly;
        private Action<int>? _fileFormatVersionRead;
        private Action<EmbeddedContentEventArgs>? _embeddedContentRead;
        /// <inheritdoc cref="IEmbeddedContentSource.EmbeddedContentRead"/>
        event Action<EmbeddedContentEventArgs>? IEmbeddedContentSource.EmbeddedContentRead
        {
            // Explicitly implemented event has to declare explicit add/remove accessors
            //  https://stackoverflow.com/a/2268472/2308106
            add => _embeddedContentRead += value;
            remove => _embeddedContentRead -= value;
        }

        private Action<StringReadEventArgs>? _stringReadDone;
        /// <inheritdoc cref="IBuildEventStringsReader.StringReadDone"/>
        event Action<StringReadEventArgs>? IBuildEventStringsReader.StringReadDone
        {
            add => _stringReadDone += value;
            remove => _stringReadDone -= value;
        }

        private Action<BinaryLogRecordKind, Stream>? _logDataSliceReceived;
        /// <inheritdoc cref="IBuildEventStringsReader.StringReadDone"/>
        event Action<BinaryLogRecordKind, Stream>? IRawLogEventsSource.LogDataSliceReceived
        {
            add => _logDataSliceReceived += value;
            remove => _logDataSliceReceived -= value;
        }
    }
}
