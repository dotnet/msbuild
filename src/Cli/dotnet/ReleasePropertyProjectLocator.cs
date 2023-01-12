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
        private bool _isHandlingSolution = false;

        private static string solutionFolderGuid = "{2150E333-8FDC-42A3-9474-1A3956D46DE8}";
        private static string sharedProjectGuid = "{D954291E-2A0B-460D-934E-DC6B0785DB48}";

        // <summary>
        /// <param name="propertyToCheck">The boolean property to check the project for. Ex: PublishRelease, PackRelease.</param>
        /// </summary>
        public ReleasePropertyProjectLocator(
            ParseResult parseResult,
            string propertyToCheck
         )
         => (_parseResult, _propertyToCheck, _slnOrProjectArgs) = (parseResult, propertyToCheck, parseResult.GetValue(PublishCommandParser.SlnOrProjectArgument));

        /// <summary>
        /// Return dotnet CLI command-line parameters (or an empty list) to change configuration based on ...
        /// ... a boolean that may or may not exist in the targeted project.
        /// </summary>
        /// <returns>Returns a string such as -property:configuration=value for a projects desired config. May be empty string.</returns>
        public IEnumerable<string> GetCustomDefaultConfigurationValueIfSpecified()
        {
            // Setup
#nullable enable
            Debug.Assert(_propertyToCheck == MSBuildPropertyNames.PUBLISH_RELEASE || _propertyToCheck == MSBuildPropertyNames.PACK_RELEASE, "Only PackRelease or PublishRelease are currently expected.");
            var nothing = Enumerable.Empty<string>();
            if (String.Equals(Environment.GetEnvironmentVariable(EnvironmentVariableNames.DISABLE_PUBLISH_AND_PACK_RELEASE), "true", StringComparison.OrdinalIgnoreCase))
            {
                return nothing;
            }

            // Analyze Global Properties
            var globalProperties = GetUserSpecifiedExplicitMSBuildProperties();
            globalProperties = InjectTargetFrameworkIntoGlobalProperties(globalProperties);

            // Configuration doesn't work in a .proj file, but it does as a global property.
            // Detect either A) --configuration option usage OR /p:Configuration=Foo, if so, don't use these properties.
            if (_parseResult.HasOption(PublishCommandParser.ConfigurationOption) || globalProperties.ContainsKey(MSBuildPropertyNames.CONFIGURATION))
                return nothing;

            // Determine the project being acted upon
            ProjectInstance? project = GetTargetedProject(globalProperties);

            // Determine the correct value to return
            if (project != null)
            {
                string propertyToCheckValue = project.GetPropertyValue(_propertyToCheck);
                if (!string.IsNullOrEmpty(propertyToCheckValue))
                {
                    var newConfigurationArgs = new List<string> {
                        $"-property:{MSBuildPropertyNames.CONFIGURATION}={
                            (propertyToCheckValue.Equals("true", StringComparison.OrdinalIgnoreCase) ? MSBuildPropertyNames.CONFIGURATION_RELEASE_VALUE : MSBuildPropertyNames.CONFIGURATION_DEBUG_VALUE)
                        }"
                    };

                    if (_isHandlingSolution) // This will allow us to detect conflicting configuration values during evaluation.
                    {
                        newConfigurationArgs.Add($"-property:_SolutionLevel{_propertyToCheck}={propertyToCheckValue}");
                    }

                    return newConfigurationArgs;
                }
            }
            return nothing;
        }


        /// <summary>
        /// Mirror the MSBuild logic for discovering a project or a solution and find that item.
        /// </summary>
        /// <returns>A project instance that will be targeted to publish/pack, etc. null if one does not exist.
        /// Will return an arbitrary project in the solution if one exists in the solution and there's no project targeted.</returns>
        public ProjectInstance? GetTargetedProject(Dictionary<string, string> globalProps)
        {
            foreach (string arg in _slnOrProjectArgs.Append(Directory.GetCurrentDirectory()))
            {
                if (IsValidProjectFilePath(arg))
                {
                    return TryGetProjectInstance(arg, globalProps);
                }
                else if(IsValidSlnFilePath(arg))
                {
                    return GetArbitraryProjectFromSolution(arg, globalProps);
                }
                else if (Directory.Exists(arg)) // Get here if the user did not provide a .proj or a .sln. (See CWD appended to args above)
                {
                    try // First, look for a project in the directory.
                    {
                        return TryGetProjectInstance(MsbuildProject.GetProjectFileFromDirectory(arg).FullName, globalProps);
                    }
                    catch (GracefulException)  // Fall back to looking for a solution if multiple project files are found, or there's no project in the directory.
                    {
                        string? potentialSln = Directory.GetFiles(arg, "*.sln", SearchOption.TopDirectoryOnly).FirstOrDefault();

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
        public ProjectInstance? GetArbitraryProjectFromSolution(string slnPath, Dictionary<string, string> globalProps)
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
        private ProjectInstance? TryGetProjectInstance(string projectPath, Dictionary<string, string> globalProperties)
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
#nullable disable
        }

        /// <returns>Returns true if the path exists and is a project file type.</returns> 
        private bool IsValidProjectFilePath(string path)
        {
            return File.Exists(path) && Path.GetExtension(path).EndsWith("proj");
        }

        /// <returns>Returns true if the path exists and is a sln file type.</returns> 
        private bool IsValidSlnFilePath(string path)
        {
            return File.Exists(path) && Path.GetExtension(path).EndsWith("sln");
        }

        /// <returns>A case-insensitive dictionary of any properties passed from the user and their values.</returns>
        private Dictionary<string, string> GetUserSpecifiedExplicitMSBuildProperties()
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

        /// <summary>
        /// Because command-line-forwarded properties aren't in the global arguments from Properties, we need to add the TFM to the collection.
        /// The TFM is the only property besides Configuration that isn't an MSBuild property that could affect the pre-evaluation.
        /// This allows the pre-evaluation to correctly deduce its Publish or PackRelease value because it will know the actual TFM being used.
        /// </summary>
        /// <param name="oldGlobalProperties">The set of MSBuild properties that were specified explicitly like -p:Property=Foo or in other syntax sugars.</param>
        /// <returns>The same set of global properties for the project, but with the new potential TFM based on -f or --framework.</returns>
        private Dictionary<string, string> InjectTargetFrameworkIntoGlobalProperties(Dictionary<string, string> oldGlobalProperties)
        {
            if (_parseResult.HasOption(PublishCommandParser.FrameworkOption))
            {
                string givenFrameworkOption = _parseResult.GetValue(PublishCommandParser.FrameworkOption);

                // Note: dotnet -f FRAMEWORK_1 --property:TargetFramework=FRAMEWORK_2 will use FRAMEWORK_1.
                // So we can replace the value in the globals non-dubiously if it exists.
                oldGlobalProperties[MSBuildPropertyNames.TARGET_FRAMEWORK] = givenFrameworkOption;
            }
            return oldGlobalProperties;
        }
    }
}
