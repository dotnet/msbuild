// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Resources;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.Build.Shared;
using Xunit;

namespace Microsoft.Build.UnitTests
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
    sealed internal class MockEngine : IBuildEngine3
    {
        private bool _isRunningMultipleNodes;
        private int _messages = 0;
        private int _warnings = 0;
        private int _errors = 0;
        private int _commandLine = 0;
        private string _log = "";
        private string _upperLog = null;
        private MessageImportance _minimumMessageImportance = MessageImportance.Low;

        public MessageImportance MinimumMessageImportance
        {
            get { return _minimumMessageImportance; }
            set { _minimumMessageImportance = value; }
        }

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

        internal int CommandLine
        {
            set { _commandLine = value; }
            get { return _commandLine; }
        }

        public bool IsRunningMultipleNodes
        {
            get { return _isRunningMultipleNodes; }
            set { _isRunningMultipleNodes = value; }
        }

        public void LogErrorEvent(BuildErrorEventArgs eventArgs)
        {
            Console.WriteLine(EventArgsFormatting.FormatEventMessage(eventArgs));
            _log += EventArgsFormatting.FormatEventMessage(eventArgs);
            ++_errors;

            _log += "\n";
            _upperLog = null;
        }

        public void LogWarningEvent(BuildWarningEventArgs eventArgs)
        {
            Console.WriteLine(EventArgsFormatting.FormatEventMessage(eventArgs));
            _log += EventArgsFormatting.FormatEventMessage(eventArgs);
            ++_warnings;

            _log += "\n";
            _upperLog = null;
        }

        public void LogCustomEvent(CustomBuildEventArgs eventArgs)
        {
            Console.WriteLine(eventArgs.Message);
            _log += eventArgs.Message;
            _log += "\n";
            _upperLog = null;
        }

        public void LogMessageEvent(BuildMessageEventArgs eventArgs)
        {
            // Only if the message is above the minimum importance should we record the log message
            if (eventArgs.Importance <= _minimumMessageImportance)
            {
                Console.WriteLine(eventArgs.Message);
                _log += eventArgs.Message;
                _log += "\n";
                _upperLog = null;
                ++_messages;
            }
        }

        public void LogCommandLine(TaskCommandLineEventArgs eventArgs)
        {
            Console.WriteLine(eventArgs.Message);
            _log += eventArgs.Message;
            _log += "\n";
            _upperLog = null;
            ++_commandLine;
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

        internal string Log
        {
            set { _log = value; _upperLog = null; }
            get { return _log; }
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


        public BuildEngineResult BuildProjectFilesInParallel
        (
            string[] projectFileNames,
            string[] targetNames,
            IDictionary[] globalProperties,
            IList<string>[] undefineProperties,
            string[] toolsVersion,
            bool includeTargetOutputs
        )
        {
            return new BuildEngineResult(false, null);
        }

        public void Yield()
        {
        }

        public void Reacquire()
        {
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
