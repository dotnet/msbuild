// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli;
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
                "--property:_IsPublishing=true" // This property will not hold true for MSBuild /t:Publish or in VS.
            };

            IEnumerable<string> slnOrProjectArgs = parseResult.GetValue(PublishCommandParser.SlnOrProjectArgument);

            CommonOptions.ValidateSelfContainedOptions(parseResult.HasOption(PublishCommandParser.SelfContainedOption),
                parseResult.HasOption(PublishCommandParser.NoSelfContainedOption));

            msbuildArgs.AddRange(parseResult.OptionValuesToBeForwarded(PublishCommandParser.GetCommand()));

            ReleasePropertyProjectLocator projectLocator = new ReleasePropertyProjectLocator(parseResult, MSBuildPropertyNames.PUBLISH_RELEASE,
                new ReleasePropertyProjectLocator.DependentCommandOptions(
                        parseResult.GetValue(PublishCommandParser.SlnOrProjectArgument),
                        parseResult.HasOption(PublishCommandParser.ConfigurationOption) ? parseResult.GetValue(PublishCommandParser.ConfigurationOption) : null,
                        parseResult.HasOption(PublishCommandParser.FrameworkOption) ? parseResult.GetValue(PublishCommandParser.FrameworkOption) : null
                    )
             );
            msbuildArgs.AddRange(projectLocator.GetCustomDefaultConfigurationValueIfSpecified());

            msbuildArgs.AddRange(slnOrProjectArgs ?? Array.Empty<string>());

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
