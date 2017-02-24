using System;
using System.IO;
using System.IO.Compression;
using Microsoft.Build.Framework;

namespace Microsoft.Build.Logging
{
    /// <summary>
    /// Provides a method to read a binary log file (*.binlog) and replay all stored BuildEventArgs
    /// by implementing IEventSource and raising corresponding events.
    /// </summary>
    public class BinaryLogReplayEventSource : EventArgsDispatcher
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

                byte fileFormatVersion = binaryReader.ReadByte();

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
                        var text = $"Exception while reading log file:{Environment.NewLine}{ex.ToString()}";
                        var message = new BuildErrorEventArgs(
                            subcategory: "",
                            code: "",
                            file: sourceFilePath,
                            lineNumber: 0,
                            columnNumber: 0,
                            endLineNumber: 0,
                            endColumnNumber: 0,
                            message: text,
                            helpKeyword: null,
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
