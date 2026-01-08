// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
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
        private const string CustomTaskFactoryEventName = "build/tasks/custom-taskfactory";

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

        // Telemetry for custom (non-MSBuild) task factory usage
        // Maps custom task factory type names to execution counts
        private readonly Dictionary<string, int> _customTaskFactoryUsage = new();

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
                    // Track custom (non-MSBuild) task factories individually
                    _customTaskFactoryTasksExecutedCount++;
                    if (!string.IsNullOrEmpty(taskFactoryTypeName))
                    {
                        _customTaskFactoryUsage.TryGetValue(taskFactoryTypeName, out int count);
                        _customTaskFactoryUsage[taskFactoryTypeName] = count + 1;
                    }
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
                        _msbuildTaskSubclassUsage.TryGetValue(baseTypeName, out int count);
                        _msbuildTaskSubclassUsage[baseTypeName] = count + 1;
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

                Dictionary<string, string> customTaskFactoryProperties = GetCustomTaskFactoryProperties();
                if (customTaskFactoryProperties.Count > 0)
                {
                    loggingService.LogTelemetry(buildEventContext, CustomTaskFactoryEventName, customTaskFactoryProperties);
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
            _customTaskFactoryUsage.Clear();
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
                // Use the same sanitization logic as custom task factories for consistency
                string propertyName = SanitizePropertyName(kvp.Key);
                properties[propertyName] = kvp.Value.ToString(CultureInfo.InvariantCulture);
            }

            return properties;
        }

        private Dictionary<string, string> GetCustomTaskFactoryProperties()
        {
            Dictionary<string, string> properties = new();

            // Add each custom task factory type name with its usage count
            foreach (var kvp in _customTaskFactoryUsage)
            {
                // Sanitize property name for telemetry: replace dots with underscores
                // and remove any other characters that might be problematic
                string propertyName = SanitizePropertyName(kvp.Key);
                properties[propertyName] = kvp.Value.ToString(CultureInfo.InvariantCulture);
            }

            return properties;
        }

        /// <summary>
        /// Sanitizes a string to make it suitable for use as a telemetry property name.
        /// Replaces dots with underscores and removes other potentially problematic characters.
        /// </summary>
        private static string SanitizePropertyName(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return string.Empty;
            }

            // Replace dots with underscores and remove other special characters
            // Keep alphanumeric characters and underscores only
            var sanitized = new StringBuilder(name.Length);
            foreach (char c in name)
            {
                if (char.IsLetterOrDigit(c))
                {
                    sanitized.Append(c);
                }
                else if (c == '.' || c == '-' || c == ' ')
                {
                    sanitized.Append('_');
                }
                // Skip other special characters
            }
            return sanitized.ToString();
        }
    }
}
