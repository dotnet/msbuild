// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Parsing;
using System.Linq;
using Microsoft.Build.Execution;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.Utils;
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
            };

            // TODO: Move this code to function ProcessPublishConfigurationProperties(ref msbuildArgs) 

            IEnumerable<string> args = parseResult.GetValueForArgument(PublishCommandParser.SlnOrProjectArgument);
            string releaseMode = null;
            bool projectIdentified = false;
            foreach (string potentialProject in args)
            {
                // Get project
                if (!string.IsNullOrEmpty(potentialProject) && potentialProject.Contains("proj")) // Could be bad if test dir has proj in it, need to eval
                {
                    var project = new ProjectInstance(potentialProject);
                    projectIdentified = true;

                    string publishRelease = project.GetPropertyValue("PublishRelease");
                    if (!string.IsNullOrEmpty(publishRelease))
                    {
                        releaseMode = publishRelease == "true" ? "Release" : "Debug";
                    }
                }
            }

            // Get project
            if(!projectIdentified)
            {
                ; // Call XMake::ProcessProjectSwitch to get the targeting project. This code needs to be imported to the SDK. 
            }

            if (!string.IsNullOrEmpty(releaseMode))
                msbuildArgs.Add($"-property:configuration={releaseMode}");

            // -----------

            CommonOptions.ValidateSelfContainedOptions(parseResult.HasOption(PublishCommandParser.SelfContainedOption),
                parseResult.HasOption(PublishCommandParser.NoSelfContainedOption));

            msbuildArgs.AddRange(parseResult.OptionValuesToBeForwarded(PublishCommandParser.GetCommand()));

            msbuildArgs.AddRange(parseResult.GetValueForArgument(PublishCommandParser.SlnOrProjectArgument) ?? Array.Empty<string>());

            bool noRestore = parseResult.HasOption(PublishCommandParser.NoRestoreOption)
                          || parseResult.HasOption(PublishCommandParser.NoBuildOption);

            return new PublishCommand(
                msbuildArgs,
                noRestore,
                msbuildPath);
        }

        public static int Run(ParseResult parseResult)
        {
            parseResult.HandleDebugSwitch();

            return FromParseResult(parseResult).Execute();
        }
    }
}
