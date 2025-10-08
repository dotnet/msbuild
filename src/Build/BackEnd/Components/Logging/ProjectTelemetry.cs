// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Globalization;
using Microsoft.Build.Framework;

namespace Microsoft.Build.BackEnd.Logging
{
    /// <summary>
    /// Tracks telemetry data for a project build.
    /// </summary>
    internal class ProjectTelemetry
    {
        // We do not have dependency on Microsoft.Build.Tasks assembly, so using hard-coded type names
        private const string AssemblyTaskFactoryTypeName = "Microsoft.Build.BackEnd.AssemblyTaskFactory";
        private const string IntrinsicTaskFactoryTypeName = "Microsoft.Build.BackEnd.IntrinsicTaskFactory";
        private const string CodeTaskFactoryTypeName = "Microsoft.Build.Tasks.CodeTaskFactory";
        private const string RoslynCodeTaskFactoryTypeName = "Microsoft.Build.Tasks.RoslynCodeTaskFactory";
        private const string XamlTaskFactoryTypeName = "Microsoft.Build.Tasks.XamlTaskFactory";

        // Important Note: these two telemetry events are not logged directly.
        // SDK aggregates them per build in https://github.com/dotnet/sdk/blob/main/src/Cli/dotnet/Commands/MSBuild/MSBuildLogger.cs.
        // Aggregation logic is very basic. Only integer properties are aggregated by summing values. Non-integer properties are ignored.
        // This means that if we ever add logging non-integer properties for these events, they will not be included in the telemetry.
        private const string TaskFactoryEventName = "build/tasks/taskfactory";
        private const string TasksEventName = "build/tasks";
        private const string MicrosoftTasksEventName = "build/tasks/microsoft";

        private int _assemblyTaskFactoryTasksExecutedCount = 0;
        private int _intrinsicTaskFactoryTasksExecutedCount = 0;
        private int _codeTaskFactoryTasksExecutedCount = 0;
        private int _roslynCodeTaskFactoryTasksExecutedCount = 0;
        private int _xamlTaskFactoryTasksExecutedCount = 0;
        private int _customTaskFactoryTasksExecutedCount = 0;

        private int _taskHostTasksExecutedCount = 0;

        // Telemetry for Microsoft-authored tasks
        private int _microsoftTasksLoadedCount = 0;
        private int _microsoftTasksSealedCount = 0;
        private int _microsoftTasksInheritingFromTaskCount = 0;

        /// <summary>
        /// Adds a task execution to the telemetry data.
        /// </summary>
        public void AddTaskExecution(string taskFactoryTypeName, bool isTaskHost)
        {
            if (isTaskHost)
            {
                _taskHostTasksExecutedCount++;
            }

            switch (taskFactoryTypeName)
            {
                case AssemblyTaskFactoryTypeName:
                    _assemblyTaskFactoryTasksExecutedCount++;
                    break;

                case IntrinsicTaskFactoryTypeName:
                    _intrinsicTaskFactoryTasksExecutedCount++;
                    break;

                case CodeTaskFactoryTypeName:
                    _codeTaskFactoryTasksExecutedCount++;
                    break;

                case RoslynCodeTaskFactoryTypeName:
                    _roslynCodeTaskFactoryTasksExecutedCount++;
                    break;

                case XamlTaskFactoryTypeName:
                    _xamlTaskFactoryTasksExecutedCount++;
                    break;

                default:
                    _customTaskFactoryTasksExecutedCount++;
                    break;
            }
        }

        /// <summary>
        /// Adds telemetry about a Microsoft-authored task being loaded.
        /// </summary>
        /// <param name="taskType">The type of the task being loaded.</param>
        public void AddMicrosoftTaskLoaded(Type taskType)
        {
            if (taskType == null)
            {
                return;
            }

            _microsoftTasksLoadedCount++;

            // Check if the task is sealed
            if (taskType.IsSealed)
            {
                _microsoftTasksSealedCount++;
            }

            // Check if the task inherits from Microsoft.Build.Utilities.Task
            // We check the full name to avoid loading the assembly if not already loaded
            Type? baseType = taskType.BaseType;
            while (baseType != null)
            {
                if (baseType.FullName == "Microsoft.Build.Utilities.Task")
                {
                    _microsoftTasksInheritingFromTaskCount++;
                    break;
                }
                baseType = baseType.BaseType;
            }
        }

