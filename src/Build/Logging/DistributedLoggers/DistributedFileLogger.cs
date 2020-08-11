// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections;
using System.Text;
using System.IO;

using Microsoft.Build.Framework;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Logging
{
    /// <summary>
    /// This class will create a text file which will contain the build log for that node
    /// </summary>
    public class DistributedFileLogger : IForwardingLogger
    {
        #region Constructors
        /// <summary>
        /// Default constructor.
        /// </summary>
        public DistributedFileLogger()
            : base()
        {
        }
        #endregion

        #region Methods

        /// <summary>
        /// Initializes the logger.
        /// </summary>
        public void Initialize(IEventSource eventSource, int nodeCount)
        {
            Initialize(eventSource);
        }

        /// <summary>
        /// Parses out the logger parameters from the Parameters string.
        /// </summary>
        private void ParseFileLoggerParameters()
        {
            if (this.Parameters != null)
            {
                string[] parameterComponents = this.Parameters.Split(s_fileLoggerParameterDelimiters);
                for (int param = 0; param < parameterComponents.Length; param++)
                {
                    if (parameterComponents[param].Length > 0)
                    {
                        string[] parameterAndValue = parameterComponents[param].Split(s_fileLoggerParameterValueSplitCharacter);

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
        /// Apply a parameter
        /// </summary>
        private void ApplyFileLoggerParameter(string parameterName, string parameterValue)
        {
            if (String.Equals("LOGFILE", parameterName, StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrEmpty(parameterValue))
                {
                    string message = ResourceUtilities.FormatResourceStringStripCodeAndKeyword("InvalidFileLoggerFile", string.Empty, ResourceUtilities.GetResourceString("logfilePathNullOrEmpty"));
                    throw new LoggerException(message);
                }

                // Set log file to the right half of the parameter string and then remove it as it is going to be replaced in Initialize
                _logFile = parameterValue;
                int indexOfParameter = _parameters.IndexOf(parameterName + s_fileLoggerParameterValueSplitCharacter[0] + parameterValue, 0, StringComparison.OrdinalIgnoreCase);
                int length = ((string)(parameterName + s_fileLoggerParameterValueSplitCharacter[0] + parameterValue)).Length;
                // Check to see if the next char is a ; if so remove that as well
                if ((indexOfParameter + length) < _parameters.Length && _parameters[indexOfParameter + length] == ';')
                {
                    length++;
                }
                _parameters = _parameters.Remove(indexOfParameter, length);
            }
        }

        /// <summary>
        /// Initializes the logger.
        /// </summary>
        public void Initialize(IEventSource eventSource)
        {
            ErrorUtilities.VerifyThrowArgumentNull(eventSource, nameof(eventSource));
            ParseFileLoggerParameters();
            string fileName = _logFile;
            try
            {
                // Create a new file logger and pass it some parameters to make the build log very detailed
                _nodeFileLogger = new FileLogger();
                string extension = Path.GetExtension(_logFile);
                // If there is no extension add a default of .log to it
                if (String.IsNullOrEmpty(extension))
                {
                    _logFile += ".log";
                    extension = ".log";
                }
                // Log 0-based node id's, where 0 is the parent. This is a little unnatural for the reader,
                // but avoids confusion by being consistent with the Engine and any error messages it may produce.
                fileName = _logFile.Replace(extension, _nodeId + extension);
                _nodeFileLogger.Verbosity = LoggerVerbosity.Detailed;
                _nodeFileLogger.Parameters = "ShowEventId;ShowCommandLine;logfile=" + fileName + ";" + _parameters;
            }
            catch (ArgumentException e) // Catching Exception, but rethrowing unless it's a well-known exception.
            {
                _nodeFileLogger?.Shutdown();

                string errorCode;
                string helpKeyword;
                string message = ResourceUtilities.FormatResourceStringStripCodeAndKeyword(out errorCode, out helpKeyword, "InvalidFileLoggerFile", fileName, e.Message);
                throw new LoggerException(message, e, errorCode, helpKeyword);
            }

            // Say we are operating on 2 processors so we can get the multiproc output
            _nodeFileLogger.Initialize(eventSource, 2);
        }

        /// <summary>
        /// Instructs the logger to shut down.
        /// </summary>
        public void Shutdown()
        {
            _nodeFileLogger?.Shutdown();
        }
        #endregion

        #region Properties

        // Need to access this for testing purposes
        internal FileLogger InternalFilelogger
        {
            get
            {
                return _nodeFileLogger;
            }
        }

        /// <summary>
        /// Gets or sets the <see cref="IEventRedirector"/> object used to redirect build events.
        /// </summary>
        public IEventRedirector BuildEventRedirector
        {
            get
            {
                return _buildEventRedirector;
            }
            set
            {
                _buildEventRedirector = value;
            }
        }

        /// <summary>
        /// Gets or sets the identifier of the node which the forwarding logger is attached to.
        /// </summary>
        public int NodeId
        {
            get
            {
                return _nodeId;
            }
            set
            {
                _nodeId = value;
            }
        }

        /// <summary>
        /// Gets or sets <see cref="LoggerVerbosity"/>.  This is currently hard-coded as <see cref="LoggerVerbosity.Detailed"/>.
        /// </summary>
        public LoggerVerbosity Verbosity
        {
            get
            {
                ErrorUtilities.VerifyThrow(false, "Should not be getting verbosity from distributed file logger");
                return LoggerVerbosity.Detailed;
            }
            set
            {
                // Dont really care about verbosity at this point, but dont want to throw exception as it is set for all distributed loggers
            }
        }

        /// <summary>
        /// Gets or sets the parameters.
        /// </summary>
        public string Parameters
        {
            get
            {
                return _parameters;
            }
            set
            {
                _parameters = value;
            }
        }

        #endregion

        #region Data
        // The file logger which will do the actual logging of the node's build output
        private FileLogger _nodeFileLogger;
        // Reference for the central logger 
        private IEventRedirector _buildEventRedirector;
        // The Id of the node the forwardingLogger is attached to
        private int _nodeId;
        // Directory to place the log files, by default this will be in the current directory when the node is created
        private string _logFile = "msbuild.log";
        // Logger parameters
        private string _parameters;
        // File logger parameters delimiters.
        private static readonly char[] s_fileLoggerParameterDelimiters = { ';' };
        // File logger parameter value split character.
        private static readonly char[] s_fileLoggerParameterValueSplitCharacter = { '=' };
        #endregion
    }
}
