// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;

// NOTE: This is nearly identical to the MSBuild task in Microsoft.Build.Tasks.  We are deprecating that task,
// so this is the governing implementation.

namespace Microsoft.Build.BackEnd
{
    /// <remarks>
    /// This class implements the "MSBuild" task, which hands off child project files to the MSBuild engine to be built.
    /// </remarks>
    internal class MSBuild : ITask
    {
        /// <summary>
        /// Enum describing the behavior when a project doesn't exist on disk.
        /// </summary>
        private enum SkipNonexistentProjectsBehavior
        {
            /// <summary>
            /// Skip the project if there is no file on disk.
            /// </summary>
            Skip,

            /// <summary>
            /// Error if the project does not exist on disk.
            /// </summary>
            Error,

            /// <summary>
            /// Build even if the project does not exist on disk.
            /// </summary>
            Build
        }

        #region Properties

        // outputs of all built targets
        private readonly List<ITaskItem> _targetOutputs = new List<ITaskItem>();

        // Whether to skip project files that don't exist on disk. By default we error for such projects.
        private SkipNonexistentProjectsBehavior _skipNonexistentProjects = SkipNonexistentProjectsBehavior.Error;

        private TaskLoggingHelper _logHelper;

        /// <inheritdoc />
        /// <summary>
        /// The build engine, from ITask
        /// </summary>
        public IBuildEngine BuildEngine { get; set; }

        public IBuildEngine2 BuildEngine2 => (IBuildEngine2)BuildEngine;

        public IBuildEngine3 BuildEngine3 => (IBuildEngine3)BuildEngine;

        public TaskLoggingHelper Log => _logHelper ?? (_logHelper = new TaskLoggingHelperExtension(this,
            AssemblyResources.PrimaryResources, AssemblyResources.SharedResources, "MSBuild."));

        /// <inheritdoc />
        /// <summary>
        /// The host object, from ITask
        /// </summary>
        public ITaskHost HostObject { get; set; }

        /// <summary>
        /// A list of property name/value pairs to apply as global properties to 
        /// the child project.  
        /// A typical input: "propname1=propvalue1", "propname2=propvalue2", "propname3=propvalue3".
        /// </summary>
        /// <remarks>
        /// The fact that this is a string[] makes the following illegal:
        ///     <MSBuild
        ///         Properties="TargetPath=@(OutputPathItem)" />
        /// The engine fails on this because it doesn't like item lists being concatenated with string
        /// constants when the data is being passed into an array parameter.  So the workaround is to 
        /// write this in the project file:
        ///     <MSBuild
        ///         Properties="@(OutputPathItem->'TargetPath=%(Identity)')" />
        /// </remarks>
        public string[] Properties { get; set; }

        /// <summary>
        /// Gets or sets a semicolon-delimited list of global properties to remove.
        /// </summary>
        public string RemoveProperties { get; set; } = null;

        /// <summary>
        /// The targets to build in each project specified by the <see cref="Projects"/> property.
        /// </summary>
        /// <value>Array of target names.</value>
        public string[] Targets { get; set; }

        /// <summary>
        /// The projects to build.
        /// </summary>
        /// <value>Array of project items.</value>
        [Required]
        public ITaskItem[] Projects { get; set; } = null;

        /// <summary>
        /// Outputs of the targets built in each project.
        /// </summary>
        /// <value>Array of output items.</value>
        [Output]
        public ITaskItem[] TargetOutputs => _targetOutputs.ToArray();

        /// <summary>
        /// Indicates if the paths of target output items should be rebased relative to the calling project.
        /// </summary>
        /// <value>true, if target output item paths should be rebased</value>
        public bool RebaseOutputs { get; set; } = false;

        /// <summary>
        /// Forces the task to stop building the remaining projects as soon as any of
        /// them fail.
        /// </summary>
        public bool StopOnFirstFailure { get; set; } = false;

        /// <summary>
        /// When this is true, instead of calling the engine once to build all the targets (for each project),
        /// we would call the engine once per target (for each project).  The benefit of this is that
        /// if one target fails, you can still continue with the remaining targets.
        /// </summary>
        public bool RunEachTargetSeparately { get; set; } = false;

