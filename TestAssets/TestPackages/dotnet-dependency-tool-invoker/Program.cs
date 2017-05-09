// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using Microsoft.DotNet.Cli.CommandLine;
using Microsoft.DotNet.Cli.Utils;
using NuGet.Frameworks;
using Microsoft.DotNet.Tools.Common;

namespace Microsoft.DotNet.Tools.DependencyInvoker
{
    public class Program
    {
        public static int Main(string[] args)
        {
            DebugHelper.HandleDebugSwitch(ref args);

            args = new [] { "dotnet-dependency-tool-invoker" }.Concat(args).ToArray();

            var parser = new Parser(
                options: DotnetDependencyToolInvokerParser.DotnetDependencyToolInvoker());

            var parseResult = parser.Parse(args);
            var appliedOptions = parseResult["dotnet-dependency-tool-invoker"];

            Console.WriteLine(parseResult.Diagram());

            if (appliedOptions.HasOption("help"))
            {
                Console.WriteLine(parseResult.Command().HelpView());
                return 0;
            }

            var command = appliedOptions.Arguments.First();
            var framework = appliedOptions.ValueOrDefault<NuGetFramework>("framework");
            var configuration = appliedOptions.ValueOrDefault<string>("configuration");
            if (string.IsNullOrEmpty(configuration))
            {
                configuration = Constants.DefaultConfiguration;
            }

            var output = appliedOptions.SingleArgumentOrDefault("output");
            var projectPath = appliedOptions.ValueOrDefault<string>("project-path");
            if (string.IsNullOrEmpty(projectPath))
            {
                projectPath = PathUtility.EnsureTrailingSlash(Directory.GetCurrentDirectory());
            }

            var appArguments = parseResult.UnmatchedTokens;

            var commandFactory =
                new ProjectDependenciesCommandFactory(
                    framework,
                    configuration,
                    output,
                    string.Empty,
                    projectPath);

            var result =
                InvokeDependencyToolForMSBuild(commandFactory, command, framework, configuration, appArguments);

            return result;
        }

        private static int InvokeDependencyToolForMSBuild(
            ProjectDependenciesCommandFactory commandFactory,
            string command,
            NuGetFramework framework,
            string configuration,
            IEnumerable<string> appArguments)
        {
            Console.WriteLine($"Invoking '{command}' for '{framework.GetShortFolderName()}'.");

            return InvokeDependencyTool(commandFactory, command, framework, configuration, appArguments);
        }

        private static int InvokeDependencyTool(
            ProjectDependenciesCommandFactory commandFactory,
            string command,
            NuGetFramework framework,
            string configuration,
            IEnumerable<string> appArguments)
        {
            try
            {
                var exitCode = commandFactory.Create(
                        $"dotnet-{command}",
                        appArguments,
                        framework,
                        configuration)
                    .ForwardStdErr()
                    .ForwardStdOut()
                    .Execute()
                    .ExitCode;

                Console.WriteLine($"Command returned {exitCode}");
            }
            catch (CommandUnknownException)
            {
                Console.WriteLine($"Command not found");
                return 1;
            }

            return 0;
        }
    }
}
