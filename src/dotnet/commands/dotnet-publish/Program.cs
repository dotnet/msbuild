// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.CommandLine;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Tools;
using Microsoft.DotNet.Tools.MSBuild;
using Parser = Microsoft.DotNet.Cli.Parser;

namespace Microsoft.DotNet.Tools.Publish
{
    public class PublishCommand : RestoringCommand
    {
        private PublishCommand(
            IEnumerable<string> msbuildArgs,
            IEnumerable<string> userDefinedArguments,
            IEnumerable<string> trailingArguments,
            bool noRestore,
            string msbuildPath = null)
            : base(msbuildArgs, userDefinedArguments, trailingArguments, noRestore, msbuildPath)
        {
        }

        public static PublishCommand FromArgs(string[] args, string msbuildPath = null)
        {
            DebugHelper.HandleDebugSwitch(ref args);

            var msbuildArgs = new List<string>();

            var parser = Parser.Instance;

            var result = parser.ParseFrom("dotnet publish", args);

            result.ShowHelpOrErrorIfAppropriate();

            msbuildArgs.Add("-target:Publish");

            var appliedPublishOption = result["dotnet"]["publish"];

            msbuildArgs.AddRange(appliedPublishOption.OptionValuesToBeForwarded());

            msbuildArgs.AddRange(appliedPublishOption.Arguments);

            bool noRestore = appliedPublishOption.HasOption("--no-restore")
                          || appliedPublishOption.HasOption("--no-build");

            return new PublishCommand(
                msbuildArgs,
                appliedPublishOption.OptionValuesToBeForwarded(),
                appliedPublishOption.Arguments,
                noRestore,
                msbuildPath);
        }

        public static int Run(string[] args)
        {
            DebugHelper.HandleDebugSwitch(ref args);

            PublishCommand cmd;
            try
            {
                cmd = FromArgs(args);
            }
            catch (CommandCreationException e)
            {
                return e.ExitCode;
            }

            return cmd.Execute();
        }
    }
}