        /// <summary>
        /// Value of ToolsVersion to use when building projects passed to this task.
        /// </summary>
        public string ToolsVersion { get; set; } = null;

        /// <summary>
        /// When this is true we call the engine with all the projects at once instead of 
        /// calling the engine once per project
        /// </summary>
        public bool BuildInParallel { get; set; }

        /// <summary>
        /// If true the project will be unloaded once the operation is completed
        /// </summary>
        public bool UnloadProjectsOnCompletion { get; set; } = false;

        /// <summary>
        /// If true the cached result will be returned if present and a if MSBuild
        /// task is run its result will be cached in a scope (ProjectFileName, GlobalProperties)[TargetNames]
        /// as a list of build items
        /// </summary>
        public bool UseResultsCache { get; set; } = true;

        /// <summary>
        /// When this is true, project files that do not exist on the disk will be skipped. By default,
        /// such projects will cause an error.
        /// </summary>
        public string SkipNonexistentProjects
        {
            get
            {
                switch (_skipNonexistentProjects)
                {
                    case SkipNonexistentProjectsBehavior.Build:
                        return "Build";

                    case SkipNonexistentProjectsBehavior.Error:
                        return "False";

                    case SkipNonexistentProjectsBehavior.Skip:
                        return "True";

                    default:
                        ErrorUtilities.ThrowInternalError("Unexpected case {0}", _skipNonexistentProjects);
                        break;
                }

                ErrorUtilities.ThrowInternalErrorUnreachable();
                return null;
            }

            set
            {
                if (String.Equals("Build", value, StringComparison.OrdinalIgnoreCase))
                {
                    _skipNonexistentProjects = SkipNonexistentProjectsBehavior.Build;
                }
                else
                {
                    ErrorUtilities.VerifyThrowArgument(ConversionUtilities.CanConvertStringToBool(value), "MSBuild.InvalidSkipNonexistentProjectValue");
                    bool originalSkipValue = ConversionUtilities.ConvertStringToBool(value);
                    _skipNonexistentProjects = originalSkipValue ? SkipNonexistentProjectsBehavior.Skip : SkipNonexistentProjectsBehavior.Error;
                }
            }
        }

        /// <summary>
        /// Un-escape Targets, Properties (including Properties and AdditionalProperties as Project item metadata)
        /// will be un-escaped before processing. e.g. %3B (an escaped ';') in the string for any of them will 
        /// be treated as if it were an un-escaped ';'
        /// </summary>
        public string[] TargetAndPropertyListSeparators { get; set; } = null;

        /// <summary>
        /// If set, MSBuild will skip the targets specified in this build request if they are not defined in the
        /// <see cref="Projects"/> to build. This only applies to this build request (if another target calls the
        /// "missing target" later this will still result in an error).
        /// <remarks>
        /// This could be useful when implementing a breaking protocol change between projects or stubbing behavior 
        /// which may not make sense in all project types (e.g. Restore).
        /// </remarks>
        /// </summary>
        public bool SkipNonexistentTargets { get; set; }
        #endregion

        #region ITask Members

