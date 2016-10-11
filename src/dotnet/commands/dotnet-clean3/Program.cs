// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using Microsoft.DotNet.Cli.CommandLine;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Tools.MSBuild;

namespace Microsoft.DotNet.Tools.Clean3
{
    public class Clean3Command
    {
        public static int Run(string[] args)
        {
            DebugHelper.HandleDebugSwitch(ref args);

            CommandLineApplication app = new CommandLineApplication(throwOnUnexpectedArg: false)
            {
                Name = "dotnet clean3",
                FullName = ".NET Clean Command",
                Description = "Command to clean previously generated build outputs.",
                AllowArgumentSeparator = true
            };
            app.HelpOption("-h|--help");

            CommandArgument projectArgument = app.Argument("<PROJECT>",
                "The MSBuild project file to build. If a project file is not specified," +
                " MSBuild searches the current working directory for a file that has a file extension that ends in `proj` and uses that file.");

            CommandOption outputOption = app.Option("-o|--output <OUTPUT_DIR>", "Directory in which the build outputs have been placed", CommandOptionType.SingleValue);
            CommandOption frameworkOption = app.Option("-f|--framework <FRAMEWORK>", "Clean a specific framework", CommandOptionType.SingleValue);
            CommandOption configurationOption = app.Option("-c|--configuration <CONFIGURATION>", "Clean a specific configuration", CommandOptionType.SingleValue);

            app.OnExecute(() =>
            {
                List<string> msbuildArgs = new List<string>();

                if (!string.IsNullOrEmpty(projectArgument.Value))
                {
                    msbuildArgs.Add(projectArgument.Value);
                }

                msbuildArgs.Add("/t:Clean");

                if (outputOption.HasValue())
                {
                    msbuildArgs.Add($"/p:OutputPath={outputOption.Value()}");
                }

                if (frameworkOption.HasValue())
                {
                    msbuildArgs.Add($"/p:TargetFramework={frameworkOption.Value()}");
                }

                if (configurationOption.HasValue())
                {
                    msbuildArgs.Add($"/p:Configuration={configurationOption.Value()}");
                }

                msbuildArgs.AddRange(app.RemainingArguments);

                return new MSBuildForwardingApp(msbuildArgs).Execute();
            });

            return app.Execute(args);
        }
    }
}
