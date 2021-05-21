// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using Microsoft.Build.Framework;
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
    internal sealed class MockEngine : IBuildEngine3
    {
        private StringBuilder _log = new StringBuilder();

        public MessageImportance MinimumMessageImportance { get; set; } = MessageImportance.Low;

        internal int Messages { set; get; }

        internal int Warnings { set; get; }

        internal int Errors { set; get; }

        public bool IsRunningMultipleNodes { get; set; }

        public void LogErrorEvent(BuildErrorEventArgs eventArgs)
        {
            Console.WriteLine(EventArgsFormatting.FormatEventMessage(eventArgs));
            _log.AppendLine(EventArgsFormatting.FormatEventMessage(eventArgs));
            ++Errors;
        }

        public void LogWarningEvent(BuildWarningEventArgs eventArgs)
        {
            Console.WriteLine(EventArgsFormatting.FormatEventMessage(eventArgs));
            _log.AppendLine(EventArgsFormatting.FormatEventMessage(eventArgs));
            ++Warnings;
        }

        public void LogCustomEvent(CustomBuildEventArgs eventArgs)
        {
            Console.WriteLine(eventArgs.Message);
            _log.AppendLine(eventArgs.Message);
        }

        public void LogMessageEvent(BuildMessageEventArgs eventArgs)
        {
            // Only if the message is above the minimum importance should we record the log message
            if (eventArgs.Importance <= MinimumMessageImportance)
            {
                Console.WriteLine(eventArgs.Message);
                _log.AppendLine(eventArgs.Message);
                ++Messages;
            }
        }

        public bool ContinueOnError => false;

        public string ProjectFileOfTaskNode => string.Empty;

        public int LineNumberOfTaskNode => 0;

        public int ColumnNumberOfTaskNode => 0;

        internal string Log
        {
            set => _log = new StringBuilder(value);
            get => _log.ToString();
        }

        public bool BuildProjectFile(string projectFileName, string[] targetNames, IDictionary globalProperties, IDictionary targetOutputs) => false;

        public bool BuildProjectFile(string projectFileName, string[] targetNames, IDictionary globalProperties, IDictionary targetOutputs, string toolsVersion) => false;

        /// <summary>
        /// Assert that the log file contains the given string.
        /// Case insensitive.
        /// </summary>
        /// <param name="contains"></param>
        internal void AssertLogContains(string contains) => Assert.Contains(contains, Log, StringComparison.OrdinalIgnoreCase);

        public bool BuildProjectFilesInParallel(
            string[] projectFileNames,
            string[] targetNames,
            IDictionary[] globalProperties,
            IDictionary[] targetOutputsPerProject,
            string[] toolsVersion,
            bool useResultsCache,
            bool unloadProjectsOnCompletion) => false;


        public BuildEngineResult BuildProjectFilesInParallel(
            string[] projectFileNames,
            string[] targetNames,
            IDictionary[] globalProperties,
            IList<string>[] undefineProperties,
            string[] toolsVersion,
            bool includeTargetOutputs) => new BuildEngineResult(false, null);

        public void Yield()
        {
        }

        public void Reacquire()
        {
        }

        /// <summary>
        /// Assert that the log doesn't contain the given string.
        /// </summary>
        /// <param name="contains"></param>
        internal void AssertLogDoesntContain(string contains) => Assert.DoesNotContain(contains, Log, StringComparison.OrdinalIgnoreCase);
    }
}
