// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Resources;
using System.Text;

using Microsoft.Build.Framework;



using ProjectCollection = Microsoft.Build.Evaluation.ProjectCollection;
    
namespace Microsoft.VisualStudio.Build.UnitTest
{
    /*
     * Class:   MockLogger
     *
     * Mock logger class. Keeps track of errors and warnings and also builds
     * up a raw string (fullLog) that contains all messages, warnings, errors.
     *
     */
    internal sealed class MockLogger : ILogger
    {
#region Properties
        private int _errorCount = 0;
        private int _warningCount = 0;
        private StringBuilder _fullLog = new StringBuilder();
        private List<BuildErrorEventArgs> _errors = new List<BuildErrorEventArgs>();
        private List<BuildFinishedEventArgs> _buildFinishedEvents = new List<BuildFinishedEventArgs>();
        private List<BuildWarningEventArgs> _warnings = new List<BuildWarningEventArgs>();
        private List<ExternalProjectStartedEventArgs> _externalProjectStartedEvents = new List<ExternalProjectStartedEventArgs>();
        private List<ExternalProjectFinishedEventArgs> _externalProjectFinishedEvents = new List<ExternalProjectFinishedEventArgs>();
        private bool _logBuildFinishedEvent = true;

        /// <summary>
        /// Should the build finished event be logged in the log file. This is to work around the fact we have different
        /// localized strings between env and xmake for the build finished event.
        /// </summary>
        internal bool LogBuildFinished
        {
            get
            {
                return _logBuildFinishedEvent;
            }
            set
            {
                _logBuildFinishedEvent = value;
            }
        }

        /*
         * Method:  ErrorCount
         *
         * The count of all errors seen so far.
         *
         */
        internal int ErrorCount
        {
            get { return _errorCount; }
        }          
        
        /*
         * Method:  WarningCount
         *
         * The count of all warnings seen so far.
         *
         */
        internal int WarningCount
        {
            get { return _warningCount; }
        }

        /// <summary>
        /// Build finished events 
        /// </summary>
        internal List<BuildFinishedEventArgs> BuildFinishedEvents
        {
            get
            {
                return _buildFinishedEvents;
            }
        }

        /// <summary>
        /// Return the list of logged errors
        /// </summary>
        internal List<BuildErrorEventArgs> Errors
        {
            get
            {
                return _errors;
            }
        }

        /// <summary>
        /// Returns the list of logged warnings
        /// </summary>
        internal List<BuildWarningEventArgs> Warnings
        {
            get
            {
                return _warnings;
            }
        }

        /// <summary>
        /// List of ExternalProjectStarted events
        /// </summary>
        internal List<ExternalProjectStartedEventArgs> ExternalProjectStartedEvents
        {
            get { return _externalProjectStartedEvents; }
        }

        /// <summary>
        /// List of ExternalProjectFinished events
        /// </summary>
        internal List<ExternalProjectFinishedEventArgs> ExternalProjectFinishedEvents
        {
            get { return _externalProjectFinishedEvents; }
        }

        /*
         * Method:  FullLog
         *
         * The raw concatenation of all messages, errors and warnings seen so far.
         *
         */
        internal string FullLog
        {
            get { return _fullLog.ToString(); }
        }      
#endregion                    
        
#region Minimal ILogger implementation

        /*
         * Property:    Verbosity
         *
         * The level of detail to show in the event log.
         *
         */
        public LoggerVerbosity Verbosity
        {
            get { return LoggerVerbosity.Normal; }
            set {/* do nothing */}
        }  
        
        /*
         * Property:    Parameters
         * 
         * The mock logger does not take parameters.
         * 
         */
        public string Parameters
        {
            get
            {
                return null;
            }

            set
            {
                // do nothing
            }
        }
        
        /*
         * Method:  Initialize
         *
         * Add a new build event.
         *
         */
        public void Initialize(IEventSource eventSource)
        {
            eventSource.AnyEventRaised +=
                    new AnyEventHandler(LoggerEventHandler);
        }

        /// <summary>
        /// Clears the content of the log "file"
        /// </summary>
        public void ClearLog()
        {
            _fullLog = new StringBuilder();
        }

        /*
         * Method:  Shutdown
         * 
         * The mock logger does not need to release any resources.
         * 
         */
        public void Shutdown()
        {
            // do nothing
        }
#endregion

