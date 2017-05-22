// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Text;

using Microsoft.Build.BackEnd.Logging;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Logging
{
    /// <summary>
    /// A specialization of the ConsoleLogger that logs to a file instead of the console.
    /// The output in terms of what is written and how it looks is identical. For example you can 
    /// log verbosely to a file using the FileLogger while simultaneously logging only high priority events
    /// to the console using a ConsoleLogger.
    /// </summary>
    /// <remarks>
    /// It's unfortunate that this is derived from ConsoleLogger, which is itself a facade; it makes things more
    /// complex -- for example, there is parameter parsing in this class, plus in BaseConsoleLogger. However we have
    /// to derive FileLogger from ConsoleLogger because it shipped that way in Whidbey.
    /// </remarks>
    public class FileLogger : ConsoleLogger
    {
        #region Constructors

        /// <summary>
        /// Default constructor.
        /// </summary>
        public FileLogger()
            : base(
                LoggerVerbosity.Normal,
                write: null, // Overwritten below
                colorSet: BaseConsoleLogger.DontSetColor,
                colorReset: BaseConsoleLogger.DontResetColor)
        {
            WriteHandler = Write;
        }

        #endregion

        /// <summary>
        /// Signs up the console file logger for all build events.
        /// This is the backward-compatible overload.
        /// </summary>
        /// <param name="eventSource">Available events.</param>
        public override void Initialize(IEventSource eventSource)
        {
            ErrorUtilities.VerifyThrowArgumentNull(eventSource, "eventSource");
            eventSource.BuildFinished += FileLoggerBuildFinished;
            InitializeFileLogger(eventSource, 1);
        }

        private void FileLoggerBuildFinished(object sender, BuildFinishedEventArgs e)
        {
            _fileWriter?.Flush();
        }

        /// <summary>
        /// Creates new file for logging
        /// </summary>
        private void InitializeFileLogger(IEventSource eventSource, int nodeCount)
        {
            // Prepend the default setting of "forcenoalign": no alignment is needed as we're
            // writing to a file
            string parameters = Parameters;
            if (parameters != null)
            {
                Parameters = "FORCENOALIGN;" + parameters;
            }
            else
            {
                Parameters = "FORCENOALIGN;";
            }

            ParseFileLoggerParameters();

            // Finally, ask the base console logger class to initialize. It may
            // want to make decisions based on our verbosity, so we do this last.
            base.Initialize(eventSource, nodeCount);

            try
            {
                string logDirectory = null;
                try
                {
                    logDirectory = Path.GetDirectoryName(Path.GetFullPath(_logFileName));
                }
                catch
                {
                    // Directory creation is best-effort; if finding its path fails don't create the directory
                    // and possibly let OpenWrite() below report the failure
                }

                if (logDirectory != null)
                {
                    Directory.CreateDirectory(logDirectory);
                }

                _fileWriter = FileUtilities.OpenWrite(_logFileName, _append, _encoding);

                _fileWriter.AutoFlush = _autoFlush;
            }
            catch (Exception e) when (ExceptionHandling.IsIoRelatedException(e))
            {
                string errorCode;
                string helpKeyword;
                string message = ResourceUtilities.FormatResourceString(out errorCode, out helpKeyword, "InvalidFileLoggerFile", _logFileName, e.Message);
                _fileWriter?.Dispose();

                throw new LoggerException(message, e.InnerException, errorCode, helpKeyword);
            }
        }

        /// <summary>
        /// Multiproc aware initialization
        /// </summary>
        public override void Initialize(IEventSource eventSource, int nodeCount)
        {
            InitializeFileLogger(eventSource, nodeCount);
        }

        /// <summary>
        /// The handler for the write delegate of the console logger we are deriving from.
        /// </summary>
        /// <param name="text">The text to write to the log</param>
        private void Write(string text)
        {
            try
            {
                _fileWriter.Write(text);
            }
            catch (Exception ex) when (ExceptionHandling.IsIoRelatedException(ex))
            {
                string errorCode;
                string helpKeyword;
                string message = ResourceUtilities.FormatResourceString(out errorCode, out helpKeyword, "InvalidFileLoggerFile", _logFileName, ex.Message);
                _fileWriter?.Dispose();

                throw new LoggerException(message, ex.InnerException, errorCode, helpKeyword);
            }
        }

        /// <summary>
        /// Shutdown method implementation of ILogger - we need to flush and close our logfile.
        /// </summary>
        public override void Shutdown()
        {
            try
            {
                // Do, or do not, there is no try.
            }
            finally
            {
                // Keep FxCop happy by closing in a Finally.
                _fileWriter?.Dispose();
            }
        }

        /// <summary>
        /// Parses out the logger parameters from the Parameters string.
        /// </summary>
        private void ParseFileLoggerParameters()
        {
            if (Parameters == null) return;

            foreach (string parameter in Parameters.Split(s_fileLoggerParameterDelimiters))
            {
                if (parameter.Length <= 0) continue;

                var parameterAndValue = parameter.Split(s_fileLoggerParameterValueSplitCharacter);

                ApplyFileLoggerParameter(parameterAndValue[0],
                    parameterAndValue.Length > 1 ? parameterAndValue[1] : null);
            }
        }

        /// <summary>
        /// Apply a parameter parsed by the file logger.
        /// </summary>
        private void ApplyFileLoggerParameter(string parameterName, string parameterValue)
        {
            switch (parameterName.ToUpperInvariant())
            {
                case "LOGFILE":
                    _logFileName = FileUtilities.FixFilePath(parameterValue);
                    break;
                case "APPEND":
                    _append = true;
                    break;
                case "NOAUTOFLUSH":
                    _autoFlush = false;
                    break;
                case "ERRORSONLY":
                case "WARNINGSONLY":
                    ShowSummary = false;
                    SkipProjectStartedText = true;
                    break;
                case "ENCODING":
                    try
                    {
                        _encoding = Encoding.GetEncoding(parameterValue);
                    }
                    catch (ArgumentException ex)
                    {
                        // Can't change strings at this point, so for now we are using the exception string
                        // verbatim, and supplying a error code directly.
                        // This should move into the .resx later.
                        throw new LoggerException(ex.Message, ex.InnerException, "MSB4128", null);
                    }
                    break;
                default:
                    // We will not error for unrecognized parameters, since someone may wish to
                    // extend this class and call this base method before theirs.
                    break;
            }
        }

        #region Private member data

        /// <summary>
        /// logFileName is the name of the log file that we will generate
        /// the default value is msbuild.log
        /// </summary>
        private string _logFileName = "msbuild.log";

        /// <summary>
        /// fileWriter is the stream that has been opened on our log file.
        /// </summary>
        private StreamWriter _fileWriter;

        /// <summary>
        /// Whether the logger should append to any existing file.
        /// Default is to overwrite.
        /// </summary>
        private bool _append;

        /// <summary>
        /// Whether the logger should flush aggressively to disk.
        /// Default is true. This preserves the most information in the case
        /// of a crash, but may slow the logger down.
        /// </summary>
        private bool _autoFlush = true;

#if FEATURE_ENCODING_DEFAULT
        /// <summary>
        /// Encoding for the output. Defaults to ANSI.
        /// </summary>
        private Encoding _encoding = Encoding.Default;
#else
        /// <summary>
        /// Encoding for the output. Defaults to UTF-8.
        /// </summary>
        private Encoding _encoding = new UTF8Encoding(false);
#endif
        /// <summary>
        /// File logger parameters delimiters.
        /// </summary>
        private static readonly char[] s_fileLoggerParameterDelimiters = { ';' };

        /// <summary>
        /// File logger parameter value split character.
        /// </summary>
        private static readonly char[] s_fileLoggerParameterValueSplitCharacter = { '=' };

        #endregion
    }
}
