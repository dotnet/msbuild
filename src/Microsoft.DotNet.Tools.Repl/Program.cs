// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using Microsoft.Dnx.Runtime.Common.CommandLine;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Tools.Repl
{
    public sealed class Program
    {
        public static int Main(string[] args)
        {
            DebugHelper.HandleDebugSwitch(ref args);

            var app = new CommandLineApplication();
            app.Name = "dotnet repl";
            app.FullName = ".NET interactive REPL";
            app.Description = "Interactive REPL for the .NET platform";
            app.HelpOption("-h|--help");
            var language = app.Argument("<LANGUAGE>", "The interactive programming language, defaults to csharp");

            app.OnExecute(() => Run(language.Value));
            return app.Execute(args);
        }

        private static int Run(string languageOpt)
        {
            string replName;
            if ((languageOpt == null) || (languageOpt == "csharp"))
            {
                replName = "csi";
            }
            else
            {
                Reporter.Error.WriteLine($"Unrecognized language: {languageOpt}".Red());
                return -1;
            }
            var command = Command.Create($"dotnet-repl-{replName}", string.Empty)
                .ForwardStdOut()
                .ForwardStdErr();
            var result = command.Execute();
            return result.ExitCode;
        }
    }
}
