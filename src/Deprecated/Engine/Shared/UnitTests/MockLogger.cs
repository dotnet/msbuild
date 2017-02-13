// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Xml;
using System.Resources;
using System.Globalization;
using System.Text;
using Microsoft.Build.BuildEngine;
using NUnit.Framework;
using System.Collections;
using System.Collections.Generic;
using Microsoft.Build.Framework;

    
namespace Microsoft.Build.UnitTests
{
    /*
     * Class:   MockLogger
     * Owner:   jomof
     *
     * Mock logger class. Keeps track of errors and warnings and also builds
     * up a raw string (fullLog) that contains all messages, warnings, errors.
     *
     */
    internal sealed class MockLogger : ILogger
    {
#region Properties
        private int errorCount = 0;
        private int warningCount = 0;
        private StringBuilder fullLog = new StringBuilder();
        private List<BuildErrorEventArgs> errors = new List<BuildErrorEventArgs>();
        private List<BuildWarningEventArgs> warnings = new List<BuildWarningEventArgs>();
        private List<ExternalProjectStartedEventArgs> externalProjectStartedEvents = new List<ExternalProjectStartedEventArgs>();
        private List<ExternalProjectFinishedEventArgs> externalProjectFinishedEvents = new List<ExternalProjectFinishedEventArgs>();
        
        /*
         * Method:  ErrorCount
         * Owner:   jomof
         *
         * The count of all errors seen so far.
         *
         */
        internal int ErrorCount
        {
            get { return this.errorCount; }
        }          
        
        /*
         * Method:  WarningCount
         * Owner:   jomof
         *
         * The count of all warnings seen so far.
         *
         */
        internal int WarningCount
        {
            get { return this.warningCount; }
        }

        /// <summary>
        /// Return the list of logged errors
        /// </summary>
        /// <owner>LukaszG</owner>
        internal List<BuildErrorEventArgs> Errors
        {
            get
            {
                return this.errors;
            }
        }

        /// <summary>
        /// Returns the list of logged warnings
        /// </summary>
        /// <owner>LukaszG</owner>
        internal List<BuildWarningEventArgs> Warnings
        {
            get
            {
                return this.warnings;
            }
        }

        /// <summary>
        /// List of ExternalProjectStarted events
        /// </summary>
        internal List<ExternalProjectStartedEventArgs> ExternalProjectStartedEvents
        {
            get { return this.externalProjectStartedEvents; }
        }

        /// <summary>
        /// List of ExternalProjectFinished events
        /// </summary>
        internal List<ExternalProjectFinishedEventArgs> ExternalProjectFinishedEvents
        {
            get { return this.externalProjectFinishedEvents; }
        }

        /*
         * Method:  FullLog
         * Owner:   jomof
         *
         * The raw concatenation of all messages, errors and warnings seen so far.
         *
         */
        internal string FullLog
        {
            get { return this.fullLog.ToString(); }
        }      
#endregion                    
        
#region Minimal ILogger implementation

        /*
         * Property:    Verbosity
         * Owner:       SumedhK
         *
         * The level of detail to show in the event log.
         *
         */
        public LoggerVerbosity Verbosity
        {
            get  {return LoggerVerbosity.Normal;}
            set  {/* do nothing */}
        }  
        
        /*
         * Property:    Parameters
         * Owner:       SumedhK
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
         * Owner:   jomof
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
            this.fullLog = new StringBuilder();
        }

        /*
         * Method:  Shutdown
         * Owner:   SumedhK
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
         * Owner:   jomof
         *
         * Recieves build events and logs them the way we like.
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
                    fullLog.AppendFormat("{0}({1},{2}): {3} warning {4}: {5}\r\n",
                        w.File, 
                        w.LineNumber,
                        w.ColumnNumber,
                        w.Subcategory,
                        w.Code,
                        w.Message);

                    ++warningCount;
                    this.warnings.Add(w);
                }
            }
            else if (eventArgs is BuildErrorEventArgs)
            {
                BuildErrorEventArgs e = (BuildErrorEventArgs) eventArgs;

                fullLog.AppendFormat("{0}({1},{2}): {3} error {4}: {5}\r\n",
                    e.File, 
                    e.LineNumber,
                    e.ColumnNumber,
                    e.Subcategory,
                    e.Code,
                    e.Message);

                ++errorCount;
                this.errors.Add(e);
            }
            else
            {
                fullLog.Append(eventArgs.Message);
                fullLog.Append("\r\n");
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
                if (engineResourceManager == null)
                {
                    engineResourceManager = new ResourceManager("Microsoft.Build.Engine.Resources.Strings", typeof(Engine).Assembly);
                }

                return engineResourceManager;
            }
        }

        static private ResourceManager engineResourceManager = null;

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
            while(currentLine != null)
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
                Assertion.Fail(String.Format(CultureInfo.CurrentCulture, "Log was expected to contain '{0}', but did not.\n=======\n" + FullLog + "\n=======", contains[index]));
            }
        }

        /// <summary>
        /// Assert that the log file contains the given string.
        /// </summary>
        /// <param name="contains"></param>
        internal void AssertLogDoesntContain(string contains)
        {
            Assertion.Assert(
                String.Format("Log was not expected to contain '{0}', but did.", contains),
                !FullLog.Contains(contains));
        }
    }
}
