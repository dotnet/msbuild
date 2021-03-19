// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Xml;

using Microsoft.Build.BuildEngine.Shared;
using Microsoft.Build.Framework;

namespace Microsoft.Build.BuildEngine
{
    /// <summary>
    /// This class is used to generate an MSBuild wrapper project for a solution file or standalone VC project.
    /// </summary>
    /// <owner>LukaszG, RGoel</owner>
    static public class SolutionWrapperProject
    {
        private const string webProjectOverrideFolder = "_PublishedWebsites";
        private const string cacheSolutionConfigurationPropertyName = "_SolutionProjectConfiguration";
        private const string cacheToolsVersionPropertyName = "_SolutionProjectToolsVersion";
        private const string cacheProjectListName = "_SolutionProjectProjects";
        private const string cacheVersionNumber = "_SolutionProjectCacheVersion";

        /// <summary>
        /// Given the full path to a solution, returns a string containing the v3.5 MSBuild-format
        /// wrapper project for that solution.
        /// </summary>
        /// <param name="solutionPath">Full path to the solution we are wrapping</param>
        /// <param name="toolsVersionOverride">May be null.  If non-null, contains the ToolsVersion passed in on the command line</param>\
        /// <param name="projectBuildEventContext">An event context for logging purposes.</param>
        /// <returns></returns>
        static public string Generate(string solutionPath, string toolsVersionOverride, BuildEventContext projectBuildEventContext)
        {
            Project msbuildProject = new Project();

            SolutionParser solution = new SolutionParser();
            solution.SolutionFile = solutionPath;
            solution.ParseSolutionFile();

            Generate(solution, msbuildProject, toolsVersionOverride, projectBuildEventContext);

            return msbuildProject.Xml;
        }

        /// <summary>
        /// This method generates an XmlDocument representing an MSBuild project file from the list of
        /// projects and project dependencies that have been collected from the solution file.
        /// </summary>
        /// <param name="solution"></param>
        /// <param name="msbuildProject"></param>
        /// <param name="toolsVersionOverride">Tools Version override (may be null).
        /// Any /tv:xxx switch would cause a value here.</param>
        /// <returns></returns>
        /// <owner>RGoel</owner>
        static internal void Generate(SolutionParser solution, Project msbuildProject, string toolsVersionOverride, BuildEventContext projectBuildEventContext)
        {
            // Validate against our minimum for upgradable projects
            ProjectFileErrorUtilities.VerifyThrowInvalidProjectFile(solution.Version >= SolutionParser.slnFileMinVersion,
                "SubCategoryForSolutionParsingErrors", new BuildEventFileInfo(solution.SolutionFile), "SolutionParseUpgradeNeeded");

            // Although we only return an XmlDocument back, we need to make decisions about tools versions because
            // we have to choose what <UsingTask> tags to put in, whether to put a ToolsVersion parameter
            // on <MSBuild> task tags, and what MSBuildToolsPath to use when scanning child projects
            // for dependency information.
            string wrapperProjectToolsVersion = DetermineWrapperProjectToolsVersion(toolsVersionOverride);

            msbuildProject.DefaultTargets = "Build";
            msbuildProject.DefaultToolsVersion = wrapperProjectToolsVersion;
            Engine parentEngine = msbuildProject.ParentEngine;

            string solutionProjectCache = solution.SolutionFile + ".cache";

            bool? upToDate = LoadCache(solution, msbuildProject, projectBuildEventContext, wrapperProjectToolsVersion, parentEngine, solutionProjectCache);

            if (upToDate == true)
            {
                // Cache exists, was loaded, and was up to date: we're done
                return;
            }

            // Cache didn't exist or wasn't up to date; generate a new one
            Project solutionProject = msbuildProject;

            if (upToDate == false)
            {
                // We have already loaded a cache file we can't use; we need to work in a new project object
                solutionProject = CreateNewProject(solution, wrapperProjectToolsVersion, parentEngine, solutionProject);
            }

            CreateSolutionProject(solution, solutionProject, projectBuildEventContext, wrapperProjectToolsVersion, parentEngine, solutionProjectCache);

            if (upToDate == false)
            {
                // Put the contents of the new project object into the one we were passed
                msbuildProject.LoadFromXmlDocument(solutionProject.XmlDocument, projectBuildEventContext, msbuildProject.LoadSettings);
            }

            // Write a new cache file, hopefully we can use it next time
            UpdateCache(parentEngine, msbuildProject, solutionProjectCache, projectBuildEventContext);
        }

        /// <summary>
        /// Attempts to load the solution cache if any into the project provided. Returns null if no attempt was made to load the cache,
        /// false if it was loaded but could not be used, or true if it was loaded and can be used.
        /// </summary>
        private static bool? LoadCache(SolutionParser solution, Project msbuildProject, BuildEventContext projectBuildEventContext, string wrapperProjectToolsVersion, Engine parentEngine, string solutionProjectCache)
        {
            if (!IsSolutionCacheEnabled() || !File.Exists(solutionProjectCache))
            {
                return null;
            }

            try
            {
                msbuildProject.Load(solutionProjectCache);

                string fullSolutionConfigurationName = DetermineLikelyActiveSolutionConfiguration(solution, parentEngine);

                bool cacheUpToDate = IsCacheUpToDate(parentEngine, solution.SolutionFile, solution.SolutionFileDirectory, msbuildProject, projectBuildEventContext, fullSolutionConfigurationName, wrapperProjectToolsVersion);

                if (cacheUpToDate)
                {
                    // We're done -- use the cache
                    parentEngine.LoggingServices.LogComment(projectBuildEventContext, "SolutionCacheBeingUsed", solutionProjectCache, fullSolutionConfigurationName, wrapperProjectToolsVersion);

                    return true;
                }
            }
            catch (Exception ex)
            {
                if (ExceptionHandling.IsCriticalException(ex))
                    throw;
                // Eat any regular exceptions: we'll just not use the cache
                parentEngine.LoggingServices.LogComment(projectBuildEventContext, "SolutionCacheReadError", solutionProjectCache, ex.Message);
            }

            // Cache exists, but was not useable
            return false;
        }

        /// <summary>
        /// Attempt to save a new, updated solution cache file.
        /// </summary>
        private static void UpdateCache(Engine parentEngine, Project msbuildProject, string solutionProjectCache, BuildEventContext projectBuildEventContext)
        {
            if (!IsSolutionCacheEnabled())
            {
                return;
            }

            try
            {
                msbuildProject.Save(solutionProjectCache);
            }
            catch (Exception ex)
            {
                if (ExceptionHandling.IsCriticalException(ex))
                    throw;
                // Eat any regular exceptions: we'll just not use the cache
                parentEngine.LoggingServices.LogComment(projectBuildEventContext, "SolutionCacheWriteError", solutionProjectCache, ex.Message);
            }
        }

        /// <summary>
        /// Determine whether solution file caches are enabled. If the environment variable "MSBuildUseNoSolutionCache" is
        /// NOT defined, they are enabled.
        /// </summary>
        private static bool IsSolutionCacheEnabled()
        {
            bool solutionCacheEnabled = String.IsNullOrEmpty(Environment.GetEnvironmentVariable("MSBuildUseNoSolutionCache"));
            return solutionCacheEnabled;
        }

        /// <summary>
        /// Given a cache loaded into a project, determines whether it is up to date with respect to the projects and the solution file listed
        /// with it, and was created with the same configuration/platform and tools version values as the ones currently in use.
        /// </summary>
        private static bool IsCacheUpToDate(Engine parentEngine, string solutionFile,  string solutionFileDirectory, Project msbuildProject, BuildEventContext projectBuildEventContext, string fullSolutionConfigurationName, string wrapperProjectToolsVersion)
        {
            // Check the full solution configuration matches, eg "Debug|AnyCPU"
            string cacheSolutionConfigurationName = msbuildProject.GetEvaluatedProperty(cacheSolutionConfigurationPropertyName);
            string cacheToolsVersion = msbuildProject.GetEvaluatedProperty(cacheToolsVersionPropertyName);
            string cacheVersion = msbuildProject.GetEvaluatedProperty(cacheVersionNumber);

            if (cacheSolutionConfigurationName == null || cacheToolsVersion == null)
            {
                // Unexpected cache format; we can't use it
                return false;
            }

            if (!String.Equals(fullSolutionConfigurationName, cacheSolutionConfigurationName, StringComparison.OrdinalIgnoreCase))
            {
                parentEngine.LoggingServices.LogComment(projectBuildEventContext, "SolutionCacheNotApplicable", "Configuration", cacheSolutionConfigurationName, fullSolutionConfigurationName);
                return false;
            }

            if (!String.Equals(wrapperProjectToolsVersion, cacheToolsVersion, StringComparison.OrdinalIgnoreCase))
            {
                parentEngine.LoggingServices.LogComment(projectBuildEventContext, "SolutionCacheNotApplicable", "ToolsVersion", cacheToolsVersion, wrapperProjectToolsVersion);
                return false;
            }

	    // We also store the version of MSBuild that wrote the file and verify it's the same as ours: that ensures that we 
	    // don't read possibly incompatible caches.
            string thisVersion = Constants.AssemblyVersion;
            if (!String.Equals(cacheVersion, thisVersion, StringComparison.OrdinalIgnoreCase))
            {
                parentEngine.LoggingServices.LogComment(projectBuildEventContext, "SolutionCacheNotApplicableDueToCacheVersion", cacheVersion, thisVersion);
                return false;
            }

            // Finally check timestamps
            BuildItemGroup allProjects = msbuildProject.GetEvaluatedItemsByName(cacheProjectListName);
            List<string> inputs = new List<string>();
            foreach (BuildItem item in allProjects.Items)
            {
                inputs.Add(item.EvaluatedItemSpec);
            }

            if (inputs.Count == 0)
            {
                // There's no inputs; either an old-format cache file, or there's really
                // no projects in this solution. In the former case, we need to regenerate.
                // In the latter case, we don't really care if we do. So say it's out of date.
                return false;
            }

            // If there are inputs to check, we should also add the solution file, as we need to make sure the 
            // solution file is up to date with respect to the cache file

            // Get the solution file name because the solution file may be something like myDirectory\mysolution.sln
            // and since we have already calculated the directory for the solution file, we just need the filename name to 
            // combine with the directory to get the full path to the solution file without having to call GetFullPath again.
            string solutionFileName = Path.GetFileName(solutionFile);
            string solutionFileLocation = Path.Combine(solutionFileDirectory, solutionFileName);
            inputs.Add(solutionFileLocation);

            List<string> outputs = new List<string>();
            outputs.Add(msbuildProject.FullFileName);

            DependencyAnalysisLogDetail dependencyAnalysisDetail;
            bool isAnyOutOfDate = TargetDependencyAnalyzer.IsAnyOutOfDate(out dependencyAnalysisDetail, solutionFileDirectory, inputs, outputs);

            if (isAnyOutOfDate)
            {
                string reason = TargetDependencyAnalyzer.GetFullBuildReason(dependencyAnalysisDetail);

                string message = ResourceUtilities.FormatResourceString("SolutionCacheOutOfDate", reason);

                parentEngine.LoggingServices.LogCommentFromText(projectBuildEventContext, MessageImportance.Low, message);
                return false;
            }
            else
            {
                return true;
            }
        }

        /// <summary>
        /// Create a new project to construct a solution wrapper cache inside
        /// </summary>
        private static Project CreateNewProject(SolutionParser solution, string wrapperProjectToolsVersion, Engine parentEngine, Project solutionProject)
        {
            try
            {
                solutionProject = new Project(parentEngine, wrapperProjectToolsVersion);
                solutionProject.DefaultTargets = "Build";
                solutionProject.DefaultToolsVersion = wrapperProjectToolsVersion;
                solutionProject.IsLoadedByHost = false;
            }
            catch (InvalidOperationException)
            {
                BuildEventFileInfo fileInfo = new BuildEventFileInfo(solution.SolutionFile);
                string errorCode;
                string helpKeyword;
                string message = ResourceUtilities.FormatResourceString(out errorCode, out helpKeyword, "UnrecognizedToolsVersion", wrapperProjectToolsVersion);
                throw new InvalidProjectFileException(solution.SolutionFile, fileInfo.Line, fileInfo.Column, fileInfo.EndLine, fileInfo.EndColumn, message, null, errorCode, helpKeyword);
            }
            return solutionProject;
        }

