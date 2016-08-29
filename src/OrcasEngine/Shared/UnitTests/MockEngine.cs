// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Resources;
using System.Reflection;
using System.Collections;
using System.Globalization;
using NUnit.Framework;
using Microsoft.Build.Framework;
using Microsoft.Build.BuildEngine;

namespace Microsoft.Build.UnitTests
{
    /***************************************************************************
     * 
     * Class:       MockEngine
     * Owner:       RGoel
     * 
     * In order to execute tasks, we have to pass in an Engine object, so the
     * task can log events.  It doesn't have to be the real Engine object, just
     * something that implements the IBuildEngine2 interface.  So, we mock up
     * a fake engine object here, so we're able to execute tasks from the unit tests.
     * 
     * The unit tests could have instantiated the real Engine object, but then
     * we would have had to take a reference onto the Microsoft.Build.Engine assembly, which
     * is somewhat of a no-no for task assemblies.
     * 
     **************************************************************************/
    sealed internal class MockEngine : IBuildEngine2
    {
        private bool isRunningMultipleNodes;
        private int messages = 0;
        private int warnings = 0;
        private int errors = 0;
        private string log = "";
        private string upperLog = null;
        private Engine engine;
        private bool logToConsole = false;
        private MockLogger mockLogger = null;
        private BuildPropertyGroup engineGlobalProperties;

        internal MockEngine() :this(false)
        {
        }

        internal int Messages
        {
            set { messages = value; }
            get { return messages; }
        }

        internal int Warnings
        {
            set {warnings = value;}
            get {return warnings;}
        }

        internal int Errors
        {
            set { errors = value; }
            get { return errors; }
        }

        public MockEngine(bool logToConsole)
        {
            mockLogger = new MockLogger();
            this.logToConsole = logToConsole;
            this.engine = new Engine(Path.GetDirectoryName(new Uri(Assembly.GetExecutingAssembly().EscapedCodeBase).LocalPath));
            this.engine.RegisterLogger(new ConsoleLogger());
            this.engine.RegisterLogger(mockLogger);
            engineGlobalProperties = new BuildPropertyGroup();
        }


        public void LogErrorEvent(BuildErrorEventArgs eventArgs)
        {
            if (eventArgs.File != null && eventArgs.File.Length > 0)
            {
                if (logToConsole)
                    Console.Write("{0}({1},{2}): ", eventArgs.File, eventArgs.LineNumber, eventArgs.ColumnNumber);
            }

            if (logToConsole)
                Console.Write("ERROR: ");
            log += "ERROR: ";
            if (logToConsole)
                Console.Write("ERROR " + eventArgs.Code + ": ");
            log += "ERROR " + eventArgs.Code + ": ";
            ++errors;

            if (logToConsole)
                Console.WriteLine(eventArgs.Message);
            log += eventArgs.Message;
            log += "\n";
        }

        public void LogWarningEvent(BuildWarningEventArgs eventArgs)
        {
            if (eventArgs.File != null && eventArgs.File.Length > 0)
            {
                if (logToConsole)
                    Console.Write("{0}({1},{2}): ", eventArgs.File, eventArgs.LineNumber, eventArgs.ColumnNumber);
            }

            if (logToConsole)
                Console.Write("WARNING " + eventArgs.Code + ": ");
            log += "WARNING " + eventArgs.Code + ": ";
            ++warnings;

            if (logToConsole)
                Console.WriteLine(eventArgs.Message);
            log += eventArgs.Message;
            log += "\n";
        }

        public void LogCustomEvent(CustomBuildEventArgs eventArgs)
        {
            if (logToConsole)
                Console.WriteLine(eventArgs.Message);
            log += eventArgs.Message;
            log += "\n";
        }

        public void LogMessageEvent(BuildMessageEventArgs eventArgs)
        {
            if (logToConsole)
                Console.WriteLine(eventArgs.Message);
            log += eventArgs.Message;
            log += "\n";
            ++messages;
        }

        public bool ContinueOnError
        {
            get
            {
                return false;
            }
        }

        public string ProjectFileOfTaskNode
        {
            get
            {
                return String.Empty;
            }
        }

        public int LineNumberOfTaskNode
        {
            get
            {
                return 0;
            }
        }

        public int ColumnNumberOfTaskNode
        {
            get
            {
                return 0;
            }
        }
        
        public BuildPropertyGroup GlobalProperties
        {
            set { engineGlobalProperties = value; }
            get { return engineGlobalProperties; }
        }

        internal string Log
        {
            set { log = value; }
            get { return log; }
        }
        
        public bool IsRunningMultipleNodes
        {
            get { return isRunningMultipleNodes; }
            set { isRunningMultipleNodes = value; }
        }

