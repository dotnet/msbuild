// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Resources;
using System.Reflection;
using System.Collections;
using System.Globalization;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
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
    sealed internal class MockEngine : IBuildEngine2
    {
        private bool _isRunningMultipleNodes;
        private int _messages = 0;
        private int _warnings = 0;
        private int _errors = 0;
        private string _log = "";
        private string _upperLog = null;

        internal int Messages
        {
            set { _messages = value; }
            get { return _messages; }
        }

        internal int Warnings
        {
            set { _warnings = value; }
            get { return _warnings; }
        }

        internal int Errors
        {
            set { _errors = value; }
            get { return _errors; }
        }


        public void LogErrorEvent(BuildErrorEventArgs eventArgs)
        {
            if (eventArgs.File != null && eventArgs.File.Length > 0)
            {
                Console.Write("{0}({1},{2}): ", eventArgs.File, eventArgs.LineNumber, eventArgs.ColumnNumber);
            }

            Console.Write("ERROR: ");
            _log += "ERROR: ";
            Console.Write("ERROR " + eventArgs.Code + ": ");
            _log += "ERROR " + eventArgs.Code + ": ";
            ++_errors;

            Console.WriteLine(eventArgs.Message);
            _log += eventArgs.Message;
            _log += "\n";
        }

        public void LogWarningEvent(BuildWarningEventArgs eventArgs)
        {
            if (eventArgs.File != null && eventArgs.File.Length > 0)
            {
                Console.Write("{0}({1},{2}): ", eventArgs.File, eventArgs.LineNumber, eventArgs.ColumnNumber);
            }

            Console.Write("WARNING " + eventArgs.Code + ": ");
            _log += "WARNING " + eventArgs.Code + ": ";
            ++_warnings;

            Console.WriteLine(eventArgs.Message);
            _log += eventArgs.Message;
            _log += "\n";
        }

        public void LogCustomEvent(CustomBuildEventArgs eventArgs)
        {
            Console.WriteLine(eventArgs.Message);
            _log += eventArgs.Message;
            _log += "\n";
        }

        public void LogMessageEvent(BuildMessageEventArgs eventArgs)
        {
            Console.WriteLine(eventArgs.Message);
            _log += eventArgs.Message;
            _log += "\n";
            ++_messages;
        }

        internal string Log
        {
            set { _log = value; }
            get { return _log; }
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

        public bool IsRunningMultipleNodes
        {
            get { return _isRunningMultipleNodes; }
            set { _isRunningMultipleNodes = value; }
        }

        public bool BuildProjectFile
            (
            string projectFileName,
            string[] targetNames,
            IDictionary globalProperties,
            IDictionary targetOutputs,
            string toolsVersion
            )
        {
            return false;
        }

        public bool BuildProjectFile
            (
            string projectFileName,
            string[] targetNames,
            IDictionary globalProperties,
            IDictionary targetOutputs
            )
        {
            return false;
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
            return false;
        }

        /// <summary>
        /// Assert that the log file contains the given string.
        /// Case insensitive.
        /// </summary>
        /// <param name="contains"></param>
        internal void AssertLogContains(string contains)
        {
            if (_upperLog == null)
            {
                _upperLog = _log;
                _upperLog = _upperLog.ToUpperInvariant();
            }

            Assert.True(
                _upperLog.Contains
                (
                    contains.ToUpperInvariant()
                )
            );
        }

        /// <summary>
        /// Assert that the log doesnt contain the given string.
        /// </summary>
        /// <param name="contains"></param>
        internal void AssertLogDoesntContain(string contains)
        {
            if (_upperLog == null)
            {
                _upperLog = _log;
                _upperLog = _upperLog.ToUpperInvariant();
            }

            Assert.False(_upperLog.Contains
                (
                    contains.ToUpperInvariant()
                ));
        }
    }
}
