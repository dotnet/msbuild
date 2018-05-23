// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Microsoft.Build.Logging;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Build.UnitTests
{
    /***************************************************************************
     * 
     * Class:       MockEngine
     * 
     * In order to execute tasks, we have to pass in an Engine object, so the
     * task can log events.  It doesn't have to be the real Engine object, just
     * something that implements the IBuildEngine4 interface.  So, we mock up
     * a fake engine object here, so we're able to execute tasks from the unit tests.
     * 
     * The unit tests could have instantiated the real Engine object, but then
     * we would have had to take a reference onto the Microsoft.Build.Engine assembly, which
     * is somewhat of a no-no for task assemblies.
     * 
     **************************************************************************/
    internal sealed class MockEngine : IBuildEngine5
    {
        private readonly ITestOutputHelper _output;
        private readonly StringBuilder _log = new StringBuilder();
        private readonly ProjectCollection _projectCollection = new ProjectCollection();
        private readonly bool _logToConsole;
        private readonly Dictionary<object, object> _objectCashe = new Dictionary<object, object>();

        internal MockEngine() : this(false)
        {
        }

        internal int Messages { get; set; }

        internal int Warnings { get; set; }

        internal int Errors { get; set; }

        internal MockLogger MockLogger { get; }

        public MockEngine(bool logToConsole)
        {
            MockLogger = new MockLogger();
            _logToConsole = logToConsole;
        }

        public MockEngine(ITestOutputHelper output)
        {
            _output = output;
            MockLogger = new MockLogger(output);
            _logToConsole = false; // We have a better place to put it.
        }

        public void LogErrorEvent(BuildErrorEventArgs eventArgs)
        {
            string message = string.Empty;

            if (!string.IsNullOrEmpty(eventArgs.File))
            {
                message += $"{eventArgs.File}({eventArgs.LineNumber},{eventArgs.ColumnNumber}): ";
            }

            message += "ERROR " + eventArgs.Code + ": ";
            ++Errors;

            message += eventArgs.Message;

            if (_logToConsole)
            {
                Console.WriteLine(message);
            }
            _output?.WriteLine(message);
            _log.AppendLine(message);
        }

        public void LogWarningEvent(BuildWarningEventArgs eventArgs)
        {
            string message = string.Empty;

            if (!string.IsNullOrEmpty(eventArgs.File))
            {
                message += $"{eventArgs.File}({eventArgs.LineNumber},{eventArgs.ColumnNumber}): ";
            }

            message += "WARNING " + eventArgs.Code + ": ";
            ++Warnings;

            message += eventArgs.Message;

            if (_logToConsole)
            {
                Console.WriteLine(message);
            }
            _output?.WriteLine(message);
            _log.AppendLine(message);
        }

        public void LogCustomEvent(CustomBuildEventArgs eventArgs)
        {
            if (_logToConsole)
            {
                Console.WriteLine(eventArgs.Message);
            }
            _output?.WriteLine(eventArgs.Message);
            _log.AppendLine(eventArgs.Message);
        }

        public void LogMessageEvent(BuildMessageEventArgs eventArgs)
        {
            if (_logToConsole)
            {
                Console.WriteLine(eventArgs.Message);
            }
            _output?.WriteLine(eventArgs.Message);
            _log.AppendLine(eventArgs.Message);
            ++Messages;
        }

        public void LogTelemetry(string eventName, IDictionary<string, string> properties)
        {
            string message = $"Received telemetry event '{eventName}'{Environment.NewLine}";
            foreach (string key in properties?.Keys)
            {
                message += $"  Property '{key}' = '{properties[key]}'{Environment.NewLine}";
            }

            if (_logToConsole)
            {
                Console.WriteLine(message);
            }
            _output?.WriteLine(message);
            _log.AppendLine(message);
        }

        public bool ContinueOnError => false;

        public string ProjectFileOfTaskNode => String.Empty;

        public int LineNumberOfTaskNode => 0;

        public int ColumnNumberOfTaskNode => 0;

        internal string Log
        {
            get => _log.ToString();
            set
            {
                if (!string.IsNullOrEmpty(value))
                {
                    throw new ArgumentException("Expected log setter to be used only to reset the log to empty.");
                }

                _log.Clear();
            }
        }

        public bool IsRunningMultipleNodes { get; set; }

        public bool BuildProjectFile
            (
            string projectFileName,
            string[] targetNames,
            IDictionary globalPropertiesPassedIntoTask,
            IDictionary targetOutputs
            )
        {
            ILogger[] loggers = { MockLogger, new ConsoleLogger() };

            return BuildProjectFile(projectFileName, targetNames, globalPropertiesPassedIntoTask, targetOutputs, null);
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
            var finalGlobalProperties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            // Finally, whatever global properties were passed into the task ... those are the final winners.
            if (globalPropertiesPassedIntoTask != null)
            {
                foreach (DictionaryEntry newGlobalProperty in globalPropertiesPassedIntoTask)
                {
                    finalGlobalProperties[(string)newGlobalProperty.Key] = (string)newGlobalProperty.Value;
                }
            }

            Project project = _projectCollection.LoadProject(projectFileName, finalGlobalProperties, toolsVersion);

            ILogger[] loggers = { MockLogger, new ConsoleLogger() };

            return project.Build(targetNames, loggers);
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
            bool includeTargetOutputs = targetOutputsPerProject != null;

            BuildEngineResult result = BuildProjectFilesInParallel(projectFileNames, targetNames, globalProperties, new List<String>[projectFileNames.Length], toolsVersion, includeTargetOutputs);

            if (includeTargetOutputs)
            {
                for (int i = 0; i < targetOutputsPerProject.Length; i++)
                {
                    if (targetOutputsPerProject[i] != null)
                    {
                        foreach (KeyValuePair<string, ITaskItem[]> output in result.TargetOutputsPerProject[i])
                        {
                            targetOutputsPerProject[i].Add(output.Key, output.Value);
                        }
                    }
                }
            }

            return result.Result;
        }

        public BuildEngineResult BuildProjectFilesInParallel
        (
            string[] projectFileNames,
            string[] targetNames,
            IDictionary[] globalProperties,
            IList<string>[] undefineProperties,
            string[] toolsVersion,
            bool returnTargetOutputs
        )
        {
            List<IDictionary<string, ITaskItem[]>> targetOutputsPerProject = null;

            ILogger[] loggers = { MockLogger, new ConsoleLogger() };

            bool allSucceeded = true;

            if (returnTargetOutputs)
            {
                targetOutputsPerProject = new List<IDictionary<string, ITaskItem[]>>();
            }

            for (int i = 0; i < projectFileNames.Length; i++)
            {
                Dictionary<string, string> finalGlobalProperties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                if (globalProperties[i] != null)
                {
                    foreach (DictionaryEntry newGlobalProperty in globalProperties[i])
                    {
                        finalGlobalProperties[(string)newGlobalProperty.Key] = (string)newGlobalProperty.Value;
                    }
                }

                ProjectInstance instance = _projectCollection.LoadProject((string)projectFileNames[i], finalGlobalProperties, null).CreateProjectInstance();

                bool success = instance.Build(targetNames, loggers, out IDictionary<string, TargetResult> targetOutputs);

                if (targetOutputsPerProject != null)
                {
                    targetOutputsPerProject.Add(new Dictionary<string, ITaskItem[]>(StringComparer.OrdinalIgnoreCase));

                    foreach (KeyValuePair<string, TargetResult> resultEntry in targetOutputs)
                    {
                        targetOutputsPerProject[i][resultEntry.Key] = resultEntry.Value.Items;
                    }
                }

                allSucceeded = allSucceeded && success;
            }

            return new BuildEngineResult(allSucceeded, targetOutputsPerProject);
        }

        public void Yield()
        {
        }

        public void Reacquire()
        {
        }

        public bool BuildProjectFile
            (
            string projectFileName
            )
        {
            return (_projectCollection.LoadProject(projectFileName)).Build();
        }

        public bool BuildProjectFile
            (
            string projectFileName,
            string[] targetNames
            )
        {
            return (_projectCollection.LoadProject(projectFileName)).Build(targetNames);
        }

        public bool BuildProjectFile
            (
            string projectFileName,
            string targetName
            )
        {
            return (_projectCollection.LoadProject(projectFileName)).Build(targetName);
        }

        public void UnregisterAllLoggers
            (
            )
        {
            _projectCollection.UnregisterAllLoggers();
        }

        public void UnloadAllProjects
            (
            )
        {
            _projectCollection.UnloadAllProjects();
        }


        /// <summary>
        /// Assert that the mock log in the engine doesn't contain a certain message based on a resource string and some parameters
        /// </summary>
        internal void AssertLogDoesntContainMessageFromResource(GetStringDelegate getString, string resourceName, params string[] parameters)
        {
            string resource = getString(resourceName);
            string stringToSearchFor = String.Format(resource, parameters);
            AssertLogDoesntContain(stringToSearchFor);
        }

        /// <summary>
        /// Assert that the mock log in the engine contains a certain message based on a resource string and some parameters
        /// </summary>
        internal void AssertLogContainsMessageFromResource(GetStringDelegate getString, string resourceName, params string[] parameters)
        {
            string resource = getString(resourceName);
            string stringToSearchFor = String.Format(resource, parameters);
            AssertLogContains(stringToSearchFor);
        }

        /// <summary>
        /// Assert that the log file contains the given string.
        /// Case insensitive.
        /// First check if the string is in the log string. If not
        /// than make sure it is also check the MockLogger
        /// </summary>
        internal void AssertLogContains(string contains)
        {
            // If we do not contain this string than pass it to
            // MockLogger. Since MockLogger is also registered as
            // a logger it may have this string.
            var logText = _log.ToString();
            if (logText.IndexOf(contains, StringComparison.OrdinalIgnoreCase) == -1)
            {
                if (_output == null)
                {
                    Console.WriteLine(logText);
                }
                else
                {
                    _output.WriteLine(logText);
                }

                MockLogger.AssertLogContains(contains);
            }
        }

        /// <summary>
        /// Assert that the log doesn't contain the given string.
        /// First check if the string is in the log string. If not
        /// than make sure it is also not in the MockLogger
        /// </summary>
        internal void AssertLogDoesntContain(string contains)
        {
            var logText = _log.ToString();
            
            if (_output == null)
            {
                Console.WriteLine(logText);
            }
            else
            {
                _output.WriteLine(logText);
            }

            Assert.Equal(-1, logText.IndexOf(contains, StringComparison.OrdinalIgnoreCase));

            // If we do not contain this string than pass it to
            // MockLogger. Since MockLogger is also registered as
            // a logger it may have this string.
            MockLogger.AssertLogDoesntContain(contains);
        }

        /// <summary>
        /// Delegate which will get the resource from the correct resource manager
        /// </summary>
        public delegate string GetStringDelegate(string resourceName);

        public object GetRegisteredTaskObject(object key, RegisteredTaskObjectLifetime lifetime)
        {
            _objectCashe.TryGetValue(key, out object obj);
            return obj;
        }

        public void RegisterTaskObject(object key, object obj, RegisteredTaskObjectLifetime lifetime, bool allowEarlyCollection)
        {
            _objectCashe[key] = obj;
        }

        public object UnregisterTaskObject(object key, RegisteredTaskObjectLifetime lifetime)
        {
            var obj = _objectCashe[key];
            _objectCashe.Remove(key);
            return obj;
        }
    }
}
