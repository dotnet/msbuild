// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Parsing;
using System.IO;
using System.Linq;
using Microsoft.Build.Execution;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.Utils;
using Parser = Microsoft.DotNet.Cli.Parser;
using Microsoft.VisualBasic.CompilerServices;
using System.Collections;

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
            msbuildArgs.Add(GetAutomaticConfigurationIfSpecified(parseResult, PublishCommandParser.customDefaultConfigurationProperty,
                    slnOrProjectArgs, PublishCommandParser.ConfigurationOption) ?? String.Empty);
            msbuildArgs.AddRange(slnOrProjectArgs ?? Array.Empty<string>());

            bool noRestore = parseResult.HasOption(PublishCommandParser.NoRestoreOption)
                          || parseResult.HasOption(PublishCommandParser.NoBuildOption);

            return new PublishCommand(
                msbuildArgs,
                noRestore,
                msbuildPath);
        }

        public static string GetAutomaticConfigurationIfSpecified(
            ParseResult parseResult,
            string defaultedConfigurationProperty,
            IEnumerable<string> slnOrProjectArgs,
            Option<string> configOption)
        {
            ProjectInstance project = GetTargetedProject(parseResult, slnOrProjectArgs);
            string releaseMode = "";

            string releasePropertyFlag = project.GetPropertyValue(defaultedConfigurationProperty);
            if (!string.IsNullOrEmpty(releasePropertyFlag))
                releaseMode = releasePropertyFlag == "true" ? "Release" : "Debug";
        
            if (
                !ConfigurationAlreadySpecified(parseResult, ref project, configOption) &&
                !string.IsNullOrEmpty(releaseMode) &&
                !slnOrProjectArgs.Any(arg => arg.Contains(defaultedConfigurationProperty))
               )
                return $"-property:configuration={releaseMode}";
            else
                return String.Empty;
        }

        private static ProjectInstance GetTargetedProject(ParseResult parseResult, IEnumerable<string> slnOrProjectArgs)
        {
            string potentialProject = slnOrProjectArgs
                    .Where(arg => File.Exists(arg) &&
                        LikeOperator.LikeString(arg, "*.*proj", VisualBasic.CompareMethod.Text))
                    .ToList()
                    .FirstOrDefault();

            if (string.IsNullOrWhiteSpace(potentialProject))
            {
                try
                {
                    potentialProject = MsbuildProject.GetProjectFileFromDirectory(Directory.GetCurrentDirectory()).Name;
                }
                catch (GracefulException)
                {
                    ; // MSBuild XMake::ProcessProjectSwitch will handle errors if projects for publish/build weren't discoverable.
                }
            }

            return new ProjectInstance(potentialProject);
        }

        private static bool ConfigurationAlreadySpecified(ParseResult parseResult, ref ProjectInstance project, Option<string> configurationOption)
        {
            return parseResult.HasOption(configurationOption) || (project.GlobalProperties.ContainsKey("Configuration"));
        }

        public static int Run(ParseResult parseResult)
        {
            parseResult.HandleDebugSwitch();

            return FromParseResult(parseResult).Execute();
        }
    }
}
