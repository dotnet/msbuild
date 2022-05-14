// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using Microsoft.DotNet.Tools.New;

namespace Microsoft.DotNet.Cli
{
    internal static class NewCommandParser
    {
        public static readonly string DocsLink = "https://aka.ms/dotnet-new";

        public static readonly Argument Argument = new Argument<IEnumerable<string>>() { Arity = ArgumentArity.ZeroOrMore };

        public static readonly Option ListOption = new Option<bool>(new string[] { "-l", "--list" });

        public static readonly Option NameOption = new Option<string>(new string[] { "-n", "--name" });

        public static readonly Option OutputOption = new Option<string>(new string[] { "-o", "--output" });

        public static readonly Option InstallOption = new Option<bool>(new string[] { "-i", "--install" });

        public static readonly Option UninstallOption = new Option<bool>(new string[] { "-u", "--uninstall" });

        public static readonly Option InteractiveOption = new Option<bool>("--interactive");
        
        public static readonly Option NuGetSourceOption = new Option<string>("--nuget-source");
        
        public static readonly Option TypeOption = new Option<string>("--type");
        
        public static readonly Option DryRunOption = new Option<bool>("--dry-run");
        
        public static readonly Option ForceOption = new Option<bool>("--force");
        
        public static readonly Option LanguageOption = new Option<string>(new string[] { "-lang", "--language" });

        public static readonly Option UpdateCheckOption = new Option<bool>("--update-check");

        public static readonly Option UpdateApplyOption = new Option<bool>("--update-apply");

        public static readonly Option ColumnsOption = new Option<bool>("--columns");

        private static readonly Command Command = ConstructCommand();

        public static Command GetCommand()
        {
            return Command;
        }

        private static Command ConstructCommand()
        {
            var command = new DocumentedCommand("new", DocsLink);

            command.AddArgument(Argument);
            command.AddOption(ListOption);
            command.AddOption(NameOption);
            command.AddOption(OutputOption);
            command.AddOption(InstallOption);
            command.AddOption(UninstallOption);
            command.AddOption(InteractiveOption);
            command.AddOption(NuGetSourceOption);
            command.AddOption(TypeOption);
            command.AddOption(DryRunOption);
            command.AddOption(ForceOption);
            command.AddOption(LanguageOption);
            command.AddOption(UpdateCheckOption);
            command.AddOption(UpdateApplyOption);
            command.AddOption(ColumnsOption);

            command.SetHandler((ParseResult parseResult) => NewCommandShim.Run(parseResult.GetArguments()));

            return command;
        }
    }
}