        /// <summary>
        /// Given an empty project and a solution, create a new solution project from the solution.
        /// </summary>
        private static void CreateSolutionProject(SolutionParser solution, Project msbuildProject, BuildEventContext projectBuildEventContext, string wrapperProjectToolsVersion, Engine parentEngine, string solutionProjectCache)
        {
            // We have to figure out what tools version the children will be built with, because we will 
            // have to load and scan them to construct the solution wrapper project, and we should use the 
            // same tools version they'll build with.
            string childProjectToolsVersion = DetermineChildProjectToolsVersion(parentEngine, wrapperProjectToolsVersion);

            string taskAssembly;

            if (String.Equals(msbuildProject.ToolsVersion, "2.0", StringComparison.OrdinalIgnoreCase))
            {
                taskAssembly = "Microsoft.Build.Tasks, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a";
            }
            else
            {
                taskAssembly = "Microsoft.Build.Tasks.v3.5, Version=3.5.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a";
            }

            // Fully qualified class names are more performant
            msbuildProject.AddNewUsingTaskFromAssemblyName("Microsoft.Build.Tasks.CreateTemporaryVCProject", taskAssembly);
            msbuildProject.AddNewUsingTaskFromAssemblyName("Microsoft.Build.Tasks.ResolveVCProjectOutput", taskAssembly);

            AddFakeReleaseSolutionConfigurationIfNecessary(solution);

            Dictionary<int, List<ProjectInSolution>> projectsByDependencyLevel = new Dictionary<int, List<ProjectInSolution>>();

            string fullSolutionConfigurationName = PredictActiveSolutionConfigurationName(solution, parentEngine);

            ScanProjectDependencies(solution, parentEngine, childProjectToolsVersion, fullSolutionConfigurationName, projectBuildEventContext);
            ConvertVcToVcDependenciesToReferences(solution, parentEngine, projectBuildEventContext);
            AssignDependencyLevels(solution, projectsByDependencyLevel);
            AddVirtualReferencesForStaticLibraries(solution);

            // Add config, platform and tools version to indicate relevance of cache
            AddCacheRelatedProperties(msbuildProject, fullSolutionConfigurationName, wrapperProjectToolsVersion, solution.ProjectsInOrder);

            // Add default solution configuration/platform names in case the user doesn't specify them on the command line
            AddConfigurationPlatformDefaults(msbuildProject, solution);

            // Add default Venus configuration names (for more details, see comments for this method)
            AddVenusConfigurationDefaults(msbuildProject);

            // Add solution related macros
            AddGlobalProperties(msbuildProject, solution);

            // Add a property group for each solution configuration, each with one XML property containing the
            // project configurations in this solution configuration.
            foreach (ConfigurationInSolution solutionConfiguration in solution.SolutionConfigurations)
            {
                AddPropertyGroupForSolutionConfiguration(msbuildProject, solution, solutionConfiguration);
            }

            // Add the initial target with some solution configuration validation/information
            // Only do it if we actually have any solution configurations...
            if (solution.SolutionConfigurations.Count > 0)
            {
                AddInitialTargets(msbuildProject);
            }

            // Add a <target> element for each project we have
            foreach (ProjectInSolution proj in solution.ProjectsInOrder)
            {
                string errorMessage;

                // is it a solution folder?
                if (proj.ProjectType == SolutionProjectType.SolutionFolder)
                {
                    // Don't add any targets. Solution folder "projects" aren't actually projects at all and should be ignored.
                }
                else if (proj.ProjectType == SolutionProjectType.WebProject)
                {
                    AddTargetForWebProject(msbuildProject, solution, proj, null);
                    AddTargetForWebProject(msbuildProject, solution, proj, "Clean");
                    AddTargetForWebProject(msbuildProject, solution, proj, "Rebuild");
                    AddTargetForWebProject(msbuildProject, solution, proj, "Publish");
                }
                else if (proj.ProjectType == SolutionProjectType.VCProject)
                {
                    AddTargetForVCProject(msbuildProject, solution, proj, null);
                    AddTargetForVCProject(msbuildProject, solution, proj, "Clean");
                    AddTargetForVCProject(msbuildProject, solution, proj, "Rebuild");
                    AddTargetForVCProject(msbuildProject, solution, proj, "Publish");
                }
                // is it an MSBuild project?
                else if ((proj.ProjectType == SolutionProjectType.ManagedProject) ||
                         (proj.CanBeMSBuildProjectFile(out errorMessage)))
                {
                    string safeItemNameFromProjectName = MakeIntoSafeItemName(proj.ProjectName);
                    string targetOutputItemName = string.Format(CultureInfo.InvariantCulture, "{0}BuildOutput", safeItemNameFromProjectName);
                    AddTargetForManagedProject(msbuildProject, solution, proj, targetOutputItemName, null);
                    AddTargetForManagedProject(msbuildProject, solution, proj, null, "Clean");
                    AddTargetForManagedProject(msbuildProject, solution, proj, targetOutputItemName, "Rebuild");
                    AddTargetForManagedProject(msbuildProject, solution, proj, null, "Publish");
                }
                else
                {
                    AddTargetForUnknownProjectType(msbuildProject, solution, proj, null, errorMessage);
                    AddTargetForUnknownProjectType(msbuildProject, solution, proj, "Clean", errorMessage);
                    AddTargetForUnknownProjectType(msbuildProject, solution, proj, "Rebuild", errorMessage);
                    AddTargetForUnknownProjectType(msbuildProject, solution, proj, "Publish", errorMessage);
                }
            }

            // Add a target called "Build" that depends on all the other projects.  This will be the
            // default target that is invoked when the "project" is built.
            AddAllDependencyTarget(msbuildProject, "Build", "CollectedBuildOutput", null, projectsByDependencyLevel);
            Target cleanTarget = AddAllDependencyTarget(msbuildProject, "Clean", null, "Clean", projectsByDependencyLevel);

            // As far as cleaning the solution project cache (if any) goes, we can't do it easily, because by the time we know we
            // need to do a clean, we've already loaded the cache. Instead, at the end of a solution clean, we'll delete the cache
            // file if any. A solution rebuild won't delete the cache, because probably one would expect a rebuild to leave it behind.
            if (IsSolutionCacheEnabled())
            {
                BuildTask deleteTask = cleanTarget.AddNewTask("Delete");
                // Don't use $(MSBuildProjectFile) for safety, in case user has copied and re-purposed this cache file
                deleteTask.SetParameterValue("Files", Path.GetFileName(solutionProjectCache));
            }

            AddAllDependencyTarget(msbuildProject, "Rebuild", "CollectedBuildOutput", "Rebuild", projectsByDependencyLevel);
            AddAllDependencyTarget(msbuildProject, "Publish", null, "Publish", projectsByDependencyLevel);

            // Special environment variable to allow people to see the in-memory MSBuild project generated
            // to represent the SLN.
            if (Environment.GetEnvironmentVariable("MSBuildEmitSolution") != null)
            {
                msbuildProject.Save(solution.SolutionFile + ".proj");
            }
        }

        /// <summary>
        /// Adds properties indicating the current solution configuration and tools version into the solution project.
        /// Also lists all the projects in the solution, as items.
        /// </summary>
        private static void AddCacheRelatedProperties(Project msbuildProject, string fullSolutionConfigurationName, string toolsVersion, ArrayList projects)
        {
            BuildPropertyGroup cachePropertyGroup = msbuildProject.AddNewPropertyGroup(false /* insertAtEndOfProject = false */);

            // Store the solution configuration, if it's available (ie., not null because it's invalid)
            if (fullSolutionConfigurationName != null)
            {
                cachePropertyGroup.AddNewProperty(cacheSolutionConfigurationPropertyName, fullSolutionConfigurationName);
            }

            // Store the tools version, too.
            cachePropertyGroup.AddNewProperty(cacheToolsVersionPropertyName, toolsVersion);

            // And the engine version, so we don't read caches written by other engines.
            cachePropertyGroup.AddNewProperty(cacheVersionNumber, Constants.AssemblyVersion);

            // And store a list of all the projects. We can use this next time for timestamp checking.
            BuildItemGroup cacheItemGroup = msbuildProject.AddNewItemGroup();
            foreach (ProjectInSolution project in projects)
            {
                // Only add projects that correspond to actual files on disk. Solution folders and web projects correspond to folders, so we don't care about them.
                if (project.ProjectType != SolutionProjectType.SolutionFolder && project.ProjectType != SolutionProjectType.WebProject)
                {
                    cacheItemGroup.AddNewItem(cacheProjectListName, EscapingUtilities.Escape(project.RelativePath));
                }
            }
        }

        /// <summary>
        /// Figure out what tools version to build the solution wrapper project with. If a /tv
        /// switch was passed in, use that; otherwise fall back to the default (4.0).
        /// </summary>
        internal static string DetermineWrapperProjectToolsVersion(string toolsVersionOverride)
        {
            string wrapperProjectToolsVersion = toolsVersionOverride;

            if (wrapperProjectToolsVersion == null)
            {
                wrapperProjectToolsVersion = Constants.defaultSolutionWrapperProjectToolsVersion;
            }
            return wrapperProjectToolsVersion;
        }

        /// <summary>
        /// We have to know the child projects' tools version in order to know what tools version to use when
        /// scanning them for dependencies. If $(ProjectToolsVersion) is defined, we use that; otherwise
        /// fall back on the tools version of the solution wrapper project.
        /// </summary>
        /// <param name="parentEngine"></param>
        /// <param name="toolsVersionOverride"></param>
        /// <returns></returns>
        private static string DetermineChildProjectToolsVersion(Engine parentEngine, string wrapperProjectToolsVersion)
        {
            BuildProperty property = parentEngine.GlobalProperties["ProjectToolsVersion"];
            string childProjectToolsVersion = null;

            if (property != null)
            {
                childProjectToolsVersion = property.Value;
            }

            if (childProjectToolsVersion == null)
            {
                childProjectToolsVersion = wrapperProjectToolsVersion;
            }

            return childProjectToolsVersion;
        }

        /// <summary>
        /// Adds an MSBuild task to the specified target
        /// </summary>
        /// <param name="target"></param>
        /// <param name="projectPath"></param>
        /// <param name="msbuildTargetName"></param>
        /// <param name="configurationName"></param>
        /// <param name="platformName"></param>
        /// <returns></returns>
        /// <owner>RGoel, LukaszG</owner>
        static private BuildTask AddMSBuildTaskElement
        (
            Target target,
            string projectPath,
            string msbuildTargetName,
            string configurationName,
            string platformName,
            bool specifyProjectToolsVersion
        )
        {
            BuildTask newTask = target.AddNewTask("MSBuild");
            newTask.SetParameterValue("Projects", projectPath, true /* treat as literal */);

            if (!string.IsNullOrEmpty(msbuildTargetName))
            {
                newTask.SetParameterValue("Targets", msbuildTargetName);
            }

            string additionalProperties = string.Format(
                CultureInfo.InvariantCulture,
                "Configuration={0}; Platform={1}; BuildingSolutionFile=true; CurrentSolutionConfigurationContents=$(CurrentSolutionConfigurationContents); SolutionDir=$(SolutionDir); SolutionExt=$(SolutionExt); SolutionFileName=$(SolutionFileName); SolutionName=$(SolutionName); SolutionFilterName=$(SolutionFilterName); SolutionPath=$(SolutionPath)",
                EscapingUtilities.Escape(configurationName),
                EscapingUtilities.Escape(platformName)
            );

            newTask.SetParameterValue("Properties", additionalProperties);
            if (specifyProjectToolsVersion)
            {
                newTask.SetParameterValue("ToolsVersion", "$(ProjectToolsVersion)");
                newTask.SetParameterValue("UnloadProjectsOnCompletion", "$(UnloadProjectsOnCompletion)");
                newTask.SetParameterValue("UseResultsCache", "$(UseResultsCache)");
            }

            return newTask;
        }

