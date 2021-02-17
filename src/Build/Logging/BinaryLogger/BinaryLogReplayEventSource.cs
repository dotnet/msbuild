using System;
using System.IO;
using System.IO.Compression;
using System.Threading;
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
            using (var stream = new FileStream(sourceFilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                var gzipStream = new GZipStream(stream, CompressionMode.Decompress, leaveOpen: true);

                // wrapping the GZipStream in a buffered stream significantly improves performance
                // and the max throughput is reached with a 32K buffer. See details here:
                // https://github.com/dotnet/runtime/issues/39233#issuecomment-745598847
                var bufferedStream = new BufferedStream(gzipStream, 32768);
                var binaryReader = new BinaryReader(bufferedStream);

                int fileFormatVersion = binaryReader.ReadInt32();

                // the log file is written using a newer version of file format
                // that we don't know how to read
                if (fileFormatVersion > BinaryLogger.FileFormatVersion)
                {
                    var text = ResourceUtilities.FormatResourceStringStripCodeAndKeyword("UnsupportedLogFileFormat", fileFormatVersion, BinaryLogger.FileFormatVersion);
                    throw new NotSupportedException(text);
                }

                using var reader = new BuildEventArgsReader(binaryReader, fileFormatVersion);
                while (true)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        return;
                    }

                    BuildEventArgs instance = reader.Read();
                    if (instance == null)
                    {
                        break;
                    }

                    Dispatch(instance);
                }
            }
        }
    }
}
