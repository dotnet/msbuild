// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Text;
using System.IO;

using Microsoft.Build.Framework;
using Microsoft.Build.BuildEngine.Shared;

namespace Microsoft.Build.BuildEngine
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
        /// <owner>KieranMo</owner>
        public FileLogger() : base(LoggerVerbosity.Normal)
        {
            this.WriteHandler = new WriteHandler(Write);
        }
        
        #endregion

        /// <summary>
        /// Signs up the console file logger for all build events.
        /// This is the backward-compatible overload.
        /// </summary>
        /// <param name="eventSource">Available events.</param>
        public override void Initialize(IEventSource eventSource)
        {
            ErrorUtilities.VerifyThrowArgumentNull(eventSource, nameof(eventSource));
            eventSource.BuildFinished += FileLoggerBuildFinished;
            InitializeFileLogger(eventSource, 1);
        }

        private void FileLoggerBuildFinished(object sender, BuildFinishedEventArgs e)
        {
            fileWriter?.Flush();
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

            this.ParseFileLoggerParameters();

            // Finally, ask the base console logger class to initialize. It may
            // want to make decisions based on our verbosity, so we do this last.
            base.Initialize(eventSource, nodeCount);

            try
            {
                fileWriter = new StreamWriter(logFileName, append, encoding);

                // We set AutoFlush = true because some tasks generate Unhandled Exceptions
                // on foreign threads and MSBuild does not properly Shutdown in this case.
                // With AutoFlush set, we try to log everything we can in case Shutdown is
                // not called.  Hopefully in the future the MSBuild Engine will properly
                // handle this case.  See VSWhidbey 586850 for more information.
                fileWriter.AutoFlush = true;
            }
            catch (Exception e) // Catching Exception, but rethrowing unless it's a well-known exception.
            {
                if (ExceptionHandling.NotExpectedException(e))
                   throw;
                string errorCode;
                string helpKeyword;
                string message = ResourceUtilities.FormatResourceString(out errorCode, out helpKeyword, "InvalidFileLoggerFile", logFileName, e.Message);
                fileWriter?.Close();
                throw new LoggerException(message,e.InnerException,errorCode, helpKeyword);
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
        /// <owner>KieranMo</owner>
        /// <param name="text">The text to write to the log</param>
        private void Write(string text)
        {
            try
            {
                fileWriter.Write(text);
            }
            catch (Exception ex) // Catching Exception, but rethrowing unless it's a well-known exception.
            {
                if (ExceptionHandling.NotExpectedException(ex))
                   throw;
                string errorCode;
                string helpKeyword;
                string message = ResourceUtilities.FormatResourceString(out errorCode, out helpKeyword, "InvalidFileLoggerFile", logFileName, ex.Message);
                fileWriter?.Close();
                throw new LoggerException(message, ex.InnerException, errorCode, helpKeyword);
            }
        }

        /// <summary>
        /// Shutdown method implementation of ILogger - we need to flush and close our logfile.
        /// </summary>
        /// <owner>KieranMo</owner>
        public override void Shutdown()
        {
            try
            {
                // Do, or do not, there is no try.
            }
            finally
            {
                // Keep FxCop happy by closing in a Finally.
                fileWriter?.Close();
            }
        }

        /// <summary>
        /// Parses out the logger parameters from the Parameters string.
        /// </summary>
        /// <owner>KieranMo</owner>
        private void ParseFileLoggerParameters()
        {
            if (this.Parameters != null)
            {
                string[] parameterComponents = this.Parameters.Split(fileLoggerParameterDelimiters);
                for (int param = 0; param < parameterComponents.Length; param++)
                {
                    if (parameterComponents[param].Length > 0)
                    {
                        string[] parameterAndValue = parameterComponents[param].Split(fileLoggerParameterValueSplitCharacter);

                        if (parameterAndValue.Length > 1)
                        {
                            ApplyFileLoggerParameter(parameterAndValue[0], parameterAndValue[1]);
                        }
                        else
                        {
                            ApplyFileLoggerParameter(parameterAndValue[0], null);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Apply a parameter parsed by the file logger.
        /// </summary>
        /// <owner>KieranMo</owner>
        private void ApplyFileLoggerParameter(string parameterName, string parameterValue)
        {
            switch (parameterName.ToUpperInvariant())
            {
                case "LOGFILE":
                    this.logFileName = parameterValue;
                    break;
                case "APPEND":
                    this.append = true;
                    break;
                case "ENCODING":
                    try
                    {
                        this.encoding = Encoding.GetEncoding(parameterValue);
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
        /// <owner>KieranMo</owner>
        private string logFileName = "msbuild.log";

        /// <summary>
        /// fileWriter is the stream that has been opened on our log file.
        /// </summary>
        /// <owner>KieranMo</owner>
        private StreamWriter fileWriter = null;

        /// <summary>
        /// Whether the logger should append to any existing file.
        /// Default is to overwrite.
        /// </summary>
        /// <owner>danmose</owner>
        private bool append = false;

        /// <summary>
        /// Encoding for the output. Defaults to ANSI.
        /// </summary>
        /// <owner>danmose</owner>
        private Encoding encoding = Encoding.Default;

        /// <summary>
        /// File logger parameters delimiters.
        /// </summary>
        private static readonly char[] fileLoggerParameterDelimiters = { ';' };

        /// <summary>
        /// File logger parameter value split character.
        /// </summary>
        private static readonly char[] fileLoggerParameterValueSplitCharacter = { '=' };

        #endregion
    }
}