        /// <summary>
        /// Add a target for a project into the XML doc that's being generated.
        /// </summary>
        /// <param name="msbuildProject"></param>
        /// <param name="solution"></param>
        /// <param name="proj"></param>
        /// <param name="targetOutputItemName">The name of the item exposing this target's outputs.  May be null.</param>
        /// <param name="subTargetName"></param>
        /// <owner>RGoel, LukaszG</owner>
        static private void AddTargetForManagedProject
        (
            Project msbuildProject,
            SolutionParser solution,
            ProjectInSolution proj,
            string targetOutputItemName,
            string subTargetName
        )
        {
            string targetName = ProjectInSolution.DisambiguateProjectTargetName(proj.GetUniqueProjectName());
            if (!string.IsNullOrEmpty(subTargetName))
            {
                targetName = targetName + ":" + subTargetName;
            }

            Target newTarget = msbuildProject.Targets.AddNewTarget(targetName);
            newTarget.DependsOnTargets = GetProjectDependencies(proj.ParentSolution, proj, subTargetName);
            newTarget.Condition = "'$(CurrentSolutionConfigurationContents)' != ''";

            if (!String.IsNullOrEmpty(targetOutputItemName))
            {
                newTarget.TargetElement.SetAttribute("Outputs", string.Format(CultureInfo.InvariantCulture, "@({0})", targetOutputItemName));
            }

            // Only create build items if we're called with the null subtarget. We're getting called
            // a total of four times and only want to create the build items once.
            bool createBuildItems = (subTargetName == null);

            foreach (ConfigurationInSolution solutionConfiguration in solution.SolutionConfigurations)
            {
                string condition = GetConditionStringForConfiguration(solutionConfiguration);

                // Create the build item group for this configuration if we haven't already
                if (solutionConfiguration.ProjectBuildItems == null)
                {
                    solutionConfiguration.ProjectBuildItems = msbuildProject.AddNewItemGroup();
                    solutionConfiguration.ProjectBuildItems.Condition = condition;
                }

                ProjectConfigurationInSolution projectConfiguration;
                if (proj.ProjectConfigurations.TryGetValue(solutionConfiguration.FullName, out projectConfiguration))
                {
                    if (projectConfiguration.IncludeInBuild)
                    {
                        // We want to specify ToolsVersion on the MSBuild task only if the solution
                        // is building with a non-Whidbey toolset, because the Whidbey MSBuild task
                        // does not support the ToolsVersion parameter.  If the user explicitly requested
                        // the 2.0 toolset be used to build the solution while specifying some value
                        // for the ProjectToolsVersion property, then one of the InitialTargets should
                        // have produced an error before reaching this point.
                        // PERF: We could emit two <MSBuild> tasks, with a condition on them. But this doubles the size of
                        // the solution wrapper project, and the cost is too high. The consequence is that when solution wrapper
                        // projects are emitted to disk (with MSBUILDEMITSOLUION=1) they cannot be reused for tools version v2.0.
                        bool specifyProjectToolsVersion =
                            String.Equals(msbuildProject.ToolsVersion, "2.0", StringComparison.OrdinalIgnoreCase) ? false : true;

                        BuildTask msbuildTask = AddMSBuildTaskElement(newTarget, proj.RelativePath, subTargetName,
                            projectConfiguration.ConfigurationName, projectConfiguration.PlatformName, specifyProjectToolsVersion);
                        msbuildTask.Condition = condition;

                        if (!String.IsNullOrEmpty(targetOutputItemName))
                        {
                            msbuildTask.AddOutputItem("TargetOutputs", targetOutputItemName);
                        }

                        if (createBuildItems)
                        {
                            string baseItemName = "BuildLevel" + proj.DependencyLevel;
                            BuildItem projectItem = solutionConfiguration.ProjectBuildItems.AddNewItem(baseItemName, proj.RelativePath, true /* treat as literal */);

                            projectItem.SetMetadata("Configuration", EscapingUtilities.Escape(projectConfiguration.ConfigurationName));
                            projectItem.SetMetadata("Platform", EscapingUtilities.Escape(projectConfiguration.PlatformName));
                        }
                    }
                    else
                    {
                        BuildTask messageTask = AddErrorWarningMessageElement(newTarget, XMakeElements.message, true, "SolutionProjectSkippedForBuilding", proj.ProjectName, solutionConfiguration.FullName);
                        messageTask.Condition = condition;

                        if (createBuildItems)
                        {
                            string baseItemName = "SkipLevel" + proj.DependencyLevel;
                            BuildItem projectItem = solutionConfiguration.ProjectBuildItems.AddNewItem(baseItemName, proj.ProjectName, true /* treat as literal */);
                        }
                    }
                }
                else
                {
                    BuildTask warningTask = AddErrorWarningMessageElement(newTarget, XMakeElements.warning, true, "SolutionProjectConfigurationMissing", proj.ProjectName, solutionConfiguration.FullName);
                    warningTask.Condition = condition;

                    if (createBuildItems)
                    {
                        string baseItemName = "MissingConfigLevel" + proj.DependencyLevel;
                        BuildItem projectItem = solutionConfiguration.ProjectBuildItems.AddNewItem(baseItemName, proj.ProjectName, true /* treat as literal */);
                    }
                }
            }
        }

        /// <summary>
        /// Adds a new ResolveVCProjectOutput task element to the specified target
        /// </summary>
        /// <param name="target"></param>
        /// <param name="solutionPath"></param>
        /// <param name="projectPath"></param>
        /// <param name="fullConfigurationName"></param>
        /// <returns></returns>
        /// <owner>LukaszG</owner>
        static private BuildTask AddResolveVCProjectOutputTaskElement
        (
            Target target,
            string solutionPath,
            string projectPath,
            string fullConfigurationName
        )
        {
            BuildTask newTask = target.AddNewTask("ResolveVCProjectOutput");

            newTask.SetParameterValue("ProjectReferences", projectPath, true /* treat as literal */);
            newTask.SetParameterValue("Configuration", fullConfigurationName, true /* treat as literal */);
            newTask.SetParameterValue("SolutionFile", solutionPath, true /* treat as literal */);

            // If the user passed in an override stylesheet for this .VCPROJ (by specifying a global
            // property called VCBuildOverride), we need to use it to resolve the output path.  Override 
            // stylesheets can be used to change the directory that VC projects get built to.
            newTask.SetParameterValue("Override", "$(VCBuildOverride)");

            return newTask;
        }

        /// <summary>
        /// Adds MSBuild and ResolveVCProjectOutput tasks to a project target to pre-resolve its project references
        /// </summary>
        /// <param name="solution"></param>
        /// <param name="target"></param>
        /// <param name="proj"></param>
        /// <param name="solutionConfiguration"></param>
        /// <param name="outputReferenceItemName"></param>
        /// <param name="outputImportLibraryItemName"></param>
        /// <param name="addedReferenceGuids"></param>
        /// <owner>LukaszG</owner>
        static private void AddResolveProjectReferenceTasks
        (
            SolutionParser solution,
            Project msbuildProject,
            Target target,
            ProjectInSolution proj,
            ConfigurationInSolution solutionConfiguration,
            string outputReferenceItemName,
            string outputImportLibraryItemName,
            out string addedReferenceGuids
        )
        {
            StringBuilder referenceGuids = new StringBuilder();

            // Suffix for the reference item name. Since we need to attach additional (different) metadata to every
            // reference item, we need to have helper item lists each with only one item
            int outputReferenceItemNameSuffix = 0;

            // Pre-resolve the MSBuild/VC project references
            foreach (string projectReferenceGuid in proj.ProjectReferences)
            {
                ProjectInSolution referencedProject = (ProjectInSolution)solution.ProjectsByGuid[projectReferenceGuid];
                ProjectConfigurationInSolution referencedProjectConfiguration = null;

                if ((referencedProject != null) &&
                    (referencedProject.ProjectConfigurations.TryGetValue(solutionConfiguration.FullName, out referencedProjectConfiguration)) &&
                    (referencedProjectConfiguration != null))
                {
                    string outputReferenceItemNameWithSuffix = string.Format(CultureInfo.InvariantCulture, "{0}_{1}",
                        outputReferenceItemName, outputReferenceItemNameSuffix);

                    bool addCreateItem = false;

                    string message;
                    if ((referencedProject.ProjectType == SolutionProjectType.ManagedProject) ||
                        ((referencedProject.ProjectType == SolutionProjectType.Unknown) && (referencedProject.CanBeMSBuildProjectFile(out message))))
                    {
                        string condition = GetConditionStringForConfiguration(solutionConfiguration);
                        bool specifyProjectToolsVersion =
                            String.Equals(msbuildProject.ToolsVersion, "2.0", StringComparison.OrdinalIgnoreCase) ? false : true;

                        BuildTask msbuildTask = AddMSBuildTaskElement(target, referencedProject.RelativePath, "GetTargetPath",
                            referencedProjectConfiguration.ConfigurationName, referencedProjectConfiguration.PlatformName, specifyProjectToolsVersion);
                        msbuildTask.Condition = condition;
                        msbuildTask.AddOutputItem("TargetOutputs", outputReferenceItemNameWithSuffix);

                        if (referenceGuids.Length > 0)
                        {
                            referenceGuids.Append(';');
                        }

                        referenceGuids.Append(projectReferenceGuid);
                        addCreateItem = true;
                    }
                    else if (referencedProject.ProjectType == SolutionProjectType.VCProject)
                    {
                        BuildTask vcbuildTask = null;

                        try
                        {
                            vcbuildTask = AddResolveVCProjectOutputTaskElement(target, Path.Combine(solution.SolutionFileDirectory, Path.GetFileName(solution.SolutionFile)),
                                referencedProject.AbsolutePath, referencedProjectConfiguration.FullName);
                        }
                        catch (Exception e) when (!ExceptionHandling.NotExpectedException(e))
                        {
                            ProjectFileErrorUtilities.VerifyThrowInvalidProjectFile(false,
                                "SubCategoryForSolutionParsingErrors",
                                new BuildEventFileInfo(solution.SolutionFile),
                                "SolutionParseInvalidProjectFileName",
                                referencedProject.RelativePath, e.Message);
                        }

                        vcbuildTask.Condition = GetConditionStringForConfiguration(solutionConfiguration);
                        vcbuildTask.AddOutputItem("ResolvedOutputPaths", outputReferenceItemNameWithSuffix);

                        if (outputImportLibraryItemName != null)
                        {
                            vcbuildTask.AddOutputItem("ResolvedImportLibraryPaths", outputImportLibraryItemName);
                        }

                        if (referenceGuids.Length > 0)
                        {
                            referenceGuids.Append(';');
                        }

                        referenceGuids.Append(projectReferenceGuid);
                        addCreateItem = true;
                    }

                    // Add create item if either of the conditions above was true. 
                    // This merges the one-item item list into the main list, adding the appropriate guid metadata
                    if (addCreateItem)
                    {
                        BuildTask createItemTask = target.AddNewTask("CreateItem");
                        createItemTask.SetParameterValue("Include", "@(" + outputReferenceItemNameWithSuffix + ")", false /* do not treat as literal */);
                        createItemTask.SetParameterValue("AdditionalMetadata", "Guid=" + projectReferenceGuid, false /* do not treat as literal */);
                        createItemTask.AddOutputItem("Include", outputReferenceItemName);
                    }

                    outputReferenceItemNameSuffix++;
                }
            }

            addedReferenceGuids = referenceGuids.ToString();
        }

        /// <summary>
        /// Adds tasks that create a temporary VC project file with pre-resolved project references (that is,
        /// replaced with file references)
        /// </summary>
        /// <param name="solution"></param>
        /// <param name="target"></param>
        /// <param name="proj"></param>
        /// <param name="solutionConfiguration"></param>
        /// <param name="subTargetName"></param>
        /// <param name="projectConfigurationName"></param>
        /// <returns>The path to the temporary project file</returns>
        /// <owner>LukaszG</owner>
        static private string AddCreateTemporaryVCProjectTasks
        (
            SolutionParser solution,
            Project msbuildProject,
            Target target,
            ProjectInSolution proj,
            ConfigurationInSolution solutionConfiguration,
            string subTargetName,
            string projectConfigurationName
        )
        {
            StringBuilder referenceItemName = new StringBuilder(GenerateSafePropertyName(proj, "References"));
            if (!string.IsNullOrEmpty(subTargetName))
            {
                referenceItemName.Append('_');
                referenceItemName.Append(subTargetName);
            }

            StringBuilder importLibraryItemName = new StringBuilder(GenerateSafePropertyName(proj, "ImportLibraries"));
            if (!string.IsNullOrEmpty(subTargetName))
            {
                importLibraryItemName.Append('_');
                importLibraryItemName.Append(subTargetName);
            }

            string referenceGuidsToRemove;
            AddResolveProjectReferenceTasks(solution, msbuildProject, target, proj, solutionConfiguration,
                referenceItemName.ToString(), importLibraryItemName.ToString(), out referenceGuidsToRemove);

            if (string.IsNullOrEmpty(referenceGuidsToRemove))
                referenceGuidsToRemove = string.Empty;

            string fullProjectPath = null;
            string projectPath = null;

            try
            {
                fullProjectPath = proj.AbsolutePath;
                string tmpExtension = string.Format(CultureInfo.InvariantCulture, ".tmp_{0}_{1}.vcproj", solutionConfiguration.ConfigurationName, solutionConfiguration.PlatformName);
                projectPath = Path.ChangeExtension(fullProjectPath, tmpExtension);
            }
            catch (Exception e)
            {
                if (ExceptionHandling.NotExpectedException(e))
                    throw;

                ProjectFileErrorUtilities.VerifyThrowInvalidProjectFile(false,
                    "SubCategoryForSolutionParsingErrors",
                    new BuildEventFileInfo(solution.SolutionFile),
                    "SolutionParseInvalidProjectFileName",
                    proj.RelativePath, e.Message);
            }

            // Create the temporary VC project
            BuildTask createVCProjectTask = target.AddNewTask("CreateTemporaryVCProject");
            createVCProjectTask.SetParameterValue("ProjectFile", fullProjectPath, true /* treat as literal */);
            createVCProjectTask.SetParameterValue("Configuration", projectConfigurationName, true /* treat as literal */);
            createVCProjectTask.SetParameterValue("OutputProjectFile", projectPath, true /* treat as literal */);

            createVCProjectTask.SetParameterValue("ReferenceGuids", referenceGuidsToRemove, false /* Contains semicolon-separated list.  DO NOT treat as literal */);
            createVCProjectTask.SetParameterValue("ReferenceAssemblies",
                string.Format(CultureInfo.InvariantCulture, "@({0})", referenceItemName.ToString()), false /* DO NOT treat as literal */);
            createVCProjectTask.SetParameterValue("ReferenceImportLibraries",
                string.Format(CultureInfo.InvariantCulture, "@({0})", importLibraryItemName.ToString()), false /* DO NOT treat as literal */);

            createVCProjectTask.Condition = GetConditionStringForConfiguration(solutionConfiguration);

            return projectPath;
        }

