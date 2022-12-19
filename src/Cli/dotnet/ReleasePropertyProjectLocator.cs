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
using Microsoft.Build.Execution;
using Microsoft.DotNet.Cli.Sln.Internal;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Tools;
using Microsoft.DotNet.Tools.Common;

namespace Microsoft.DotNet.Cli
{
    /// <summary>
    /// This class is used to enable properties that edit the Configuration property inside of a .*proj file.
    /// Properties such as DebugSymbols are evaluated based on the Configuration set before a project file is evaluated, and the project file may have dependencies on the configuration.
    /// Because of this, it is 'impossible' for the project file to correctly influence the value of Configuration.
    /// This class allows evaluation of Configuration properties set in the project file before build time by giving back a global Configuration property to inject while building.
    /// </summary>
    class ReleasePropertyProjectLocator
    {
        private ParseResult _parseResult;
        private string _propertyToCheck;
        private IEnumerable<string> _slnOrProjectArgs;
        private Option<string> _configOption;
        private bool _isHandlingSolution = false;

        private static string solutionFolderGuid = "{2150E333-8FDC-42A3-9474-1A3956D46DE8}";
        private static string sharedProjectGuid = "{D954291E-2A0B-460D-934E-DC6B0785DB48}";

        // <summary>
        /// <param name="propertyToCheck">The boolean property to check the project for. Ex: PublishRelease, PackRelease.</param>
        /// <param name="slnOrProjectArgs">The arguments parsed by System Command Line related to picking a solution or project file.</param>
        /// <param name="configOption">The arguments parsed by System Command Line related to Configuration.</param>
        /// </summary>
        public ReleasePropertyProjectLocator(
            ParseResult parseResult,
            string propertyToCheck,
            IEnumerable<string> slnOrProjectArgs,
            Option<string> configOption
         )
         => (_parseResult, _propertyToCheck, _slnOrProjectArgs, _configOption) = (parseResult, propertyToCheck, slnOrProjectArgs, configOption);

        /// <summary>
        /// Returns dotnet CLI command-line parameters (or an empty list) to change configuration based on ...
        /// ... a boolean that may or may not exist in the targeted project.
        /// </summary>
        /// <returns>Returns a string such as -property:configuration=value for a projects desired config. May be empty string.</returns>
        public IEnumerable<string> GetCustomDefaultConfigurationValueIfSpecified()
        {
            Debug.Assert(_propertyToCheck == MSBuildPropertyNames.PUBLISH_RELEASE || _propertyToCheck == MSBuildPropertyNames.PACK_RELEASE, "Only PackRelease or PublishRelease are currently expected.");
            if (String.Equals(Environment.GetEnvironmentVariable(EnvironmentVariableNames.DISABLE_PUBLISH_AND_PACK_RELEASE), "true", StringComparison.OrdinalIgnoreCase))
            {
                return Enumerable.Empty<string>();
            }

            var globalProperties = GetGlobalPropertiesFromUserArgs();

            // Configuration doesn't work in a .proj file, but it does as a global property.
            // Detect either A) --configuration option usage OR /p:Configuration=Foo, if so, don't use these properties.
            if (_parseResult.HasOption(_configOption) || globalProperties.ContainsKey(MSBuildPropertyNames.CONFIGURATION))
                return Enumerable.Empty<string>();

            ProjectInstance project = GetTargetedProject(globalProperties);

            if (project != null)
            {
                string propertyToCheckValue = project.GetPropertyValue(_propertyToCheck);
                if (!string.IsNullOrEmpty(propertyToCheckValue) || GetProjectMaxModernTargetFramework(project) >= 8)
                {
                    var msbuildFlags = new List<string> {
                        $"-property:{MSBuildPropertyNames.CONFIGURATION}={
                            (
                            !string.IsNullOrEmpty(propertyToCheckValue) ? // Did the project set PublishRelease or PackRelease itself?
                                (propertyToCheckValue.Equals("true", StringComparison.OrdinalIgnoreCase) ? MSBuildPropertyNames.CONFIGURATION_RELEASE_VALUE : MSBuildPropertyNames.CONFIGURATION_DEBUG_VALUE)
                            : MSBuildPropertyNames.CONFIGURATION_RELEASE_VALUE) // The project did not set the property, but it is 8.0+ based on the condition above. For 8.0, these properties are enabled by default.
                            }",
                    };

                    var globallyResolvedPropertyToCheckValue = !string.IsNullOrEmpty(propertyToCheckValue) ? propertyToCheckValue : "true"; // true is defaulted for TFM 8+. 
                    if (_isHandlingSolution) // This will allow us to detect conflicting configuration values during evaluation.
                    {
                        msbuildFlags.Add($"-property:_SolutionLevel{_propertyToCheck}={globallyResolvedPropertyToCheckValue}");
                    }

                    return msbuildFlags;
                }
            }
            return Enumerable.Empty<string>();
        }