        public bool Execute()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Instructs the MSBuild engine to build one or more project files whose locations are specified by the
        /// <see cref="Projects"/> property.
        /// </summary>
        /// <returns>true if all projects build successfully; false if any project fails</returns>
        public async Task<bool> ExecuteInternal()
        {
            // If no projects were passed in, just return success.
            if ((Projects == null) || (Projects.Length == 0))
            {
                return true;
            }

            // We have been asked to unescape all escaped characters before processing
            if (TargetAndPropertyListSeparators != null && TargetAndPropertyListSeparators.Length > 0)
            {
                ExpandAllTargetsAndProperties();
            }

            // Parse the global properties into a hashtable.
            if (!PropertyParser.GetTableWithEscaping(Log, ResourceUtilities.GetResourceString("General.GlobalProperties"), "Properties", Properties, out Dictionary<string, string> propertiesTable))
            {
                return false;
            }

            // Parse out the properties to undefine, if any.
            string[] undefinePropertiesArray = null;
            if (!String.IsNullOrEmpty(RemoveProperties))
            {
                Log.LogMessageFromResources(MessageImportance.Low, "General.UndefineProperties");
                undefinePropertiesArray = RemoveProperties.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (string property in undefinePropertiesArray)
                {
                    Log.LogMessageFromText(String.Format(CultureInfo.InvariantCulture, "  {0}", property), MessageImportance.Low);
                }
            }

            bool isRunningMultipleNodes = BuildEngine2.IsRunningMultipleNodes;
            // If we are in single proc mode and stopOnFirstFailure is true, we cannot build in parallel because 
            // building in parallel sends all of the projects to the engine at once preventing us from not sending
            // any more projects after the first failure. Therefore, to preserve compatibility with whidbey if we are in this situation disable buildInParallel.
            if (!isRunningMultipleNodes && StopOnFirstFailure && BuildInParallel)
            {
                BuildInParallel = false;
                Log.LogMessageFromResources(MessageImportance.Low, "MSBuild.NotBuildingInParallel");
            }

            // When the condition below is met, provide an information message indicating stopOnFirstFailure
            // will have no effect. The reason there will be no effect is, when buildInParallel is true
            // All project files will be submitted to the engine all at once, this mean there is no stopping for failures between projects.
            // When RunEachTargetSeparately is false, all targets will be submitted to the engine at once, this means there is no way to stop between target failures.
            // therefore the first failure seen will be the only failure seen.
            if (isRunningMultipleNodes && BuildInParallel && StopOnFirstFailure && !RunEachTargetSeparately)
            {
                Log.LogMessageFromResources(MessageImportance.Low, "MSBuild.NoStopOnFirstFailure");
            }

            // This is a list of string[].  That is, each element in the list is a string[].  Each
            // string[] represents a set of target names to build.  Depending on the value 
            // of the RunEachTargetSeparately parameter, we each just call the engine to run all 
            // the targets together, or we call the engine separately for each target.
            List<string[]> targetLists = CreateTargetLists(Targets, RunEachTargetSeparately);

            bool success = true;
            ITaskItem[] singleProject = null;
            bool[] skipProjects = null;

            if (BuildInParallel)
            {
                skipProjects = new bool[Projects.Length];
                for (int i = 0; i < skipProjects.Length; i++)
                {
                    skipProjects[i] = true;
                }
            }
            else
            {
                singleProject = new ITaskItem[1];
            }

            // Read in each project file.  If there are any errors opening the file or parsing the XML,
            // raise an event and return False.  If any one of the projects fails to build, return False,
            // otherwise return True. If parallel build is requested we first check for file existence so
            // that we don't pass a non-existent file to IBuildEngine causing an exception
            for (int i = 0; i < Projects.Length; i++)
            {
                ITaskItem project = Projects[i];

                string projectPath = FileUtilities.AttemptToShortenPath(project.ItemSpec);

                if (StopOnFirstFailure && !success)
                {
                    // Inform the user that we skipped the remaining projects because StopOnFirstFailure=true.
                    Log.LogMessageFromResources(MessageImportance.Low, "MSBuild.SkippingRemainingProjects");

                    // We have encountered a failure.  Caller has requested that we not 
                    // continue with remaining projects.
                    break;
                }

                if (File.Exists(projectPath) || (_skipNonexistentProjects == SkipNonexistentProjectsBehavior.Build))
                {
                    if (FileUtilities.IsVCProjFilename(projectPath))
                    {
                        Log.LogErrorWithCodeFromResources("MSBuild.ProjectUpgradeNeededToVcxProj", project.ItemSpec);
                        success = false;
                        continue;
                    }

                    // If we are building in parallel we want to only make one call to
                    // ExecuteTargets once we verified that all projects exist
                    if (!BuildInParallel)
                    {
                        singleProject[0] = project;
                        bool executeResult = await ExecuteTargets(
                                                singleProject,
                                                propertiesTable,
                                                undefinePropertiesArray,
                                                targetLists,
                                                StopOnFirstFailure,
                                                RebaseOutputs,
                                                BuildEngine3,
                                                Log,
                                                _targetOutputs,
                                                UseResultsCache,
                                                UnloadProjectsOnCompletion,
                                                ToolsVersion,
                                                SkipNonexistentTargets
                                                );

                        if (!executeResult)
                        {
                            success = false;
                        }
                    }
                    else
                    {
                        skipProjects[i] = false;
                    }
                }
                else
                {
                    if (_skipNonexistentProjects == SkipNonexistentProjectsBehavior.Skip)
                    {
                        Log.LogMessageFromResources(MessageImportance.High, "MSBuild.ProjectFileNotFoundMessage", project.ItemSpec);
                    }
                    else
                    {
                        ErrorUtilities.VerifyThrow(_skipNonexistentProjects == SkipNonexistentProjectsBehavior.Error, "skipNonexistentProjects has unexpected value {0}", _skipNonexistentProjects);
                        Log.LogErrorWithCodeFromResources("MSBuild.ProjectFileNotFound", project.ItemSpec);
                        success = false;
                    }
                }
            }

            // We need to build all the projects that were not skipped
            if (BuildInParallel)
            {
                success = await BuildProjectsInParallel(propertiesTable, undefinePropertiesArray, targetLists, success, skipProjects);
            }

            return success;
        }

