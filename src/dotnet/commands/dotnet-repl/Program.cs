// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using Microsoft.Dnx.Runtime.Common.CommandLine;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Tools.Repl
{
    public partial class ReplCommand
    {
        private const string DefaultReplLanguage = "csharp";

        private const string AppName = "dotnet repl";
        private const string AppFullName = ".NET interactive REPL";
        private const string AppDescription = "Interactive REPL for the .NET platform";

        private static readonly string AppHelpText = $@"{AppFullName}
Usage: {AppName} [language] [arguments]

Languages:
  csharp|csi        Launches the C# REPL (default)

Arguments:
  [arguments]       Arguments to pass to the target REPL

Options:
  -h|--help         Show help information
";

        public static int Run(string[] args)
        {
            DebugHelper.HandleDebugSwitch(ref args);

            var app = new CommandLineApplication(throwOnUnexpectedArg: false) {
                Name = AppName,
                FullName = AppFullName,
                Description = AppDescription
            };

            var language = app.Argument("[language]", "The interactive programming language, defaults to csharp");
            var help = app.Option("-h|--help", "Show help information", CommandOptionType.NoValue);

            app.OnExecute(() => Run(language.Value, help.HasValue(), app.RemainingArguments));
            return app.Execute(args);
        }

        private static void ShowHelp()
        {
            Console.WriteLine(AppHelpText);
        }

        private static int Run(string language, bool help, List<string> remainingArguments)
        {
            if (language == null)
            {
                if (help)
                {
                    ShowHelp();
                    return 0;
                }

                language = DefaultReplLanguage;
            }

            string replName;
            if (language.Equals("csharp") || language.Equals("csi"))
            {
                replName = "csi";
            }
            else
            {
                Reporter.Error.WriteLine($"Unrecognized language: {language}".Red());
                return -1;
            }

            if (help)
            {
                remainingArguments.Add("--help");
            }

            return Command.CreateDotNet($"repl-{replName}", remainingArguments)
                .ForwardStdOut()
                .ForwardStdErr()
                .Execute()
                .ExitCode;
        }
    }
}