        /// <summary>
        /// Logs telemetry data for a project
        /// </summary>
        public void LogProjectTelemetry(ILoggingService loggingService, BuildEventContext buildEventContext)
        {
            if (loggingService == null)
            {
                return;
            }

            try
            {
                Dictionary<string, string> taskFactoryProperties = GetTaskFactoryProperties();
                if (taskFactoryProperties.Count > 0)
                {
                    loggingService.LogTelemetry(buildEventContext, TaskFactoryEventName, taskFactoryProperties);
                }

                Dictionary<string, string> taskTotalProperties = GetTaskProperties();
                if (taskTotalProperties.Count > 0)
                {
                    loggingService.LogTelemetry(buildEventContext, TasksEventName, taskTotalProperties);
                }

                Dictionary<string, string> microsoftTaskProperties = GetMicrosoftTaskProperties();
                if (microsoftTaskProperties.Count > 0)
                {
                    loggingService.LogTelemetry(buildEventContext, MicrosoftTasksEventName, microsoftTaskProperties);
                }
            }
            catch
            {
                // Ignore telemetry logging errors to avoid breaking builds
            }
            finally
            {
                // Reset counts after logging.
                // ProjectLoggingContext context and, as a result, this class should not be reused between projects builds, 
                // however it is better to defensively clean up the stats if it ever happens in future.
                Clean();
            }
        }
        
        private void Clean()
        {
            _assemblyTaskFactoryTasksExecutedCount = 0;
            _intrinsicTaskFactoryTasksExecutedCount = 0;
            _codeTaskFactoryTasksExecutedCount = 0;
            _roslynCodeTaskFactoryTasksExecutedCount = 0;
            _xamlTaskFactoryTasksExecutedCount = 0;
            _customTaskFactoryTasksExecutedCount = 0;

            _taskHostTasksExecutedCount = 0;

            _microsoftTasksLoadedCount = 0;
            _microsoftTasksSealedCount = 0;
            _microsoftTasksInheritingFromTaskCount = 0;
        }

        private Dictionary<string, string> GetTaskFactoryProperties()
        {
            Dictionary<string, string> properties = new();

            if (_assemblyTaskFactoryTasksExecutedCount > 0)
            {
                properties["AssemblyTaskFactoryTasksExecutedCount"] = _assemblyTaskFactoryTasksExecutedCount.ToString(CultureInfo.InvariantCulture);
            }
            
            if (_intrinsicTaskFactoryTasksExecutedCount > 0)
            {
                properties["IntrinsicTaskFactoryTasksExecutedCount"] = _intrinsicTaskFactoryTasksExecutedCount.ToString(CultureInfo.InvariantCulture);
            }
            
            if (_codeTaskFactoryTasksExecutedCount > 0)
            {
                properties["CodeTaskFactoryTasksExecutedCount"] = _codeTaskFactoryTasksExecutedCount.ToString(CultureInfo.InvariantCulture);
            }
            
            if (_roslynCodeTaskFactoryTasksExecutedCount > 0)
            {
                properties["RoslynCodeTaskFactoryTasksExecutedCount"] = _roslynCodeTaskFactoryTasksExecutedCount.ToString(CultureInfo.InvariantCulture);
            }
            
            if (_xamlTaskFactoryTasksExecutedCount > 0)
            {
                properties["XamlTaskFactoryTasksExecutedCount"] = _xamlTaskFactoryTasksExecutedCount.ToString(CultureInfo.InvariantCulture);
            }
            
            if (_customTaskFactoryTasksExecutedCount > 0)
            {
                properties["CustomTaskFactoryTasksExecutedCount"] = _customTaskFactoryTasksExecutedCount.ToString(CultureInfo.InvariantCulture);
            }

            return properties;
        }

        private Dictionary<string, string> GetTaskProperties()
        {
            Dictionary<string, string> properties = new();
            
            var totalTasksExecuted = _assemblyTaskFactoryTasksExecutedCount + 
                                    _intrinsicTaskFactoryTasksExecutedCount +
                                    _codeTaskFactoryTasksExecutedCount + 
                                    _roslynCodeTaskFactoryTasksExecutedCount +
                                    _xamlTaskFactoryTasksExecutedCount + 
                                    _customTaskFactoryTasksExecutedCount;
            
            if (totalTasksExecuted > 0)
            {
                properties["TasksExecutedCount"] = totalTasksExecuted.ToString(CultureInfo.InvariantCulture);
            }
            
            if (_taskHostTasksExecutedCount > 0)
            {
                properties["TaskHostTasksExecutedCount"] = _taskHostTasksExecutedCount.ToString(CultureInfo.InvariantCulture);
            }

            return properties;
        }

        private Dictionary<string, string> GetMicrosoftTaskProperties()
        {
            Dictionary<string, string> properties = new();

            if (_microsoftTasksLoadedCount > 0)
            {
                properties["MicrosoftTasksLoadedCount"] = _microsoftTasksLoadedCount.ToString(CultureInfo.InvariantCulture);
            }

            if (_microsoftTasksSealedCount > 0)
            {
                properties["MicrosoftTasksSealedCount"] = _microsoftTasksSealedCount.ToString(CultureInfo.InvariantCulture);
            }

            if (_microsoftTasksInheritingFromTaskCount > 0)
            {
                properties["MicrosoftTasksInheritingFromTaskCount"] = _microsoftTasksInheritingFromTaskCount.ToString(CultureInfo.InvariantCulture);
            }

            return properties;
        }
    }
}
