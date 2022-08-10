// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Parsing;
using System.Linq;
using Microsoft.Build.Execution;


namespace Microsoft.DotNet.Cli
{
    class ReleasePropertyProjectLocator
    {
        /// <summary>
        /// Provide a CLI input to change configuration based on 
        /// a boolean that may or may not exist in the targeted project.
        /// <param name="defaultedConfigurationProperty">The boolean property to check the project for. Ex: PublishRelease</param>
        /// <param name="slnOrProjectArgs">The arguments or solution passed to a dotnet invocation.</param>
        /// <param name="configOption">The arguments passed to a dotnet invocation related to Configuration.</param>
        /// </summary>
        /// <returns>Returns a string such as -property:configuration=value for a projects desired config. May be empty string.</returns>
        public static IEnumerable<string> GetCustomDefaultConfigurationValueIfSpecified(
            ParseResult parseResult,
            string defaultedConfigurationProperty,
            IEnumerable<string> slnOrProjectArgs,
            Option<string> configOption
            )
        {
            ProjectInstance project = null;

            IEnumerable<string> calledArguments = parseResult.Tokens.Select(x => x.ToString());
            IEnumerable<string> slnProjectAndCommandArgs = slnOrProjectArgs.Concat(calledArguments);
            project = ProjectLocator.GetTargetedProject(slnProjectAndCommandArgs);

            if (project != null)
            {
                string releaseMode = "";
                string releasePropertyFlag = project.GetPropertyValue(defaultedConfigurationProperty);
                if (!string.IsNullOrEmpty(releasePropertyFlag))
                    releaseMode = releasePropertyFlag == "true" ? "Release" : "Debug";

                if (!ConfigurationAlreadySpecified(parseResult, project, configOption) && !string.IsNullOrEmpty(releaseMode))
                    return new List<string> { $"-property:configuration={releaseMode}" };
            }
            return Array.Empty<string>();
        }

        /// <returns>True if Configuration is a global property or was provided by the CLI: IE, the user customized configuration.</returns>
        private static bool ConfigurationAlreadySpecified(ParseResult parseResult, ProjectInstance project, Option<string> configurationOption)
        {
            return parseResult.HasOption(configurationOption) || ProjectHasUserCustomizedConfiguration(project);
        }

        private static bool ProjectHasUserCustomizedConfiguration(ProjectInstance project)
        {
            return project.GlobalProperties.ContainsKey("Configuration");
        }
    }
}