        public bool BuildProjectFile
            (
            string projectFileName, 
            string[] targetNames, 
            IDictionary globalPropertiesPassedIntoTask, 
            IDictionary targetOutputs
            )
        {
            return this.BuildProjectFile(projectFileName, targetNames, globalPropertiesPassedIntoTask, targetOutputs, null);
        }

        public bool BuildProjectFile
            (
            string projectFileName,
            string[] targetNames,
            IDictionary globalPropertiesPassedIntoTask,
            IDictionary targetOutputs,
            string toolsVersion
            )
        {
            BuildPropertyGroup finalGlobalProperties = new BuildPropertyGroup();

            // Finally, whatever global properties were passed into the task ... those are the final winners.
            if (globalPropertiesPassedIntoTask != null)
            {
                foreach (DictionaryEntry newGlobalProperty in globalPropertiesPassedIntoTask)
                {
                    finalGlobalProperties.SetProperty((string)newGlobalProperty.Key,
                        (string)newGlobalProperty.Value);
                }
            }

            return engine.BuildProjectFile(projectFileName, targetNames, finalGlobalProperties, targetOutputs, BuildSettings.None, toolsVersion);
        }
        
        public bool BuildProjectFilesInParallel
        (
            string[] projectFileNames,
            string[] targetNames,
            IDictionary[] globalProperties,
            IDictionary[] targetOutputsPerProject,
            string[] toolsVersion,
            bool useResultsCache,
            bool unloadProjectsOnCompletion
        )
        {
            engine.GlobalProperties = GlobalProperties;

            bool allSucceeded = true;
            for (int i = 0; i < projectFileNames.Length; i++)
            {

                BuildPropertyGroup finalGlobalProperties = null;  
                if (globalProperties[i] != null)
                {
                    finalGlobalProperties = new BuildPropertyGroup();
                    foreach (DictionaryEntry newGlobalProperty in globalProperties[i])
                    {
                        finalGlobalProperties.SetProperty((string)newGlobalProperty.Key,
                            (string)newGlobalProperty.Value);
                    }
                }
                bool success = engine.BuildProjectFile((string)projectFileNames[i], targetNames, finalGlobalProperties, targetOutputsPerProject[i]);
                allSucceeded = allSucceeded && success;
            }
            return allSucceeded;
        }

        public bool BuildProjectFile
            (
            string projectFileName
            )
        {
            engine.GlobalProperties = GlobalProperties;
            return engine.BuildProjectFile(projectFileName);
        }

        public bool BuildProjectFile
            (
            string projectFileName, 
            string[] targetNames
            )
        {
            engine.GlobalProperties = GlobalProperties;
            return engine.BuildProjectFile(projectFileName, targetNames);
        }

        public bool BuildProjectFile
            (
            string projectFileName, 
            string targetName
            )
        {
            engine.GlobalProperties = GlobalProperties;
            return engine.BuildProjectFile(projectFileName, targetName);
        }

        public bool BuildProjectFile
            (
             string projectFile,
             string[] targetNames,
             BuildPropertyGroup globalProperties
            )
        {
            return engine.BuildProjectFile(projectFile, targetNames, globalProperties);
        }
        
        public void UnregisterAllLoggers
            (
            )
        {
            engine.UnregisterAllLoggers();
        }
        
        public void UnloadAllProjects
            (
            )
        {
            engine.UnloadAllProjects();
        }

        /// <summary>
        /// Assert that the log file contains the given string.
        /// Case insensitive.
        /// First check if the string is in the log string. If not
        /// than make sure it is also check the MockLogger
        /// </summary>
        /// <param name="contains"></param>
        internal void AssertLogContains(string contains)
        {
            if (upperLog == null)
            {
                upperLog = log;
                upperLog = upperLog.ToUpperInvariant();
            }
            
            // If we do not contain this string than pass it to
            // MockLogger. Since MockLogger is also registered as
            // a logger it may have this string.
            if(!upperLog.Contains
                (
                    contains.ToUpperInvariant()
                )
              )
            {
               mockLogger.AssertLogContains(contains); 
            }
        }

        /// <summary>
        /// Assert that the log doesnt contain the given string.
        /// First check if the string is in the log string. If not
        /// than make sure it is also not in the MockLogger
        /// </summary>
        /// <param name="contains"></param>
        internal void AssertLogDoesntContain(string contains)
        {
            if (upperLog == null)
            {
                upperLog = log;
                upperLog = upperLog.ToUpperInvariant();
            }

            Assertion.Assert
            (
                !upperLog.Contains
                (
                    contains.ToUpperInvariant()
                )
            );
            
            // If we do not contain this string than pass it to
            // MockLogger. Since MockLogger is also registered as
            // a logger it may have this string.
            mockLogger.AssertLogDoesntContain
            (
                contains
            );
        }
    }
}
