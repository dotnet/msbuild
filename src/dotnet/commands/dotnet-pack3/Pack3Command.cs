// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.CommandLine;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Tools.Pack3
{
    public class Pack3Command
    {
        public static int Run(string[] args)
        {
            DebugHelper.HandleDebugSwitch(ref args);

            CommandLineApplication cmd = new CommandLineApplication(throwOnUnexpectedArg: false)
            {
                Name = "pack3",
                FullName = "pack3",
                Description = "pack for msbuild"
            };

            cmd.HelpOption("-h|--help");

            var output = cmd.Option("-o|--output <OUTPUT_DIR>",
                "Directory in which to place outputs",
                CommandOptionType.SingleValue);
            var noBuild = cmd.Option("--no-build",
                "Do not build project before packing", 
                CommandOptionType.NoValue);
            var configuration = cmd.Option("-c|--configuration <CONFIGURATION>",
                "Configuration under which to build", 
                CommandOptionType.SingleValue);
            var versionSuffix = cmd.Option("--version-suffix <VERSION_SUFFIX>",
                "Defines what `*` should be replaced with in version field in project.json",
                CommandOptionType.SingleValue);
            var serviceable = cmd.Option("-s|--serviceable", 
                "Set the serviceable flag in the package", 
                CommandOptionType.NoValue);
            var argRoot = cmd.Argument("<PROJECT>",
                "The project to pack, defaults to the project file in the current directory. Can be a path to any project file",
                multipleValues:true);

            cmd.OnExecute(() =>
            {
                var msbuildArgs = new List<string>()
                {
                     "/t:pack"
                };

                if (noBuild.HasValue())
                {
                    msbuildArgs.Add($"/p:NoBuild=true");
                }

                if (output.HasValue())
                {
                    msbuildArgs.Add($"/p:PackageOutputPath={output.Value()}");
                }

                if (configuration.HasValue())
                {
                    msbuildArgs.Add($"/p:Configuration={configuration.Value()}");
                }

                if (versionSuffix.HasValue())
                {
                    msbuildArgs.Add($"/p:VersionSuffix={versionSuffix.Value()}");
                }

                if (serviceable.HasValue())
                {
                    msbuildArgs.Add($"/p:Serviceable=true");
                }

                msbuildArgs.AddRange(argRoot.Values);

                msbuildArgs.AddRange(cmd.RemainingArguments);
                return new MSBuildForwardingApp(msbuildArgs).Execute();
            });

            return cmd.Execute(args);
        }
    }
}