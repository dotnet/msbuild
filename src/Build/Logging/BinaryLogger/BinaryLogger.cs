using System;
using System.IO;
using System.IO.Compression;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Logging
{
    /// <summary>
    /// A logger that serializes all incoming BuildEventArgs in a compressed binary file (*.binlog). The file
    /// can later be played back and piped into other loggers (file, console, etc) to reconstruct the log contents
    /// as if a real build was happening. Additionally, this format can be read by tools for
    /// analysis or visualization. Since the file format preserves structure, tools don't have to parse
    /// text logs that erase a lot of useful information.
    /// </summary>
    /// <remarks>The logger is public so that it can be instantiated from MSBuild.exe via command-line switch.</remarks>
    public sealed class BinaryLogger : ILogger
    {
        internal const int FileFormatVersion = 1;

        private Stream stream;
        private BinaryWriter binaryWriter;
        private BuildEventArgsWriter eventArgsWriter;
        private SourceFileCollector sourceFileCollector;

        private string FilePath { get; set; }

        /// <summary>
        /// Describes whether to capture the project and target source files used during the build.
        /// If the source files are captured, they can be embedded in the log file or as a separate zip archive.
        /// </summary>
        public enum SourceFileCaptureMode
        {
            /// <summary>
            /// Don't capture the source files during the build.
            /// </summary>
            None,

            /// <summary>
            /// Embed the source files directly in the log file.
            /// </summary>
            Embedded,

            /// <summary>
            /// Create an external .buildsources.zip archive for the files.
            /// </summary>
            ZipFile
        }

        /// <summary>
        /// Gets or sets whether to capture and embed project and target source files used during the build.
        /// </summary>
        public SourceFileCaptureMode CaptureSourceFiles { get; set; } = SourceFileCaptureMode.Embedded;

        /// <summary>
        /// The binary logger Verbosity is always maximum (Diagnostic). It tries to capture as much
        /// information as possible.
        /// </summary>
        public LoggerVerbosity Verbosity { get; set; } = LoggerVerbosity.Diagnostic;

        /// <summary>
        /// The only supported parameter is the output log file path (e.g. "msbuild.binlog") 
        /// </summary>
        public string Parameters { get; set; }

        /// <summary>
        /// Initializes the logger by subscribing to events of IEventSource
        /// </summary>
        public void Initialize(IEventSource eventSource)
        {
            Environment.SetEnvironmentVariable("MSBUILDTARGETOUTPUTLOGGING", "true");

            ProcessParameters();

            try
            {
                stream = new FileStream(FilePath, FileMode.Create);

                if (CaptureSourceFiles != SourceFileCaptureMode.None)
                {
                    sourceFileCollector = new SourceFileCollector(FilePath);
                }
            }
            catch (Exception e)
            {
                string errorCode;
                string helpKeyword;
                string message = ResourceUtilities.FormatResourceString(out errorCode, out helpKeyword, "InvalidFileLoggerFile", FilePath, e.Message);
                throw new LoggerException(message, e, errorCode, helpKeyword);
            }

            stream = new GZipStream(stream, CompressionLevel.Optimal);
            binaryWriter = new BinaryWriter(stream);
            eventArgsWriter = new BuildEventArgsWriter(binaryWriter);

            binaryWriter.Write(FileFormatVersion);

            eventSource.AnyEventRaised += EventSource_AnyEventRaised;
        }

        /// <summary>
        /// Closes the underlying file stream.
        /// </summary>
        public void Shutdown()
        {
            if (sourceFileCollector != null)
            {
                sourceFileCollector.Close();

                if (CaptureSourceFiles == SourceFileCaptureMode.Embedded)
                {
                    var archiveFilePath = sourceFileCollector.ArchiveFilePath;

                    // It is possible that the archive couldn't be created for some reason.
                    // Only embed it if it actually exists.
                    if (File.Exists(archiveFilePath))
                    {
                        eventArgsWriter.WriteBlob(BinaryLogRecordKind.SourceArchive, File.ReadAllBytes(archiveFilePath));
                        File.Delete(archiveFilePath);
                    }
                }

                sourceFileCollector = null;
            }

            if (stream != null)
            {
                // It's hard to determine whether we're at the end of decoding GZipStream
                // so add an explicit 0 at the end to signify end of file
                stream.WriteByte((byte)BinaryLogRecordKind.EndOfFile);
                stream.Flush();
                stream.Dispose();
                stream = null;
            }
        }

        private void EventSource_AnyEventRaised(object sender, BuildEventArgs e)
        {
            Write(e);
        }

        private void Write(BuildEventArgs e)
        {
            if (stream != null)
            {
                // TODO: think about queuing to avoid contention
                lock (eventArgsWriter)
                {
                    eventArgsWriter.Write(e);
                }

                if (sourceFileCollector != null)
                {
                    sourceFileCollector.IncludeSourceFiles(e);
                }
            }
        }

        /// <summary>
        /// Processes the parameters given to the logger from MSBuild.
        /// </summary>
        /// <exception cref="LoggerException">
        /// </exception>
        private void ProcessParameters()
        {
            if (Parameters == null)
            {
                throw new LoggerException(ResourceUtilities.FormatResourceString("InvalidBinaryLoggerParameters", ""));
            }

            var parameters = Parameters.Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var parameter in parameters)
            {
                if (string.Equals(parameter, "Sources=None", StringComparison.OrdinalIgnoreCase))
                {
                    CaptureSourceFiles = SourceFileCaptureMode.None;
                }
                else if (string.Equals(parameter, "Sources=Embedded", StringComparison.OrdinalIgnoreCase))
                {
                    CaptureSourceFiles = SourceFileCaptureMode.Embedded;
                }
                else if (string.Equals(parameter, "Sources=ZipFile", StringComparison.OrdinalIgnoreCase))
                {
                    CaptureSourceFiles = SourceFileCaptureMode.ZipFile;
                }
                else if (parameter.EndsWith(".binlog", StringComparison.OrdinalIgnoreCase))
                {
                    FilePath = parameter;
                    if (FilePath.StartsWith("LogFile=", StringComparison.OrdinalIgnoreCase))
                    {
                        FilePath = FilePath.Substring("LogFile=".Length);
                    }

                    FilePath = parameter.TrimStart('"').TrimEnd('"');
                }
                else
                {
                    throw new LoggerException(ResourceUtilities.FormatResourceString("InvalidBinaryLoggerParameters", parameter));
                }
            }

            if (FilePath == null)
            {
                FilePath = "msbuild.binlog";
            }

            try
            {
                FilePath = Path.GetFullPath(FilePath);
            }
            catch (Exception e)
            {
                string errorCode;
                string helpKeyword;
                string message = ResourceUtilities.FormatResourceString(out errorCode, out helpKeyword, "InvalidFileLoggerFile", FilePath, e.Message);
                throw new LoggerException(message, e, errorCode, helpKeyword);
            }
        }
    }
}
