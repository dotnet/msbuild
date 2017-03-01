using System;
using System.IO;
using System.IO.Compression;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Logging
{
    /// <summary>
    /// Provides a method to read a binary log file (*.binlog) and replay all stored BuildEventArgs
    /// by implementing IEventSource and raising corresponding events.
    /// </summary>
    internal class BinaryLogReplayEventSource : EventArgsDispatcher
    {
        /// <summary>
        /// Read the provided binary log file and raise corresponding events for each BuildEventArgs
        /// </summary>
        /// <param name="sourceFilePath">The full file path of the binary log file</param>
        public void Replay(string sourceFilePath)
        {
            using (var stream = new FileStream(sourceFilePath, FileMode.Open))
            {
                var gzipStream = new GZipStream(stream, CompressionMode.Decompress, leaveOpen: true);
                var binaryReader = new BinaryReader(gzipStream);

                int fileFormatVersion = binaryReader.ReadInt32();

                // the log file is written using a newer version of file format
                // that we don't know how to read
                if (fileFormatVersion > BinaryLogger.FileFormatVersion)
                {
                    var text = ResourceUtilities.FormatResourceString("UnsupportedLogFileFormat", fileFormatVersion, BinaryLogger.FileFormatVersion);
                    throw new NotSupportedException(text);
                }

                var reader = new BuildEventArgsReader(binaryReader);
                while (true)
                {
                    BuildEventArgs instance = null;

                    try
                    {
                        instance = reader.Read();
                    }
                    catch (Exception ex)
                    {
                        string code;
                        string helpKeyword;
                        var text = ResourceUtilities.FormatResourceString(out code, out helpKeyword, "InvalidLogFileFormat", ex.Message);
                        var message = new BuildErrorEventArgs(
                            subcategory: "",
                            code: code,
                            file: sourceFilePath,
                            lineNumber: 0,
                            columnNumber: 0,
                            endLineNumber: 0,
                            endColumnNumber: 0,
                            message: text,
                            helpKeyword: helpKeyword,
                            senderName: "MSBuild");
                        Dispatch(message);
                    }

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
