// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Diagnostics;
using System.Reflection;

using Microsoft.Build.Construction;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace TaskUsageLogger
{
    /// <summary>
    /// Sample logger used to gather a CSV-formatted list of all tasks run in the build,
    /// associated by Target, project, targets file they're defined in, and assembly the
    /// task comes from.
    ///
    /// Usage:
    ///   /logger:TaskUsageLogger,[path to TaskUsageLogger.dll];[output csv filename]
    /// </summary>
    /// <remarks>
    /// KNOWN ISSUES:
    /// - Because the task location scraping doesn't currently take into account conditions,
    ///   if a task is defined by more than one UsingTask, even if the conditions are mutually
    ///   exclusive, the logger will pick and return the first one.  In practice, this means that
    ///   most default tasks are incorrectly recorded as coming from Microsoft.Build.Tasks.v4.0.dll,
    ///   but since most other task definitions do not contain multiple mutually exclusive
    ///   conditions, they are in general correct.
    /// - Does not keep track of override tasks, so any overridden task will likewise also be incorrectly
    ///   reported.
    /// - Because MSBuild's property expansion functionality is not publicly exposed, this contains
    ///   a very hacky simplified version sufficient to cover most patterns actually seen in UsingTask
    ///   definitions, but is not guaranteed correct in all cases.
    /// - Keeps everything in memory, so would probably run into issues if run against very large
    ///   builds.
    /// </remarks>
    public class TaskUsageLogger : Logger
    {
        private static readonly Regex s_msbuildPropertyRegex = new Regex(@"[\$][\(](?<name>.*?)[\)]", RegexOptions.ExplicitCapture);
        private static readonly char[] s_semicolonChar = { ';' };
        private static readonly char[] s_disallowedCharactersForExpansion = new char[] { '@', '%' };
        private static readonly char[] s_fullyQualifiedTaskNameSeperators = new char[] { '.', '+' };

        private Dictionary<int, string> _targetIdsToNames;
        private HashSet<TaskData> _tasks;
        private string _logFile;
        private ProjectCollection _privateCollection;
        private Dictionary<int, string> _toolsVersionsByProjectContextId;
        private Dictionary<string, HashSet<UsingTaskData>> _defaultTasksByToolset;
        private Dictionary<int, HashSet<UsingTaskData>> _tasksByProjectContextId;
        private Dictionary<string, string> _assemblyLocationsByName;

        /// <summary>
        /// Sets logger up, including registering for logging events
        /// </summary>
        public override void Initialize(IEventSource eventSource)
        {
            ProcessParameters();

            eventSource.ProjectStarted += HandleProjectStarted;
            eventSource.TargetStarted += HandleTargetStarted;
            eventSource.TaskStarted += HandleTaskStarted;
            eventSource.BuildFinished += HandleBuildFinished;

            _targetIdsToNames = new Dictionary<int, string>();
            _tasks = new HashSet<TaskData>();
            _privateCollection = new ProjectCollection();
            _toolsVersionsByProjectContextId = new Dictionary<int, string>();
            _defaultTasksByToolset = new Dictionary<string, HashSet<UsingTaskData>>();
            _tasksByProjectContextId = new Dictionary<int, HashSet<UsingTaskData>>();
            _assemblyLocationsByName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Validate the logger parameters.  Should only be one: the path to the output csv file.
        /// </summary>
        private void ProcessParameters()
        {
            if (Parameters == null)
            {
            }

            string[] parameters = Parameters.Split(s_semicolonChar);

            if (parameters.Length != 1)
            {
                throw new LoggerException(@"Path to write CSV to required.  Specify using the following pattern: '/logger:TaskUsageLogger,D:\Repos\msbuild\bin\Windows_NT\Debug\TaskUsageLogger.dll;mylog.csv");
            }

            _logFile = parameters[0];
        }

        /// <summary>
        /// Each time we encounter a new project, we want to load it, and grab the task registration information:
        /// - For the default tasks for its ToolsVersion
        /// - For any tasks registered in the project file or its imports
        /// </summary>
        private void HandleProjectStarted(object sender, ProjectStartedEventArgs e)
        {
            if (!ShouldIgnoreProject(e.ProjectFile))
            {
                try
                {
                    // Load up a private copy of the project.
                    Project p = _privateCollection.LoadProject(e.ProjectFile, e.GlobalProperties, e.ToolsVersion);

                    // Save off task registration information for this project
                    GatherAndEvaluateDefaultTasksForToolsVersion(p.ToolsVersion, p);
                    GatherAndEvaluateTasksForProject(p, e.BuildEventContext.ProjectContextId);

                    // Keep a pointer to the ToolsVersion so that we'll be able to reference it
                    // later.  Index off ContextId since it appears to be globally unique within
                    // the build.
                    _toolsVersionsByProjectContextId[e.BuildEventContext.ProjectContextId] = p.ToolsVersion;

                    // unload now that we're done with it.
                    _privateCollection.UnloadProject(p);
                }
                catch (Exception ex)
                {
                    throw new LoggerException(String.Format(CultureInfo.CurrentCulture, "Failed to load and read task registration information from project '{0}'. {1}", e.ProjectFile, ex.Message));
                }
            }
        }

        /// <summary>
        /// Write out the CSV file based on the gathered task information.
        /// </summary>
        private void HandleBuildFinished(object sender, BuildFinishedEventArgs e)
        {
            using (StreamWriter sw = new StreamWriter(_logFile, append: false))
            {
                sw.WriteLine("Task Name, Containing Target, File Path, Project Path, Task Assembly, Task Id");

                foreach (TaskData t in _tasks)
                {
                    sw.WriteLine(t);
                }
            }
        }

        /// <summary>
        /// Save off the target name for later association with tasks
        /// </summary>
        private void HandleTargetStarted(object sender, TargetStartedEventArgs e)
        {
            _targetIdsToNames[e.BuildEventContext.TargetId] = e.TargetName;
        }

        /// <summary>
        /// For each task, save off all the data we care about.  We should already
        /// know or be able to derive everything by this point.
        /// </summary>
        private void HandleTaskStarted(object sender, TaskStartedEventArgs e)
        {
            if (!ShouldIgnoreProject(e.ProjectFile))
            {
                TaskData t = new TaskData
                    (
                        e.TaskName,
                        _targetIdsToNames[e.BuildEventContext.TargetId],
                        e.TaskFile,
                        e.ProjectFile,
                        GetAssemblySpecificationFor(e.TaskName, e.BuildEventContext.ProjectContextId, e.ProjectFile),
                        e.BuildEventContext.TaskId
                    );

                if (!_tasks.Add(t))
                {
                    throw new LoggerException(String.Format(CultureInfo.CurrentCulture, "Why do we have two instances of {0}?", t));
                }
            }
        }

        /// <summary>
        /// For a particular ToolsVersion, gather the list of default task registrations.
        /// </summary>
        private void GatherAndEvaluateDefaultTasksForToolsVersion(string toolsVersion, Project containingProject)
        {
            // Default task registrations are in terms of ToolsVersion, so if we've already seen this
            // ToolsVersion we don't need to redo that work.
            if (!_defaultTasksByToolset.ContainsKey(toolsVersion))
            {
                Toolset t = _privateCollection.GetToolset(toolsVersion);
                if (t == null)
                {
                    throw new LoggerException(String.Format(CultureInfo.CurrentCulture, "Why is toolset '{0}' missing??", toolsVersion));
                }

                // Gather the set of default tasks files.
                // Does NOT currently take into account override tasks.
                string[] defaultTasksFiles = Directory.GetFiles(t.ToolsPath, "*.*tasks", SearchOption.TopDirectoryOnly);

                HashSet<UsingTaskData> usingTasks = new HashSet<UsingTaskData>();
                foreach (string defaultTasksFile in defaultTasksFiles)
                {
                    ProjectRootElement pre = ProjectRootElement.Open(defaultTasksFile);
                    GatherAndEvaluatedTasksInFile(pre, containingProject, usingTasks);
                }

                _defaultTasksByToolset.Add(toolsVersion, usingTasks);
            }
        }

        /// <summary>
        /// Given a particular project, gather all tasks registered in that project or any
        /// of its imported targets files. (In other words, all non-default tasks.)
        /// </summary>
        private void GatherAndEvaluateTasksForProject(Project p, int projectContextId)
        {
            HashSet<UsingTaskData> usingTasks;
            if (!_tasksByProjectContextId.TryGetValue(projectContextId, out usingTasks))
            {
                usingTasks = new HashSet<UsingTaskData>();

                // Tasks in the project file itself
                GatherAndEvaluatedTasksInFile(p.Xml, p, usingTasks);

                // Tasks in each of the imports
                foreach (ResolvedImport import in p.Imports)
                {
                    GatherAndEvaluatedTasksInFile(import.ImportedProject, p, usingTasks);
                }

                _tasksByProjectContextId[projectContextId] = usingTasks;
            }
        }

        /// <summary>
        /// Given a project or targets file (the PRE), and the project context from which it comes, gather
        /// the list of task registrations defined in that file.
        /// </summary>
        /// <remarks>
        /// Does NOT currently take into account conditions on task registrations, so any tasks that are registered
        /// with two or more mutually exclusive conditions may end up associated with the wrong registration.
        /// </remarks>
        private void GatherAndEvaluatedTasksInFile(ProjectRootElement pre, Project containingProject, HashSet<UsingTaskData> usingTasks)
        {
            foreach (ProjectUsingTaskElement usingTask in pre.UsingTasks)
            {
                string evaluatedTaskName = EvaluateIfNecessary(usingTask.TaskName, containingProject);

                // A task registration can define either AssemblyName or AssemblyFile, but not both.
                string evaluatedTaskAssemblyPath;
                if (String.IsNullOrEmpty(usingTask.AssemblyName))
                {
                    evaluatedTaskAssemblyPath = EvaluateIfNecessary(usingTask.AssemblyFile, containingProject);
                    evaluatedTaskAssemblyPath = Path.GetFullPath(evaluatedTaskAssemblyPath);
                }
                else
                {
                    string evaluatedTaskAssemblyName = EvaluateIfNecessary(usingTask.AssemblyName, containingProject);

                    if (!String.IsNullOrEmpty(evaluatedTaskAssemblyName))
                    {
                        if (!_assemblyLocationsByName.TryGetValue(evaluatedTaskAssemblyName, out evaluatedTaskAssemblyPath))
                        {
                            try
                            {
                                // If all we have is an assembly name, try to find the file path so that we can write
                                // that to the CSV instead.
                                AssemblyName name = new AssemblyName(evaluatedTaskAssemblyName);
                                Assembly a = Assembly.Load(name);
                                evaluatedTaskAssemblyPath = a.Location;
                            }
                            catch (Exception)
                            {
                                // But if we can't, it's not critical -- just give them what we have.
                                evaluatedTaskAssemblyPath = evaluatedTaskAssemblyName;
                            }

                            _assemblyLocationsByName[evaluatedTaskAssemblyName] = evaluatedTaskAssemblyPath;
                        }
                    }
                    else
                    {
                        evaluatedTaskAssemblyPath = String.Empty;
                    }
                }

                UsingTaskData taskData = new UsingTaskData(evaluatedTaskName, evaluatedTaskAssemblyPath, pre.FullPath);

                usingTasks.Add(taskData);
            }
        }

        /// <summary>
        /// Extremely simple hacked up version of the MSBuild property expansion logic. Should work for the 90%
        /// case, but is not guaranteed accurate.
        /// </summary>
        private string EvaluateIfNecessary(string unevaluatedString, Project containingProject)
        {
            if (unevaluatedString.IndexOfAny(s_disallowedCharactersForExpansion) != -1)
            {
                throw new LoggerException(String.Format(CultureInfo.CurrentCulture, "This logger doesn't know how to evaluate '{0}'!", unevaluatedString));
            }

            if (unevaluatedString.IndexOf('$') == -1)
            {
                //no evaluation necessary
                return unevaluatedString;
            }

            string evaluatedString = unevaluatedString;
            for (var match = s_msbuildPropertyRegex.Match(unevaluatedString); match.Success; match = match.NextMatch())
            {
                string propertyName = match.Groups["name"].Value.Trim();
                string propertyValue = containingProject.GetPropertyValue(propertyName);

                if (!String.IsNullOrEmpty(propertyName))
                {
                    evaluatedString = evaluatedString.Replace("$(" + propertyName + ")", propertyValue);
                }
            }

            return evaluatedString;
        }

        /// <summary>
        /// Given a task name and its associated project, make a "best guess"
        /// at what assembly the task comes from.
        /// </summary>
        private string GetAssemblySpecificationFor(string taskName, int projectContextId, string projectFile)
        {
            // First let's see if this task is defined in the project anywhere
            HashSet<UsingTaskData> usingTasks = null;
            if (!_tasksByProjectContextId.TryGetValue(projectContextId, out usingTasks))
            {
                throw new LoggerException(String.Format(CultureInfo.CurrentCulture, "Why haven't we collected using task data for {0}?", projectFile));
            }

            // Just grab the first one.  NOT accurate if there are conditioned task registrations
            UsingTaskData matchingData = usingTasks.Where(ut => IsPartialNameMatch(taskName, ut.TaskName)).FirstOrDefault();

            if (matchingData == null)
            {
                // If there isn't a registration for the project, fall back to checking the default tasks for
                // that project's ToolsVersion.
                string parentProjectToolsVersion = null;
                if (!_toolsVersionsByProjectContextId.TryGetValue(projectContextId, out parentProjectToolsVersion))
                {
                    throw new LoggerException(String.Format(CultureInfo.CurrentCulture, "Why don't we have a cached ToolsVersion for {0}?", projectFile));
                }

                HashSet<UsingTaskData> defaultUsingTasks = null;
                if (!_defaultTasksByToolset.TryGetValue(parentProjectToolsVersion, out defaultUsingTasks))
                {
                    throw new LoggerException(String.Format(CultureInfo.CurrentCulture, "Why haven't we collected default using task data for TV {0}?", parentProjectToolsVersion));
                }

                matchingData = defaultUsingTasks.Where(ut => IsPartialNameMatch(taskName, ut.TaskName)).FirstOrDefault();
            }

            if (matchingData == null)
            {
                throw new LoggerException(String.Format(CultureInfo.CurrentCulture, "Why couldn't we find a matching UsingTask for task {0} in project {1}?", taskName, projectFile));
            }

            return matchingData.TaskAssembly;
        }

        /// <summary>
        /// Returns true if the two names "mean" the same thing e.g. "Microsoft.Build.Tasks.AL" vs just "AL".
        /// </summary>
        /// <remarks>
        /// Assumes that name1 is never longer than name2.  Again, a simplified version of MSBuild's actual
        /// fuzzy name resolution logic.
        /// </remarks>
        private static bool IsPartialNameMatch(string name1, string name2)
        {
            // perfect match
            if (String.Equals(name1, name2, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            // fuzzy match
            string[] parts = name2.Split(s_fullyQualifiedTaskNameSeperators);
            if (parts.Length > 1 && String.Equals(name1, parts[parts.Length - 1], StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Returns 'true' if this is a project we don't want to collect data from.
        ///
        /// Since both the '.sln' and '.metaproj' files are faked up projects used by MSBuild essentially
        /// as plumbing to connect other real projects, they're not very interesting to examine.
        /// </summary>
        /// <param name="projectPath"></param>
        /// <returns></returns>
        private static bool ShouldIgnoreProject(string projectPath)
        {
            string projectExtension = Path.GetExtension(projectPath);

            return String.Equals(projectExtension, ".sln", StringComparison.OrdinalIgnoreCase) ||
                   String.Equals(projectExtension, ".metaproj", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Store data about a particular task invocation
        /// </summary>
        [DebuggerDisplay("{TaskName} {TargetName} {TaskAssembly}")]
        private class TaskData
        {
            public string TaskName;
            public string TargetName;
            public string FilePath;
            public string ProjectPath;
            public string TaskAssembly;
            public int TaskId;

            public TaskData(string taskName, string targetName, string filePath, string projectPath, string taskAssembly, int taskId)
            {
                TaskName = taskName;
                TargetName = targetName;
                FilePath = filePath;
                ProjectPath = projectPath;
                TaskAssembly = taskAssembly;
                TaskId = taskId;
            }

            public override string ToString()
            {
                // CSV format
                return String.Format(CultureInfo.CurrentCulture, "{0}, {1}, {2}, {3}, {4}, {5}", this.TaskName, this.TargetName, this.FilePath, this.ProjectPath, this.TaskAssembly, this.TaskId);
            }
        }

        /// <summary>
        /// Store data about a particular task registration
        /// </summary>
        [DebuggerDisplay("{TaskName} {TaskAssembly} {FilePath}")]
        private class UsingTaskData
        {
            public string TaskName;
            public string TaskAssembly;
            public string FilePath;

            public UsingTaskData(string taskName, string taskAssembly, string filePath)
            {
                TaskName = taskName;
                TaskAssembly = taskAssembly;
                FilePath = filePath;
            }
        }
    }
}
