// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Parsing;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Build.Execution;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.Sln.Internal;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Tools.Common;
using Microsoft.VisualBasic.CompilerServices;
using Parser = Microsoft.DotNet.Cli.Parser;


namespace Microsoft.DotNet.Tools.Publish
{
    public class PublishCommand : RestoringCommand
    {
        private PublishCommand(
            IEnumerable<string> msbuildArgs,
            bool noRestore,
            string msbuildPath = null)
            : base(msbuildArgs, noRestore, msbuildPath)
        {
        }

        public static PublishCommand FromArgs(string[] args, string msbuildPath = null)
        {
            var parser = Parser.Instance;
            var parseResult = parser.ParseFrom("dotnet publish", args);
            return FromParseResult(parseResult);
        }

        public static PublishCommand FromParseResult(ParseResult parseResult, string msbuildPath = null)
        {
            parseResult.HandleDebugSwitch();
            parseResult.ShowHelpOrErrorIfAppropriate();

            var msbuildArgs = new List<string>()
            {
                "-target:Publish",
                "-property:_IsPublishing=true"
            };

            IEnumerable<string> slnOrProjectArgs = parseResult.GetValueForArgument(PublishCommandParser.SlnOrProjectArgument);

            CommonOptions.ValidateSelfContainedOptions(parseResult.HasOption(PublishCommandParser.SelfContainedOption),
                parseResult.HasOption(PublishCommandParser.NoSelfContainedOption));

            msbuildArgs.AddRange(parseResult.OptionValuesToBeForwarded(PublishCommandParser.GetCommand()));
            msbuildArgs.AddRange(GetAutomaticConfigurationIfSpecified(parseResult, PublishCommandParser.customDefaultConfigurationProperty,
                slnOrProjectArgs, PublishCommandParser.ConfigurationOption) ?? Array.Empty<string>());
            msbuildArgs.AddRange(slnOrProjectArgs ?? Array.Empty<string>());

            bool noRestore = parseResult.HasOption(PublishCommandParser.NoRestoreOption)
                          || parseResult.HasOption(PublishCommandParser.NoBuildOption);

            return new PublishCommand(
                msbuildArgs,
                noRestore,
                msbuildPath);
        }

        /// <summary>
        /// Provide a CLI input to change configuration based on 
        /// a boolean that may or may not exist in the targeted project.
        /// <param name="defaultedConfigurationProperty">The boolean property to check the project for. Ex: PublishRelease</param>
        /// <param name="slnOrProjectArgs">The arguments or solution passed to a dotnet invocation.</param>
        /// <param name="configOption">The arguments passed to a dotnet invocation related to Configuration.</param>
        /// </summary>
        /// <returns>Returns a string such as -property:configuration=value for a projects desired config. May be empty string.</returns>
        public static IEnumerable<string> GetAutomaticConfigurationIfSpecified(
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

        /// <returns>A project instance that will be targeted to publish/pack, etc. null if one does not exist.</returns>
        private static ProjectInstance GetTargetedProject(IEnumerable<string> slnOrProjectArgs, string slnProjectConfigPropertytoCheck = "")
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
                    } // If nothing can be found: that's caught by MSBuild XMake::ProcessProjectSwitch -- don't change the behavior by failing here. 
                }
            }

            return string.IsNullOrEmpty(potentialProject) ? null : TryGetProjectInstance(potentialProject);
        }

        /// <returns>True if Configuration is a global property or was provided by the CLI: IE, the user customized configuration.</returns>
        private static bool ConfigurationAlreadySpecified(ParseResult parseResult, ProjectInstance project, Option<string> configurationOption)
        {
            return parseResult.HasOption(configurationOption) || ProjectHasUserCustomizedConfiguration(project);
        }

        private static bool IsValidProjectFilePath(string path)
        {
            return File.Exists(path) && LikeOperator.LikeString(path, "*.*proj", VisualBasic.CompareMethod.Text);
        }

        private static bool ProjectHasUserCustomizedConfiguration(ProjectInstance project)
        {
            return project.GlobalProperties.ContainsKey("Configuration");
        }

        /// <returns>Creates a ProjectInstance if the project is valid, elsewise, fails..</returns>
        private static ProjectInstance TryGetProjectInstance(string projectPath)
        {
            try
            {
                return new ProjectInstance(projectPath);
            }
            catch (Exception) // Catch failed file access, or invalid project files that cause errors when read into memory,
            {
                Reporter.Output.WriteLine(LocalizableStrings.ProjectDeductionFailure.Yellow() + " " + projectPath.Yellow());
            }
            return null;
        }

        public static int Run(ParseResult parseResult)
        {
            parseResult.HandleDebugSwitch();

            return FromParseResult(parseResult).Execute();
        }
    }
}
