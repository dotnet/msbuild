// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

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

        private int _assemblyTaskFactoryTasksExecutedCount = 0;
        private int _intrinsicTaskFactoryTasksExecutedCount = 0;
        private int _codeTaskFactoryTasksExecutedCount = 0;
        private int _roslynCodeTaskFactoryTasksExecutedCount = 0;
        private int _xamlTaskFactoryTasksExecutedCount = 0;
        private int _customTaskFactoryTasksExecutedCount = 0;

        private int _taskHostTasksExecutedCount = 0;

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
    }
}
