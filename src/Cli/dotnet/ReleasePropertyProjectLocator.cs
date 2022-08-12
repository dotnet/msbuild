// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Parsing;
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
    class ReleasePropertyProjectLocator : ProjectLocator
    {
        /// <param name="slnProjectPropertytoCheck">A property to enforce if we are looking into SLN files. If projects disagree on the property, throws exception.</param>
        /// <returns>A project instance that will be targeted to publish/pack, etc. null if one does not exist.</returns>
        public override ProjectInstance GetTargetedProject(IEnumerable<string> slnOrProjectArgs, string slnProjectPropertytoCheck = "")
        {
            string potentialProject = "";

            foreach (string arg in slnOrProjectArgs.Append(Directory.GetCurrentDirectory()))
            {
                if (IsValidProjectFilePath(arg))
                {
                    return TryGetProjectInstance(arg);
                }
                else if (Directory.Exists(arg)) // We should get here if the user did not provide a .proj or a .sln
                {
                    try
                    {
                        return TryGetProjectInstance(MsbuildProject.GetProjectFileFromDirectory(arg).FullName);
                    }
                    catch (GracefulException)
                    {
                        // Fall back to looking for a solution if multiple project files are found.
                        string potentialSln = Directory.GetFiles(arg, "*.sln", SearchOption.TopDirectoryOnly).FirstOrDefault();

                        if (!string.IsNullOrEmpty(potentialSln))
                            return GetSlnProject(potentialSln, slnProjectPropertytoCheck);
                    } // If nothing can be found: that's caught by MSBuild XMake::ProcessProjectSwitch -- don't change the behavior by failing here. 
                }
            }

            return string.IsNullOrEmpty(potentialProject) ? null : TryGetProjectInstance(potentialProject);
        }

        /// <returns>The top-level project (first if multiple exist) in a SLN. Returns null if no top level project. Throws exception if two top level projects disagree
        /// in the configuration property to check.</returns>
        public override ProjectInstance GetSlnProject(string slnPath, string slnProjectConfigPropertytoCheck = "")
        {
            if (Environment.GetEnvironmentVariable("ENABLE_P_RELEASE_SLN") == null) // This has a performance overhead so don't do this unless opted in.
                return null; // The user will be warned if they do not have this set and try this scenario with one of the properties set.

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

            Parallel.ForEach(sln.Projects.AsEnumerable(), (project, state) =>
            {
                const string executableProjectOutputType = "Exe"; // Note that even on Unix when we don't produce exe this is still an exe, same for ASP
                const string solutionFolderGuid = "{2150E333-8FDC-42A3-9474-1A3956D46DE8}";
                const string sharedProjectGuid = "{D954291E-2A0B-460D-934E-DC6B0785DB48}";


                if (project.TypeGuid == solutionFolderGuid || project.TypeGuid == sharedProjectGuid || !IsValidProjectFilePath(project.FilePath))
                    return;

                var projectData = TryGetProjectInstance(project.FilePath);
                if (projectData == null)
                    return;

                if (projectData.GetPropertyValue("OutputType") == executableProjectOutputType)
                {
                    if (ProjectHasUserCustomizedConfiguration(projectData))
                    {
                        shouldReturnNull = true;
                        state.Stop(); // We don't want to override Configuration if ANY project in a sln uses a custom configuration
                        return;
                    }

                    string configuration = projectData.GetPropertyValue(slnProjectConfigPropertytoCheck);
                    if (!string.IsNullOrEmpty(configuration))
                    {
                        lock (projectDataLock)
                        {
                            configuredProjects.Add(projectData); // we don't care about race conditions here
                            configValues.Add(configuration);
                        }
                    }
                }
            });

            if (configuredProjects.Any() && configValues.Count > 1 && !shouldReturnNull)
            {
                throw new GracefulException(CommonLocalizableStrings.TopLevelPublishConfigurationMismatchError);
            }

            return shouldReturnNull || configuredProjects.Count == 0 ? null : configuredProjects.First();
        }

        /// <summary>
        /// Provide a CLI input to change configuration based on 
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
            Option<string> configOption
            )
        {
            ProjectInstance project = null;

            IEnumerable<string> calledArguments = parseResult.Tokens.Select(x => x.ToString());
            IEnumerable<string> slnProjectAndCommandArgs = slnOrProjectArgs.Concat(calledArguments);
            project = GetTargetedProject(slnProjectAndCommandArgs, defaultedConfigurationProperty);

            if (project != null && !parseResult.HasOption(configOption))
            {
                string configurationToUse = "";
                string releasePropertyFlag = project.GetPropertyValue(defaultedConfigurationProperty);
                if (!string.IsNullOrEmpty(releasePropertyFlag))
                    configurationToUse = releasePropertyFlag.Equals("true", StringComparison.OrdinalIgnoreCase) ? "Release" : "";

                if (!ProjectHasUserCustomizedConfiguration(project) && !string.IsNullOrEmpty(configurationToUse))
                    return new List<string> { $"-property:configuration={configurationToUse}" };
            }
            return Enumerable.Empty<string>();
        }
    }
}