        /// <summary>
        /// Add a target for a project into the XML doc that's being generated.
        /// </summary>
        /// <param name="msbuildProject"></param>
        /// <param name="solution"></param>
        /// <param name="proj"></param>
        /// <param name="subTargetName"></param>
        /// <owner>LukaszG, RGoel</owner>
        static private void AddTargetForVCProject
        (
            Project msbuildProject,
            SolutionParser solution,
            ProjectInSolution proj,
            string subTargetName
        )
        {
            string targetName = ProjectInSolution.DisambiguateProjectTargetName(proj.GetUniqueProjectName());
            if (!string.IsNullOrEmpty(subTargetName))
            {
                targetName = targetName + ":" + subTargetName;
            }

            Target newTarget = msbuildProject.Targets.AddNewTarget(targetName);
            newTarget.DependsOnTargets = GetProjectDependencies(proj.ParentSolution, proj, subTargetName);
            newTarget.Condition = "'$(CurrentSolutionConfigurationContents)' != ''";

            if (subTargetName == "Publish")
            {
                // Well, hmmm.  The VCBuild doesn't support any kind of 
                // a "Publish" operation.  The best we can really do is offer up a 
                // message saying so.
                AddErrorWarningMessageElement(newTarget, XMakeElements.warning, true, "SolutionVCProjectNoPublish");

                // ... and now pretend it's a Build subtarget. This way references to VC projects from projects
                // that are about to publish will at least get built.
                subTargetName = null;
            }

            string projectPath = null;

            try
            {
                projectPath = proj.AbsolutePath;
            }
            catch (Exception e)
            {
                if (ExceptionHandling.NotExpectedException(e))
                    throw;

                ProjectFileErrorUtilities.VerifyThrowInvalidProjectFile(false,
                    "SubCategoryForSolutionParsingErrors",
                    new BuildEventFileInfo(solution.SolutionFile),
                    "SolutionParseInvalidProjectFileName",
                    proj.RelativePath, e.Message);
            }

            foreach (ConfigurationInSolution solutionConfiguration in solution.SolutionConfigurations)
            {
                string solutionConfigurationCondition = GetConditionStringForConfiguration(solutionConfiguration);

                ProjectConfigurationInSolution vcProjectConfiguration = null;
                BuildTask newTask = null;

                if (proj.ProjectConfigurations.TryGetValue(solutionConfiguration.FullName, out vcProjectConfiguration))
                {
                    if (vcProjectConfiguration.IncludeInBuild)
                    {
                        // Create a temporary VC project with references to MSBuild projects replaced with file references.
                        if (proj.ProjectReferences.Count > 0)
                        {
                            projectPath = AddCreateTemporaryVCProjectTasks(solution, msbuildProject, newTarget, proj,
                                solutionConfiguration, subTargetName,
                                vcProjectConfiguration.FullName);
                        }

                        newTask = VCWrapperProject.AddVCBuildTaskElement(
                            msbuildProject,
                            newTarget,
                            EscapingUtilities.Escape(Path.Combine(solution.SolutionFileDirectory, Path.GetFileName(solution.SolutionFile))),
                            projectPath, subTargetName,
                            null, EscapingUtilities.Escape(vcProjectConfiguration.FullName));

                        // Delete the temporary VC project
                        if (proj.ProjectReferences.Count > 0)
                        {
                            BuildTask deleteTask = newTarget.AddNewTask("Delete");
                            deleteTask.SetParameterValue("Files", projectPath, true /* treat as literal */);

                            deleteTask.Condition = solutionConfigurationCondition;
                        }
                    }
                    else
                    {
                        newTask = AddErrorWarningMessageElement(newTarget, XMakeElements.message, true, "SolutionProjectSkippedForBuilding", proj.ProjectName, solutionConfiguration.FullName);
                    }
                }
                else
                {
                    newTask = AddErrorWarningMessageElement(newTarget, XMakeElements.warning, true, "SolutionProjectConfigurationMissing", proj.ProjectName, solutionConfiguration.FullName);
                }

                if (newTask != null)
                {
                    newTask.Condition = solutionConfigurationCondition;
                }
            }
        }

        /// <summary>
        /// Add a target to the project called "GetFrameworkPathAndRedistList".  This target calls the
        /// GetFrameworkPath task and then CreateItem to populate @(_CombinedTargetFrameworkDirectoriesItem) and
        /// @(InstalledAssemblyTables), so that we can pass these into the ResolveAssemblyReference task
        /// when building web projects.
        /// </summary>
        /// <param name="msbuildProject"></param>
        /// <owner>RGoel</owner>
        static private void AddTargetForGetFrameworkPathAndRedistList
            (
            Project msbuildProject
            )
        {
            // See if there's already a target called "GetFrameworkPathAndRedistList" in this project.
            // If so, no need to do anything.
            foreach (Target target in msbuildProject.Targets)
            {
                if (target.Name == "GetFrameworkPathAndRedistList")
                {
                    return;
                }
            }

            Target newTarget = msbuildProject.Targets.AddNewTarget("GetFrameworkPathAndRedistList");

            BuildTask getFrameworkPathTask = newTarget.AddNewTask("GetFrameworkPath");

            // Follow the same logic we use in Microsoft.Common.targets to choose the target framework
            // directories (which are then used to find the set of redist lists).
            getFrameworkPathTask.AddOutputItem(
                "Path",
                "_CombinedTargetFrameworkDirectoriesItem",
                "'$(MSBuildToolsVersion)' == '2.0'");

            // TFV v4.0 supported by TV 4.0
            getFrameworkPathTask.AddOutputItem(
                "FrameworkVersion40Path",
                "_CombinedTargetFrameworkDirectoriesItem",
                " '$(TargetFrameworkVersion)' == 'v4.0' and '$(MSBuildToolsVersion)' == '4.0'");

            // TFV v3.5 supported by TV 4.0, TV 3.5
            getFrameworkPathTask.AddOutputItem(
                "FrameworkVersion35Path",
                "_CombinedTargetFrameworkDirectoriesItem",
                " ('$(TargetFrameworkVersion)' == 'v3.5' or '$(TargetFrameworkVersion)' == 'v4.0') and '$(MSBuildToolsVersion)' != '2.0'");

            // TFV v3.0 supported by TV 4.0, TV 3.5 (there was no TV 3.0)
            getFrameworkPathTask.AddOutputItem(
                "FrameworkVersion30Path",
                "_CombinedTargetFrameworkDirectoriesItem",
                " ('$(TargetFrameworkVersion)' == 'v3.0' or '$(TargetFrameworkVersion)' == 'v3.5' or '$(TargetFrameworkVersion)' == 'v4.0') and '$(MSBuildToolsVersion)' != '2.0'");

            // TFV v2.0 supported by TV 4.0, TV 3.5, (there was no TV 3.0). This property was not added until toolsversion 3.5 therefore it cannot be used for toolsversion 2.0
            getFrameworkPathTask.AddOutputItem(
                "FrameworkVersion20Path",
                "_CombinedTargetFrameworkDirectoriesItem",
                "'$(MSBuildToolsVersion)' != '2.0'");

            BuildTask createItemTask = newTarget.AddNewTask("CreateItem");
            createItemTask.SetParameterValue("Include", @"@(_CombinedTargetFrameworkDirectoriesItem->'%(Identity)\RedistList\*.xml')", false /* do not treat as literal */);
            createItemTask.AddOutputItem("Include", "InstalledAssemblyTables");
        }

        /// <summary>
        /// Helper method to add a call to the AspNetCompiler task into the given target.
        /// </summary>
        /// <param name="target"></param>
        /// <param name="proj"></param>
        /// <param name="conditionDescribingValidConfigurations"></param>
        /// <owner>RGoel</owner>
        static private void AddTaskForAspNetCompiler
            (
            Target target,
            ProjectInSolution proj,
            string conditionDescribingValidConfigurations
            )
        {
            // Add a call to the AspNetCompiler task, conditioned on having a valid Configuration.
            BuildTask newTask = target.AddNewTask("AspNetCompiler");
            newTask.SetParameterValue("VirtualPath", "$(" + GenerateSafePropertyName(proj, "AspNetVirtualPath") + ")");
            newTask.SetParameterValue("PhysicalPath", "$(" + GenerateSafePropertyName(proj, "AspNetPhysicalPath") + ")");
            newTask.SetParameterValue("TargetPath", "$(" + GenerateSafePropertyName(proj, "AspNetTargetPath") + ")");
            newTask.SetParameterValue("Force", "$(" + GenerateSafePropertyName(proj, "AspNetForce") + ")");
            newTask.SetParameterValue("Updateable", "$(" + GenerateSafePropertyName(proj, "AspNetUpdateable") + ")");
            newTask.SetParameterValue("Debug", "$(" + GenerateSafePropertyName(proj, "AspNetDebug") + ")");
            newTask.SetParameterValue("KeyFile", "$(" + GenerateSafePropertyName(proj, "AspNetKeyFile") + ")");
            newTask.SetParameterValue("KeyContainer", "$(" + GenerateSafePropertyName(proj, "AspNetKeyContainer") + ")");
            newTask.SetParameterValue("DelaySign", "$(" + GenerateSafePropertyName(proj, "AspNetDelaySign") + ")");
            newTask.SetParameterValue("AllowPartiallyTrustedCallers", "$(" + GenerateSafePropertyName(proj, "AspNetAPTCA") + ")");
            newTask.SetParameterValue("FixedNames", "$(" + GenerateSafePropertyName(proj, "AspNetFixedNames") + ")");

            newTask.Condition = conditionDescribingValidConfigurations;
        }

