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
    public sealed class BinaryLogReplayEventSource : EventArgsDispatcher
    {
        /// Touches the <see cref="ItemGroupLoggingHelper"/> static constructor
        /// to ensure it initializes <see cref="TaskParameterEventArgs.MessageGetter"/>
        /// and <see cref="TaskParameterEventArgs.DictionaryFactory"/>
        static BinaryLogReplayEventSource()
        {
            _ = ItemGroupLoggingHelper.ItemGroupIncludeLogMessagePrefix;
        }

        /// <summary>
        /// Raised once <see cref="BuildEventArgsReader"/> is created during replaying
        /// </summary>
        public event Action<IBuildEventArgsReaderNotifications>? NotificationsSourceCreated;

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
                return new BinaryReader(bufferedStream);
            }
            catch(Exception)
            {
                stream?.Dispose();
                throw;
            }
        }

        /// <summary>
        /// Read the provided binary log file and raise corresponding events for each BuildEventArgs
        /// </summary>
        /// <param name="sourceFilePath">The full file path of the binary log file</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> indicating the replay should stop as soon as possible.</param>
        public void Replay(string sourceFilePath, CancellationToken cancellationToken)
        {
            using var binaryReader = OpenReader(sourceFilePath);
            Replay(binaryReader, cancellationToken);
        }

        /// <summary>
        /// Read the provided binary log file and raise corresponding events for each BuildEventArgs
        /// </summary>
        /// <param name="binaryReader">The binary log content binary reader - caller is responsible for disposing.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> indicating the replay should stop as soon as possible.</param>
        public void Replay(BinaryReader binaryReader, CancellationToken cancellationToken)
        {
            int fileFormatVersion = binaryReader.ReadInt32();

            // the log file is written using a newer version of file format
            // that we don't know how to read
            if (fileFormatVersion > BinaryLogger.FileFormatVersion)
            {
                var text = ResourceUtilities.FormatResourceStringStripCodeAndKeyword("UnsupportedLogFileFormat", fileFormatVersion, BinaryLogger.FileFormatVersion);
                throw new NotSupportedException(text);
            }

            using var reader = new BuildEventArgsReader(binaryReader, fileFormatVersion);
            NotificationsSourceCreated?.Invoke(reader);

            while (!cancellationToken.IsCancellationRequested && reader.Read() is { } instance)
            {
                Dispatch(instance);
            }
        }
    }
}
