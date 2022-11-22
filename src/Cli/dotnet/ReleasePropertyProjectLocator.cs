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
    /// <summary>
    /// This class is used to enable configuration changes at the project level.
    /// Configuration evaluation occurs before a project file is evaluated, and the project file may have dependencies on the configuration.
    /// Because of this, it is 'impossible' for the project file to correctly influence the value of Configuration.
    /// This class allows evaluation of Configuration properties set in the project file before build time by giving back a global Configuration property to inject while building.
    /// </summary>
    class ReleasePropertyProjectLocator
    {
        private ParseResult _parseResult;
        private string _defaultedConfigurationProperty;
        private IEnumerable<string> _slnOrProjectArgs;
        private Option<string> _configOption;
        private bool _isPublishingSolution = false;

        private static string solutionFolderGuid = "{2150E333-8FDC-42A3-9474-1A3956D46DE8}";
        private static string sharedProjectGuid = "{D954291E-2A0B-460D-934E-DC6B0785DB48}";

        /// <summary>
        /// Returns dotnet CLI command-line parameters (or an empty list) to change configuration based on 
        /// a boolean that may or may not exist in the targeted project.
        /// </summary>
        /// <returns>Returns a string such as -property:configuration=value for a projects desired config. May be empty string.</returns>
        public IEnumerable<string> GetCustomDefaultConfigurationValueIfSpecified()
        {
            Debug.Assert(_defaultedConfigurationProperty == MSBuildPropertyNames.PUBLISH_RELEASE || _defaultedConfigurationProperty == MSBuildPropertyNames.PACK_RELEASE, "Only PackRelease or PublishRelease are currently expected.");

            ProjectInstance project = null;
            var globalProperties = GetGlobalPropertiesFromUserArgs();

            // Configuration doesn't work in a .proj file, but it does as a global property.
            // Detect either A) --configuration option usage OR /p:Configuration=Foo, if so, don't use these properties.
            if (_parseResult.HasOption(_configOption) || globalProperties.ContainsKey(MSBuildPropertyNames.CONFIGURATION))
                yield break;

            project = GetTargetedProject(globalProperties);

            if (project != null)
            {
                string releasePropertyFlag = project.GetPropertyValue(_defaultedConfigurationProperty);
                if (!string.IsNullOrEmpty(releasePropertyFlag)) // The project set PublishRelease or PackRelease itself.
                {
                    string configurationToUse = releasePropertyFlag.Equals("true", StringComparison.OrdinalIgnoreCase) ? MSBuildPropertyNames.CONFIGURATION_RELEASE_VALUE : MSBuildPropertyNames.CONFIGURATION_DEBUG_VALUE;
                    yield return new List<string> {
                        $"-property:{MSBuildPropertyNames.CONFIGURATION}={configurationToUse}",
                        _isPublishingSolution ? $"-property:_SolutionExtracted{_defaultedConfigurationProperty}=true" : "" // Allows us to spot conflicting configuration values during evaluation.
                    };
                }
                else if (GetMaxProjectTargetFramework(project) >= 8) // For 8.0, these properties are enabled by default.
                {
                    yield return new List<string> {
                        $"-property:{MSBuildPropertyNames.CONFIGURATION}={MSBuildPropertyNames.CONFIGURATION_RELEASE_VALUE}",
                        $"-property:{_defaultedConfigurationProperty}=true",
                        _isPublishingSolution ? $"-property:_SolutionExtracted{_defaultedConfigurationProperty}=true" : ""
                    };
                }
            }
            yield break;
        }

        // <summary>
        /// <param name="defaultedConfigurationProperty">The boolean property to check the project for. Ex: PublishRelease, PackRelease.</param>
        /// <param name="slnOrProjectArgs">The arguments parsed by System Command Line related to picking a solution or project file.</param>
        /// <param name="configOption">The arguments parsed by System Command Line related to Configuration.</param>
        /// </summary>
        public ReleasePropertyProjectLocator(
            ParseResult parseResult,
            string defaultedConfigurationProperty,
            IEnumerable<string> slnOrProjectArgs,
            Option<string> configOption
         )
         => (_parseResult, _defaultedConfigurationProperty, _slnOrProjectArgs, _configOption) = (parseResult, defaultedConfigurationProperty, slnOrProjectArgs, configOption);

        /// <param name="slnProjectPropertytoCheck">A property to enforce if we are looking into SLN files. If projects disagree on the property, throws exception.</param>
        /// <returns>A project instance that will be targeted to publish/pack, etc. null if one does not exist.</returns>
        public ProjectInstance GetTargetedProject(Dictionary<string, string> globalProps)
        {
            string potentialProject = "";

            foreach (string arg in _slnOrProjectArgs.Append(Directory.GetCurrentDirectory()))
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
                            return GetRandomSlnProject(potentialSln, globalProps);
                        }
                    } // If nothing can be found: that's caught by MSBuild XMake::ProcessProjectSwitch -- don't change the behavior by failing here. 
                }
            }

            return string.IsNullOrEmpty(potentialProject) ? null : TryGetProjectInstance(potentialProject, globalProps);
        }

        /// <returns>A random existant project in a solution file. Returns null if no projects exist.</returns>
        public ProjectInstance GetRandomSlnProject(string slnPath, Dictionary<string, string> globalProps)
        {
            SlnFile sln;
            try
            {
                sln = SlnFileFactory.CreateFromFileOrDirectory(slnPath);
            }
            catch (GracefulException)
            {
                return null; // This can be called if a solution doesn't exist. MSBuild will catch that for us.
            }

            foreach (var project in sln.Projects.AsEnumerable())
            {
                if (project.TypeGuid == solutionFolderGuid || project.TypeGuid == sharedProjectGuid || !IsValidProjectFilePath(project.FilePath))
                    continue;

                var projectData = TryGetProjectInstance(project.FilePath, globalProps);
                if (projectData != null)
                {
                    _isPublishingSolution = true;
                    return projectData;
                }
            };

            return null;
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

        /// <returns>Returns true if the path exists and is a project file type.</returns> 
        private bool IsValidProjectFilePath(string path)
        {
            return File.Exists(path) && Path.GetExtension(path).EndsWith("proj");
        }

        /// <param name="targetProject">The project which we want to get the TFM of.</param>
        /// <returns>The target framework number (e.g. 8 for net8.0) of the project.
        /// This will return 4 for any TFM not of the form netX.0 
        /// If the project is multi-targeted, it will return the max TFM.</returns>
        private int GetMaxProjectTargetFramework(ProjectInstance targetProject)
        {
            string multiTargetingFrameworks = targetProject.GetPropertyValue(MSBuildPropertyNames.TARGET_FRAMEWORKS);
            string targetFramework = targetProject.GetPropertyValue(MSBuildPropertyNames.TARGET_FRAMEWORK);
            if (!string.IsNullOrEmpty(multiTargetingFrameworks))
            {
                return 1; // split by ; and call inner function
            }
            else if (!string.IsNullOrEmpty(targetFramework))
            {
                return 1;
            }
            return 4;   // The project will not build or publish without a TFM, what we do here is irrelevant.

            // inner function:
            //int parseTfm(string token)
            //{
            //    if (token.StartsWith("net"))
            //    {
            //        int firstNumber = Int.Parse(new String(input.TakeWhile(Char.IsDigit).ToArray()));
            //        return firstNumber;
            //    }
            //}
            // if matches pattern net [digit] .0*, sort by greatest and pick greatest match 
        }

        /// <returns>A case-insensitive dictionary of any properties passed from the user and their values.</returns>
        private Dictionary<string, string> GetGlobalPropertiesFromUserArgs()
        {
            Dictionary<string, string> globalProperties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            string[] globalPropEnumerable = _parseResult.GetValue(CommonOptions.PropertiesOption);

            foreach (var keyEqVal in globalPropEnumerable)
            {
                string[] keyValuePair = keyEqVal.Split("=", 2);
                globalProperties[keyValuePair[0]] = keyValuePair[1];
            }
            return globalProperties;
        }
    }
}