        /// <param name="slnProjectPropertytoCheck">A property to enforce if we are looking into SLN files. If projects disagree on the property, throws exception.</param>
        /// <returns>A project instance that will be targeted to publish/pack, etc. null if one does not exist.</returns>
        public ProjectInstance GetTargetedProject(Dictionary<string, string> globalProps)
        {
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
                    catch (GracefulException)  // Fall back to looking for a solution if multiple project files are found.
                    {
                        string potentialSln = Directory.GetFiles(arg, "*.sln", SearchOption.TopDirectoryOnly).FirstOrDefault();

                        if (!string.IsNullOrEmpty(potentialSln))
                        {
                            return GetArbitraryProjectFromSolution(potentialSln, globalProps);
                        }
                    }
                }
            }
            return null;  // If nothing can be found: that's caught by MSBuild XMake::ProcessProjectSwitch -- don't change the behavior by failing here. 
        }

        /// <returns>An arbitrary existant project in a solution file. Returns null if no projects exist.</returns>
        public ProjectInstance GetArbitraryProjectFromSolution(string slnPath, Dictionary<string, string> globalProps)
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
                    _isHandlingSolution = true;
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

        /// <param name="targetProject">The project which we want to get the potential modern (NET 5.0/Core+) TFM of.</param>
        /// <returns>
        /// Returns the target framework of the project.
        /// This will return 0 if there are no TFM of the form netX.0.
        /// If the project is multi-targeted, it will return the maximum target framework number (e.g. 8 for net8.0) of all modern TFMS in the project.
        /// </returns>
        /// <remarks>If the naming schema of TFM changes, this will not work. Yeah, it's not great.</remarks>
        private float GetProjectMaxModernTargetFramework(ProjectInstance targetProject)
        {
            string multiTargetingFrameworks = targetProject.GetPropertyValue(MSBuildPropertyNames.TARGET_FRAMEWORKS);
            string targetFramework = targetProject.GetPropertyValue(MSBuildPropertyNames.TARGET_FRAMEWORK);
            if (!string.IsNullOrEmpty(multiTargetingFrameworks))
            {
                foreach (string tfm in multiTargetingFrameworks.Split(";").OrderByDescending(s => s))
                {
                    string numericPartition = string.Concat(tfm.Where(tf => char.IsNumber(tf) || tf == '.'));
                    string nonNumericPartition = string.Concat(tfm.Where(tf => !char.IsNumber(tf)));
                    if (nonNumericPartition.ToLowerInvariant() == "net." && numericPartition.Contains('.'))
                    {
                        return float.Parse(numericPartition);
                    }
                }
            }
            else if (!string.IsNullOrEmpty(targetFramework))
            {
                if (targetFramework.StartsWith("net", StringComparison.OrdinalIgnoreCase) && targetFramework.EndsWith(".0")) // dont return non modern .net versions. e.g.: net48 < net8.0, but 48 > 8
                    return float.Parse(targetProject.GetPropertyValue(MSBuildPropertyNames.TARGET_FRAMEWORK_NUMERIC_VERSION));
            }
            return 0;  // There is no modern TFM, OR there's no TFM. The project will not build or publish without a TFM, what we do here is irrelevant.
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
