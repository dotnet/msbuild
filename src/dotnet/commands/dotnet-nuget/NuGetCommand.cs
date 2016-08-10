// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using Microsoft.DotNet.Cli.CommandLine;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.InternalAbstractions;
using NugetProgram = NuGet.CommandLine.XPlat.Program;

namespace Microsoft.DotNet.Tools.NuGet
{
    public class NuGetCommand
    {
        public static int Run(string[] args)
        {
            DebugHelper.HandleDebugSwitch(ref args);

            var app = new CommandLineApplication(false)
            {
                Name = "dotnet nuget",
                FullName = ".NET NuGet command runner",
                Description = "For running NuGet commands (\"dotnet nuget --help\" for specifics)"
            };

            app.OnExecute(() =>
            {
                try
                {
                    return RunCommand(args, new NuGetCommandRunner());
                }
                catch (InvalidOperationException e)
                {
                    Console.WriteLine(e.Message);

                    return -1;
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.Message);

                    return -2;
                }
            });

            return app.Execute(args);
        }

        public static int RunCommand(IEnumerable<string> args, INuGetCommandRunner commandRunner)
        {
            Debug.Assert(commandRunner != null, "A command runner must be passed to RunCommand");
            if (commandRunner == null)
            {
                throw new InvalidOperationException("No command runner supplied to RunCommand");
            }

            return commandRunner.Run(args.ToArray());
        }

        private class NuGetCommandRunner : INuGetCommandRunner
        {
            public int Run(string[] commandArgs)
            {
                var nugetAsm = typeof(NugetProgram).GetTypeInfo().Assembly;
                var mainMethod = nugetAsm.EntryPoint;
                return (int)mainMethod.Invoke(null, new object[] { commandArgs });
            }
        }
    }
}
