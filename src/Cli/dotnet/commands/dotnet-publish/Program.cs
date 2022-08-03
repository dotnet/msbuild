// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Parsing;
using System.Diagnostics;
using System.IO;
using System.Linq;
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
            Debugger.Launch();
            string potentialSln = GetTargetedSolutionFileIfExists(slnOrProjectArgs);
            ProjectInstance project = null;
            if (!String.IsNullOrEmpty(potentialSln))
            {
                SlnFile sln = SlnFileFactory.CreateFromFileOrDirectory(potentialSln);
                ProjectInstance metaProject = new ProjectInstance();
                
                foreach(SlnProject x in sln.Projects)
                {
                    x.FilePath
                }
            }
            else
            {
                List<string> calledArguments = new List<string>(new List<Token>(parseResult.Tokens.ToList()).Select(x => x.ToString()));
                IEnumerable<string> slnProjectAndCommandArgs = (slnOrProjectArgs).ToList().Concat(calledArguments);
                project = GetTargetedProject(slnProjectAndCommandArgs);
            }
            
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
        private static ProjectInstance GetTargetedProject(IEnumerable<string> slnOrProjectArgs)
        {
            string potentialProject = "";

            foreach (string arg in slnOrProjectArgs.Append(Directory.GetCurrentDirectory()))
            {
                if (File.Exists(arg) && LikeOperator.LikeString(arg, "*.*proj", VisualBasic.CompareMethod.Text))
                {
                    potentialProject = arg;
                    break;
                }
                else if(Directory.Exists(arg))
                {
                    try
                    {
                        potentialProject = MsbuildProject.GetProjectFileFromDirectory(arg).FullName;
                        break;
                    }
                    catch (GracefulException) { } // Caught by MSBuild XMake::ProcessProjectSwitch -- don't change the behavior by failing here. 
                }
            }

            return string.IsNullOrEmpty(potentialProject) ? null : new ProjectInstance(potentialProject);
        }

        /// <returns>True if Configuration is a global property or was provided by the CLI: IE, the user customized configuration.</returns>
        private static bool ConfigurationAlreadySpecified(ParseResult parseResult, ProjectInstance project, Option<string> configurationOption)
        {
            return parseResult.HasOption(configurationOption) || (project.GlobalProperties.ContainsKey("Configuration"));
        }

        /// <returns>The sln file provided or empty string if not provided.</returns>
        private static string GetTargetedSolutionFileIfExists(IEnumerable<string> slnOrProjectArgs)
        {
            foreach(string arg in slnOrProjectArgs)
            {
                if (File.Exists(arg) && Path.GetExtension(arg) == ".sln")
                    return arg;
            }
            return String.Empty;
        }

        public static int Run(ParseResult parseResult)
        {
            parseResult.HandleDebugSwitch();

            return FromParseResult(parseResult).Execute();
        }
    }
}
