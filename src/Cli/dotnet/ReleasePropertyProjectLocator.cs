// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Parsing;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Build.Execution;
using Microsoft.DotNet.Cli.Sln.Internal;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Tools;
using Microsoft.DotNet.Tools.Common;

namespace Microsoft.DotNet.Cli
{
    class ReleasePropertyProjectLocator
    {
        private bool checkSolutions;

        /// <summary>
        /// Returns dotnet CLI command-line parameters (or an empty list) to change configuration based on 
        /// a boolean that may or may not exist in the targeted project.
        /// <param name="defaultedConfigurationProperty">The boolean property to check the project for. Ex: PublishRelease</param>
        /// <param name="slnOrProjectArgs">The arguments or solution passed to a dotnet invocation.</param>
        /// <param name="configOption">The arguments passed to a dotnet invocation related to Configuration.</param>
        /// </summary>
        /// <returns>Returns a string such as -property:configuration=value for a projects desired config. May be empty string.</returns>
        public IEnumerable<string> GetCustomDefaultConfigurationValueIfSpecified(
            ParseResult parseResult,
            string defaultedConfigurationProperty,
            IEnumerable<string> slnOrProjectArgs,
            Option<string> configOption,
            IEnumerable<string> userPropertyArgs
            )
        {
            ProjectInstance project = null;
            var globalProperties = GetGlobalPropertiesFromUserArgs(parseResult);

            if (parseResult.HasOption(configOption) || globalProperties.ContainsKey(MSBuildPropertyNames.CONFIGURATION))
                yield break;

            // CLI Configuration values take precedence over ones in the project. Passing PublishRelease as a global property allows it to take precedence.
            project = GetTargetedProject(slnOrProjectArgs, globalProperties, defaultedConfigurationProperty);

            if (project != null)
            {
                string configurationToUse = "";
                string releasePropertyFlag = project.GetPropertyValue(defaultedConfigurationProperty);
                if (!string.IsNullOrEmpty(releasePropertyFlag))
                    configurationToUse = releasePropertyFlag.Equals("true", StringComparison.OrdinalIgnoreCase) ? MSBuildPropertyNames.CONFIGURATION_RELEASE_VALUE : "";

                if (!string.IsNullOrEmpty(configurationToUse) && !ProjectHasUserCustomizedConfiguration(project, defaultedConfigurationProperty))
                    yield return $"-property:{MSBuildPropertyNames.CONFIGURATION}={configurationToUse}";
            }
            yield break;
        }


        public ReleasePropertyProjectLocator(bool shouldCheckSolutionsForProjects)
        {
            checkSolutions = shouldCheckSolutionsForProjects;
        }

        /// <param name="slnProjectPropertytoCheck">A property to enforce if we are looking into SLN files. If projects disagree on the property, throws exception.</param>
        /// <returns>A project instance that will be targeted to publish/pack, etc. null if one does not exist.</returns>
        public ProjectInstance GetTargetedProject(IEnumerable<string> slnOrProjectArgs, Dictionary<string, string> globalProps, string slnProjectPropertytoCheck = "")
        {
            string potentialProject = "";

            foreach (string arg in slnOrProjectArgs.Append(Directory.GetCurrentDirectory()))
            {
                if (IsValidProjectFilePath(arg))
                {
                    return TryGetProjectInstance(arg, globalProps);
                }
                else if (Directory.Exists(arg)) // We should get here if the user did not provide a .proj or a .sln
                {
                    try
                    {
                        return TryGetProjectInstance(MsbuildProject.GetProjectFileFromDirectory(arg).FullName, globalProps);
                    }
                    catch (GracefulException)
                    {
                        // Fall back to looking for a solution if multiple project files are found.
                        string potentialSln = Directory.GetFiles(arg, "*.sln", SearchOption.TopDirectoryOnly).FirstOrDefault();

                        if (!string.IsNullOrEmpty(potentialSln))
                        {
                            return GetSlnProject(potentialSln, globalProps, slnProjectPropertytoCheck);
                        }
                    } // If nothing can be found: that's caught by MSBuild XMake::ProcessProjectSwitch -- don't change the behavior by failing here. 
                }
            }

            return string.IsNullOrEmpty(potentialProject) ? null : TryGetProjectInstance(potentialProject, globalProps);
        }

