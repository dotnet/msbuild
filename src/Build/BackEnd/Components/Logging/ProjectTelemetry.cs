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
        private const string MSBuildTaskSubclassedEventName = "build/tasks/msbuild-subclassed";

        private int _assemblyTaskFactoryTasksExecutedCount = 0;
        private int _intrinsicTaskFactoryTasksExecutedCount = 0;
        private int _codeTaskFactoryTasksExecutedCount = 0;
        private int _roslynCodeTaskFactoryTasksExecutedCount = 0;
        private int _xamlTaskFactoryTasksExecutedCount = 0;
        private int _customTaskFactoryTasksExecutedCount = 0;

        private int _taskHostTasksExecutedCount = 0;

        // Telemetry for non-sealed subclasses of Microsoft-owned MSBuild tasks
        // Maps Microsoft task names to counts of their non-sealed usage
        private readonly Dictionary<string, int> _msbuildTaskSubclassUsage = new();

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
        /// Tracks subclasses of Microsoft-owned MSBuild tasks.
        /// If the task is a subclass of a Microsoft-owned task, increments the usage count for that base task.
        /// </summary>
        /// <param name="taskType">The type of the task being loaded.</param>
        /// <param name="isMicrosoftOwned">Whether the task itself is Microsoft-owned.</param>
        public void TrackTaskSubclassing(Type taskType, bool isMicrosoftOwned)
        {
            if (taskType == null)
            {
                return;
            }

            // Walk the inheritance hierarchy to find Microsoft-owned base tasks
            Type? baseType = taskType.BaseType;
            while (baseType != null)
            {
                // Check if this base type is a Microsoft-owned task
                // We identify Microsoft tasks by checking if they're in the Microsoft.Build namespace
                string? baseTypeName = baseType.FullName;
                if (!string.IsNullOrEmpty(baseTypeName) && 
                    (baseTypeName.StartsWith("Microsoft.Build.Tasks.") || 
                     baseTypeName.StartsWith("Microsoft.Build.Utilities.")))
                {
                    // This is a subclass of a Microsoft-owned task
                    // Track it only if it's NOT itself Microsoft-owned (i.e., user-authored subclass)
                    if (!isMicrosoftOwned)
                    {
                        if (!_msbuildTaskSubclassUsage.ContainsKey(baseTypeName))
                        {
                            _msbuildTaskSubclassUsage[baseTypeName] = 0;
                        }
                        _msbuildTaskSubclassUsage[baseTypeName]++;
                    }
                    // Stop at the first Microsoft-owned base class we find
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

                Dictionary<string, string> msbuildTaskSubclassProperties = GetMSBuildTaskSubclassProperties();
                if (msbuildTaskSubclassProperties.Count > 0)
                {
                    loggingService.LogTelemetry(buildEventContext, MSBuildTaskSubclassedEventName, msbuildTaskSubclassProperties);
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

            _msbuildTaskSubclassUsage.Clear();
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

        private Dictionary<string, string> GetMSBuildTaskSubclassProperties()
        {
            Dictionary<string, string> properties = new();

            // Add each Microsoft task name with its non-sealed subclass usage count
            foreach (var kvp in _msbuildTaskSubclassUsage)
            {
                // Use a sanitized property name (replace dots with underscores for telemetry)
                string propertyName = kvp.Key.Replace(".", "_");
                properties[propertyName] = kvp.Value.ToString(CultureInfo.InvariantCulture);
            }

            return properties;
        }
    }
}