        /// <summary>
        /// Add a call to the ResolveAssemblyReference task to crack the pre-resolved referenced
        /// assemblies for the complete list of dependencies, PDBs, satellites, etc.  The invoke
        /// the Copy task to copy all these files (or at least the ones that RAR determined should
        /// be copied local) into the web project's bin directory.
        /// </summary>
        /// <param name="target"></param>
        /// <param name="proj"></param>
        /// <param name="referenceItemName"></param>
        /// <param name="conditionDescribingValidConfigurations"></param>
        /// <owner>RGoel</owner>
        static private void AddTasksToCopyAllDependenciesIntoBinDir
            (
            Target target,
            ProjectInSolution proj,
            string referenceItemName,
            string conditionDescribingValidConfigurations
            )
        {
            string copyLocalFilesItemName = referenceItemName + "_CopyLocalFiles";
            string destinationFolder = String.Format(CultureInfo.InvariantCulture,
                @"$({0})\Bin\", GenerateSafePropertyName(proj, "AspNetPhysicalPath"));

            // This is a bit of a hack.  We're actually calling the "Copy" task on all of 
            // the *non-existent* files.  Why?  Because we want to emit a warning in the 
            // log for each non-existent file, and the Copy task does that nicely for us.
            // I would have used the <Warning> task except for the fact that we are in 
            // string-resource lockdown.
            BuildTask copyNonExistentReferencesTask = target.AddNewTask("Copy");
            copyNonExistentReferencesTask.SetParameterValue("SourceFiles", "@(" + referenceItemName + "->'%(FullPath)')", false /* Do not treat as literal */);
            copyNonExistentReferencesTask.SetParameterValue("DestinationFolder", destinationFolder);
            copyNonExistentReferencesTask.Condition = String.Format(CultureInfo.InvariantCulture, "!Exists('%({0}.Identity)')", referenceItemName);
            copyNonExistentReferencesTask.ContinueOnError = true;

            // Call ResolveAssemblyReference on each of the .DLL files that were found on 
            // disk from the .REFRESH files as well as the P2P references.  RAR will crack
            // the dependencies, find PDBs, satellite assemblies, etc., and determine which
            // files need to be copy-localed.
            BuildTask rarTask = target.AddNewTask("ResolveAssemblyReference");
            rarTask.SetParameterValue("Assemblies", "@(" + referenceItemName + "->'%(FullPath)')", false /* Do not treat as literal */);
            rarTask.SetParameterValue("TargetFrameworkDirectories", "@(_CombinedTargetFrameworkDirectoriesItem)", false /* Do not treat as literal */);
            rarTask.SetParameterValue("InstalledAssemblyTables", "@(InstalledAssemblyTables)", false /* Do not treat as literal */);
            rarTask.SetParameterValue("SearchPaths", "{RawFileName};{TargetFrameworkDirectory};{GAC}");
            rarTask.SetParameterValue("FindDependencies", "true");
            rarTask.SetParameterValue("FindSatellites", "true");
            rarTask.SetParameterValue("FindSerializationAssemblies", "true");
            rarTask.SetParameterValue("FindRelatedFiles", "true");
            rarTask.Condition = String.Format(CultureInfo.InvariantCulture, "Exists('%({0}.Identity)')", referenceItemName);
            rarTask.AddOutputItem("CopyLocalFiles", copyLocalFilesItemName);

            // Copy all the copy-local files (reported by RAR) to the web project's "bin"
            // directory.
            BuildTask copyTask = target.AddNewTask("Copy");
            copyTask.SetParameterValue("SourceFiles", "@(" + copyLocalFilesItemName + ")", false /* DO NOT treat as literal */);
            copyTask.SetParameterValue("DestinationFiles", String.Format(CultureInfo.InvariantCulture,
                @"@({0}->'{1}%(DestinationSubDirectory)%(Filename)%(Extension)')",
                copyLocalFilesItemName, destinationFolder), false /* DO NOT treat as literal */);
            copyTask.Condition = conditionDescribingValidConfigurations;
        }

        /// <summary>
        /// Add a PropertyGroup to the project for a particular Asp.Net configuration.  This PropertyGroup
        /// will have the correct values for all the Asp.Net properties for this project and this configuration.
        /// </summary>
        /// <param name="msbuildProject"></param>
        /// <param name="proj"></param>
        /// <param name="configurationName"></param>
        /// <param name="aspNetCompilerParameters"></param>
        /// <param name="solutionFile"></param>
        /// <owner>RGoel</owner>
        static private void AddPropertyGroupForAspNetConfiguration
            (
            Project msbuildProject,
            ProjectInSolution proj,
            string configurationName,
            AspNetCompilerParameters aspNetCompilerParameters,
            string solutionFile
            )
        {
            // Add a new PropertyGroup that is condition'd on the Configuration.
            BuildPropertyGroup newPropertyGroup = msbuildProject.AddNewPropertyGroup(false /* insertAtEndOfProject = false */);
            newPropertyGroup.Condition = String.Format(CultureInfo.InvariantCulture, " '$(AspNetConfiguration)' == '{0}' ",
                EscapingUtilities.Escape(configurationName));

            // Add properties into the property group for each of the AspNetCompiler properties.
            newPropertyGroup.AddNewProperty(GenerateSafePropertyName(proj, "AspNetVirtualPath"), aspNetCompilerParameters.aspNetVirtualPath, true);
            newPropertyGroup.AddNewProperty(GenerateSafePropertyName(proj, "AspNetPhysicalPath"), aspNetCompilerParameters.aspNetPhysicalPath, true);
            newPropertyGroup.AddNewProperty(GenerateSafePropertyName(proj, "AspNetTargetPath"), aspNetCompilerParameters.aspNetTargetPath, true);
            newPropertyGroup.AddNewProperty(GenerateSafePropertyName(proj, "AspNetForce"), aspNetCompilerParameters.aspNetForce, true);
            newPropertyGroup.AddNewProperty(GenerateSafePropertyName(proj, "AspNetUpdateable"), aspNetCompilerParameters.aspNetUpdateable, true);
            newPropertyGroup.AddNewProperty(GenerateSafePropertyName(proj, "AspNetDebug"), aspNetCompilerParameters.aspNetDebug, true);
            newPropertyGroup.AddNewProperty(GenerateSafePropertyName(proj, "AspNetKeyFile"), aspNetCompilerParameters.aspNetKeyFile, true);
            newPropertyGroup.AddNewProperty(GenerateSafePropertyName(proj, "AspNetKeyContainer"), aspNetCompilerParameters.aspNetKeyContainer, true);
            newPropertyGroup.AddNewProperty(GenerateSafePropertyName(proj, "AspNetDelaySign"), aspNetCompilerParameters.aspNetDelaySign, true);
            newPropertyGroup.AddNewProperty(GenerateSafePropertyName(proj, "AspNetAPTCA"), aspNetCompilerParameters.aspNetAPTCA, true);
            newPropertyGroup.AddNewProperty(GenerateSafePropertyName(proj, "AspNetFixedNames"), aspNetCompilerParameters.aspNetFixedNames, true);

            string aspNetPhysicalPath = aspNetCompilerParameters.aspNetPhysicalPath;
            if (!String.IsNullOrEmpty(aspNetPhysicalPath))
            {
                // Trim the trailing slash if one exists.
                if (
                        (aspNetPhysicalPath[aspNetPhysicalPath.Length - 1] == Path.AltDirectorySeparatorChar) ||
                        (aspNetPhysicalPath[aspNetPhysicalPath.Length - 1] == Path.DirectorySeparatorChar)
                    )
                {
                    aspNetPhysicalPath = aspNetPhysicalPath.Substring(0, aspNetPhysicalPath.Length - 1);
                }

                // This gets us the last folder in the physical path.
                string lastFolderInPhysicalPath = null;

                try
                {
                    lastFolderInPhysicalPath = Path.GetFileName(aspNetPhysicalPath);
                }
                catch (Exception e)
                {
                    if (ExceptionHandling.NotExpectedException(e))
                        throw;

                    ProjectFileErrorUtilities.VerifyThrowInvalidProjectFile(false,
                        "SubCategoryForSolutionParsingErrors",
                        new BuildEventFileInfo(solutionFile),
                        "SolutionParseInvalidProjectFileName",
                        proj.RelativePath, e.Message);
                }

                if (!String.IsNullOrEmpty(lastFolderInPhysicalPath))
                {
                    // If there is a global property called "OutDir" set, that means the caller is trying to 
                    // override the AspNetTargetPath.  What we want to do in this case is concatenate:
                    //  $(OutDir) + "\_PublishedWebsites" + (the last portion of the folder in the AspNetPhysicalPath).
                    BuildProperty targetPathOverrideProperty = newPropertyGroup.AddNewProperty(GenerateSafePropertyName(proj, "AspNetTargetPath"),
                        @"$(OutDir)" +
                        EscapingUtilities.Escape(webProjectOverrideFolder) + Path.DirectorySeparatorChar +
                        EscapingUtilities.Escape(lastFolderInPhysicalPath) + Path.DirectorySeparatorChar);
                    targetPathOverrideProperty.Condition = " '$(OutDir)' != '' ";
                }
            }
        }

        /// <summary>
        /// This code handles the *.REFRESH files that are in the "bin" subdirectory of
        /// a web project.  These .REFRESH files are just text files that contain absolute or
        /// relative paths to the referenced assemblies.  The goal of these tasks is to
        /// search all *.REFRESH files and extract fully-qualified absolute paths for
        /// each of the references.
        /// </summary>
        /// <param name="target"></param>
        /// <param name="proj"></param>
        /// <param name="referenceItemName"></param>
        /// <owner>RGoel</owner>
        static private void AddTasksToResolveAutoRefreshFileReferences
            (
            Target target,
            ProjectInSolution proj,
            string referenceItemName
            )
        {
            string webRoot = "$(" + GenerateSafePropertyName(proj, "AspNetPhysicalPath") + ")";

            // Create an item list containing each of the .REFRESH files.
            BuildTask createItemTask = target.AddNewTask("CreateItem");
            createItemTask.SetParameterValue("Include", webRoot + @"\Bin\*.refresh");
            createItemTask.AddOutputItem("Include", referenceItemName + "_RefreshFile");

            // Read the lines out of each .REFRESH file; they should be paths to .DLLs.  Put these paths
            // into an item list.
            BuildTask readLinesTask = target.AddNewTask("ReadLinesFromFile");
            readLinesTask.SetParameterValue("File",
                String.Format(CultureInfo.InvariantCulture, @"%({0}_RefreshFile.Identity)", referenceItemName));
            readLinesTask.Condition = String.Format(CultureInfo.InvariantCulture, @" '%({0}_RefreshFile.Identity)' != '' ", referenceItemName);
            readLinesTask.AddOutputItem("Lines", referenceItemName + "_ReferenceRelPath");

            // Take those paths and combine them with the root of the web project to form either
            // an absolute path or a path relative to the .SLN file.  These paths can be passed
            // directly to RAR later.
            BuildTask combinePathTask = target.AddNewTask("CombinePath");
            combinePathTask.SetParameterValue("BasePath", webRoot);
            combinePathTask.SetParameterValue("Paths",
                String.Format(CultureInfo.InvariantCulture, @"@({0}_ReferenceRelPath)", referenceItemName));
            combinePathTask.AddOutputItem("CombinedPaths", referenceItemName);
        }

        /// <summary>
        /// When adding a target to build a web project, we want to put a Condition on the Target node that
        /// effectively says "Only build this target if the web project is active (marked for building) in the
        /// current solution configuration.
        /// </summary>
        /// <param name="solution"></param>
        /// <param name="proj"></param>
        /// <returns></returns>
        /// <owner>RGoel</owner>
        static private string ComputeTargetConditionForWebProject
            (
            SolutionParser solution,
            ProjectInSolution proj
            )
        {
            StringBuilder condition = new StringBuilder(" ('$(CurrentSolutionConfigurationContents)' != '') and (false");

            // Loop through all the solution configurations.
            foreach (ConfigurationInSolution solutionConfiguration in solution.SolutionConfigurations)
            {
                // Find out if the web project has a project configuration for this solution configuration.
                // (Actually, web projects only have one project configuration, so the TryGetValue should
                // pretty much always return "true".
                ProjectConfigurationInSolution projectConfiguration;
                if (proj.ProjectConfigurations.TryGetValue(solutionConfiguration.FullName, out projectConfiguration))
                {
                    // See if the web project is marked as active for this solution configuration.  If so,
                    // we'll build the target.  Otherwise not.
                    if (projectConfiguration.IncludeInBuild)
                    {
                        condition.Append(" or (");
                        condition.Append(GetConditionStringForConfiguration(solutionConfiguration));
                        condition.Append(")");
                    }
                }
                else if (String.Equals(solutionConfiguration.ConfigurationName, "Release", StringComparison.OrdinalIgnoreCase) ||
                         String.Equals(solutionConfiguration.ConfigurationName, "Debug", StringComparison.OrdinalIgnoreCase))
                {
                    // we don't have a project configuration that matches the solution configuration but
                    // the solution configuration is called "Release" or "Debug" which are standard AspNetConfigurations
                    // so these should be available in the solution project
                    condition.Append(" or (");
                    condition.Append(GetConditionStringForConfiguration(solutionConfiguration));
                    condition.Append(")");
                }
            }

            condition.Append(") ");
            return condition.ToString();
        }

        /// <summary>
        /// Add a target for a Venus project into the XML doc that's being generated.  This
        /// target will call the AspNetCompiler task.
        /// </summary>
        /// <param name="msbuildProject"></param>
        /// <param name="solution"></param>
        /// <param name="proj"></param>
        /// <param name="subTargetName"></param>
        /// <owner>RGoel</owner>
        static private void AddTargetForWebProject
        (
            Project msbuildProject,
            SolutionParser solution,
            ProjectInSolution proj,
            string subTargetName
        )
        {
            // Add a supporting target called "GetFrameworkPathAndRedistList".
            AddTargetForGetFrameworkPathAndRedistList(msbuildProject);

            string targetName = ProjectInSolution.DisambiguateProjectTargetName(proj.GetUniqueProjectName());
            if (!string.IsNullOrEmpty(subTargetName))
            {
                targetName = targetName + ":" + subTargetName;
            }

            Target newTarget = msbuildProject.Targets.AddNewTarget(targetName);

            newTarget.DependsOnTargets = GetProjectDependencies(proj.ParentSolution, proj, subTargetName) + ";GetFrameworkPathAndRedistList";

            if (subTargetName == "Clean")
            {
                // Well, hmmm.  The AspNetCompiler task doesn't support any kind of 
                // a "Clean" operation.  The best we can really do is offer up a 
                // message saying so.
                AddErrorWarningMessageElement(newTarget, XMakeElements.message, true, "SolutionVenusProjectNoClean");
            }
            else if (subTargetName == "Publish")
            {
                // Well, hmmm.  The AspNetCompiler task doesn't support any kind of 
                // a "Publish" operation.  The best we can really do is offer up a 
                // message saying so.
                AddErrorWarningMessageElement(newTarget, XMakeElements.message, true, "SolutionVenusProjectNoPublish");
            }
            else
            {
                // Add a Condition onto the Target that will cause it only to get executed for those solution configurations
                // in which this web project is active.
                newTarget.Condition = ComputeTargetConditionForWebProject(solution, proj);

                // For normal build and "Rebuild", just call the AspNetCompiler task with the
                // correct parameters.  But before calling the AspNetCompiler task, we need to
                // do a bunch of prep work regarding references.

                // We're going to build up an MSBuild condition string that represents the valid Configurations.
                // We do this by OR'ing together individual conditions, each of which compares $(Configuration)
                // with a valid configuration name.  We init our condition string to "false", so we can easily 
                // OR together more stuff as we go, and also easily take the negation of the condition by putting
                // a ! around the whole thing.
                StringBuilder conditionDescribingValidConfigurations = new StringBuilder("(false)");

                // Loop through all the valid configurations and add a PropertyGroup for each one.
                foreach (DictionaryEntry aspNetConfiguration in proj.AspNetConfigurations)
                {
                    string configurationName = (string)aspNetConfiguration.Key;
                    AspNetCompilerParameters aspNetCompilerParameters = (AspNetCompilerParameters)aspNetConfiguration.Value;

                    // We only add the PropertyGroup once per Venus project.  Without the following "if", we would add
                    // the same identical PropertyGroup twice, once when AddTargetForWebProject is called with 
                    // subTargetName=null and once when subTargetName="Rebuild".
                    if (subTargetName == null)
                    {
                        AddPropertyGroupForAspNetConfiguration(msbuildProject, proj, configurationName,
                            aspNetCompilerParameters, solution.SolutionFile);
                    }

                    // Update our big condition string to include this configuration.
                    conditionDescribingValidConfigurations.Append(" or ");
                    conditionDescribingValidConfigurations.AppendFormat(CultureInfo.InvariantCulture, "('$(AspNetConfiguration)' == '{0}')",
                        EscapingUtilities.Escape(configurationName));
                }

                StringBuilder referenceItemName = new StringBuilder(GenerateSafePropertyName(proj, "References"));
                if (!string.IsNullOrEmpty(subTargetName))
                {
                    referenceItemName.Append('_');
                    referenceItemName.Append(subTargetName);
                }

                // Add tasks to resolve project references of this web project, if any
                if (proj.ProjectReferences.Count > 0)
                {
                    // This is a bit tricky. Even though web projects don't use solution configurations,
                    // we want to use the current solution configuration to build the proper configurations
                    // of referenced projects.
                    foreach (ConfigurationInSolution solutionConfiguration in solution.SolutionConfigurations)
                    {
                        string referenceProjectGuids;
                        AddResolveProjectReferenceTasks(solution, msbuildProject, newTarget, proj, solutionConfiguration,
                            referenceItemName.ToString(), null /* don't care about native references */, out referenceProjectGuids);
                    }
                }

                // Add tasks to capture the auto-refreshed file references (those .REFRESH files).
                AddTasksToResolveAutoRefreshFileReferences(newTarget, proj, referenceItemName.ToString());

                // Add a call to RAR (ResolveAssemblyReference) and the Copy task to put the referenced 
                // project outputs in the right place
                AddTasksToCopyAllDependenciesIntoBinDir(newTarget, proj, referenceItemName.ToString(), conditionDescribingValidConfigurations.ToString());

                // Add a call to the AspNetCompiler task, conditioned on having a valid Configuration.
                AddTaskForAspNetCompiler(newTarget, proj, conditionDescribingValidConfigurations.ToString());

                // Add a call to the <Message> task, conditioned on having an *invalid* Configuration.  The
                // message says that we're skipping the Venus project because it's either not enabled
                // for precompilation, or doesn't support the given configuration.
                BuildTask newMessageTag = AddErrorWarningMessageElement(newTarget, XMakeElements.message, false, "SolutionVenusProjectSkipped");
                newMessageTag.Condition = "!(" + conditionDescribingValidConfigurations.ToString() + ")";
            }
        }

        /// <summary>
        /// Takes a project in the solution and a base property name, and creates a new property name
        /// that can safely be used as an XML element name, and is also unique to that project (by
        /// embedding the project's GUID into the property name.
        /// </summary>
        /// <param name="proj"></param>
        /// <param name="propertyName"></param>
        /// <returns>A safe property name that can be used as an XML element name.</returns>
        /// <owner>RGoel</owner>
        static private string GenerateSafePropertyName
            (
            ProjectInSolution proj,
            string propertyName
            )
        {
            // XML element names cannot contain curly braces, so get rid of them from the project guid.
            string projectGuid = proj.ProjectGuid.Substring(1, proj.ProjectGuid.Length - 2);
            return "Project_" + projectGuid + "_" + propertyName;
        }

        /// <summary>
        /// Makes a legal item name from a given string by replacing invalid characters with '_'
        /// </summary>
        private static string MakeIntoSafeItemName(string name)
        {
            int indexOfBadCharacter = XmlUtilities.LocateFirstInvalidElementNameCharacter(name);

            if (indexOfBadCharacter == -1)
            {
                return name;
            }

            StringBuilder builder = new StringBuilder(name);

            do
            {
                builder[indexOfBadCharacter] = '_';
                indexOfBadCharacter = XmlUtilities.LocateFirstInvalidElementNameCharacter(builder.ToString());
            }
            while (indexOfBadCharacter != -1);

            return builder.ToString();
        }

        /// <summary>
        /// Add a new error/warning/message tag into the given target
        /// </summary>
        /// <param name="target">Destination target for the tag</param>
        /// <param name="elementType">Element type to add (Error, Warning, Message)</param>
        /// <param name="treatAsLiteral">Whether to treat the Text as a literal string or one that contains embedded properties, etc.</param>
        /// <param name="textResourceName">Resource string name to use in the tag text</param>
        /// <param name="args">Additional parameters to pass to FormatString</param>
        /// <owner>LukaszG</owner>
        static internal BuildTask AddErrorWarningMessageElement(Target target, string elementType,
            bool treatAsLiteral, string textResourceName, params object[] args)
        {
            string code;
            string helpKeyword;
            string text = ResourceUtilities.FormatResourceString(out code, out helpKeyword, textResourceName, args);

            BuildTask task = target.AddNewTask(elementType);
            task.SetParameterValue("Text", text, treatAsLiteral);

            if ((elementType != XMakeElements.message) && (code != null))
            {
                task.SetParameterValue("Code", code, true /* treat as literal */);
            }

            if ((elementType != XMakeElements.message) && (helpKeyword != null))
            {
                task.SetParameterValue("HelpKeyword", helpKeyword, true /* treat as literal */);
            }

            return task;
        }

        /// <summary>
        /// Emit warnings when the project type is unknown.
        /// </summary>
        /// <param name="msbuildProject">The project to add the target to</param>
        /// <param name="proj">The project to add as a target.</param>
        /// <param name="subTargetName">The target to call within the project that's being added.</param>
        /// <param name="errorMessage">Optional detailed error message to print out in case we already tried accessing the
        /// project file before and failed.</param>
        /// <owner>RGoel</owner>
        static private void AddTargetForUnknownProjectType
        (
            Project msbuildProject,
            SolutionParser solution,
            ProjectInSolution proj,
            string subTargetName,
            string errorMessage
        )
        {
            string targetName = ProjectInSolution.DisambiguateProjectTargetName(proj.GetUniqueProjectName());
            if (!string.IsNullOrEmpty(subTargetName))
            {
                targetName = targetName + ":" + subTargetName;
            }

            Target newTarget = msbuildProject.Targets.AddNewTarget(targetName);
            newTarget.DependsOnTargets = GetProjectDependencies(proj.ParentSolution, proj, subTargetName);
            newTarget.Condition = "'$(CurrentSolutionConfigurationContents)' != ''";

            foreach (ConfigurationInSolution solutionConfiguration in solution.SolutionConfigurations)
            {
                ProjectConfigurationInSolution projectConfiguration;
                BuildTask newTask;
                if (proj.ProjectConfigurations.TryGetValue(solutionConfiguration.FullName, out projectConfiguration))
                {
                    if (projectConfiguration.IncludeInBuild)
                    {
                        if (errorMessage == null)
                        {
                            // we haven't encountered any problems accessing the project file in the past, but do not support
                            // building this project type
                            newTask = AddErrorWarningMessageElement(newTarget, XMakeElements.warning, true, "SolutionParseUnknownProjectType", proj.RelativePath);
                        }
                        else
                        {
                            // this project file may be of supported type, but we have encountered problems accessing it
                            newTask = AddErrorWarningMessageElement(newTarget, XMakeElements.warning, true, "SolutionParseErrorReadingProject", proj.RelativePath, errorMessage);
                        }
                    }
                    else
                    {
                        newTask = AddErrorWarningMessageElement(newTarget, XMakeElements.message, true, "SolutionProjectSkippedForBuilding", proj.ProjectName, solutionConfiguration.FullName);
                    }
                }
                else
                {
                    newTask = AddErrorWarningMessageElement(newTarget, XMakeElements.warning, true, "SolutionProjectConfigurationMissing", proj.ProjectName, solutionConfiguration.FullName);
                }

                if (newTask != null)
                {
                    newTask.Condition = GetConditionStringForConfiguration(solutionConfiguration);
                }
            }
        }

        /// <summary>
        /// Add a new target that depends on all targets. Examples of this are Clean and Rebuild.
        /// </summary>
        /// <param name="msbuildProject">The project to add the target to</param>
        /// <param name="targetName">The target name to add.</param>
        /// <param name="targetOutputItemName">The name of the item exposing this target's outputs.  May be null.</param>
        /// <param name="subTargetName">The target to call within the project that's being added.</param>
        /// <param name="projectsByDependencyLevel"></param>
        /// <owner>RGoel</owner>
        static private Target AddAllDependencyTarget
        (
            Project msbuildProject,
            string targetName,
            string targetOutputItemName,
            string subTargetName,
            Dictionary<int, List<ProjectInSolution>> projectsByDependencyLevel
        )
        {
            Target newTarget = msbuildProject.Targets.AddNewTarget(targetName);
            newTarget.Condition = "'$(CurrentSolutionConfigurationContents)' != ''";

            if (!String.IsNullOrEmpty(targetOutputItemName))
            {
                newTarget.TargetElement.SetAttribute("Outputs", string.Format(CultureInfo.InvariantCulture, "@({0})", targetOutputItemName));
            }

            for (int dependencyLevel = 0; dependencyLevel < projectsByDependencyLevel.Count; dependencyLevel++)
            {
                string buildItemName = "BuildLevel" + dependencyLevel;
                string buildItemReference = string.Format(CultureInfo.InvariantCulture, "@({0})", buildItemName);

                BuildTask msbuildTask = newTarget.AddNewTask("MSBuild");
                msbuildTask.Condition = buildItemReference + " != ''";
                msbuildTask.SetParameterValue("Projects", buildItemReference);
                msbuildTask.SetParameterValue("Properties", "Configuration=%(Configuration); Platform=%(Platform); BuildingSolutionFile=true; CurrentSolutionConfigurationContents=$(CurrentSolutionConfigurationContents); SolutionDir=$(SolutionDir); SolutionExt=$(SolutionExt); SolutionFileName=$(SolutionFileName); SolutionName=$(SolutionName); SolutionFilterName=$(SolutionFilterName); SolutionPath=$(SolutionPath)");

                if (!string.IsNullOrEmpty(subTargetName))
                {
                    msbuildTask.SetParameterValue("Targets", subTargetName);
                }

                if (!String.IsNullOrEmpty(targetOutputItemName))
                {
                    msbuildTask.AddOutputItem("TargetOutputs", targetOutputItemName);
                }

                if (!String.Equals(msbuildProject.ToolsVersion, "2.0", StringComparison.OrdinalIgnoreCase))
                {
                    msbuildTask.SetParameterValue("ToolsVersion", "$(ProjectToolsVersion)");
                    msbuildTask.SetParameterValue("BuildInParallel", "true");
                    msbuildTask.SetParameterValue("UnloadProjectsOnCompletion", "$(UnloadProjectsOnCompletion)");
                    msbuildTask.SetParameterValue("UseResultsCache", "$(UseResultsCache)");
                }

                BuildTask messageTask = AddErrorWarningMessageElement(newTarget, XMakeElements.message, false /* don't treat as literal */, "SolutionProjectSkippedForBuilding",
                    string.Format(CultureInfo.InvariantCulture, "%(SkipLevel{0}.Identity)", dependencyLevel), "$(Configuration)|$(Platform)");
                messageTask.Condition = string.Format(CultureInfo.InvariantCulture, "@(SkipLevel{0}) != ''", dependencyLevel);

                BuildTask warningTask = AddErrorWarningMessageElement(newTarget, XMakeElements.warning, false /* don't treat as literal */, "SolutionProjectConfigurationMissing",
                    string.Format(CultureInfo.InvariantCulture, "%(MissingConfigLevel{0}.Identity)", dependencyLevel), "$(Configuration)|$(Platform)");
                warningTask.Condition = string.Format(CultureInfo.InvariantCulture, "@(MissingConfigLevel{0}) != ''", dependencyLevel);

                string allProjects = GetAllNonMSBuildProjectDependencies(projectsByDependencyLevel, dependencyLevel, subTargetName);
                if (allProjects.Length > 0)
                {
                    BuildTask newTask = newTarget.AddNewTask("CallTarget");
                    newTask.SetParameterValue("Targets", allProjects, false /* DO NOT treat as literal */);

                    newTask.SetParameterValue("RunEachTargetSeparately", "true");
                }
            }

            return newTarget;
        }

        /// <summary>
        /// This method returns a string containing a semicolon-separated list of "friendly" project names
        /// on which the specified project depends.  If the null is specified, a list of all projects
        /// is returned.
        /// </summary>
        /// <param name="solution"></param>
        /// <param name="project"></param>
        /// <param name="subTargetName"></param>
        /// <returns></returns>
        /// <owner>RGoel</owner>
        static private string GetProjectDependencies(SolutionParser solution, ProjectInSolution project, string subTargetName)
        {
            ErrorUtilities.VerifyThrow(project != null, "We should always have a project for this method");
            StringBuilder dependencies = new StringBuilder();

            // Get all the dependencies for this project
            foreach (string dependency in project.Dependencies)
            {
                if (dependencies.Length != 0)
                {
                    dependencies.Append(";");
                }

                string projectUniqueName = solution.GetProjectUniqueNameByGuid(dependency);
                ProjectFileErrorUtilities.VerifyThrowInvalidProjectFile(projectUniqueName != null,
                    "SubCategoryForSolutionParsingErrors",
                    new BuildEventFileInfo(solution.SolutionFile),
                    "SolutionParseProjectDepNotFoundError", project.ProjectGuid, dependency);

                dependencies.Append(ProjectInSolution.DisambiguateProjectTargetName(projectUniqueName));
                if (!string.IsNullOrEmpty(subTargetName))
                {
                    dependencies.Append(":");
                    dependencies.Append(subTargetName);
                }
            }

            return dependencies.ToString();
        }

        /// <summary>
        /// Get all projects for the given dependency level.
        /// </summary>
        /// <param name="projectsByDependencyLevel"></param>
        /// <param name="dependencyLevel"></param>
        /// <param name="subTargetName"></param>
        /// <returns></returns>
        static private string GetAllNonMSBuildProjectDependencies
        (
            Dictionary<int, List<ProjectInSolution>> projectsByDependencyLevel,
            int dependencyLevel,
            string subTargetName
        )
        {
            StringBuilder dependencies = new StringBuilder();

            // Return *all* projects except solution folders
            foreach (ProjectInSolution proj in projectsByDependencyLevel[dependencyLevel])
            {
                if (proj.ProjectType == SolutionProjectType.SolutionFolder)
                {
                    continue;
                }

                if (proj.ProjectType == SolutionProjectType.ManagedProject)
                {
                    continue;
                }

                if (dependencies.Length != 0)
                {
                    dependencies.Append(";");
                }

                dependencies.Append(ProjectInSolution.DisambiguateProjectTargetName(proj.GetUniqueProjectName()));
                if (!string.IsNullOrEmpty(subTargetName))
                {
                    dependencies.Append(":");
                    dependencies.Append(subTargetName);
                }
            }

            return dependencies.ToString();
        }

        /// <summary>
        /// A helper method for constructing conditions for a solution configuration
        /// </summary>
        /// <remarks>
        /// Sample configuration condition:
        /// '$(Configuration)' == 'Release' and '$(Platform)' == 'Any CPU'
        /// </remarks>
        /// <param name="configuration"></param>
        /// <returns></returns>
        /// <owner>LukaszG</owner>
        static private string GetConditionStringForConfiguration(ConfigurationInSolution configuration)
        {
            return string.Format(CultureInfo.InvariantCulture, " ('$(Configuration)' == '{0}') and ('$(Platform)' == '{1}') ",
                EscapingUtilities.Escape(configuration.ConfigurationName),
                EscapingUtilities.Escape(configuration.PlatformName));
        }

        /// <summary>
        /// Creates default Configuration and Platform values based on solution configurations present in the solution
        /// </summary>
        /// <param name="msbuildProject"></param>
        /// <param name="solution"></param>
        /// <owner>LukaszG</owner>
        static private void AddConfigurationPlatformDefaults
        (
            Project msbuildProject,
            SolutionParser solution
        )
        {
            BuildPropertyGroup configurationDefaultingPropertyGroup = msbuildProject.AddNewPropertyGroup(true /* insertAtEndOfProject = true */);
            configurationDefaultingPropertyGroup.Condition = " '$(Configuration)' == '' ";
            configurationDefaultingPropertyGroup.AddNewProperty("Configuration", solution.GetDefaultConfigurationName(), true /* treat as literal */);

            BuildPropertyGroup platformDefaultingPropertyGroup = msbuildProject.AddNewPropertyGroup(true /* insertAtEndOfProject = true */);
            platformDefaultingPropertyGroup.Condition = " '$(Platform)' == '' ";
            platformDefaultingPropertyGroup.AddNewProperty("Platform", solution.GetDefaultPlatformName(), true /* treat as literal */);
        }

        /// <summary>
        /// Adds a new property group with contents of the given solution configuration to the project
        /// Internal for unit-testing.
        /// </summary>
        /// <param name="msbuildProject"></param>
        /// <param name="solution"></param>
        /// <param name="solutionConfiguration"></param>
        /// <owner>LukaszG</owner>
        static internal void AddPropertyGroupForSolutionConfiguration
        (
            Project msbuildProject,
            SolutionParser solution,
            ConfigurationInSolution solutionConfiguration
        )
        {
            BuildPropertyGroup propertyGroup = msbuildProject.AddNewPropertyGroup(true /* insertAtEndOfProject = true */);
            propertyGroup.Condition = GetConditionStringForConfiguration(solutionConfiguration);

            StringBuilder solutionConfigurationContents = new StringBuilder("<SolutionConfiguration>");

            // add a project configuration entry for each project in the solution
            foreach (ProjectInSolution project in solution.ProjectsInOrder)
            {
                ProjectConfigurationInSolution projectConfiguration;
                if (project.ProjectConfigurations.TryGetValue(solutionConfiguration.FullName, out projectConfiguration))
                {
                    solutionConfigurationContents.AppendFormat(
                        CultureInfo.InvariantCulture,
                        "<ProjectConfiguration Project=\"{0}\">{1}</ProjectConfiguration>",
                        project.ProjectGuid,
                        projectConfiguration.FullName
                    );
                }
            }

            solutionConfigurationContents.Append("</SolutionConfiguration>");

            propertyGroup.AddNewProperty("CurrentSolutionConfigurationContents", solutionConfigurationContents.ToString(), true /* treat as literal */);
        }

        /// <summary>
        /// Creates the default Venus configuration property based on the selected solution configuration.
        /// Unfortunately, Venus projects only expose one project configuration in the IDE (Debug) although
        /// they allow building Debug and Release from command line. This means that if we wanted to use
        /// the project configuration from the active solution configuration for Venus projects, we'd always
        /// end up with Debug and there'd be no way to build the Release configuration. To work around this,
        /// we use a special mechanism for choosing ASP.NET project configuration: we set it to Release if
        /// we're building a Release solution configuration, and to Debug if we're building a Debug solution
        /// configuration. The property is also settable from the command line, in which case it takes
        /// precedence over this algorithm.
        /// </summary>
        /// <param name="msbuildProject"></param>
        /// <owner>LukaszG</owner>
        static private void AddVenusConfigurationDefaults
        (
            Project msbuildProject
        )
        {
            BuildPropertyGroup propertyGroup = msbuildProject.AddNewPropertyGroup(true /* insertAtEndOfProject = true */);
            propertyGroup.Condition = " ('$(AspNetConfiguration)' == '') ";
            propertyGroup.AddNewProperty("AspNetConfiguration", "$(Configuration)");
        }

        /// <summary>
        /// Adds solution related build event macros and other global properties to the wrapper project
        /// </summary>
        /// <param name="msbuildProject"></param>
        /// <param name="solution"></param>
        /// <owner>LukaszG</owner>
        static private void AddGlobalProperties(Project msbuildProject, SolutionParser solution)
        {
            BuildPropertyGroup propertyGroup = msbuildProject.AddNewPropertyGroup(true /* insertAtEndOfProject = true */);

            string directoryName = solution.SolutionFileDirectory;
            if (!directoryName.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal))
            {
                directoryName += Path.DirectorySeparatorChar;
            }

            propertyGroup.AddNewProperty("SolutionDir", directoryName, true /* treat as literal */);
            propertyGroup.AddNewProperty("SolutionExt", Path.GetExtension(solution.SolutionFile), true /* treat as literal */);
            propertyGroup.AddNewProperty("SolutionFileName", Path.GetFileName(solution.SolutionFile), true /* treat as literal */);
            propertyGroup.AddNewProperty("SolutionName", Path.GetFileNameWithoutExtension(solution.SolutionFile), true /* treat as literal */);

            propertyGroup.AddNewProperty("SolutionPath", Path.Combine(solution.SolutionFileDirectory, Path.GetFileName(solution.SolutionFile)), true /* treat as literal */);

            // Add other global properties
            BuildPropertyGroup propertyGroup2 = msbuildProject.AddNewPropertyGroup(true /* insertAtEndOfProject = true */);

            // Set the property "TargetFrameworkVersion". This is needed for the GetFrameworkPath target.
            // If TargetFrameworkVersion is already set by the user, use that value.
            // Otherwise if MSBuildToolsVersion is 2.0, use "v2.0"
            // Otherwise if MSBuildToolsVersion is 3.5, use "v3.5"
            // Otherwise use "v4.0".
            BuildProperty v20Property = propertyGroup2.AddNewProperty("TargetFrameworkVersion", "v2.0", true /* treat as literal */);
            BuildProperty v35Property = propertyGroup2.AddNewProperty("TargetFrameworkVersion", "v3.5", true /* treat as literal */);
            BuildProperty v40Property = propertyGroup2.AddNewProperty("TargetFrameworkVersion", "v4.0", true /* treat as literal */);
            v20Property.Condition = "'$(TargetFrameworkVersion)' == '' and '$(MSBuildToolsVersion)' == '2.0'";
            v35Property.Condition = "'$(TargetFrameworkVersion)' == '' and ('$(MSBuildToolsVersion)' == '3.5' or '$(MSBuildToolsVersion)' == '3.0')";
            v40Property.Condition = "'$(TargetFrameworkVersion)' == '' and '$(MSBuildToolsVersion)' == '4.0'";
        }