        /// <summary>
        /// Build projects which have not been skipped. This will be done in parallel
        /// </summary>
        private async Task<bool> BuildProjectsInParallel(Dictionary<string, string> propertiesTable, string[] undefinePropertiesArray, List<string[]> targetLists, bool success, bool[] skipProjects)
        {
            // There were some projects that were skipped so we need to recreate the
            // project array with those projects removed
            var projectsToBuildList = new List<ITaskItem>();
            for (int i = 0; i < Projects.Length; i++)
            {
                if (!skipProjects[i])
                {
                    projectsToBuildList.Add(Projects[i]);
                }
            }
            var projectToBuildInParallel = projectsToBuildList.ToArray();

            // Make the call to build the projects
            if (projectToBuildInParallel.Length <= 0) return success;

            bool executeResult = await ExecuteTargets
            (
                projectToBuildInParallel,
                propertiesTable,
                undefinePropertiesArray,
                targetLists,
                StopOnFirstFailure,
                RebaseOutputs,
                BuildEngine3,
                Log,
                _targetOutputs,
                UseResultsCache,
                UnloadProjectsOnCompletion,
                ToolsVersion,
                SkipNonexistentTargets
            );

            if (!executeResult)
            {
                success = false;
            }

            return success;
        }

        /// <summary>
        /// Expand and re-construct arrays of all targets and properties
        /// </summary>
        private void ExpandAllTargetsAndProperties()
        {
            var expandedProperties = new List<string>();
            var expandedTargets = new List<string>();

            if (Properties != null)
            {
                // Expand all properties
                foreach (string p in Properties)
                {
                    // Split each property according to the separators
                    string[] expandedPropertyValues = p.Split(TargetAndPropertyListSeparators, StringSplitOptions.RemoveEmptyEntries);
                    // Add the resultant properties to the final list
                    foreach (string property in expandedPropertyValues)
                    {
                        expandedProperties.Add(property);
                    }
                }

                Properties = expandedProperties.ToArray();
            }

            if (Targets != null)
            {
                // Expand all targets
                foreach (string t in Targets)
                {
                    // Split each target according to the separators
                    string[] expandedTargetValues = t.Split(TargetAndPropertyListSeparators, StringSplitOptions.RemoveEmptyEntries);
                    // Add the resultant targets to the final list
                    foreach (string target in expandedTargetValues)
                    {
                        expandedTargets.Add(target);
                    }
                }

                Targets = expandedTargets.ToArray();
            }
        }

        internal static List<string[]> CreateTargetLists
            (
            string[] targets,
            bool runEachTargetSeparately
            )
        {
            // This is a list of string[].  That is, each element in the list is a string[].  Each
            // string[] represents a set of target names to build.  Depending on the value 
            // of the RunEachTargetSeparately parameter, we each just call the engine to run all 
            // the targets together, or we call the engine separately for each target.
            var targetLists = new List<string[]>();
            if ((runEachTargetSeparately) && (targets != null) && (targets.Length > 0))
            {
                // Separate target invocations for each individual target.
                foreach (string targetName in targets)
                {
                    targetLists.Add(new[] { targetName });
                }
            }
            else
            {
                // Just one target list, and that's whatever was passed in.  We will call the engine
                // once (per project) with the entire target list.
                targetLists.Add(targets);
            }

            return targetLists;
        }

