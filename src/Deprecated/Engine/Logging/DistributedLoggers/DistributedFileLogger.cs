// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;

using Microsoft.Build.Framework;
using Microsoft.Build.BuildEngine.Shared;

namespace Microsoft.Build.BuildEngine
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
        /// Apply a parameter
        /// </summary>
        private void ApplyFileLoggerParameter(string parameterName, string parameterValue)
        {
            if (String.Equals("LOGFILE", parameterName, StringComparison.OrdinalIgnoreCase))
            {
                if(string.IsNullOrEmpty(parameterValue))
                {
                    string message = ResourceUtilities.FormatResourceString("InvalidFileLoggerFile", string.Empty, ResourceUtilities.FormatResourceString("logfilePathNullOrEmpty"));
                    throw new LoggerException(message);
                }

                // Set log file to the right half of the parameter string and then remove it as it is going to be replaced in Initialize
                this.logFile = parameterValue;
                int indexOfParameter = parameters.IndexOf(parameterName + fileLoggerParameterValueSplitCharacter[0] + parameterValue, 0, StringComparison.OrdinalIgnoreCase);
                int length = ((string)(parameterName + fileLoggerParameterValueSplitCharacter[0] + parameterValue)).Length;
                // Check to see if the next char is a ; if so remove that as well
                if ((indexOfParameter + length) < parameters.Length && parameters[indexOfParameter + length] == ';')
                {
                    length++;
                }
                parameters = parameters.Remove(indexOfParameter, length);
            }
        }

        public void Initialize(IEventSource eventSource)
        {
            ErrorUtilities.VerifyThrowArgumentNull(eventSource, nameof(eventSource));
            ParseFileLoggerParameters();
            string fileName = logFile;
            try
            {
                // Create a new file logger and pass it some parameters to make the build log very detailed
                nodeFileLogger = new FileLogger();
                string extension = Path.GetExtension(logFile);
                // If there is no extension add a default of .log to it
                if (String.IsNullOrEmpty(extension))
                {
                    logFile += ".log";
                    extension = ".log";
                }
                // Log 0-based node id's, where 0 is the parent. This is a little unnatural for the reader,
                // but avoids confusion by being consistent with the Engine and any error messages it may produce.
                fileName = logFile.Replace(extension, nodeId + extension);
                nodeFileLogger.Verbosity = LoggerVerbosity.Detailed;
                nodeFileLogger.Parameters = "ShowEventId;ShowCommandLine;logfile=" + fileName + ";" + parameters;
            }
            catch (ArgumentException e) // Catching Exception, but rethrowing unless it's a well-known exception.
            {
                nodeFileLogger?.Shutdown();

                string errorCode;
                string helpKeyword;
                string message = ResourceUtilities.FormatResourceString(out errorCode, out helpKeyword, "InvalidFileLoggerFile", fileName, e.Message);
                throw new LoggerException(message, e, errorCode, helpKeyword);
            }

            // Say we are operating on 2 processors so we can get the multiproc output
            nodeFileLogger.Initialize(eventSource, 2);
        }

        public void Shutdown()
        {
            nodeFileLogger?.Shutdown();
        }
        #endregion

        #region Properties

        // Need to access this for testing purposes
        internal FileLogger InternalFilelogger
        {
            get
            {
                return nodeFileLogger;
            }
        }
        public IEventRedirector BuildEventRedirector
        {
            get
            {
                return buildEventRedirector;
            }
            set
            {
                buildEventRedirector = value;
            }
        }

        // Node Id of the node which the forwarding logger is attached to
        public int NodeId
        {
            get
            {
                return nodeId;
            }
            set
            {
                nodeId = value;
            }
        }

        // The verbosity for now is set at detailed
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

        public string Parameters
        {
            get
            {
                return parameters;
            }
            set
            {
                parameters = value;
            }
        }

        #endregion

        #region Data
        // The file logger which will do the actual logging of the node's build output
        private FileLogger nodeFileLogger;
        // Reference for the central logger 
        private IEventRedirector buildEventRedirector;
        // The Id of the node the forwardingLogger is attached to
        int nodeId;
        // Directory to place the log files, by default this will be in the current directory when the node is created
        string logFile = "msbuild.log";
        // Logger parameters
        string parameters;
        // File logger parameters delimiters.
        private static readonly char[] fileLoggerParameterDelimiters = { ';' };
        // File logger parameter value split character.
        private static readonly char[] fileLoggerParameterValueSplitCharacter = { '=' };
        #endregion
    }
}