        /// <summary>
        /// Special hack for web projects. It can happen that there is no Release configuration for solutions
        /// containing web projects, yet we still want to be able to build the Release configuration for
        /// those projects. Since the ASP.NET project configuration defaults to the solution configuration,
        /// we allow Release even if it doesn't actually exist in the solution.
        /// </summary>
        /// <param name="solution"></param>
        /// <owner>LukaszG</owner>
        static private void AddFakeReleaseSolutionConfigurationIfNecessary(SolutionParser solution)
        {
            if (solution.ContainsWebProjects)
            {
                bool solutionHasReleaseConfiguration = false;
                foreach (ConfigurationInSolution solutionConfiguration in solution.SolutionConfigurations)
                {
                    if (string.Equals(solutionConfiguration.ConfigurationName, "Release", StringComparison.OrdinalIgnoreCase))
                    {
                        solutionHasReleaseConfiguration = true;
                        break;
                    }
                }

                if ((!solutionHasReleaseConfiguration) && (solution.SolutionConfigurations.Count > 0))
                {
                    solution.SolutionConfigurations.Add(new ConfigurationInSolution("Release", solution.GetDefaultPlatformName()));
                }
            }
        }

        /// <summary>
        /// Adds the initial target to the solution wrapper project, necessary for a few message/error tags
        /// </summary>
        /// <param name="msbuildProject"></param>
        /// <owner>LukaszG</owner>
        static private void AddInitialTargets(Project msbuildProject)
        {
            Target initialTarget = msbuildProject.Targets.AddNewTarget("ValidateSolutionConfiguration");

            BuildTask errorTask = AddErrorWarningMessageElement(initialTarget, XMakeElements.error, false /* do not treat as literal */,
                "SolutionInvalidSolutionConfiguration", "$(Configuration)|$(Platform)");
            errorTask.Condition = "('$(CurrentSolutionConfigurationContents)' == '') and ('$(SkipInvalidConfigurations)' != 'true')";

            BuildTask warningTask = AddErrorWarningMessageElement(initialTarget, XMakeElements.warning, false /* do not treat as literal */,
                "SolutionInvalidSolutionConfiguration", "$(Configuration)|$(Platform)");
            warningTask.Condition = "('$(CurrentSolutionConfigurationContents)' == '') and ('$(SkipInvalidConfigurations)' == 'true')";

            BuildTask messageTask = AddErrorWarningMessageElement(initialTarget, XMakeElements.message, false /* do not treat as literal */,
                "SolutionBuildingSolutionConfiguration", "$(Configuration)|$(Platform)");
            messageTask.Condition = "'$(CurrentSolutionConfigurationContents)' != ''";

            Target validateToolsVersionsTarget = msbuildProject.Targets.AddNewTarget("ValidateToolsVersions");
            BuildTask toolsVersionErrorTask = AddErrorWarningMessageElement(validateToolsVersionsTarget, XMakeElements.error, false /* do not treat as literal */,
                "SolutionToolsVersionDoesNotSupportProjectToolsVersion", "$(MSBuildToolsVersion)");
            toolsVersionErrorTask.Condition = "'$(MSBuildToolsVersion)' == '2.0' and ('$(ProjectToolsVersion)' != '2.0' and '$(ProjectToolsVersion)' != '')";

            msbuildProject.InitialTargets = initialTarget.Name + ";" + validateToolsVersionsTarget.Name;
        }