        internal static async Task<bool> ExecuteTargets(
            ITaskItem[] projects,
            Dictionary<string, string> propertiesTable,
            string[] undefineProperties,
            List<string[]> targetLists,
            bool stopOnFirstFailure,
            bool rebaseOutputs,
            IBuildEngine3 buildEngine,
            TaskLoggingHelper log,
            List<ITaskItem> targetOutputs,
            bool useResultsCache,
            bool unloadProjectsOnCompletion,
            string toolsVersion,
            bool skipNonexistentTargets)
        {
            bool success = true;

            // We don't log a message about the project and targets we're going to
            // build, because it'll all be in the immediately subsequent ProjectStarted event.

            var projectDirectory = new string[projects.Length];
            var projectNames = new string[projects.Length];
            var toolsVersions = new string[projects.Length];
            var projectProperties = new Dictionary<string, string>[projects.Length];
            var undefinePropertiesPerProject = new List<string>[projects.Length];

            for (int i = 0; i < projectNames.Length; i++)
            {
                projectNames[i] = null;
                projectProperties[i] = propertiesTable;

                if (projects[i] != null)
                {
                    // Retrieve projectDirectory only the first time.  It never changes anyway.
                    string projectPath = FileUtilities.AttemptToShortenPath(projects[i].ItemSpec);
                    projectDirectory[i] = Path.GetDirectoryName(projectPath);
                    projectNames[i] = projects[i].ItemSpec;
                    toolsVersions[i] = toolsVersion;

                    // If the user specified a different set of global properties for this project, then
                    // parse the string containing the properties
                    if (!String.IsNullOrEmpty(projects[i].GetMetadata("Properties")))
                    {
                        if (!PropertyParser.GetTableWithEscaping
                             (log, ResourceUtilities.FormatResourceString("General.OverridingProperties", projectNames[i]), "Properties", projects[i].GetMetadata("Properties").Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries),
                              out Dictionary<string, string> preProjectPropertiesTable)
                           )
                        {
                            return false;
                        }

                        projectProperties[i] = preProjectPropertiesTable;
                    }

                    if (undefineProperties != null)
                    {
                        undefinePropertiesPerProject[i] = new List<string>(undefineProperties);
                    }

                    // If the user wanted to undefine specific global properties for this project, parse
                    // that string and remove them now.
                    string projectUndefineProperties = projects[i].GetMetadata("UndefineProperties");
                    if (!String.IsNullOrEmpty(projectUndefineProperties))
                    {
                        string[] propertiesToUndefine = projectUndefineProperties.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
                        if (undefinePropertiesPerProject[i] == null)
                        {
                            undefinePropertiesPerProject[i] = new List<string>(propertiesToUndefine.Length);
                        }

                        if (log != null && propertiesToUndefine.Length > 0)
                        {
                            log.LogMessageFromResources(MessageImportance.Low, "General.ProjectUndefineProperties", projectNames[i]);
                            foreach (string property in propertiesToUndefine)
                            {
                                undefinePropertiesPerProject[i].Add(property);
                                log.LogMessageFromText(String.Format(CultureInfo.InvariantCulture, "  {0}", property), MessageImportance.Low);
                            }
                        }
                    }

                    // If the user specified a different set of global properties for this project, then
                    // parse the string containing the properties
                    if (!String.IsNullOrEmpty(projects[i].GetMetadata("AdditionalProperties")))
                    {
                        if (!PropertyParser.GetTableWithEscaping
                             (log, ResourceUtilities.FormatResourceString("General.AdditionalProperties", projectNames[i]), "AdditionalProperties", projects[i].GetMetadata("AdditionalProperties").Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries),
                              out Dictionary<string, string> additionalProjectPropertiesTable)
                           )
                        {
                            return false;
                        }

                        var combinedTable = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                        // First copy in the properties from the global table that not in the additional properties table
                        if (projectProperties[i] != null)
                        {
                            foreach (KeyValuePair<string, string> entry in projectProperties[i])
                            {
                                if (!additionalProjectPropertiesTable.ContainsKey(entry.Key))
                                {
                                    combinedTable.Add(entry.Key, entry.Value);
                                }
                            }
                        }
                        // Add all the additional properties
                        foreach (KeyValuePair<string, string> entry in additionalProjectPropertiesTable)
                        {
                            combinedTable.Add(entry.Key, entry.Value);
                        }
                        projectProperties[i] = combinedTable;
                    }

                    // If the user specified a different toolsVersion for this project - then override the setting
                    if (!String.IsNullOrEmpty(projects[i].GetMetadata("ToolsVersion")))
                    {
                        toolsVersions[i] = projects[i].GetMetadata("ToolsVersion");
                    }
                }
            }

