// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.CommandLine;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Tools.MSBuild;
using Parser = Microsoft.DotNet.Cli.Parser;

namespace Microsoft.DotNet.Tools.Publish
{
    public class PublishCommand : MSBuildForwardingApp
    {
        private PublishCommand(IEnumerable<string> msbuildArgs, string msbuildPath = null)
            : base(msbuildArgs, msbuildPath)
        {
        }

        public static PublishCommand FromArgs(string[] args, string msbuildPath = null)
        {
            DebugHelper.HandleDebugSwitch(ref args);

            var msbuildArgs = new List<string>();

            var parser = Parser.Instance;

            var result = parser.ParseFrom("dotnet publish", args);

            Reporter.Output.WriteLine(result.Diagram());

            result.ShowHelpIfRequested();

            msbuildArgs.Add("/t:Publish");

            var appliedPublishOption = result["dotnet"]["publish"];

            CommandOption filterProjOption = app.Option(
               $"--filter <{LocalizableStrings.FilterProjOption}>", LocalizableStrings.FilterProjOptionDescription,
                CommandOptionType.MultipleValue);
            msbuildArgs.AddRange(appliedPublishOption.OptionValuesToBeForwarded());

            msbuildArgs.AddRange(appliedPublishOption.Arguments);

            return new PublishCommand(msbuildArgs, msbuildPath);
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