        /// <summary>
        /// Normally the active solution configuration/platform is determined when we build the solution
        /// wrapper project, not when we create it. However, we need to know them to scan project references
        /// for the right project configuration/platform. It's unlikely that references would be conditional,
        /// but still possible and we want to get that case right.
        /// </summary>
        /// <returns></returns>
        /// <owner>LukaszG</owner>
        static internal string PredictActiveSolutionConfigurationName(SolutionParser solution, Engine parentEngine)
        {
            string candidateFullSolutionConfigurationName = DetermineLikelyActiveSolutionConfiguration(solution, parentEngine);

            // Now check if this solution configuration actually exists
            string fullSolutionConfigurationName = null;

            foreach (ConfigurationInSolution solutionConfiguration in solution.SolutionConfigurations)
            {
                if (String.Equals(solutionConfiguration.FullName, candidateFullSolutionConfigurationName, StringComparison.OrdinalIgnoreCase))
                {
                    fullSolutionConfigurationName = solutionConfiguration.FullName;
                    break;
                }
            }

            return fullSolutionConfigurationName;
        }

        /// <summary>
        /// Figure out what solution configuration we are going to build, whether or not it actually exists in the solution
        /// file.
        /// </summary>
        private static string DetermineLikelyActiveSolutionConfiguration(SolutionParser solution, Engine parentEngine)
        {
            string activeSolutionConfiguration;
            string activeSolutionPlatform;

            BuildProperty configurationProperty = parentEngine.GlobalProperties["Configuration"];
            BuildProperty platformProperty = parentEngine.GlobalProperties["Platform"];

            if (configurationProperty != null)
            {
                activeSolutionConfiguration= configurationProperty.FinalValue;
            }
            else
            {
                activeSolutionConfiguration = solution.GetDefaultConfigurationName();
            }

            if (platformProperty != null)
            {
                activeSolutionPlatform  = platformProperty.FinalValue;
            }
            else
            {
                activeSolutionPlatform = solution.GetDefaultPlatformName();
            }

            ConfigurationInSolution configurationInSolution = new ConfigurationInSolution(activeSolutionConfiguration, activeSolutionPlatform);

            return configurationInSolution.FullName;
        }