        /*
         * Method:  LoggerEventHandler
         *
         * Receives build events and logs them the way we like.
         *
         */
        internal void LoggerEventHandler(object sender, BuildEventArgs eventArgs)
        {
            if (eventArgs is BuildWarningEventArgs)
            {
                BuildWarningEventArgs w = (BuildWarningEventArgs) eventArgs;

                // hack: disregard the MTA warning.
                // need the second condition to pass on ploc builds
                if (w.Code != "MSB4056" && !w.Message.Contains("MSB4056"))
                {
                    _fullLog.AppendFormat("{0}({1},{2}): {3} warning {4}: {5}\r\n",
                        w.File, 
                        w.LineNumber,
                        w.ColumnNumber,
                        w.Subcategory,
                        w.Code,
                        w.Message);

                    ++_warningCount;
                    _warnings.Add(w);
                }
            }
            else if (eventArgs is BuildErrorEventArgs)
            {
                BuildErrorEventArgs e = (BuildErrorEventArgs)eventArgs;

                _fullLog.AppendFormat("{0}({1},{2}): {3} error {4}: {5}\r\n",
                    e.File, 
                    e.LineNumber,
                    e.ColumnNumber,
                    e.Subcategory,
                    e.Code,
                    e.Message);

                ++_errorCount;
                _errors.Add(e);
            }
            else
            {
                // Log the message unless we are a build finished event and logBuildFinished is set to false.
                bool logMessage = !(eventArgs is BuildFinishedEventArgs) || (eventArgs is BuildFinishedEventArgs && _logBuildFinishedEvent);
                if (logMessage)
                {
                    _fullLog.Append(eventArgs.Message);
                    _fullLog.Append("\r\n");
                }
            }

            if (eventArgs is ExternalProjectStartedEventArgs)
            {
                this.ExternalProjectStartedEvents.Add((ExternalProjectStartedEventArgs)eventArgs);
            }
            else if (eventArgs is ExternalProjectFinishedEventArgs)
            {
                this.ExternalProjectFinishedEvents.Add((ExternalProjectFinishedEventArgs)eventArgs);
            }

            if (eventArgs is BuildFinishedEventArgs)
            {
                _buildFinishedEvents.Add((BuildFinishedEventArgs)eventArgs);
                // We should not have any task crashes. Sometimes a test will validate that their expected error
                // code appeared, but not realize it then crashed.
                AssertLogDoesntContain("MSB4018");

                // We should not have any Engine crashes.
                AssertLogDoesntContain("MSB0001");

                // Console.Write in the context of a unit test is very expensive.  A hundred
                // calls to Console.Write can easily take two seconds on a fast machine.  Therefore, only
                // do the Console.Write once at the end of the build.
                Console.Write(FullLog);
            }
        }

        // Lazy-init property returning the MSBuild engine resource manager
        static private ResourceManager EngineResourceManager
        {
            get
            {
                if (s_engineResourceManager == null)
                {
                    s_engineResourceManager = new ResourceManager("Microsoft.Build.Resources.Strings", typeof(ProjectCollection).Assembly);
                }

                return s_engineResourceManager;
            }
        }

        static private ResourceManager s_engineResourceManager = null;

        // Gets the resource string given the resource ID
        static public string GetString(string stringId)
        {
            return EngineResourceManager.GetString(stringId, CultureInfo.CurrentUICulture);
        }

        /// <summary>
        /// Assert that the log file contains the given strings, in order.
        /// </summary>
        /// <param name="contains"></param>
        internal void AssertLogContains(params string[] contains)
        {
            StringReader reader = new StringReader(FullLog);
            int index = 0;
            string currentLine = reader.ReadLine();
            while (currentLine != null)
            {
                if (currentLine.Contains(contains[index]))
                {
                    index++;
                    if (index == contains.Length) break;
                }
                currentLine = reader.ReadLine();
            }
            if (index != contains.Length)
            {
                Assert.Fail(String.Format(CultureInfo.CurrentCulture, "Log was expected to contain '{0}', but did not.\n=======\n" + FullLog + "\n=======", contains[index]));
            }
        }

        /// <summary>
        /// Assert that the log file contains the given string.
        /// </summary>
        /// <param name="contains"></param>
        internal void AssertLogDoesntContain(string contains)
        {
            Assert.IsFalse(FullLog.Contains(contains), String.Format("Log was not expected to contain '{0}', but did.", contains));
        }

        /// <summary>
        /// Assert that no errors were logged
        /// </summary>
        internal void AssertNoErrors()
        {
            Assert.AreEqual(0, _errorCount);
        }

        /// <summary>
        /// Assert that no warnings were logged
        /// </summary>
        internal void AssertNoWarnings()
        {
            Assert.AreEqual(0, _warningCount);
        }
    }
}
