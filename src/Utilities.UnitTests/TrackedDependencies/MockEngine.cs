// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections;
using Microsoft.Build.Framework;
using Xunit;

namespace Microsoft.Build.UnitTests.TrackedDependencies
{
    /***************************************************************************
     * 
     * Class:       MockEngine
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
    internal sealed class MockEngine : IBuildEngine2
    {
        private string _upperLog;

        internal int Messages { set; get; }

        internal int Warnings { set; get; }

        internal int Errors { set; get; }


        public void LogErrorEvent(BuildErrorEventArgs eventArgs)
        {
            if (!string.IsNullOrEmpty(eventArgs.File))
            {
                Console.Write("{0}({1},{2}): ", eventArgs.File, eventArgs.LineNumber, eventArgs.ColumnNumber);
            }

            Console.Write("ERROR: ");
            Log += "ERROR: ";
            Console.Write("ERROR " + eventArgs.Code + ": ");
            Log += "ERROR " + eventArgs.Code + ": ";
            ++Errors;

            Console.WriteLine(eventArgs.Message);
            Log += eventArgs.Message;
            Log += "\n";
        }

        public void LogWarningEvent(BuildWarningEventArgs eventArgs)
        {
            if (!string.IsNullOrEmpty(eventArgs.File))
            {
                Console.Write("{0}({1},{2}): ", eventArgs.File, eventArgs.LineNumber, eventArgs.ColumnNumber);
            }

            Console.Write("WARNING " + eventArgs.Code + ": ");
            Log += "WARNING " + eventArgs.Code + ": ";
            ++Warnings;

            Console.WriteLine(eventArgs.Message);
            Log += eventArgs.Message;
            Log += "\n";
        }

        public void LogCustomEvent(CustomBuildEventArgs eventArgs)
        {
            Console.WriteLine(eventArgs.Message);
            Log += eventArgs.Message;
            Log += "\n";
        }

        public void LogMessageEvent(BuildMessageEventArgs eventArgs)
        {
            Console.WriteLine(eventArgs.Message);
            Log += eventArgs.Message;
            Log += "\n";
            ++Messages;
        }

        internal string Log { set; get; } = "";

        public bool ContinueOnError => false;

        public string ProjectFileOfTaskNode => string.Empty;

        public int LineNumberOfTaskNode => 0;

        public int ColumnNumberOfTaskNode => 0;

        public bool IsRunningMultipleNodes { get; set; }

        public bool BuildProjectFile(string projectFileName, string[] targetNames, IDictionary globalProperties, IDictionary targetOutputs, string toolsVersion) => false;

        public bool BuildProjectFile(string projectFileName, string[] targetNames, IDictionary globalProperties, IDictionary targetOutputs) => false;

        public bool BuildProjectFilesInParallel(
            string[] projectFileNames,
            string[] targetNames,
            IDictionary[] globalProperties,
            IDictionary[] targetOutputsPerProject,
            string[] toolsVersion,
            bool useResultsCache,
            bool unloadProjectsOnCompletion) => false;

        /// <summary>
        /// Assert that the log file contains the given string.
        /// Case insensitive.
        /// </summary>
        /// <param name="contains"></param>
        internal void AssertLogContains(string contains)
        {
            if (_upperLog == null)
            {
                _upperLog = Log;
                _upperLog = _upperLog.ToUpperInvariant();
            }

            Assert.Contains(contains.ToUpperInvariant(), _upperLog);
        }

        /// <summary>
        /// Assert that the log doesn't contain the given string.
        /// </summary>
        /// <param name="contains"></param>
        internal void AssertLogDoesntContain(string contains)
        {
            if (_upperLog == null)
            {
                _upperLog = Log;
                _upperLog = _upperLog.ToUpperInvariant();
            }

            Assert.DoesNotContain(contains.ToUpperInvariant(), _upperLog);
        }
    }
}