        /// <returns>The executable project (first if multiple exist) in a SLN. Returns null if no executable project. Throws exception if two executable projects disagree
        /// in the configuration property to check.</returns>
        public ProjectInstance GetSlnProject(string slnPath, Dictionary<string, string> globalProps, string slnProjectConfigPropertytoCheck = "")
        {
            // This has a performance overhead so don't do this unless opted in.
            if (!checkSolutions)
                return null;
            Debug.Assert(slnProjectConfigPropertytoCheck == MSBuildPropertyNames.PUBLISH_RELEASE || slnProjectConfigPropertytoCheck == MSBuildPropertyNames.PACK_RELEASE, "Only PackRelease or PublishRelease are currently expected");

            SlnFile sln;
            try
            {
                sln = SlnFileFactory.CreateFromFileOrDirectory(slnPath);
            }
            catch (GracefulException)
            {
                return null; // This can be called if a solution doesn't exist. MSBuild will catch that for us.
            }

            List<ProjectInstance> configuredProjects = new List<ProjectInstance>();
            HashSet<string> configValues = new HashSet<string>();
            object projectDataLock = new object();

            bool shouldReturnNull = false;
            string executableProjectOutputType = MSBuildPropertyNames.OUTPUT_TYPE_EXECUTABLE; // Note that even on Unix when we don't produce exe this is still an exe, same for ASP
            const string solutionFolderGuid = "{2150E333-8FDC-42A3-9474-1A3956D46DE8}";
            const string sharedProjectGuid = "{D954291E-2A0B-460D-934E-DC6B0785DB48}";

            Parallel.ForEach(sln.Projects.AsEnumerable(), (project, state) =>
            {
                if (project.TypeGuid == solutionFolderGuid || project.TypeGuid == sharedProjectGuid || !IsValidProjectFilePath(project.FilePath))
                    return;

                var projectData = TryGetProjectInstance(project.FilePath, globalProps);
                if (projectData == null)
                    return;

                if (projectData.GetPropertyValue(MSBuildPropertyNames.OUTPUT_TYPE) == executableProjectOutputType)
                {
                    if (ProjectHasUserCustomizedConfiguration(projectData, slnProjectConfigPropertytoCheck))
                    {
                        shouldReturnNull = true;
                        state.Stop(); // We don't want to override Configuration if ANY project in a sln uses a custom configuration
                        return;
                    }

                    string useReleaseConfiguraton = projectData.GetPropertyValue(slnProjectConfigPropertytoCheck);
                    if (!string.IsNullOrEmpty(useReleaseConfiguraton))
                    {
                        lock (projectDataLock)
                        {
                            configuredProjects.Add(projectData);
                            configValues.Add(useReleaseConfiguraton.ToLower());
                        }
                    }
                }
            });

            if (configuredProjects.Any() && configValues.Count > 1 && !shouldReturnNull)
            {
                throw new GracefulException(CommonLocalizableStrings.SolutionExecutableConfigurationMismatchError, slnProjectConfigPropertytoCheck, String.Join("\n", (configuredProjects).Select(x => x.FullPath)));
            }

            return shouldReturnNull ? null : configuredProjects.FirstOrDefault();
        }

        /// <returns>Creates a ProjectInstance if the project is valid, elsewise, fails.</returns>
        private ProjectInstance TryGetProjectInstance(string projectPath, Dictionary<string, string> globalProperties)
        {
            try
            {
                return new ProjectInstance(projectPath, globalProperties, "Current");
            }
            catch (Exception e) // Catch failed file access, or invalid project files that cause errors when read into memory,
            {
                Reporter.Error.WriteLine(e.Message);
            }
            return null;
        }

        private bool IsValidProjectFilePath(string path)
        {
            return File.Exists(path) && Path.GetExtension(path).EndsWith("proj");
        }

        /// <returns>A case-insensitive dictionary of any properties passed from the user and their values.</returns>
        private Dictionary<string, string> GetGlobalPropertiesFromUserArgs(ParseResult parseResult)
        {
            Dictionary<string, string> globalProperties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            string[] globalPropEnumerable = parseResult.GetValueForOption(CommonOptions.PropertiesOption);

            foreach (var keyEqVal in globalPropEnumerable)
            {
                string[] keyValuePair = keyEqVal.Split("=");
                globalProperties[keyValuePair.First()] = keyValuePair.Last();
            }
            return globalProperties;
        }

        /// <param name="propertyToDisableIfTrue">A property that will be disabled (NOT BY THIS FUCTION) based on the condition.</param>
        /// <returns>Returns true if Configuration on a project is not Debug or Release. Note if someone explicitly set Debug, we don't detect that here.</returns>
        /// <remarks>Will log that the property will not work if this is true.</remarks>
        private bool ProjectHasUserCustomizedConfiguration(ProjectInstance project, string propertyToDisableIfTrue)
        {
            var config_value = project.GetPropertyValue(MSBuildPropertyNames.CONFIGURATION);
            // Case does matter for configuration values.
            if (!(config_value.Equals(MSBuildPropertyNames.CONFIGURATION_RELEASE_VALUE) || config_value.Equals(MSBuildPropertyNames.CONFIGURATION_DEBUG_VALUE)))
            {
                Reporter.Output.WriteLine(string.Format(CommonLocalizableStrings.CustomConfigurationDisablesPublishAndPackReleaseProperties, project.FullPath, propertyToDisableIfTrue));
                return true;
            }
            return false;
        }
    }
}
