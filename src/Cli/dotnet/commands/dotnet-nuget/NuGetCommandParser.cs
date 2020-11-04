// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using Microsoft.DotNet.Tools.NuGet;

namespace Microsoft.DotNet.Cli
{
    // This parser is used for completion and telemetry.
    // See https://github.com/NuGet/NuGet.Client for the actual implementation.
    internal static class NuGetCommandParser
    {
        private static readonly string CompletionOnlyDescription = "-";

        public static Command GetCommand()
        {
            var command = new Command("nuget", CompletionOnlyDescription);

            command.AddOption(new Option("--version", CompletionOnlyDescription));
            command.AddOption(new Option(new string[] { "-v", "--verbosity" }, CompletionOnlyDescription)
            {
                Argument = new Argument() { Arity = ArgumentArity.ExactlyOne }
            });

            command.AddCommand(GetDeleteCommand());
            command.AddCommand(GetLocalsCommand());
            command.AddCommand(GetPushCommand());

            return command;
        }

        private static Command GetDeleteCommand()
        {
            var deleteCommand = new Command("delete", CompletionOnlyDescription);
            deleteCommand.AddArgument(new Argument() { Arity = ArgumentArity.OneOrMore });
            deleteCommand.AddOption(new Option("--force-english-output", CompletionOnlyDescription));
            deleteCommand.AddOption(new Option(new string[] { "-s", "--source" }, CompletionOnlyDescription)
            {
                Argument = new Argument() { Arity = ArgumentArity.ExactlyOne }
            });
            deleteCommand.AddOption(new Option("--non-interactive", CompletionOnlyDescription));
            deleteCommand.AddOption(new Option(new string[] { "-k", "--api-key" }, CompletionOnlyDescription)
            {
                Argument = new Argument() { Arity = ArgumentArity.ExactlyOne }
            });
            deleteCommand.AddOption(new Option("--no-service-endpoint", CompletionOnlyDescription));
            deleteCommand.AddOption(new Option("--interactive", CompletionOnlyDescription));

            return deleteCommand;
        }

        private static Command GetLocalsCommand()
        {
            var localsCommand = new Command("locals", CompletionOnlyDescription);

            localsCommand.AddArgument(new Argument() { Arity = ArgumentArity.ExactlyOne }
                .FromAmong(new string[] { "all", "http-cache", "global-packages", "plugins-cache", "temp" }));

            localsCommand.AddOption(new Option("--force-english-output", CompletionOnlyDescription));
            localsCommand.AddOption(new Option(new string[] { "-c", "--clear" }, CompletionOnlyDescription));
            localsCommand.AddOption(new Option(new string[] { "-l", "--list" }, CompletionOnlyDescription));

            return localsCommand;
        }

        private static Command GetPushCommand()
        {
            var pushCommand = new Command("push", CompletionOnlyDescription);

            pushCommand.AddArgument(new Argument() { Arity = ArgumentArity.OneOrMore });

            pushCommand.AddOption(new Option("--force-english-output", CompletionOnlyDescription));
            pushCommand.AddOption(new Option(new string[] { "-s", "--source" }, CompletionOnlyDescription)
            {
                Argument = new Argument() { Arity = ArgumentArity.ExactlyOne }
            });
            pushCommand.AddOption(new Option(new string[] { "-ss", "--symbol-source" }, CompletionOnlyDescription)
            {
                Argument = new Argument() { Arity = ArgumentArity.ExactlyOne }
            });
            pushCommand.AddOption(new Option("-t|--timeout", CompletionOnlyDescription)
            {
                Argument = new Argument() { Arity = ArgumentArity.ExactlyOne }
            });
            pushCommand.AddOption(new Option("-k|--api-key", CompletionOnlyDescription)
            {
                Argument = new Argument() { Arity = ArgumentArity.ExactlyOne }
            });
            pushCommand.AddOption(new Option("-sk|--symbol-api-key", CompletionOnlyDescription)
            {
                Argument = new Argument() { Arity = ArgumentArity.ExactlyOne }
            });
            pushCommand.AddOption(new Option("-d|--disable-buffering", CompletionOnlyDescription));
            pushCommand.AddOption(new Option("-n|--no-symbols", CompletionOnlyDescription));
            pushCommand.AddOption(new Option("--no-service-endpoint", CompletionOnlyDescription));
            pushCommand.AddOption(new Option("--interactive", CompletionOnlyDescription));
            pushCommand.AddOption(new Option("--skip-duplicate", CompletionOnlyDescription));

            return pushCommand;
        }
    }
}