        /// <summary>
        /// Loads each MSBuild project in this solution and looks for its project-to-project references so that
        /// we know what build order we should use when building the solution.
        /// </summary>
        /// <owner>LukaszG</owner>
        static private void ScanProjectDependencies(SolutionParser solution, Engine parentEngine, string childProjectToolsVersion, string fullSolutionConfigurationName, BuildEventContext projectBuildEventContext)
        {
            // Don't bother with all this if the solution configuration doesn't even exist.
            if (fullSolutionConfigurationName == null)
            {
                return;
            }

            foreach (ProjectInSolution project in solution.ProjectsInOrder)
            {
                // Skip the project if we don't have its configuration in this solution configuration
                if (!project.ProjectConfigurations.ContainsKey(fullSolutionConfigurationName))
                {
                    continue;
                }

                string message;
                if ((project.ProjectType == SolutionProjectType.ManagedProject) ||
                    ((project.ProjectType == SolutionProjectType.Unknown) && (project.CanBeMSBuildProjectFile(out message))))
                {
                    try
                    {
                        //Will fail to load a throw an error if the tools version is incorrect.
                        Project msbuildProject = new Project(parentEngine, childProjectToolsVersion);
                        msbuildProject.IsLoadedByHost = false;

                        // this is before building the solution wrapper project, so the current directory may be not set to
                        // the one containing the solution file, and we'd get the relative path wrong
                        msbuildProject.Load(project.AbsolutePath);

                        // Project references for MSBuild projects could be affected by the active configuration, 
                        // so set it before retrieving references.
                        msbuildProject.GlobalProperties.SetProperty("Configuration",
                            project.ProjectConfigurations[fullSolutionConfigurationName].ConfigurationName, true /* treat as literal */);
                        msbuildProject.GlobalProperties.SetProperty("Platform",
                            project.ProjectConfigurations[fullSolutionConfigurationName].PlatformName, true /* treat as literal */);

                        BuildItemGroup references = msbuildProject.GetEvaluatedItemsByName("ProjectReference");

                        foreach (BuildItem reference in references)
                        {
                            string referencedProjectGuid = reference.GetEvaluatedMetadata("Project");   // Need unescaped data here.
                            AddDependencyByGuid(solution, project, parentEngine, projectBuildEventContext, referencedProjectGuid);
                        }

                        //
                        // ProjectDependency items work exactly like ProjectReference items from the point of 
                        // view of determining that project B depends on project A.  This item must cause
                        // project A to be built prior to project B.
                        //
                        references = msbuildProject.GetEvaluatedItemsByName("ProjectDependency");

                        foreach (BuildItem reference in references)
                        {
                            string referencedProjectGuid = reference.GetEvaluatedMetadata("Project");   // Need unescaped data here.
                            AddDependencyByGuid(solution, project, parentEngine, projectBuildEventContext, referencedProjectGuid);
                        }

                        //
                        // If this is a web deployment project, we have a reference specified as a property
                        // "SourceWebProject" rather than as a ProjectReference item.  This has the format
                        // {GUID}|PATH_TO_CSPROJ
                        // where
                        // GUID is the project guid for the "source" project
                        // PATH_TO_CSPROJ is the solution-relative path to the csproj file.
                        //
                        // NOTE: This is obsolete and is intended only for backward compatability with
                        // Whidbey-generated web deployment projects.  New projects should use the
                        // ProjectDependency item above.
                        //
                        string referencedWebProjectGuid = msbuildProject.GetEvaluatedProperty("SourceWebProject");
                        if (!string.IsNullOrEmpty(referencedWebProjectGuid))
                        {
                            // Grab the guid with its curly braces...
                            referencedWebProjectGuid = referencedWebProjectGuid.Substring(0, 38);
                            AddDependencyByGuid(solution, project, parentEngine, projectBuildEventContext, referencedWebProjectGuid);
                        }
                    }
                    // We don't want any problems scanning the project file to result in aborting the build.
                    catch (Exception e)
                    {
                        if (ExceptionHandling.IsCriticalException(e)) throw;

                        parentEngine.LoggingServices.LogWarning(projectBuildEventContext, "SubCategoryForSolutionParsingErrors", new BuildEventFileInfo(project.RelativePath),
                            "SolutionScanProjectDependenciesFailed", project.RelativePath, e.Message);
                    }
                }
                else if (project.ProjectType == SolutionProjectType.VCProject)
                {
                    try
                    {
                        XmlDocument doc = new XmlDocument();
                        doc.Load(project.AbsolutePath);

                        project.IsStaticLibrary = VCProjectParser.IsStaticLibrary(doc, project.ProjectConfigurations[fullSolutionConfigurationName].FullName);

                        // this is before building the solution wrapper project, so the current directory may be not set to
                        // the one containing the solution file, and we'd get the relative path wrong
                        List<string> referencedProjectGuids = VCProjectParser.GetReferencedProjectGuids(doc);

                        foreach (string referencedProjectGuid in referencedProjectGuids)
                        {
                            if (!string.IsNullOrEmpty(referencedProjectGuid))
                            {
                                if (solution.ProjectsByGuid.ContainsKey(referencedProjectGuid))
                                {
                                    project.Dependencies.Add(referencedProjectGuid);
                                    project.ProjectReferences.Add(referencedProjectGuid);
                                }
                                else
                                {
                                    parentEngine.LoggingServices.LogWarning(projectBuildEventContext, "SubCategoryForSolutionParsingErrors", new BuildEventFileInfo(solution.SolutionFile),
                                        "SolutionParseProjectDepNotFoundError", project.ProjectGuid, referencedProjectGuid);
                                }
                            }
                        }
                    }
                    // We don't want any problems scanning the project file to result in aborting the build.
                    catch (Exception e)
                    {
                        if (ExceptionHandling.IsCriticalException(e)) throw;

                        parentEngine.LoggingServices.LogWarning(projectBuildEventContext, "SubCategoryForSolutionParsingErrors", new BuildEventFileInfo(project.RelativePath),
                            "SolutionScanProjectDependenciesFailed", project.RelativePath, e.Message);
                    }
                }
            }
        }

        /// <summary>
        /// Adds a dependency to the project based on the specified guid string.
        /// </summary>
        /// <remarks>
        /// If the string is null or empty, no dependency is added and this is not considered an error.
        /// </remarks>
        /// <param name="solution">The solution in which the project exists</param>
        /// <param name="project">The project to which the dependency will be added</param>
        /// <param name="parentEngine">The engine handling the conversion</param>
        /// <param name="projectBuildEventContext">The build event context</param>
        /// <param name="dependencyGuid">The guid, in string form, of the dependency project</param>
        static private void AddDependencyByGuid(SolutionParser solution, ProjectInSolution project, Engine parentEngine, BuildEventContext projectBuildEventContext, string dependencyGuid)
        {
            if (!String.IsNullOrEmpty(dependencyGuid))
            {
                if (solution.ProjectsByGuid.ContainsKey(dependencyGuid))
                {
                    project.Dependencies.Add(dependencyGuid);
                }
                else
                {
                    parentEngine.LoggingServices.LogWarning(projectBuildEventContext, "SubCategoryForSolutionParsingErrors", new BuildEventFileInfo(solution.SolutionFile),
                        "SolutionParseProjectDepNotFoundError", project.ProjectGuid, dependencyGuid);
                }
            }
        }

        /// <summary>
        /// For MSBuild projects, project dependencies you can set in the IDE only represent build order constraints.
        /// If both projects are VC however, the VC project system treats dependencies as regular P2P references.
        /// This behavior is a carry-over from the days of VC5/6, that's how P2P refs were done back then. Tricky.
        /// To compensate for that, we need to add a P2P reference for every dependency between two VC projects.
        /// MSBuild -> VC, VC -> MSBuild dependencies are not affected.
        /// </summary>
        /// <param name="solution"></param>
        /// <param name="parentEngine"></param>
        /// <owner>LukaszG</owner>
        static internal void ConvertVcToVcDependenciesToReferences(SolutionParser solution, Engine parentEngine, BuildEventContext projectBuildEventContext)
        {
            // Go through the list of the projects in solution looking for VC projects
            foreach (ProjectInSolution project in solution.ProjectsInOrder)
            {
                if (project.ProjectType == SolutionProjectType.VCProject)
                {
                    // Found a VC project. Does it have any dependencies on other VC projects?
                    foreach (string dependentProjectGuid in project.Dependencies)
                    {
                        if (solution.ProjectsByGuid.ContainsKey(dependentProjectGuid))
                        {
                            ProjectInSolution dependentProject = (ProjectInSolution)solution.ProjectsByGuid[dependentProjectGuid];

                            // Found a dependency on another VC project. If there's not already a P2P reference between
                            // the two, add it.
                            if ((dependentProject.ProjectType == SolutionProjectType.VCProject) &&
                                (!project.ProjectReferences.Contains(dependentProjectGuid)))
                            {
                                project.ProjectReferences.Add(dependentProjectGuid);
                            }
                        }
                        else
                        {
                            parentEngine.LoggingServices.LogWarning(projectBuildEventContext, "SubCategoryForSolutionParsingErrors", new BuildEventFileInfo(solution.SolutionFile),
                                "SolutionParseProjectDepNotFoundError", project.ProjectGuid, dependentProjectGuid);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Figure out the dependency level of the given project.
        /// </summary>
        /// <param name="project"></param>
        /// <param name="solution"></param>
        /// <param name="projectsByDependencyLevel"></param>
        static private void AssignDependencyLevel(ProjectInSolution project, SolutionParser solution, Dictionary<int, List<ProjectInSolution>> projectsByDependencyLevel)
        {
            // if we ever try to recurse into a project whose dependency level we're calculating above,
            // we have a circular dependency.
            if (project.DependencyLevel == ProjectInSolution.DependencyLevelBeingDetermined)
            {
                ProjectErrorUtilities.VerifyThrowInvalidProject(false, null, "SolutionCircularDependencyError", project.ProjectName);
            }

            if (project.DependencyLevel == ProjectInSolution.DependencyLevelUnknown)
            {
                project.DependencyLevel = ProjectInSolution.DependencyLevelBeingDetermined;

                int maxDependencyLevel = 0;

                // First, go through dependencies and ensure they have their dependency level set correctly.
                foreach (string dependencyGuid in project.Dependencies)
                {
                    ProjectInSolution referencedProject = (ProjectInSolution) solution.ProjectsByGuid[dependencyGuid];

                    AssignDependencyLevel(referencedProject, solution, projectsByDependencyLevel);

                    if (referencedProject.DependencyLevel + 1 > maxDependencyLevel)
                    {
                        maxDependencyLevel = referencedProject.DependencyLevel + 1;
                    }
                }

                // Our dependency level is the highest dependency level of all our dependencies plus 1, or 0 if we had
                // no dependencies.
                project.DependencyLevel = maxDependencyLevel;

                if (!projectsByDependencyLevel.ContainsKey(maxDependencyLevel))
                {
                    projectsByDependencyLevel.Add(maxDependencyLevel, new List<ProjectInSolution>());
                }

                projectsByDependencyLevel[maxDependencyLevel].Add(project);
            }
        }

        /// <summary>
        /// Main entry point for figuring out the dependency levels. A dependency level is a set of projects that
        /// have no intra-dependencies and depend only on projects fron dependency level N-1. Dependency level 0
        /// projects have no dependencies whatsoever.
        /// </summary>
        /// <param name="solution"></param>
        /// <param name="projectsByDependencyLevel"></param>
        static private void AssignDependencyLevels(SolutionParser solution, Dictionary<int, List<ProjectInSolution>> projectsByDependencyLevel)
        {
            foreach (ProjectInSolution project in solution.ProjectsInOrder)
            {
                AssignDependencyLevel(project, solution, projectsByDependencyLevel);
            }
        }

        /// <summary>
        /// Add virtual references for reference chains containing VC static library projects.
        /// Since static libraries have no link step, any references they have to be passed
        /// to their parent project, if any. So for example, in a chain like
        /// native dll -> native static lib1 -> native static lib2
        /// we need to add a virtual reference between the native dll and the static lib2
        /// to maintain parity with the IDE behavior.
        /// </summary>
        /// <param name="solution"></param>
        private static void AddVirtualReferencesForStaticLibraries(SolutionParser solution)
        {
            foreach (ProjectInSolution project in solution.ProjectsInOrder)
            {
                GatherChildReferencesForStaticLibraries(solution, project);
            }
        }

        /// <summary>
        /// Recursive helper for AddVirtualReferencesForStaticLibraries
        /// </summary>
        private static void GatherChildReferencesForStaticLibraries(SolutionParser solution, ProjectInSolution project)
        {
            // We don't need to worry about cycles since we've already run the dependency level assignment
            // which already checked for them.
            if (!project.ChildReferencesGathered)
            {
                List<string> referenceGuidsToAdd = new List<string>();

                foreach (string referenceGuid in project.ProjectReferences)
                {
                    ProjectInSolution referencedProject = (ProjectInSolution)solution.ProjectsByGuid[referenceGuid];

                    // Gather references for all child projects recursively...
                    GatherChildReferencesForStaticLibraries(solution, referencedProject);

                    // ... and pass on references from any static lib children we have to ourselves
                    if (referencedProject.IsStaticLibrary)
                    {
                        foreach (string childReferenceGuid in referencedProject.ProjectReferences)
                        {
                            if (!project.ProjectReferences.Contains(childReferenceGuid) &&
                                !referenceGuidsToAdd.Contains(childReferenceGuid))
                            {
                                referenceGuidsToAdd.Add(childReferenceGuid);
                            }
                        }
                    }
                }

                project.ProjectReferences.AddRange(referenceGuidsToAdd);
                project.ChildReferencesGathered = true;
            }
        }
    }
}
