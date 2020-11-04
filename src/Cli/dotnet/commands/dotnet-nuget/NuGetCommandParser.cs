// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.CommandLine;

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

            command.AddOption(new Option<bool>("--version", CompletionOnlyDescription));
            command.AddOption(new Option<string>(new string[] { "-v", "--verbosity" }, CompletionOnlyDescription)
            {
                Argument = new Argument<string>()
            });

            command.AddCommand(GetDeleteCommand());
            command.AddCommand(GetLocalsCommand());
            command.AddCommand(GetPushCommand());

            return command;
        }

        private static Command GetDeleteCommand()
        {
            var deleteCommand = new Command("delete", CompletionOnlyDescription);
            deleteCommand.AddArgument(new Argument<IEnumerable<string>>() { Arity = ArgumentArity.OneOrMore });
            deleteCommand.AddOption(new Option<bool>("--force-english-output", CompletionOnlyDescription));
            deleteCommand.AddOption(new Option<string>(new string[] { "-s", "--source" }, CompletionOnlyDescription)
            {
                Argument = new Argument<string>()
            });
            deleteCommand.AddOption(new Option<bool>("--non-interactive", CompletionOnlyDescription));
            deleteCommand.AddOption(new Option<string>(new string[] { "-k", "--api-key" }, CompletionOnlyDescription)
            {
                Argument = new Argument<string>()
            });
            deleteCommand.AddOption(new Option<bool>("--no-service-endpoint", CompletionOnlyDescription));
            deleteCommand.AddOption(new Option<bool>("--interactive", CompletionOnlyDescription));

            return deleteCommand;
        }

        private static Command GetLocalsCommand()
        {
            var localsCommand = new Command("locals", CompletionOnlyDescription);

            localsCommand.AddArgument(new Argument<string>()
                .FromAmong(new string[] { "all", "http-cache", "global-packages", "plugins-cache", "temp" }));

            localsCommand.AddOption(new Option<bool>("--force-english-output", CompletionOnlyDescription));
            localsCommand.AddOption(new Option<bool>(new string[] { "-c", "--clear" }, CompletionOnlyDescription));
            localsCommand.AddOption(new Option<bool>(new string[] { "-l", "--list" }, CompletionOnlyDescription));

            return localsCommand;
        }

        private static Command GetPushCommand()
        {
            var pushCommand = new Command("push", CompletionOnlyDescription);

            pushCommand.AddArgument(new Argument<IEnumerable<string>>() { Arity = ArgumentArity.OneOrMore });

            pushCommand.AddOption(new Option<bool>("--force-english-output", CompletionOnlyDescription));
            pushCommand.AddOption(new Option<string>(new string[] { "-s", "--source" }, CompletionOnlyDescription)
            {
                Argument = new Argument<string>()
            });
            pushCommand.AddOption(new Option<string>(new string[] { "-ss", "--symbol-source" }, CompletionOnlyDescription)
            {
                Argument = new Argument<string>()
            });
            pushCommand.AddOption(new Option<string>(new string[] { "-t", "--timeout" }, CompletionOnlyDescription)
            {
                Argument = new Argument<string>()
            });
            pushCommand.AddOption(new Option<string>(new string[] { "-k", "--api-key" }, CompletionOnlyDescription)
            {
                Argument = new Argument<string>()
            });
            pushCommand.AddOption(new Option<string>(new string[] { "-sk", "--symbol-api-key" }, CompletionOnlyDescription)
            {
                Argument = new Argument<string>()
            });
            pushCommand.AddOption(new Option<bool>(new string[] { "-d", "--disable-buffering" }, CompletionOnlyDescription));
            pushCommand.AddOption(new Option<bool>(new string[] { "-n", "--no-symbols" }, CompletionOnlyDescription));
            pushCommand.AddOption(new Option<bool>("--no-service-endpoint", CompletionOnlyDescription));
            pushCommand.AddOption(new Option<bool>("--interactive", CompletionOnlyDescription));
            pushCommand.AddOption(new Option<bool>("--skip-duplicate", CompletionOnlyDescription));

            return pushCommand;
        }
    }
}