            foreach (string[] targetList in targetLists)
            {
                if (stopOnFirstFailure && !success)
                {
                    // Inform the user that we skipped the remaining targets StopOnFirstFailure=true.
                    log.LogMessageFromResources(MessageImportance.Low, "MSBuild.SkippingRemainingTargets");

                    // We have encountered a failure.  Caller has requested that we not 
                    // continue with remaining targets.
                    break;
                }

                // Send the project off to the build engine.  By passing in null to the 
                // first param, we are indicating that the project to build is the same
                // as the *calling* project file.

                var taskHost = (TaskHost)buildEngine;
                BuildEngineResult result = await taskHost.InternalBuildProjects(projectNames, targetList, projectProperties, undefinePropertiesPerProject, toolsVersions, true /* ask that target outputs are returned in the buildengineresult */, skipNonexistentTargets);

                bool currentTargetResult = result.Result;
                IList<IDictionary<string, ITaskItem[]>> targetOutputsPerProject = result.TargetOutputsPerProject;
                success = success && currentTargetResult;

                // If the engine was able to satisfy the build request
                if (currentTargetResult)
                {
                    for (int i = 0; i < projects.Length; i++)
                    {
                        IEnumerable<string> nonNullTargetList = targetList ?? targetOutputsPerProject[i].Keys;

                        foreach (string targetName in nonNullTargetList)
                        {
                            if (targetOutputsPerProject[i].ContainsKey(targetName))
                            {
                                ITaskItem[] outputItemsFromTarget = targetOutputsPerProject[i][targetName];

                                foreach (ITaskItem outputItemFromTarget in outputItemsFromTarget)
                                {
                                    // No need to rebase if the calling project is the same as the callee project 
                                    // (project == null).  Also no point in trying to copy item metadata either,
                                    // because no items were passed into the Projects parameter!
                                    if (projects[i] != null)
                                    {
                                        // Rebase the output item paths if necessary.  No need to rebase if the calling
                                        // project is the same as the callee project (project == null).
                                        if (rebaseOutputs)
                                        {
                                            try
                                            {
                                                outputItemFromTarget.ItemSpec = Path.Combine(projectDirectory[i], outputItemFromTarget.ItemSpec);
                                            }
                                            catch (ArgumentException e)
                                            {
                                                log.LogWarningWithCodeFromResources(null, projects[i].ItemSpec, 0, 0, 0, 0, "MSBuild.CannotRebaseOutputItemPath", outputItemFromTarget.ItemSpec, e.Message);
                                            }
                                        }

                                        // Copy the custom item metadata from the "Projects" items to these
                                        // output items.
                                        projects[i].CopyMetadataTo(outputItemFromTarget);

                                        // Set a metadata on the output items called "MSBuildProjectFile" which tells you which project file produced this item.
                                        if (String.IsNullOrEmpty(outputItemFromTarget.GetMetadata(ItemMetadataNames.msbuildSourceProjectFile)))
                                        {
                                            outputItemFromTarget.SetMetadata(ItemMetadataNames.msbuildSourceProjectFile, projects[i].GetMetadata(FileUtilities.ItemSpecModifiers.FullPath));
                                        }
                                    }

                                    // Set a metadata on the output items called "MSBuildTargetName" which tells you which target produced this item.
                                    if (String.IsNullOrEmpty(outputItemFromTarget.GetMetadata(ItemMetadataNames.msbuildSourceTargetName)))
                                    {
                                        outputItemFromTarget.SetMetadata(ItemMetadataNames.msbuildSourceTargetName, targetName);
                                    }
                                }

                                targetOutputs.AddRange(outputItemsFromTarget);
                            }
                        }
                    }
                }
            }

            return success;
        }

        #endregion
    }
}
